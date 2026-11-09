# Flows: authentication

Scope: sign-in, password change, refresh rotation with replay revocation, and sign-out, as the API implements them today.
Load when: changing any `/authentication/*` endpoint, token issuing, refresh-session rotation or revocation, or the must-change-password rule.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [player-administration.md](player-administration.md), [startup.md](startup.md), [../capabilities/identity/state.md](../capabilities/identity/state.md)

"Client" is any HTTP client of the API, calling it directly over HTTPS; the Web host exposes no route to it ([../hosts/web.md](../hosts/web.md)). `UserManager` reads and writes `AspNetUsers` and `AspNetUserRoles` through its EF stores. Token lifetimes and claims: [../cross-cutting/security.md](../cross-cutting/security.md).

## Sign-in

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Endpoint as SignInEndpoint
    participant Auth as AuthenticationService
    participant Users as UserManager
    participant Tokens as AuthenticationTokenService
    participant Db as PostgreSQL
    Client->>Endpoint: POST /authentication/sign-in (Username, Password)
    Endpoint->>Auth: SignInAsync(request)
    Auth->>Users: FindByNameAsync(trimmed Username)
    Auth->>Users: CheckPasswordAsync(user, password)
    alt unknown Username, inactive account, or wrong password
        Auth-->>Endpoint: Unauthorized
        Endpoint-->>Client: 401
    else MustChangePassword is true
        Auth->>Tokens: CreateRestrictedAccessToken(user)
        Auth-->>Endpoint: RestrictedAccessTokenResponse
        Endpoint-->>Client: 200 restricted access token, no refresh token
    else normal account
        Auth->>Users: GetRolesAsync(user)
        Auth->>Tokens: CreateNormalTokenPairAsync(user, roles)
        Tokens->>Db: insert RefreshSession in a new family
        Auth-->>Endpoint: TokenPairResponse
        Endpoint-->>Client: 200 access token and refresh token
    end
```

Failed sign-ins do not count toward lockout; there is no lockout or rate limiting.

## Password change

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Endpoint as ChangePasswordEndpoint
    participant Auth as AuthenticationService
    participant Users as UserManager
    participant Tokens as AuthenticationTokenService
    participant Db as PostgreSQL
    Client->>Endpoint: POST /authentication/change-password with Bearer token
    Note over Endpoint: policy PasswordChange requires role Player or scope password-change
    Endpoint->>Endpoint: parse sub claim as Guid, else 401
    Endpoint->>Auth: ChangePasswordAsync(accountId, request)
    Auth->>Users: FindByIdAsync, CheckPasswordAsync(current password)
    alt unknown, inactive, or wrong current password
        Endpoint-->>Client: 401
    else
        Auth->>Users: ChangePasswordAsync(current, new)
        alt password rules or Identity errors
            Endpoint-->>Client: 400 validation problem
        else
            Auth->>Users: UpdateAsync(MustChangePassword = false)
            Auth->>Tokens: RevokeAllRefreshSessionsAsync(accountId)
            Tokens->>Db: set RevokedAtUtc on all unrevoked sessions of the account
            Auth->>Users: GetRolesAsync(user)
            Auth->>Tokens: CreateNormalTokenPairAsync(user, roles)
            Tokens->>Db: insert RefreshSession in a new family
            Endpoint-->>Client: 200 access token and refresh token
        end
    end
```

The password change and the `MustChangePassword` update are separate `UserManager` calls without a shared transaction. Access tokens issued earlier stay valid until they expire.

## Refresh rotation and replay

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Endpoint as RefreshEndpoint
    participant Auth as AuthenticationService
    participant Tokens as AuthenticationTokenService
    participant Db as PostgreSQL
    Client->>Endpoint: POST /authentication/refresh (RefreshToken)
    Endpoint->>Auth: RefreshAsync(request)
    Auth->>Tokens: RefreshNormalTokenPairAsync(token, UserManager)
    Tokens->>Db: begin transaction in the execution strategy
    Tokens->>Db: select session by SHA-256 hash, with its account
    alt no session matches
        Endpoint-->>Client: 401
    else session revoked or expired, family expired, or account inactive
        Tokens->>Db: revoke all unrevoked sessions of the family, commit
        Endpoint-->>Client: 401
    else session active
        Tokens->>Db: set RevokedAtUtc where Id matches and RevokedAtUtc is null
        alt no row updated because a concurrent request won
            Tokens->>Db: revoke all unrevoked sessions of the family, commit
            Endpoint-->>Client: 401
        else one row updated
            Tokens->>Db: insert replacement session in the same family, commit
            Tokens->>Tokens: GetRolesAsync through UserManager, issue access token
            Endpoint-->>Client: 200 new access token and refresh token
        end
    end
```

A blank refresh token returns 401 without a database call. Presenting a rotated token again is a replay and revokes the family.

## Sign-out

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant Endpoint as SignOutEndpoint
    participant Auth as AuthenticationService
    participant Tokens as AuthenticationTokenService
    participant Db as PostgreSQL
    Client->>Endpoint: POST /authentication/sign-out (RefreshToken)
    Endpoint->>Auth: SignOutAsync(request)
    Auth->>Tokens: RevokeRefreshSessionFamilyForTokenAsync(token)
    Tokens->>Db: select session by SHA-256 hash
    opt session found
        Tokens->>Db: revoke all unrevoked sessions of its family
    end
    Endpoint-->>Client: 204
```

Sign-out is anonymous: the refresh token alone authorizes revoking its family.

## Known deviations

- Clients call the API authentication endpoints directly and receive access and refresh tokens in JSON; Web offers no sign-in, sign-out, or password change. Deviates from [ADR 0004](../../adr/0004-same-origin-web-api-gateway.md): the authentication, refresh, and sign-out endpoints are never browser-proxied, Web holds the tokens in a server-side session, and browser sign-in, sign-out, and password change are static SSR form posts. Tracking: #84.
