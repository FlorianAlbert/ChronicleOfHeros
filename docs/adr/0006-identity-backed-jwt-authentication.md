# ASP.NET Core Identity with JWT bearer authentication

Status: Accepted, under revision (#84)

ChronicleOfHeros will own username-and-password accounts through ASP.NET Core Identity. Every account has the Player role; Operators have the additional Operator role for Player-account administration and may access only their own character data. Operators enroll Players, and the API generates a temporary credential for direct out-of-application delivery; the Player must replace it before normal access. The initial Operator is bootstrapped from Aspire parameters only when no Operator exists, and later startups never mutate existing accounts.

The API exposes custom JSON authentication endpoints rather than acting as an
OAuth/OpenID Connect authorization server; they return the issued tokens to
their caller in JSON response bodies. The API issues 15-minute RS256 JWT access
tokens with immutable UUID subjects and configured issuer/audience values, plus
server-stored, opaque refresh tokens that rotate on every use. Refresh-token
replay revokes its sign-in-session family. Password changes and Operator resets
revoke all refresh-token families; access tokens otherwise remain valid until
expiry. Bearer authentication is the API's sole authentication mechanism, and
protected API requests use the Bearer authorization header.

All HTTP traffic, browser-facing and service-to-service, uses HTTPS only;
credentials travel only in HTTPS JSON bodies.

Aspire supplies the signing key as the secret `jwt-signing-private-key`
parameter, passed only to the API as `Jwt__SigningPrivateKey`: a Base64
PKCS#8 RSA private key. The API derives its validation public key from that
private key. Issuer and audience come from the `jwt-issuer` and
`jwt-audience` parameters. Browser-session and token custody are being
revisited in #84.

CORS, API versioning, public deployment topology, production health-probe
exposure, broader Operator lifecycle, and rate limiting are intentionally
deferred.