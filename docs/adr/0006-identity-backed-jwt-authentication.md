# ASP.NET Core Identity with JWT bearer authentication and Web BFF custody

ChronicleOfHeros will own username-and-password accounts through ASP.NET Core Identity. Every account has the Player role; Operators have the additional Operator role for Player-account administration and may access only their own character data. Operators enroll Players, and the API generates a temporary credential for direct out-of-application delivery; the Player must replace it before normal access. The initial Operator is bootstrapped from Aspire parameters only when no Operator exists, and later startups never mutate existing accounts.

The API exposes custom JSON authentication endpoints rather than acting as an
OAuth/OpenID Connect authorization server. It issues 15-minute ES256 JWT access
tokens with immutable UUID subjects and configured issuer/audience values, plus
server-stored, opaque refresh tokens that rotate on every use. Refresh-token
replay revokes its sign-in-session family. Password changes and Operator resets
revoke all refresh-token families; access tokens otherwise remain valid until
expiry. Credentials are sent only over HTTPS in JSON bodies and protected API
requests use the Bearer authorization header.

The browser does not consume this API contract directly. Under ADR-0004,
`ChronicleOfHeros.Web` is a BFF client of the API. It retains API access and
refresh tokens in its private Redis session store and presents the access token
only on internal API calls. The browser holds an opaque Web session cookie only.
Cookie authentication therefore applies to Web's browser surface, while bearer
authentication remains the API's sole authentication mechanism.

Aspire provisions an ES256 P-256 signing key pair derived from the required,
opaque, non-empty `Jwt:SigningSeed` secret. It supplies the Base64 PKCS#8
private key only to API and the Base64 SubjectPublicKeyInfo public key to API
and Web.

The seed is encoded as exact UTF-8, without trimming or Unicode normalization.
HKDF-SHA-256 uses a fixed, versioned application context and rejection samples
the derived candidate until it is a valid non-zero P-256 private scalar.
Modular reduction is not used. The application imposes no seed character-set or
length format beyond non-empty; deployment infrastructure must generate and
protect the random value.

Routine signing-key rotation is not used. If the seed is compromised, replace
it and redeploy AppHost, API, and Web together. This intentionally invalidates
all access tokens; the BFF can use its server-held refresh tokens to obtain
newly signed tokens.

CORS, API versioning, public deployment topology, production health-probe
exposure, broader Operator lifecycle, and rate limiting are intentionally
deferred.