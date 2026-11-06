# Identity: classes

Scope: the Identity types that collaborate at runtime: contract interfaces, services, token handling, persistence entry points, registration, and hosted services.
Load when: changing an Identity contract operation, service, token or refresh-session handling, registration, or hosted service.
Parent: [index.md](index.md) · Neighbours: [components.md](components.md), [domain.md](domain.md), [persistence.md](persistence.md), [state.md](state.md)

> Pending approved changes: #75

## Contract services

```mermaid
classDiagram
    direction LR
    class IAuthenticationService {
        <<interface>>
        +SignInAsync(SignInRequest) IdentityOperationResult~AccessTokenResponse~
        +ChangePasswordAsync(Guid, ChangePasswordRequest) IdentityOperationResult~TokenPairResponse~
        +RefreshAsync(RefreshTokenRequest) IdentityOperationResult~TokenPairResponse~
        +SignOutAsync(RefreshTokenRequest)
    }
    class IPlayerAdministrationService {
        <<interface>>
        +EnrollAsync(EnrollPlayerRequest) IdentityOperationResult~PlayerEnrollmentResponse~
        +ResetPasswordAsync(ResetPasswordRequest) IdentityOperationResult~TemporaryCredentialResponse~
    }
    class IPlayerIdentityService {
        <<interface>>
        +GetAsync(Guid) IdentityOperationResult~PlayerIdentityResponse~
    }
    class AuthenticationService {
        <<service>>
    }
    class PlayerAdministrationService {
        <<service>>
    }
    class PlayerIdentityService {
        <<service>>
    }
    class AuthenticationTokenService {
        <<service>>
        +CreateRestrictedAccessToken(ApplicationUser) RestrictedAccessTokenResponse
        +CreateNormalTokenPairAsync(ApplicationUser, roles) TokenPairResponse
        +RefreshNormalTokenPairAsync(refreshToken, UserManager) TokenPairResponse?
        +RevokeRefreshSessionFamilyForTokenAsync(refreshToken)
        +RevokeAllRefreshSessionsAsync(userId)
    }
    class JwtKeyMaterial {
        +SigningCredentials
        +ValidationKey
        +Create(JwtOptions) JwtKeyMaterial$
    }
    class JwtOptions {
        <<options>>
    }
    class UserManager~ApplicationUser~
    class ChronicleOfHerosDbContext
    class UsernameValidator {
        <<rule>>
        +IsValid(string?) bool$
    }
    class IdentityOperationResults {
        +Success(T)$
        +Unauthorized()$
        +NotFound()$
        +Validation(errors)$
    }
    AuthenticationService ..|> IAuthenticationService
    PlayerAdministrationService ..|> IPlayerAdministrationService
    PlayerIdentityService ..|> IPlayerIdentityService
    AuthenticationService ..> UserManager : find, check and change password, roles
    PlayerAdministrationService ..> UserManager : create, role, reset, delete
    PlayerIdentityService ..> UserManager : find by id
    AuthenticationService ..> AuthenticationTokenService : issue, refresh, revoke
    PlayerAdministrationService ..> AuthenticationTokenService : revoke all sessions
    PlayerAdministrationService ..> UsernameValidator
    AuthenticationService ..> IdentityOperationResults
    PlayerAdministrationService ..> IdentityOperationResults
    PlayerIdentityService ..> IdentityOperationResults
    AuthenticationTokenService ..> ChronicleOfHerosDbContext : RefreshSessions
    AuthenticationTokenService ..> JwtKeyMaterial : signs JWT
    AuthenticationTokenService ..> JwtOptions : issuer, audience
    UserManager ..> ChronicleOfHerosDbContext : EF stores
```

- The services return `IdentityOperationResult<T>`: a value, or an `IdentityFailure` of kind `Unauthorized`, `NotFound`, or `Validation` (with errors keyed by field or Identity error code). `SignOutAsync` returns nothing.
- `AuthenticationTokenService` has no interface; both services depend on the class. It holds the lifetimes: access token 15 minutes, restricted access token 5 minutes, refresh session 30 days idle, refresh family 90 days absolute.
- `JwtKeyMaterial` imports the Base64 PKCS#8 RSA private key from `Jwt:SigningPrivateKey`, signs with RS256, and validates with the derived public key.
- The services are scoped; `JwtKeyMaterial` and `TimeProvider.System` are singletons.

## Registration and hosted services

```mermaid
classDiagram
    direction LR
    class AspNetCoreIdentityRegistration {
        <<registration>>
        +AddAspNetCoreIdentity(Action~AspNetCoreIdentityRegistrationOptions~?) IHostApplicationBuilder
    }
    class AspNetCoreIdentityRegistrationOptions {
        <<options>>
        +EnableMigrations()
    }
    class IdentityCapabilityRegistration {
        +Register(builder, configure) IHostApplicationBuilder$
    }
    class IdentityMigrationHostedService {
        <<hosted>>
    }
    class BootstrapOperatorHostedService {
        <<hosted>>
    }
    class BootstrapOperatorInitializer {
        +InitializeBootstrapOperatorAsync(IServiceProvider)
    }
    class BootstrapOperatorOptions {
        <<options>>
    }
    class ChronicleOfHerosDbContext
    class UsernameValidator {
        <<rule>>
    }
    AspNetCoreIdentityRegistration ..> AspNetCoreIdentityRegistrationOptions : configure
    AspNetCoreIdentityRegistration ..> IdentityCapabilityRegistration : Register
    IdentityCapabilityRegistration ..> IdentityMigrationHostedService : adds when migrations enabled
    IdentityCapabilityRegistration ..> BootstrapOperatorHostedService : adds otherwise
    IdentityMigrationHostedService ..> ChronicleOfHerosDbContext : MigrateAsync, then stop host
    BootstrapOperatorHostedService ..> BootstrapOperatorInitializer : on start
    BootstrapOperatorInitializer ..> BootstrapOperatorOptions
    BootstrapOperatorInitializer ..> UsernameValidator
    BootstrapOperatorInitializer ..> ChronicleOfHerosDbContext : operator exists?
```

`IdentityCapabilityRegistration.Register` always registers `ChronicleOfHerosDbContext` through `AddNpgsqlDbContext` (connection name `chronicleofheros`, retries enabled). Then:

| Mode | Also registers |
|---|---|
| `EnableMigrations()` (Migrations host) | `IdentityMigrationHostedService` only |
| default (Api) | `JwtOptions` (validated on start: key, issuer, audience non-empty), `BootstrapOperatorOptions`, `JwtKeyMaterial`, `TimeProvider`, the four services, JWT-bearer authentication, the authorization policies, `AddIdentityCore<ApplicationUser>` with roles, EF stores and default token providers, and `BootstrapOperatorHostedService` |

Behavior: [../../flows/authentication.md](../../flows/authentication.md), [../../flows/player-administration.md](../../flows/player-administration.md), [../../flows/startup.md](../../flows/startup.md).

## Known deviations

- `BootstrapOperatorInitializer` is a runtime workflow declared as an `IServiceProvider` extension container without the `Extensions` suffix. Deviates from [CODING_STANDARDS.md § C# style](../../../../CODING_STANDARDS.md#c-style): extension containers represent adapter behavior and use the `Extensions` suffix. Tracking: not yet filed.
- The public registration extension container `AspNetCoreIdentityRegistration` has no `Extensions` suffix. Deviates from [CODING_STANDARDS.md § C# style](../../../../CODING_STANDARDS.md#c-style): extension containers use the `Extensions` suffix. Tracking: not yet filed.
- `JwtOptions` validation checks only that values are present; a malformed signing key fails when `JwtKeyMaterial` is first resolved, not at startup. Deviates from [CODING_STANDARDS.md § Registration and composition](../../../../CODING_STANDARDS.md#registration-and-composition): required invalid configuration fails at startup. Tracking: not yet filed.
- RS256 keys from an RSA private-key parameter instead of ES256 keys derived from `Jwt:SigningSeed`. Deviates from [ADR 0006](../../../adr/0006-identity-backed-jwt-authentication.md). Tracking: #76.
