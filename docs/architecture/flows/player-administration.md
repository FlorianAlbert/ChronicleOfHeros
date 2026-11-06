# Flows: Player administration

Scope: Player enrollment, Operator password reset, and current-Player lookup, as the API implements them today.
Load when: changing `/players` endpoints, enrollment, password reset, temporary credentials, or the current-Player lookup.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [authentication.md](authentication.md), [web-forwarding.md](web-forwarding.md), [startup.md](startup.md), [../capabilities/identity/domain.md](../capabilities/identity/domain.md)

> Pending approved changes: #75

"Client" is any HTTP client of the API holding a valid access token ([authentication.md](authentication.md)). `UserManager` persists through its EF stores.

## Player enrollment

```mermaid
sequenceDiagram
    autonumber
    actor Operator
    participant Endpoint as EnrollPlayerEndpoint
    participant Admin as PlayerAdministrationService
    participant Users as UserManager
    Operator->>Endpoint: POST /players (Username) with Bearer token
    Note over Endpoint: policy Operator requires role Operator
    Endpoint->>Admin: EnrollAsync(request)
    Admin->>Admin: trim Username, UsernameValidator.IsValid
    alt Username invalid
        Endpoint-->>Operator: 400 validation problem, key username
    else
        Admin->>Admin: create temporary credential
        Admin->>Users: CreateAsync(user, temporary credential)
        alt duplicate Username or password rules fail
            Endpoint-->>Operator: 400 validation problem
        else
            Admin->>Users: AddToRoleAsync(user, Player)
            alt role assignment fails
                Admin->>Users: DeleteAsync(user)
                Endpoint-->>Operator: 400 validation problem
            else
                Endpoint-->>Operator: 201 temporary credential, Location /players/accountId
            end
        end
    end
```

The new account has `MustChangePassword = true`; the Player's first sign-in yields a restricted access token. Account creation and role assignment run without a shared transaction; a failed role assignment is compensated by deleting the account.

## Operator password reset

```mermaid
sequenceDiagram
    autonumber
    actor Operator
    participant Endpoint as ResetPlayerPasswordEndpoint
    participant Admin as PlayerAdministrationService
    participant Users as UserManager
    participant Tokens as AuthenticationTokenService
    participant Db as PostgreSQL
    Operator->>Endpoint: POST /players/username/reset-password with Bearer token
    Note over Endpoint: policy Operator requires role Operator
    Endpoint->>Admin: ResetPasswordAsync(Username)
    Admin->>Users: FindByNameAsync(trimmed Username)
    alt no account
        Endpoint-->>Operator: 404
    else
        Admin->>Users: GeneratePasswordResetTokenAsync, ResetPasswordAsync(temporary credential) with MustChangePassword = true
        alt Identity errors
            Endpoint-->>Operator: 400 validation problem
        else
            Admin->>Tokens: RevokeAllRefreshSessionsAsync(accountId)
            Tokens->>Db: set RevokedAtUtc on all unrevoked sessions of the account
            Endpoint-->>Operator: 200 temporary credential
        end
    end
```

The reset does not check the target's roles or `IsActive`, so it also applies to Operator accounts. Access tokens issued earlier stay valid until they expire.

## Current Player

`GET /players/me` requires policy `Player`. `CurrentPlayerEndpoint` parses the `sub` claim as a `Guid` (401 when it is not one), `PlayerIdentityService.GetAsync` finds the account by ID (404 when absent), and the endpoint returns 200 with `PlayerIdentityResponse(AccountId)`.
