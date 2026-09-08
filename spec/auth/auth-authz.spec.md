# Auth / AuthZ — derived specification

**What this is.** A description of how authentication and authorization *actually behave* in this
codebase, read off the implementation rather than off intent. `Auth.md` beside it is the **build
spec** — what was built and why, in the order it was built. This file is the **reference**: the
contract a caller can rely on, the enforcement points, and the gaps that exist today.

Derived from `0443898` plus the uncommitted 預設密碼 forced-change work in the tree. Every claim
below is traceable to a file named in §12. When the code changes, this file is wrong until it is
updated — §13 lists the things that must not change silently.

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

`dbo.SysConfig['appConfig']` is a JSON document. All three properties are live:

| Property | Read by | Semantics |
|---|---|---|
| `symmetricSecurityKey` | `JwtTokenService.GetSigningKeyAsync` | HS256 signing **and** validation key. Must be ≥ 32 bytes; a shorter, absent or malformed value throws a named `InvalidOperationException` |
| `enforcePasswordPolicy` | `PasswordPolicyService` | Password strength switch. **Fails closed** — missing row, unreadable JSON or absent property all read as *enforced* |
| `defaultPassword` | `DefaultPasswordService` | The shared password an administrator writes into `AppUser.PasswordHash` to reset an account. An account still on it is flagged at login (§5.5). **Fails open** — missing row, unreadable JSON, an absent, non-string or blank value all mean *nobody is forced* |

> The two fail directions are deliberate and opposite, over the *same* JSON row. A configuration
> mistake must not switch a protection **off** (the policy), and must not switch a lockout **on**
> (the default-password check): failing closed there would 403 every account out of the entire API
> at once. Both service files say so, and `DefaultPasswordServiceTests` asserts the pair together.

Every property is read **per operation**, never cached. Rotating the secret therefore invalidates
every outstanding token on the next request, with no restart; flipping the policy takes effect on
the next password change; rotating `defaultPassword` takes effect on the next **login**, because
that is the only place the flag is decided (§5.5). No secret is compiled in or held in
`appsettings.json`.

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
| `mustChangePassword` | `DefaultPasswordService.IsDefaultAsync` | **Emitted only when true**, and always as the string `"true"`, not a JSON boolean. Absence means "not flagged", so a token minted before the feature reads as unflagged rather than as broken |
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

`AuthPolicies.PasswordNotDefault` = `RequireAuthenticatedUser()` + `MustChangePasswordRequirement`,
and it is set as **both** `AuthorizationOptions.FallbackPolicy` **and**
`AuthorizationOptions.DefaultPolicy`. A newly routed controller is protected without anyone
remembering to protect it.

Both, deliberately. The fallback policy applies only to an endpoint carrying **no** `IAuthorizeData`
at all; an endpoint with a *bare* `[Authorize]` bypasses it and combines the **default** policy
instead. Setting only the fallback would therefore have left `PUT /api/auth/profile` — which
carries `[Authorize]` today — exempt from the password half of the rule.

The same escape applies to `[Authorize(Roles = …)]`, which also sets `useDefaultPolicy = false`.
That matters for the very next change §5.3 asks for, so
`AuthorizationConventionTests.NoAuthorizeAttribute_EscapesTheDefaultPolicy_ByNamingRolesOrAnUnknownPolicy`
fails the build for any attribute that names roles without also naming a policy.

### 5.2 The two exceptions

`AuthController.Login` carries `[AllowAnonymous]`. **No type carries it.** This is load-bearing, not
stylistic: a class-level `[AllowAnonymous]` beats an action-level `[Authorize]`, so putting it on
`AuthController` would silently expose every action added beside `Login` — which is exactly what
`profile` and `password` are. `AuthorizationConventionTests` pins both halves (no anonymous type;
exactly one anonymous action).

There is a **second** exception, of a different kind and also scoped to one action:
`AuthController.ChangePassword` carries `[Authorize(Policy = AuthPolicies.PasswordChangeExempt)]` —
authenticated, but with the default-password requirement lifted. Without it a flagged user would be
403'd out of the one endpoint that clears the flag. `AuthorizationConventionTests` pins it with the
same exact set equality it uses for `[AllowAnonymous]`: exactly one exempt action in the whole API,
and it is that one.

Swagger (`/swagger`) is served **unauthenticated and unconditionally**, in every environment — see
§12.4.

### 5.3 Roles are issued but never enforced server-side

> **This is the most consequential gap in the system as built.**

Grepping the API for `[Authorize(Roles …)]`, `RequireRole`, `RequireClaim` or `IsInRole` returns
**nothing**. The two authorization requirements in the application are "be authenticated" and "not
still on the 預設密碼" (§5.4); neither has anything to do with *who* is asking. Role claims are
issued, validated and shipped to the browser, where they decide whether the `系統管理 Admin` sidebar
group is rendered — and that is their entire effect.

Two direct consequences, both live today:

1. **The role gate is cosmetic.** A non-Admin who types `/app-roles` reaches the page and the API
   serves it. Hiding the menu group hides nothing.
2. **Any authenticated user can grant themselves any role.** `PUT /api/app-roles` takes
   `AppRoleRequest.UserIds`, and `AppRoleRepository` replaces the `dbo.AppUserRole` rows for that
   role from it. Submitting the `Admin` role with your own `UserId` in the list writes the
   membership; the next login mints a token carrying `role: Admin`. Nothing in the request path
   checks who is asking.

Established by construction from four facts, each read directly: no policy anywhere names a role;
no role check exists anywhere; `AppRolesController` declares no policy of its own; and
`AppRoleRequest.UserIds` drives a junction replace. It was not exploited.

Fixing it means gating server-side **first** on the role-and-user-administration endpoints, and only
then adding a client-side role guard. A client guard alone changes nothing. Write the attribute as
`[Authorize(Policy = AuthPolicies.PasswordNotDefault, Roles = "Admin")]`: a bare `Roles` opts the
endpoint out of the default policy (§5.1), which would make the new role gate the one route a user
on the 預設密碼 could still reach. `AuthorizationConventionTests` fails the build on that mistake.

### 5.4 Forced password change

The one rule the API enforces beyond "be authenticated".

`AuthController.Login` asks `DefaultPasswordService` whether the **stored hash** equals
`Hash(appConfig.defaultPassword)` — the stored hash, not the submitted password, because the
credential check has already proved the two match and `PasswordHasher.Matches` is fixed-time and
hex-case-insensitive. The check runs *after* the credential guard, so a rejected login pays for no
extra config read. If it answers yes, the issued token carries `mustChangePassword` (§4.3), and
`MustChangePasswordRequirement` then refuses that token everywhere but `PUT /api/auth/password`.

| Aspect | Behaviour |
|---|---|
| Status | **403**, never 401 — a 401 would reach `authErrorInterceptor` and sign the user out instead of sending them to 變更密碼 (§7) |
| Body | `ProblemDetails` with `請先變更預設密碼。`, written by `PasswordChangeRequiredResultHandler`. The framework's own 403 is bodyless, which would be the one refusal here that does not say why |
| Decided | Per **login**, not per request. Rotating `defaultPassword` leaves everyone already holding a token exactly as they were |
| Cleared | By signing in again after the change. The token is **not** re-issued, so the caller stays flagged until they do — which is why the browser signs them out on success |
| Pre-existing tokens | Carry no claim, so they are **not** flagged and keep full access for up to 24 h. Rotating `symmetricSecurityKey` at deploy kills them all on the next request, needs no restart, and is the rollout step |
| Terminating on the default | Impossible: 新密碼不可與目前密碼相同。 already refuses it, and for a flagged user "same as current" *is* "still the default" |
| Interaction with `enforcePasswordPolicy` | None. With the policy off a forced user may pick something weak — the two switches are independent, and each owns one thing |

### 5.5 Endpoint authorization matrix

"Authenticated" below means authenticated **and** not still on the 預設密碼; the 403 column is that
second half.

| Endpoint | Anonymous | On the default password | Any other authenticated user | Role required |
|---|---|---|---|---|
| `POST /api/auth/login` | ✅ | ✅ | ✅ | — |
| `PUT /api/auth/password` | ❌ 401 | ✅ **the one exemption** | ✅ own row only | none |
| `PUT /api/auth/profile` | ❌ 401 | ❌ 403 | ✅ own row only | none |
| `GET /api/lookups/app-users` | ❌ 401 | ❌ 403 | ✅ **full user roster** (`userId`, `userName`, `isActive`; no hashes) | none |
| `PUT /api/app-roles` | ❌ 401 | ❌ 403 | ✅ **rewrites role membership** (§5.3) | none |
| Every other CRUD route | ❌ 401 | ❌ 403 | ✅ full read/write | none |
| `GET /swagger/**` | ✅ | ✅ | ✅ | — |

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
| **Token flagged `mustChangePassword`**, on anything but 變更密碼 | **403** | Deliberately not a 401: the session is valid, and signing the user out would lose the very page that fixes it. `authErrorInterceptor` ignores a 403 (pinned by `auth-error.interceptor.spec.ts`), so the guards own the redirect — teaching the interceptor about 403 was considered and **rejected**, as a second, racy enforcement path fighting the guard |
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
| 預設密碼 flag | Decoded from the token like the roles. `mustChangePassword()` (signal) is for templates; `requiresPasswordChange()` re-reads **storage** and is what the guards ask, for the same reason `hasToken()` does |
| Forced state | The shell hides its sidebar and header (`showChrome`), `passwordChangeGuard` holds every feature route on `/change-password`, `unflaggedAwayFromForceGuard` keeps everyone else off it, and 登入 sends a flagged sign-in there **ignoring `returnUrl`** |
| Forced change succeeds | Unlike 個人資料, this one **does** end the session: `clearSession()` then `/login` with 密碼已變更，請重新登入。 The API does not re-issue the token, so the held one still carries the flag |

## 9. Enforcement matrix

| Rule | Server | Browser |
|---|---|---|
| Authentication required | ✅ fallback policy | guard (cosmetic) |
| Identity of the acting user | ✅ token claim | — |
| Password strength | ✅ `PasswordPolicyService` | hint text only — the flag is invisible to the browser, so the form checks only "filled in" and "both entries match" and shows the server's 400 |
| Forced password change | ✅ `MustChangePasswordRequirement` → 403 on every endpoint but 變更密碼 | guard + hidden chrome (cosmetic). The server was written first, per §13.5 |
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
| **Default and fallback policies both** carry `MustChangePasswordRequirement` | `AuthorizationConventionTests` |
| `ChangePassword` is the only `PasswordChangeExempt` action in the API | `AuthorizationConventionTests` (exact set equality) |
| No `[Authorize]` escapes the default policy by naming roles or an unknown policy | `AuthorizationConventionTests` |
| A flagged token is 403 — **asserted not to be 401** — on every route but 變更密碼 | `AuthorizationTests` `[Theory]` |
| A flagged token may still change its own password, and a fresh login is then unflagged | `AuthorizationTests` |
| The default-password check fails **open** on every unreadable config shape | `DefaultPasswordServiceTests` |
| A blank `defaultPassword` does not match `Hash("")` | `DefaultPasswordServiceTests` |
| The flag is emitted only when true, as the string `"true"` | `AuthControllerTests` |
| A pre-feature token is not flagged (accepted rollout gap) | `AuthorizationTests` |
| Every feature route carries both guards; `change-password` carries neither | `password-change.guard.spec.ts` |
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
   administration* — but note §5.1: a bare `Roles` also escapes the default policy, so it must be
   written `[Authorize(Policy = AuthPolicies.PasswordNotDefault, Roles = "Admin")]`. A convention
   test already fails the build otherwise.
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
11. **No *self-serve* password reset.** 變更密碼 still requires the current password, so a forgotten
    password needs an administrator writing `Hash(defaultPassword)` into `AppUser.PasswordHash`.
    What is now closed is the half that mattered: that shared secret is single-use, because the
    account is held on 變更密碼 until it is replaced (§5.4). What remains open is the administrator
    step itself — there is no token-by-email flow, and no endpoint that performs the reset.
12. **A flagged user with another tab already open** on a feature page is not pushed off it: guards
    run on navigation only, and the interceptor ignores 403. They see requests fail with no
    explanation. Accepted — the server is the control and the page is genuinely unusable, which is
    the honest outcome; polling or a 403 handler would be worse than the guard alone.

## 12. Traceability

| File | Responsibility |
|---|---|
| `Program.cs` | Scheme registration, fallback policy, middleware order, CORS, Swagger |
| `Infrastructure/JwtBearerSetup.cs` | Validation parameters, per-request key resolution |
| `Infrastructure/JwtTokenService.cs` | Claim set, lifetime, signing-key read, config key names |
| `Infrastructure/PasswordHasher.cs` | Hash and fixed-time comparison |
| `Infrastructure/PasswordPolicyService.cs` | Strength rules, `enforcePasswordPolicy`, fail-closed |
| `Infrastructure/DefaultPasswordService.cs` | `defaultPassword` comparison, fail-**open** |
| `Infrastructure/AuthPolicies.cs` | The two policy names |
| `Infrastructure/MustChangePasswordRequirement.cs` | The requirement, its handler, and the 403 message |
| `Infrastructure/PasswordChangeRequiredResultHandler.cs` | Gives that 403 a Chinese `ProblemDetails` body |
| `Controllers/AuthController.cs` | The four behaviours and every status code in §7 |
| `Repositories/AuthRepository.cs` | The only writes to `dbo.AppUser` |
| `Repositories/SysConfigRepository.cs` | Config read |
| `core/services/auth.service.ts` | Session ownership, storage-as-source-of-truth |
| `core/interceptors/*.ts` | Token attachment; 401 → sign-out |
| `core/guards/auth.guard.ts` | Route gate (cosmetic) |
| `core/guards/password-change.guard.ts` | The forced-page gate, both directions (cosmetic) |
| `core/utils/jwt.util.ts` | Unverified claim decode, roles and the 預設密碼 flag |
| `core/utils/problem-detail.util.ts` | The 400 → `detail` rule §7 depends on, shared by both password forms |
| `features/profile/*` | 個人資料 and 變更密碼 |
| `features/auth/force-password-change/*` | The forced 變更密碼 page |

| Suite | Pins |
|---|---|
| `AuthorizationTests` | The real pipeline over HTTP — the only place middleware is exercised |
| `AuthorizationConventionTests` | The wiring, by reflection and resolved options |
| `AuthControllerTests` | Behaviour, over in-memory fakes |
| `DefaultPasswordServiceTests` | The fail-open branches, which `Login` cannot reach — it throws on the signing key first |
| `AuthRoutingConventionTests` | Route and DTO shapes |
| `auth.service.spec.ts`, `auth-*.interceptor.spec.ts`, `auth.guard.spec.ts`, `password-change.guard.spec.ts`, `profile.spec.ts`, `force-password-change.spec.ts` | Client session, guards and forms |

## 13. Rules for changing this area

1. **Never put `[AllowAnonymous]` on a type.** Action level only, and every addition is a deliberate
   act that must update §5.2.
2. **Never answer a bad password with 401 outside `/auth/login`.** It signs the user out (§7).
3. **A self-service endpoint takes its subject from the token**, and its DTO gets no key and no role
   list (§6).
4. **Never pin `IssuerSigningKey`.** It would survive a rotation.
5. **Add the server-side check before the client-side one.** A guard or a hidden menu is not a
   control.
6. **An endpoint that opts out of the default policy is an exemption.** Action level only, and every
   addition must update §5.2 — including `[Authorize(Roles = …)]`, which opts out whether or not it
   means to. A new self-service endpoint must decide, explicitly, which of the two policies it takes.
7. **Secrets stay in `dbo.SysConfig`**, read at runtime — never `appsettings.json`, never compiled
   in.
8. A change here is done when `dotnet test` **and** `ng test --watch=false` both pass, and this file
   still describes the code.
