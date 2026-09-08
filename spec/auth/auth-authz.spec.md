# Auth / AuthZ — derived specification

**What this is.** A description of how authentication and authorization *actually behave* in this
codebase, read off the implementation rather than off intent. `Auth.md` beside it is the **build
spec** — what was built and why, in the order it was built. This file is the **reference**: the
contract a caller can rely on, the enforcement points, and the gaps that exist today.

Derived from `9cf6779` plus the uncommitted 變更密碼 work in the tree. Every claim below is
traceable to a file named in §13. When the code changes, this file is wrong until it is updated —
§14 lists the things that must not change silently.

---

## 1. Actors and trust boundaries

| Actor | Trusted for | Never trusted for |
|---|---|---|
| Browser (Angular app) | Rendering; deciding what to *show* | Any authorization decision |
| Access token | Identity (`userId`) and role names, **after** signature + lifetime validation | Anything before validation |
| `dbo.SysConfig` | The signing secret, the password-policy switch | — |
| `dbo.AppUser` / `AppUserRole` | Credentials and role membership | — |

The boundary is the ASP.NET request pipeline. Everything the browser does — the route guard, the
role-gated sidebar, the disabled-looking fields — is convenience. The API re-derives identity from
the token on every request and re-checks authorization from its own policy.

## 2. Identity data model

Only the columns auth actually reads or writes.

| Table | Column | Used for |
|---|---|---|
| `dbo.AppUser` | `UserId` (PK, `nvarchar(200)`) | Identity. Matched case-insensitively (database collation); the **stored** casing is what is returned and put in the token |
| | `UserName` (`nvarchar(200)`) | Display only. Writable by its owner via 個人資料 |
| | `IsActive` (`bit`) | Checked at login and before a password change |
| | `PasswordHash` (`nvarchar(800)`) | Lowercase hex SHA-256 of the plain password |
| | `PasswordUpdatedTime` (`datetime`, null) | Stamped by 變更密碼. Read by nothing |
| `dbo.AppRole` | `RoleId` (PK) | The name that becomes a `role` claim |
| `dbo.AppUserRole` | (`UserId`, `RoleId`) | Role membership. Read at login; **written by `PUT /api/app-roles`** — see §6.3 |
| `dbo.SysConfig` | `configKey = 'appConfig'` | JSON holding `symmetricSecurityKey` and `enforcePasswordPolicy` |

## 3. Configuration and secrets

`dbo.SysConfig['appConfig']` is a JSON document. Two properties are live:

| Property | Read by | Semantics |
|---|---|---|
| `symmetricSecurityKey` | `JwtTokenService.GetSigningKeyAsync` | HS256 signing **and** validation key. Must be ≥ 32 bytes; a shorter, absent or malformed value throws a named `InvalidOperationException` |
| `enforcePasswordPolicy` | `PasswordPolicyService` | Password strength switch. **Fails closed** — missing row, unreadable JSON or absent property all read as *enforced* |
| `defaultPassword` | **nothing** | Dead configuration |

Both live properties are read **per operation**, never cached. Rotating the secret therefore
invalidates every outstanding token on the next request, with no restart; flipping the policy takes
effect on the next password change. No secret is compiled in or held in `appsettings.json`.

## 4. Authentication

### 4.1 Credentials

`PasswordHasher` is the only thing that compares a password. It decodes both sides from hex and uses
`CryptographicOperations.FixedTimeEquals`, so hex casing is irrelevant and the comparison leaks no
prefix; a stored value that is not 32 bytes of hex never matches. The digest is **unsalted SHA-256**
— see §12.1.

### 4.2 Login — `POST /api/auth/login`

Request `{ userId, password }`, both `[Required]`. `userId` is trimmed; **the password is not**.

Three failures — unknown user, `IsActive = 0`, wrong password — return the **identical** 401
`ProblemDetails` (`帳號或密碼錯誤。`). `AuthRepository` deliberately does not filter on `IsActive`;
the controller checks it, so all three take one code path and cannot diverge in message or in
timing. On success: `{ userId, userName, accessToken }` and nothing else — `AppUserCredential`,
which carries the hash, never leaves the server.

### 4.3 Token format

Verified against a live token. HS256, compact JWS, no encryption.

| Claim | Source | Notes |
|---|---|---|
| `sub`, `userId` | `AppUser.UserId` | Both carry the same value. `userId` is `NameClaimType` |
| `userName` | `AppUser.UserName` | **Display only, and goes stale** — a rename does not re-issue a token |
| `jti` | new GUID (`N`) | Not recorded or checked anywhere |
| `role` | one per `AppUserRole` row | Trimmed, de-duplicated case-insensitively, blanks skipped. Serialises as a bare string for one role, an array for several |
| `iat`, `nbf` | issue time | |
| `exp` | issue + `JwtTokenService.TokenLifetime` (**24 h**) | Assert against the constant, never a literal |

There is no `iss` and no `aud`: the API issues and consumes its own tokens.

### 4.4 Validation

`JwtBearerSetup.Configure`, applied to the default (and only) scheme, Bearer.

| Setting | Value | Why |
|---|---|---|
| `ValidateIssuerSigningKey`, `ValidateLifetime` | true | |
| `ValidateIssuer`, `ValidateAudience` | **false** | Self-issued, self-consumed |
| `ClockSkew` | 1 minute | |
| `MapInboundClaims` | false | Keeps the short claim names |
| `NameClaimType` / `RoleClaimType` | `userId` / `role` | `User.IsInRole("Admin")` reads exactly what was issued |
| `IssuerSigningKey` | **null** | A pinned key would survive a secret rotation |
| `IssuerSigningKeyResolver` | reads `HttpContext.Items` | Filled by `OnMessageReceived`, which may await the SysConfig read; the resolver itself stays synchronous and blocks no thread |

A request with no `Authorization: Bearer` header triggers no SysConfig read at all.

Middleware order (`Program.cs`): `UseSwagger` → `UseCors` → `UseAuthentication` → `UseAuthorization`
→ `MapControllers`.

## 5. Authorization

### 5.1 Closed by default

`AuthorizationOptions.FallbackPolicy` = `RequireAuthenticatedUser()`. It applies to every endpoint
that declares no policy of its own, so a newly routed controller is protected without anyone
remembering to protect it.

### 5.2 The single exception

`AuthController.Login` carries `[AllowAnonymous]`. **No type carries it.** This is load-bearing, not
stylistic: a class-level `[AllowAnonymous]` beats an action-level `[Authorize]`, so putting it on
`AuthController` would silently expose every action added beside `Login` — which is exactly what
`profile` and `password` are. `AuthorizationConventionTests` pins both halves (no anonymous type;
exactly one anonymous action).

Swagger (`/swagger`) is served **unauthenticated and unconditionally**, in every environment — see
§12.4.

### 5.3 Roles are issued but never enforced server-side

> **This is the most consequential gap in the system as built.**

Grepping the API for `[Authorize(Roles …)]`, `RequireRole`, `RequireClaim` or `IsInRole` returns
**nothing**. The only authorization requirement in the application is "be authenticated". Role
claims are issued, validated and shipped to the browser, where they decide whether the
`系統管理 Admin` sidebar group is rendered — and that is their entire effect.

Two direct consequences, both live today:

1. **The role gate is cosmetic.** A non-Admin who types `/app-roles` reaches the page and the API
   serves it. Hiding the menu group hides nothing.
2. **Any authenticated user can grant themselves any role.** `PUT /api/app-roles` takes
   `AppRoleRequest.UserIds`, and `AppRoleRepository` replaces the `dbo.AppUserRole` rows for that
   role from it. Submitting the `Admin` role with your own `UserId` in the list writes the
   membership; the next login mints a token carrying `role: Admin`. Nothing in the request path
   checks who is asking.

Established by construction from four facts, each read directly: the fallback policy is the only
policy; no role check exists anywhere; `AppRolesController` declares no policy of its own; and
`AppRoleRequest.UserIds` drives a junction replace. It was not exploited.

Fixing it means gating server-side **first** — `[Authorize(Roles = "Admin")]` on the
role-and-user-administration endpoints — and only then adding a client-side role guard. A client
guard alone changes nothing.

### 5.4 Endpoint authorization matrix

| Endpoint | Anonymous | Any authenticated user | Role required |
|---|---|---|---|
| `POST /api/auth/login` | ✅ | ✅ | — |
| `PUT /api/auth/profile` | ❌ 401 | ✅ own row only | none |
| `PUT /api/auth/password` | ❌ 401 | ✅ own row only | none |
| `GET /api/lookups/app-users` | ❌ 401 | ✅ **full user roster** (`userId`, `userName`, `isActive`; no hashes) | none |
| `PUT /api/app-roles` | ❌ 401 | ✅ **rewrites role membership** (§5.3) | none |
| Every other CRUD route | ❌ 401 | ✅ full read/write | none |
| `GET /swagger/**` | ✅ | ✅ | — |

## 6. Operations that act on the caller

The rules `profile` and `password` share, and that any future self-service endpoint must follow.

- **The account comes from the token**, via `User.FindFirstValue(JwtTokenService.UserIdClaimType)`
  with `sub` as fallback. Never from the body, and there is no route parameter to pass one.
- **The request DTO has no property for a key or a role list.** `UpdateProfileRequest` is
  `{ UserName }`; `ChangePasswordRequest` is `{ CurrentPassword, NewPassword }`. A body carrying
  `userId` or `roles` has nothing to bind to and is dropped during deserialization — this is *why*
  "a userId in the body is ignored" is true, not merely intended, and reflection tests pin the
  shapes so a later property cannot undo it.
- **The write names one column.** `UpdateUserNameAsync` sets `UserName`; `UpdatePasswordAsync` sets
  `PasswordHash` + `PasswordUpdatedTime`. Neither touches the key, `IsActive`, or
  `dbo.AppUserRole`. Self-service cannot become an escalation path.
- **The response is narrow or empty.** `UserProfileResponse` is `{ userId, userName }`; the password
  change answers **204 with no body**. No hash, and no role list.
- `UserName` is trimmed (and whitespace-only is rejected — `[Required]` does not catch `"   "`).
  **Passwords are never trimmed**, on either side, or a legitimate password bounded by spaces could
  never be typed again.

### 6.1 Password policy

`PasswordPolicyService`, driven by `enforcePasswordPolicy` (§3):

| Policy | Rule | Message |
|---|---|---|
| Enforced (default, and on failure to read config) | ≥ `MinimumLength` (8) **and** an upper-case letter, a lower-case letter, a digit and a non-alphanumeric | `PolicyMessage` |
| Off | non-empty | `RequiredMessage` |

The new password must also differ from the current one, and the current one must verify first.
Order of checks: model state → `userId` claim → account exists and is active → current password
matches → new differs → policy. Nothing is written until all pass.

## 7. HTTP status semantics

> **The contract the client depends on. Breaking it logs users out.**

`authErrorInterceptor` treats **any 401 outside `/auth/login`** as "your session expired": it clears
the whole session and redirects to `/login`. Therefore:

| Situation | Status | Rationale |
|---|---|---|
| Bad credentials at **login** | 401 | The one place a 401 means "wrong password"; the login page shows it |
| No token / expired / forged / tampered / wrong scheme | 401 | Genuinely expired — signing out is correct |
| Token with no `userId` claim | 401 | Unusable identity; signing out is correct |
| **Wrong current password** at 變更密碼 | **400** | A 401 would sign the user out over a typo |
| Weak new password, or same as current | **400** | ditto |
| Empty / whitespace `userName` | 400 | |
| Token valid but account gone or disabled | 404 | |

Every 400 from these endpoints carries a user-facing Chinese message in `ProblemDetails.detail`,
written to be displayed verbatim.

## 8. Client session model

| Concern | Behaviour |
|---|---|
| Storage | `sessionStorage['cms-auth']` = `{ userId, userName, accessToken }`. **Session**, not local — it dies with the tab |
| Source of truth | `token()` / `hasToken()` **re-read storage on every call**, so a sign-out in another tab is seen at once. The signals only mirror it for templates |
| Roles | Decoded (never verified) from the token by `jwt.util.ts`. Both claim shapes flatten to a list. **No second API call** |
| Token attachment | `authTokenInterceptor` adds the header only when the URL starts with `environment.apiBaseUrl` — `/api` in production, `http://localhost:5000/api` in development. The token cannot leak to a third party |
| Sign-out | `clearSession()` calls `sessionStorage.clear()` — the profile **and** every list page's filters, sort and paging, so the next user inherits nothing. The single place a session ends; 登出 and the 401 path both route through it |
| Route guard | `authGuard` returns a `UrlTree` to `/login?returnUrl=…`, so the redirect is one navigation. Convenience only |
| `returnUrl` | Honoured only if it starts with a single `/` and is not the login page; anything else lands on `DEFAULT_ROUTE` |
| Password change | Does **not** touch the session — the API keeps the token valid and answers 204 |

## 9. Enforcement matrix

| Rule | Server | Browser |
|---|---|---|
| Authentication required | ✅ fallback policy | guard (cosmetic) |
| Identity of the acting user | ✅ token claim | — |
| Password strength | ✅ `PasswordPolicyService` | hint text only — the flag is invisible to the browser, so the form checks only "filled in" and "both entries match" and shows the server's 400 |
| New-password confirmation | — (not sent) | ✅ cross-field validator |
| `UserName` required | ✅ | ✅ |
| Role-based access | ❌ **nothing** | menu visibility only |

Where a rule is enforced in both places, the server's message is the one displayed, so the two
cannot drift.

## 10. Invariants

Each is pinned by a test; breaking one should fail the build, not production.

| Invariant | Pinned by |
|---|---|
| Fallback policy carries `DenyAnonymousAuthorizationRequirement` | `AuthorizationConventionTests` |
| No controller **type** is `[AllowAnonymous]`; `Login` is the only anonymous **action** | `AuthorizationConventionTests` |
| `IssuerSigningKey` is null and the resolver is not | `AuthorizationConventionTests` |
| `MapInboundClaims` off, `RoleClaimType = "role"` | `AuthorizationConventionTests` |
| Every route/verb is 401 without a token | `AuthorizationTests` `[Theory]` |
| Rotating the SysConfig secret invalidates outstanding tokens immediately | `AuthorizationTests` |
| One identical 401 for every login failure | `AuthControllerTests` |
| `PasswordHash` reaches neither response nor token | `AuthControllerTests` |
| A body naming another account changes only the caller's row | `AuthorizationTests` (raw JSON) |
| A wrong current password is 400 and **asserted not to be 401** | `AuthControllerTests`, `AuthorizationTests` |
| Password policy fails closed on unreadable config | `AuthControllerTests` |
| Write DTOs carry no key and no role list | `AuthRoutingConventionTests` |

## 11. Residual risk

Ranked by exposure. None of these are defects in what was built — they are the edges of what was
built.

1. **No server-side role enforcement (§5.3).** Any authenticated user can rewrite role membership
   and escalate to Admin. *Highest-value fix: `[Authorize(Roles = "Admin")]` on role and user
   administration.*
2. **Unsalted SHA-256 password hashes.** Fast and unsalted: a leaked `AppUser` table is offline-
   crackable at high rate, and identical passwords share a digest. Migrating means an algorithm
   marker on the column, verify-against-old, and re-hash on next login — its own piece of work.
3. **No token revocation.** One global signing secret and no per-user version, so a password change
   invalidates nothing — not the caller's token, not a session on another device. Rotating the
   secret is all-or-nothing. A `TokenVersion` on `dbo.AppUser`, checked during validation, is the
   shape that fixes it.
4. **Swagger is public in every environment.** `app.UseSwagger()` is unconditional and sits ahead of
   authorization, so the full API surface is enumerable anonymously.
5. **No brute-force protection.** No rate limit, no lockout, no delay, no audit trail of failed
   logins. `PasswordUpdatedTime` is the only auth-related timestamp written anywhere.
6. **The full user roster is readable by any authenticated user** via `GET /api/lookups/app-users`.
   No hashes, but names and `isActive` for every account.
7. **Token in `sessionStorage`** is readable by any script on the origin, so any XSS is a full
   session compromise. The trade was deliberate (it dies with the tab); an httpOnly cookie plus CSRF
   handling is the alternative.
8. **No HTTPS enforcement.** No `UseHttpsRedirection`, no HSTS; development runs plain HTTP on
   `:5000`, so tokens and passwords cross the wire in clear.
9. **CORS allows any loopback origin** with `AllowAnyHeader`, `AllowAnyMethod` and
   `AllowCredentials` — fine for development, too broad if the API is ever exposed.
10. **The token's `userName` claim goes stale** after a rename. Nothing reads it, so this is
    cosmetic today, but it is a trap for anyone who starts to.
11. **No password reset.** 變更密碼 requires the current password, so a forgotten password needs an
    administrator and a SQL statement. `SysConfig.defaultPassword` is dead configuration that would
    seed exactly that flow.

## 12. Traceability

| File | Responsibility |
|---|---|
| `Program.cs` | Scheme registration, fallback policy, middleware order, CORS, Swagger |
| `Infrastructure/JwtBearerSetup.cs` | Validation parameters, per-request key resolution |
| `Infrastructure/JwtTokenService.cs` | Claim set, lifetime, signing-key read, config key names |
| `Infrastructure/PasswordHasher.cs` | Hash and fixed-time comparison |
| `Infrastructure/PasswordPolicyService.cs` | Strength rules, `enforcePasswordPolicy`, fail-closed |
| `Controllers/AuthController.cs` | The four behaviours and every status code in §7 |
| `Repositories/AuthRepository.cs` | The only writes to `dbo.AppUser` |
| `Repositories/SysConfigRepository.cs` | Config read |
| `core/services/auth.service.ts` | Session ownership, storage-as-source-of-truth |
| `core/interceptors/*.ts` | Token attachment; 401 → sign-out |
| `core/guards/auth.guard.ts` | Route gate (cosmetic) |
| `core/utils/jwt.util.ts` | Unverified claim decode |
| `features/profile/*` | 個人資料 and 變更密碼 |

| Suite | Pins |
|---|---|
| `AuthorizationTests` | The real pipeline over HTTP — the only place middleware is exercised |
| `AuthorizationConventionTests` | The wiring, by reflection and resolved options |
| `AuthControllerTests` | Behaviour, over in-memory fakes |
| `AuthRoutingConventionTests` | Route and DTO shapes |
| `auth.service.spec.ts`, `auth-*.interceptor.spec.ts`, `auth.guard.spec.ts`, `profile.spec.ts` | Client session and forms |

## 13. Rules for changing this area

1. **Never put `[AllowAnonymous]` on a type.** Action level only, and every addition is a deliberate
   act that must update §5.2.
2. **Never answer a bad password with 401 outside `/auth/login`.** It signs the user out (§7).
3. **A self-service endpoint takes its subject from the token**, and its DTO gets no key and no role
   list (§6).
4. **Never pin `IssuerSigningKey`.** It would survive a rotation.
5. **Add the server-side check before the client-side one.** A guard or a hidden menu is not a
   control.
6. **Secrets stay in `dbo.SysConfig`**, read at runtime — never `appsettings.json`, never compiled
   in.
7. A change here is done when `dotnet test` **and** `ng test --watch=false` both pass, and this file
   still describes the code.
