# Build Spec for Auth (登入／授權)
- database schema: `.\database\auth.sql` (`AppUser`, `AppRole`, `AppUserRole`, `SysConfig`)
- **See also `auth-authz.spec.md`** — the derived reference for how auth and authz actually behave
  now, including the enforcement matrix and the residual risks. This file is the build history; that
  one is the contract.

Auth is **not** a CRUD feature. There is no `Auth` table, no list / detail / form triple and no
`/api/lookups` entry. It is two endpoints plus the request-pipeline and app-shell wiring that makes
every other feature private.

This spec covers five rounds of work, in the order they were built:

1. **Issuing** — `POST /api/auth/login`: credential check + JWT signed with the secret in
   `dbo.SysConfig`.
2. **Locking down** — bearer validation, global authorization, the login page, the HTTP
   interceptors, the route guard, logout, and the role-gated sidebar.
3. **個人資料 My Profile** — `PUT /api/auth/profile`, the header user menu, and the `/profile` page:
   the signed-in user renames themselves.
4. **變更密碼** — `PUT /api/auth/password` and a second card on the same page: the signed-in user
   changes their own password, after proving they know the current one.
5. **預設密碼 forced change** — the `defaultPassword` in `dbo.SysConfig` stops being dead
   configuration: an account still sitting on it gets a flagged token, the API answers **403** to
   everything but `PUT /api/auth/password`, and the browser holds the user on a dedicated
   `/change-password` page until they pick a new one.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | N/A — `dbo.AppUser.UserId` (`nvarchar(200)`) is read and matched, never written |
| Foreign Keys | `AppUserRole.UserId` → `AppUser.UserId`; `AppUserRole.RoleId` → `AppRole.RoleId` |
| Required Fields | login: `userId`, `password` — both `[Required]`. Profile: `userName` — `[Required]`, plus a trim check for whitespace |
| N-N Relationships | `AppUser` ↔ `AppRole` through `AppUserRole` — **read-only** throughout; the roles become token claims and are never written by either endpoint |
| Primary-Foreign Links | N/A — nothing is inserted or deleted, so there is no delete-409 path; the one UPDATE touches a non-key column |
| Query Filters | N/A |
| Default Sort | role ids come back `RoleId ASC`, so a token's claims are stable |

## Localization

- Login: 登入 · Logout: 登出 · Profile: 個人資料 My Profile
- UserId: 帳號 · Password: 密碼 · UserName: 使用者名稱 · Roles: 角色
- The one login failure message, for every cause: 帳號或密碼錯誤。
- 使用者名稱為必填。 · 未設定角色 · 帳號與角色由系統管理員維護，無法自行修改。

## API

| Method | Route | Notes |
|--------|-------|-------|
| `POST` | `/api/auth/login` | `{ userId, password }` → 200 `{ userId, userName, accessToken }` · 400 invalid model · **401 for every credential failure** |
| `PUT` | `/api/auth/profile` | `{ userName }` → 200 `{ userId, userName }` · 400 empty / whitespace name · 401 no or bad token · 404 the token's account is gone |
| `PUT` | `/api/auth/password` | `{ currentPassword, newPassword }` → **204 no body** · **400 for every rejection** · 401 no or bad token · 404 the account is gone or disabled |

Every route above but `login` and `password` also answers **403** while the caller's password is
still the configured 預設密碼 — see 預設密碼 below.

`api/auth` is the one route that is not a kebab-case plural, because it is not a table.
`Login` is also the one `[AllowAnonymous]` **action** — the attribute sits on the method, never on
the type, because a class-level one beats an action-level `[Authorize]` and would have opened up
`profile` the moment it was added.

### Credential check

- `dbo.AppUser.PasswordHash` holds the **lowercase hex SHA-256** of the plain password.
  `PasswordHasher.Matches` decodes both sides and compares with
  `CryptographicOperations.FixedTimeEquals`, so hex casing does not matter and the comparison leaks
  no prefix. A stored value that is not 32 bytes of hex never matches.
- Unknown UserId, `IsActive = 0` and a wrong password all return the **same** 401 `ProblemDetails`.
  `AuthRepository` deliberately does not filter on `IsActive` — the controller checks it, so every
  failure takes one code path and cannot diverge in message or in timing.
- `AppUserCredential` carries `PasswordHash` and is internal. The wire shape is `LoginResponse`
  (`userId`, `userName`, `accessToken`) and nothing else.

### The token

- Signed HS256 with the `symmetricSecurityKey` property of the JSON in `dbo.SysConfig` where
  `configKey = 'appConfig'`. Read through `ISysConfigRepository` on **every** login — and on every
  validation — so a rotated secret needs no restart. Never hard-coded, never in `appsettings.json`.
  HS256 needs ≥ 32 bytes; `JwtTokenService` throws a named `InvalidOperationException` when the row,
  the property or the length is wrong.
- Claims: `sub` / `userId` / `userName`, a `jti`, and one **`role`** claim per `dbo.AppUserRole` row
  (trimmed, de-duplicated case-insensitively, blanks skipped).
- Lifetime `JwtTokenService.TokenLifetime` = 24 h. Assert against the constant, never a literal.

### 個人資料 — `PUT /api/auth/profile`

The one endpoint that writes, and it writes exactly one column of exactly one row: the caller's own
`dbo.AppUser.UserName`.

- **The account comes from the token.** `User.FindFirstValue(JwtTokenService.UserIdClaimType)` —
  `sub` as a fallback — and never from the request. There is no route param either, so a caller has
  nothing to pass.
- **`UpdateProfileRequest` has one property, `UserName`.** No `UserId`, no role list. A body that
  sends them has nothing to bind to and System.Text.Json drops them, which is what makes "a userId
  in the body is ignored" true rather than merely intended. A reflection test pins the shape so a
  later property cannot quietly undo it.
- **Validation.** `[Required]` rejects `null` and `""`; it does **not** reject `"   "`, so the action
  trims and re-checks. Both answer 400 with `ModelState`, and the stored name is untouched.
- **`AuthRepository.UpdateUserNameAsync` sets `UserName` and nothing else** — not the key, not
  `IsActive`, not `PasswordHash` — and never touches `dbo.AppUserRole`. 個人資料 therefore cannot
  become a privilege-escalation path. It re-reads the row **inside** the transaction, or returns
  `null` when no row matched → 404 (the token validated, but its account has since gone).
- **`UserProfileResponse` is `{ userId, userName }`.** No PasswordHash; no roles either, because a
  rename does **not** re-issue a token, so nothing about the caller's authority changes.

### 變更密碼 — `PUT /api/auth/password`

- **Every rejection is a 400, never a 401.** This is the rule the whole feature hangs on. To
  `authErrorInterceptor` a 401 outside `/auth/login` means "session expired": it clears the session
  and redirects to `/login`. A mistyped 目前密碼 answered with 401 would therefore sign the user out
  instead of telling them. `PasswordRejected` is the single helper for these, and its
  `ProblemDetails.Detail` is meant to be shown verbatim: 目前密碼不正確。/ 新密碼不可與目前密碼相同。
  / the policy message. The one 401 left is a token with no `userId` claim.
- **Order of checks:** model state → userId claim → account exists and is active → current password
  matches → new password differs from it → policy. Nothing is written until all of them pass.
- **Strength is `IPasswordPolicyService`**, not a data annotation, because the rules are switched by
  `enforcePasswordPolicy` in dbo.SysConfig['appConfig'] — the same JSON as the signing key, read on
  every call so a flip needs no restart. It **fails closed**: a missing row, unreadable JSON or an
  absent property all read as enforced. On: `MinimumLength` (8) plus upper, lower, digit and symbol.
  Off: non-empty. Tests assert `PasswordPolicyService.PolicyMessage`, never the literal text.
- **Neither password is ever trimmed** — a legitimate password that begins or ends with a space has
  to stay typeable. UserName is trimmed; passwords are not.
- **204 with no body.** `UpdatePasswordAsync` takes an already-hashed value, so a plain password
  never reaches a SQL parameter, a log or a profiler trace, and it stamps
  `PasswordUpdatedTime = SYSDATETIME()` — the first thing in the app to write that column.
- **The session survives.** No revocation exists, so forcing a re-login would imply an invalidation
  that does not actually happen on the user's other devices. The caller's own token keeps working
  on the very next request, which is asserted. (The *forced* flow below is the one exception on the
  client side — and it is the browser that signs out, not the API.)

### 預設密碼 — forcing a change

`dbo.SysConfig['appConfig'].defaultPassword` had been carried since the first round and read by
nothing. It is the value an administrator writes — as `Hash(defaultPassword)` — into
`AppUser.PasswordHash` to reset an account, which meant the reset password was shared by every
account ever reset and stayed valid indefinitely. This round makes it single-use.

- **Detection is at login, on the stored hash.** `DefaultPasswordService.IsDefaultAsync` compares
  `AppUser.PasswordHash` to `Hash(defaultPassword)`. The stored hash, not the submitted password:
  the credential check has already proved they match, and `PasswordHasher.Matches` is fixed-time
  and hex-case-insensitive, so the comparison is correct for free. It runs **after** the credential
  guard, so a rejected login pays for no extra SysConfig read. No schema change: `AuthRepository`
  already selected `PasswordHash`.
- **It fails open**, and it is the only thing in the auth code that does. A missing row, unreadable
  JSON, an absent, non-string or blank `defaultPassword` all mean "nobody is forced" — failing
  closed would flag every account at once and 403 the entire API. Note that
  `PasswordPolicyService` reads the *same* row and fails **closed**; both files point at each
  other, because two opposite failure modes over one JSON document is the confusing part.
  The blank check runs *before* the comparison, or `Hash("")` would become a real target.
- **The flag rides in the token**, as `mustChangePassword`, emitted **only when true** and as the
  string `"true"`. `LoginResponse` is unchanged: one source of truth, and it survives a reload
  exactly as the roles do. Absence means "not flagged", so an unflagged token is byte-identical to
  one issued before this round.
- **Enforcement is a policy, not a branch.** `AuthPolicies.PasswordNotDefault` is
  `RequireAuthenticatedUser()` + `MustChangePasswordRequirement`, and it is set as **both** the
  fallback **and** the default policy. Both, because an endpoint carrying a *bare* `[Authorize]` —
  `UpdateProfile` does — bypasses the fallback and combines the default instead. So does
  `[Authorize(Roles = …)]`, which is the trap waiting for whoever adds the role gate: a convention
  test fails the build for any attribute that names roles without also naming a policy.
- **`ChangePassword` is the one exemption**, `[Authorize(Policy = AuthPolicies.PasswordChangeExempt)]`
  — authenticated, requirement lifted. Without it a flagged user would be 403'd out of the endpoint
  that clears the flag. It is pinned with the same exact set equality as `[AllowAnonymous]`.
- **403, never 401.** `authErrorInterceptor` signs the user out on any 401 outside the login call,
  and ignores a 403. A 401 here would therefore bounce the user to `/login` instead of to 變更密碼.
  `PasswordChangeRequiredResultHandler` gives that 403 a `ProblemDetails` body carrying
  請先變更預設密碼。— the framework's own 403 is bodyless, which would have been the one refusal in
  this API that does not say why.
- **The forced change cannot end on the default.** 新密碼不可與目前密碼相同。already refuses it, and
  for a flagged user "same as current" *is* "still the default" — no extra check was needed.
- **Nothing is re-issued.** The token still carries the flag after a successful change, so the
  browser signs the user out and sends them back to 登入 with 密碼已變更，請重新登入。 Leaving them
  signed in would leave them holding a token every other route refuses.
- **Rollout.** A token minted before this round carries no claim, so it is not flagged and keeps
  full access for up to 24 hours. Rotating `symmetricSecurityKey` in `dbo.SysConfig` at deploy
  kills every outstanding token on the next request and needs no restart — one `UPDATE`.

## Authorization (backend)

- **Closed by default.** `Program.cs` builds `AuthPolicies.PasswordNotDefault` —
  `RequireAuthenticatedUser()` + `MustChangePasswordRequirement` — and sets it as **both**
  `AuthorizationOptions.FallbackPolicy` and `AuthorizationOptions.DefaultPolicy`. A new controller
  is protected the moment it is routed — nothing to remember, no list of routes to keep in step.
  `AuthController.Login` opts back out with `[AllowAnonymous]`, and is the only **action** that may
  — **no type** carries the attribute. That distinction is load-bearing: a class-level
  `[AllowAnonymous]` beats an action-level `[Authorize]`, so leaving it on the controller would have
  served `PUT /api/auth/profile` to anyone the moment it was added.
- **Why both policies.** The fallback applies only to an endpoint carrying no `IAuthorizeData` at
  all; an endpoint with a bare `[Authorize]` bypasses it and combines the *default* policy instead.
  `UpdateProfile` names `AuthPolicies.PasswordNotDefault` outright, and `ChangePassword` names
  `AuthPolicies.PasswordChangeExempt` — the one exemption in the API.
- `JwtBearerSetup.Configure` (`Infrastructure\`) holds the validation parameters: signature and
  lifetime checked, issuer and audience **not** (the API issues and consumes its own tokens),
  `ClockSkew` one minute, `MapInboundClaims = false` and `RoleClaimType = "role"` so the short claim
  names survive and `User.IsInRole("Admin")` reads exactly what was issued.
- **The validation key is read per request, not pinned.** `TokenValidationParameters` has no
  `IssuerSigningKey`; `IssuerSigningKeyResolver` returns whatever `OnMessageReceived` stashed in
  `HttpContext.Items`. That event may await, so it is where the SysConfig read happens — the
  resolver itself is synchronous and nothing blocks a thread. A request with no
  `Authorization: Bearer` header triggers no read at all.
- Wired with `AddOptions<JwtBearerOptions>(…).Configure<IHttpContextAccessor>(JwtBearerSetup.Configure)`,
  which is how the accessor reaches the resolver.
- Middleware order: `UseSwagger` → `UseCors` → `UseAuthentication` → `UseAuthorization` →
  `MapControllers`. Swagger deliberately sits ahead of authorization — the docs are how a developer
  gets a token — and `AddSecurityDefinition("Bearer", …)` lets the UI send one.
- No endpoint gates on a **role** yet. Today the roles only drive the sidebar. When one is added,
  write it `[Authorize(Policy = AuthPolicies.PasswordNotDefault, Roles = "Admin")]`: a bare `Roles`
  also sets `useDefaultPolicy = false`, so it would opt the endpoint out of the 預設密碼 check and
  make the new role gate the one route a flagged user could still reach.
  `AuthorizationConventionTests` fails the build on that mistake.

## Frontend

- Route `/login` (public, `loadComponent`); every feature route and `/profile` carry
  `canActivate: [authGuard, passwordChangeGuard]`. The empty path and the wildcard both redirect to
  `/featured-promo-items` — the 首頁 Home landing page — rather than `/app-roles`, which is
  Admin-only in the menu.
- Route `/change-password` is the 預設密碼 page. It carries `unflaggedAwayFromForceGuard` and
  **not** `passwordChangeGuard`: giving it both would redirect a flagged user to the page they are
  already on, and the `**` fallback would turn that into an infinite loop. The guard sends a
  signed-out user to `/login` and an unflagged one to `DEFAULT_ROUTE`, so the page cannot be
  reached by typing its URL.
- `passwordChangeGuard` deliberately lets a **signed-out** user through — `authGuard` owns that
  case and runs first, so they still land on `/login?returnUrl=…` rather than on 變更密碼. It
  carries no `returnUrl` of its own: a successful forced change ends in a sign-out, so there is
  nothing to return to.
- The shell's `showChrome` is `signedIn() && !mustChangePassword()`, kept separate from
  `signedIn` rather than folded into it — the two really are different states, and `app.spec`
  asserts on both. A flagged session therefore gets the same bare window the login page gets.
- 登入 sends a flagged sign-in to `/change-password`, **ignoring `returnUrl`**: every other route
  would answer that token 403.
- `core/models/auth.model.ts` — `LoginRequest`, `AuthProfile`, `UpdateProfileRequest`,
  `UserProfile`, `ChangePasswordRequest`. None of them has a `roles` field: the roles ride inside
  the token, and none has a `userId` on the write side: the API takes the account from the token.
- `core/services/auth.service.ts` — owns the session. The profile lives in **session** storage under
  `cms-auth`, so it dies with the tab. Exports `AUTH_SESSION_KEY`, `LOGIN_ROUTE` (`/login`),
  `PROFILE_ROUTE` (`/profile`), `FORCE_PASSWORD_CHANGE_ROUTE` (`/change-password`) and
  `DEFAULT_ROUTE` (`/featured-promo-items`) so nothing hard-codes a path.
  - **Storage is the source of truth for "is there a token?"** — `token()` / `hasToken()` re-read it
    on every call, because another tab may have signed out. The `profile` / `userName` / `roles`
    signals mirror it for the templates.
  - `clearSession()` calls `sessionStorage.clear()`, not `removeItem`: signing out must not leave
    the next user looking at the previous one's list filters, sort and paging. It is the single
    place a session ends — both 登出 and the 401 path go through it.
  - `updateUserName(name)` PUTs `{ userName }` — and only that — then merges the **returned** name
    into the stored profile, keeping `accessToken` as it is. The API does not re-issue a token, so
    `userId` and the roles are unchanged; the header updates because it reads the `userName`
    signal. Store what came back, not what was typed.
  - `changePassword(current, next)` PUTs `{ currentPassword, newPassword }` and **touches the
    session not at all** — the API answers 204 and keeps the token valid, so there is nothing to
    re-store and nobody is signed out. Passwords go exactly as typed, never trimmed. (The *forced*
    page signs the user out itself, after the call returns; the service still does nothing.)
  - Two names for the 預設密碼 flag, deliberately, and it is the shape the file already chose for
    `profile` vs `token()`: `mustChangePassword()` is a **signal** over the cached profile, for
    the shell's `showChrome`; `requiresPasswordChange()` re-reads **storage** and is what the
    guards ask, because another tab may have replaced the session.
- `core/utils/jwt.util.ts` — decodes (never verifies) the payload. `rolesFromToken` flattens both
  claim shapes: one role serialises as a bare string, several as an array. Base64url is padded and
  percent-unescaped so a Chinese `userName` survives the round trip.
  `mustChangePasswordFromToken` reads the 預設密碼 flag: true for the string `'true'`
  (case-insensitively) — the shape the API really emits — and for the boolean `true`, so switching
  the server to a JSON boolean could never silently unflag everybody. Everything else, absence
  included, is false.
- `core/utils/problem-detail.util.ts` — `problemDetail(error, fallback)`, the 400 → `detail` rule,
  lifted out of `profile.ts` when the forced page became its second caller. It is worth sharing
  because the 400-vs-401 contract depends on it, and because the message a forced user is likeliest
  to see — 新密碼不可與目前密碼相同。, from retyping the default — is exactly the one a second copy
  would end up swallowing.
- `core/interceptors/auth-token.interceptor.ts` — attaches `Authorization: Bearer …` to requests
  whose URL starts with `environment.apiBaseUrl`, and to nothing else, so the token cannot leak to a
  third party.
- `core/interceptors/auth-error.interceptor.ts` — a 401 clears the session, navigates to `/login`
  with the current URL as `returnUrl`, and **re-throws** so the caller still sees the error.
  `/auth/login` is exempt: its 401 means "wrong password", not "session expired", and the login page
  shows that itself. It deliberately **ignores 403**, and teaching it about the 預設密碼 403 was
  considered and rejected: the flag can only appear at login and the guards cover every navigation,
  so a second, racy enforcement path fighting the guard would be strictly worse than the guard
  alone.
- `core/guards/auth.guard.ts` — returns a `UrlTree` to `/login?returnUrl=…` rather than `false`, so
  the redirect is part of the same navigation.
- `core/guards/password-change.guard.ts` — two `CanActivateFn`s in one file. `passwordChangeGuard`
  sends a flagged user to `/change-password`; `unflaggedAwayFromForceGuard` guards that route in
  the other direction. Both are convenience: the API answers 403 whatever the browser rendered.
- `features/auth/login` — 帳號 / 密碼, both required. The userId is trimmed; the **password is sent
  exactly as typed** (trimming would reject a legitimate one). A failure shows the one generic
  message and stays put. `returnUrl` is honoured only when it is a path inside this app: it must
  start with a single `/` and must not be the login page; anything else lands on `DEFAULT_ROUTE`.
- `features/profile` — 個人資料 My Profile, a **single component** on `/profile` rather than a
  list / detail / form triple, because everything it shows is already in the session:
  帳號 from `AuthService.userId`, 角色 as `p-tag` chips from `AuthService.roles` (未設定角色 when
  there are none). **There is no GET endpoint and no second request** — the same rule the sidebar's
  role gate follows. Both render read-only, and read-only means *no control at all*, not a disabled
  one; a note says 帳號與角色由系統管理員維護，無法自行修改。 使用者名稱 is the one control:
  required, trimmed, and trimmed-to-empty refused before the request with the same message the
  API's 400 carries. 取消 restores the name the session still holds.

  A **second, independent form** on the same page is 變更密碼: 目前密碼 / 新密碼 / 確認新密碼, all
  `type="password"`. Independent matters — a failure in one card must not discard what was typed in
  the other, and a name save must send no password request.

  **The form does not restate the strength rules.** `enforcePasswordPolicy` lives in SysConfig,
  which the browser cannot see, so the form validates only "filled in" and "the two new entries
  match" (a cross-field validator, 兩次輸入的新密碼不一致。) and renders the policy as hint text
  above the fields. Everything else comes back as a **400** whose `ProblemDetails.detail` is shown
  verbatim in `.profile__error`. Encoding the rules here as well would let the two drift apart —
  and would be wrong outright the moment the flag is turned off.

  On success the boxes are emptied, so the secrets are not left sitting in the DOM; on failure what
  was typed stays, so it can be corrected. 取消 clears both the boxes and the message.
- `features/auth/force-password-change` — the 變更密碼 page a flagged user is held on. A **bare
  page like 登入**, not a mode on `/profile`: the shell hides its chrome here, because every menu
  entry would lead somewhere the API answers 403. Three `type="password"` boxes and the same
  `newPasswordsMatch` cross-field validator the profile page uses — copied rather than shared, at
  five lines, because it is a form concern and not a cross-cutting rule. On 204 it calls
  `clearSession()` **before** navigating (so no guard can still see the flagged token), toasts
  密碼已變更，請重新登入。 and lands on `/login`; on 400 it shows the server's reason verbatim and
  keeps what was typed. A 登出 button is the only other way off the page — without it a flagged
  user would be trapped, since the header user menu is hidden.
- `app.ts` / `app.html` — the shell chrome renders only when signed in **and** not flagged
  (`showChrome`). The header's `userName` is
  the trigger for a **user menu** (`p-menu`, `[popup]`, model `App.userMenuItems`): 個人資料 as a
  `routerLink`, 登出 as a `command`, because signing out has to clear the session *before* the
  navigation. The menu carries **no** `appendTo` — unlike the drawer selects — so the overlay dies
  with the shell instead of being left in `document.body`. `.app-shell--anonymous` drops the
  sidebar column so the login page fills the viewport.
- `NavGroup` gained an optional `roles`. Omit it and everyone signed in sees the group; list roles
  and `App` filters the group **out of the DOM** for anyone else — it is not merely disabled.
  `系統管理 Admin` lists `Admin`, matched case-insensitively. This gates what is *shown*; the API
  re-validates every request regardless.
- `core/testing/auth.testing.ts` — test-only: `signIn(roles)` writes a session, `fakeAccessToken`
  and `fakeProfile` build the shapes. Nothing in the browser verifies a signature, so a correctly
  *shaped* token is all a spec needs.

## Tests

- Backend `AuthControllerTests` (62, counting the theory cases): the credential paths, one identical
  401 for every cause, the claims and 24-hour lifetime of a genuinely signed token, the SysConfig
  failures, and that `PasswordHash` reaches neither the response nor the token. Its 個人資料 half
  builds the controller under a hand-made `ClaimsPrincipal` (`SignedInAs`) and covers: the token's
  user is the row that changes, every other account is untouched, the key / roles / IsActive /
  PasswordHash are all unchanged, the name is trimmed, empty and whitespace both 400 without
  writing, no `userId` claim → 401, a vanished account → 404, and the two DTO shapes.
  `AuthRoutingConventionTests` pins `api/auth` + `login` + `profile` + `password` + `[FromBody]`,
  that `UpdateProfileRequest` carries `UserName` and `ChangePasswordRequest` the two passwords and
  nothing else, and that ChangePassword declares 204 (and **not** 200) and returns a bare
  `IActionResult` — there is no shape a password endpoint could return.
  Its 變更密碼 half covers: the hash is rewritten for the token's user and nobody else,
  `PasswordUpdatedTime` is stamped, name / roles / IsActive are untouched, the wrong current
  password is **400 and asserted not to be 401**, a new password equal to the current one is
  refused, a `[Theory]` over one case per strength rule, another over passwords that pass
  (including the 8-character boundary and one padded with spaces that must survive untrimmed), the
  policy switched off accepting a weak password but still refusing a blank one, an unreadable
  config **failing closed**, 401 with no userId claim, 404 for a deleted and for a disabled
  account, and the round trip that matters — the new password logs in, the old one does not.
  Its 預設密碼 half needs no new fixtures — the seeded `AppConfigJson.defaultPassword` is admin's
  password and not helen's — and covers: the default password issues a token whose flag is exactly
  the string `"true"`, any other password omits the claim **entirely** (absence, not `"false"`),
  no configured default flags nobody, the flag does not disturb the role claims, a flagged user is
  refused when they retype the default (新密碼不可與目前密碼相同。— the rule that makes the forced
  flow airtight for free), the round trip where changing the password and logging in again yields an
  unflagged token, and the documented interaction with `enforcePasswordPolicy` off. The
  "no password material in the token" test now excludes the flag **by type** before its substring
  scan and asserts its value separately: the claim's *name* legitimately contains "Password".
- Backend `DefaultPasswordServiceTests` (21, counting the theory cases) — its own suite, because
  most of what matters is unreachable through `Login`: an appConfig row that is missing or
  unreadable makes `GetSigningKeyAsync` throw first. Covers the match itself, hex casing, a stored
  value that is not a hash, a `[Theory]` over every fail-**open** shape (absent row, empty,
  unparseable, non-object root, absent / blank / non-string / null / array property), that a blank
  default does not match `Hash("")` — the blank-check-ordering regression — that the config is
  re-read per call, and one test asserting the deliberate opposite failure modes of this service
  and `PasswordPolicyService` over the *same* bad row.
- Backend `AuthorizationTests` (61, counting the theory cases) hosts the real pipeline through
  `TestApiFactory : WebApplicationFactory<Program>` — a `[Fact]` calling an action directly would
  never reach the middleware. Covers: 401 with no token and a `Bearer` challenge header; a `[Theory]`
  over twelve route/verb pairs — `PUT /api/auth/profile` among them, because it shares a controller
  foreign-signed, tampered and wrong-scheme tokens; login working with no token and with a stale
  one; and that rotating the SysConfig secret invalidates outstanding tokens while a re-issued one
  is accepted at once (its own factory, so the rotation cannot leak into the class fixture).
  Swagger stays reachable unauthenticated. 個人資料 is exercised here too, because a **raw JSON**
  body carrying a foreign `userId` and a role list is a request the typed DTO cannot express: helen
  renames herself, admin is untouched, helen gains no role. Those tests build their own factory —
  they mutate the seeded user. 變更密碼 is covered the same way: the new password really does log
  in afterwards and the old one does not, a wrong current password is 400 (asserted **not** 401)
  and writes nothing, a weak one comes back with the policy message, a raw body naming another
  account changes only the caller's own password, the caller's token still works on the very next
  request, and a foreign-signed token is 401 and changes nothing.
  預設密碼 is covered here too, and only here, because the requirement lives in the middleware: a
  `[Theory]` over the same twelve route/verb pairs as the 401 one — deliberately, so the two read
  as a pair — answering **403**, asserted **not** to be a 401; the `ProblemDetails` body carrying
  請先變更預設密碼。 as `application/problem+json`; `PUT /api/auth/password` still answering 204;
  the same token still 403 afterwards **because nothing is re-issued**; a fresh login then 200; an
  ordinary user unaffected (the direction that would hurt most); a hand-minted flagged token 403
  (so the gate is the middleware, not the login path); a pre-feature token **not** flagged, pinned
  as an accepted rollout gap; rotating `defaultPassword` changing nobody already holding a token
  while flagging the next login; and login itself still reachable by a flagged user.
  `TestApiFactory` gained `DefaultPassword` — which **must not** equal `AdminPassword`, or the
  admin token almost every test here signs in with would 403 — plus a `stale@example.com` seeded on
  it, a `SeedAppConfig` that rotates the two independently, and a `SignToken` overload that mints
  a flagged token directly.
- Backend `AuthorizationConventionTests` (17) pins the wiring: the fallback policy carries
  `DenyAnonymousAuthorizationRequirement`, Bearer is the default scheme, `IssuerSigningKey` is null
  while the resolver is not, `MapInboundClaims` is off with `RoleClaimType = "role"`, **no
  controller type** is `[AllowAnonymous]`, `AuthController.Login` is the only `[AllowAnonymous]`
  action in the assembly, `UpdateProfile` names `PasswordNotDefault` and `ChangePassword` names
  `PasswordChangeExempt` — plus a guard that the reflection query actually found the controllers,
  so "only one" cannot pass vacuously.
  Its 預設密碼 half pins: **both** `DefaultPolicy` and `FallbackPolicy` carrying
  `MustChangePasswordRequirement` (asserting only the fallback would let someone drop the default
  and silently reopen `UpdateProfile`), both named policies registered and shaped correctly,
  `ChangePassword` as the only `PasswordChangeExempt` action in the API by exact set equality, the
  requirement handler and the result handler both resolving, and — the one shaped around the *next*
  change — that no `[Authorize]` anywhere escapes the default policy by naming roles or an unknown
  policy, with a failure message telling the author to name `AuthPolicies.PasswordNotDefault`
  alongside the roles.
- Frontend: `jwt.util.spec` (both claim shapes, multi-byte values, every unreadable input, and the
  預設密碼 flag: the string `'true'` case-insensitively, the boolean `true`, an absent claim as
  false, and every other value false),
  `problem-detail.util.spec` (the 400's `detail` shown, a blank / non-string / absent detail and a
  non-object body all falling back, every other status falling back, and a non-HTTP error falling
  back),
  `auth.service.spec` (login body, session-not-local storage, restore, corrupt session, storage read
  per call, the 預設密碼 signal and the storage-reading method agreeing for a flagged, an unflagged
  and a signed-out session — and the method seeing a session another tab replaced, without a new
  instance — roles from the token with **no** second request, case-insensitive `hasRole`,
  `clearSession` dropping list state, storage unavailable, and `updateUserName`: the PUT body is
  `{ userName }` alone, the stored name is refreshed while the token / userId / roles survive, the
  API's name wins over the typed one, and a failure leaves the session exactly as it was),
  `auth-token.interceptor.spec` (header
  attached on every verb, absent when signed out, picked up mid-session, never sent off-API),
  `auth-error.interceptor.spec` (401 clears + redirects, `returnUrl`, no self-referencing
  `returnUrl`, error re-thrown, login exempt, other statuses ignored), `auth.guard.spec`
  (allow / `UrlTree` / `returnUrl` / blocks after a clear / blocks on an empty token),
  `login.spec` (validation, trimmed userId and untrimmed password, session stored, `returnUrl`
  honoured and rejected, 401 message, and a flagged sign-in landing on `/change-password` **even
  with a `returnUrl`**),
  `password-change.guard.spec` (an ordinary user through, a flagged one redirected with no
  `returnUrl`, storage read rather than a cached signal, a signed-out user left to `authGuard`, the
  reverse guard in all three states, the two guards' fixed point — proof there is no loop — and a
  route-config assertion that every feature route carries **both** guards while `change-password`
  carries only the reverse one),
  `force-password-change.spec` (three password boxes and no 帳號 control, nothing sent until all
  three are filled or while they disagree, a PUT of exactly the two untrimmed passwords, the whole
  session cleared and `/login` reached with the toast on 204, the server's reason shown verbatim
  for a wrong current password / for 新密碼不可與目前密碼相同。/ for the policy message, what was
  typed surviving, a generic fallback on a 500, the previous message cleared on resubmit, and 登出
  as the escape hatch), `app.spec` (Admin group shown only to Admin — group, links
  and item count — hidden for `User` and for no roles at all, case-insensitive, the header
  `userName` and its refresh after a rename, the user menu offering 個人資料 → `/profile` and 登出,
  登出 clearing the session and returning to `/login`, no chrome when signed out, and **no chrome
  while flagged either** — a session exists, but every menu entry would lead to a 403), and
  `profile.spec` (帳號 and 角色 read-only — asserted by the **absence** of a control, with exactly
  one `<input>` on the page — the 未設定角色 fallback, the pre-filled name, a save that PUTs the
  trimmed `{ userName }` and lands in all three places at once, userId / roles / token unchanged
  across it, empty and whitespace refused with no request, a failed save leaving the session alone,
  and 取消 restoring the stored name, plus 變更密碼: three `type="password"` boxes, the policy
  hint, a PUT of the two untrimmed passwords that empties the boxes on success, the session
  surviving intact, a mismatch and an empty box both refused **with no request**, the API's message
  shown verbatim for a wrong current password and for a weak one, what was typed surviving a
  failure, a generic fallback when the failure carries no detail, 取消 clearing both, and the two
  forms staying independent in both directions).

## Deferred

- **No refresh token and no expiry countdown.** A 24-hour token simply stops working and the next
  API call's 401 sends the user back to `/login`. The browser never checks `exp` itself.
- **No role gate on routes.** A non-Admin who types `/app-roles` still reaches the page; the API
  serves it, because no endpoint gates on a role yet. Add the attribute server-side first, then a
  role guard, if that becomes a requirement — and name a policy beside the roles, per Authorization
  (backend) above.
- **No *self-serve* password reset.** 變更密碼 still needs the current password, so a user who has
  forgotten theirs needs an administrator writing `Hash(defaultPassword)` into
  `AppUser.PasswordHash`. What the 預設密碼 round closed is the half that mattered — that shared
  secret is now single-use. What is still missing is the administrator's own step: no reset endpoint
  and no token-by-email flow, and it waits on AppUser getting a CRUD feature.
- **A flagged user with another tab already open** on a feature page is not pushed off it. Guards
  run on navigation only and the interceptor ignores 403, so they see requests fail with no
  explanation. Accepted: the server is the control, the page is genuinely unusable, and polling or
  a 403 handler racing the guard would be worse than the guard alone.
- **The 預設密碼 flag is per login, not per request.** Rotating `defaultPassword` leaves everyone
  already holding a token exactly as they were. That matches `enforcePasswordPolicy` and differs
  from `symmetricSecurityKey`, which is re-read per request — worth knowing before assuming.
- **Login now reads the appConfig row twice** — once for the default-password check, once for the
  signing key. A shared `AppConfigReader` would fix it, and would churn two working, well-tested
  services for no user-visible gain on an operation that happens once a day.
- **PasswordHash is still a bare SHA-256 hex digest**, unsalted and fast, which is not what a
  password should be stored with in 2026. It is the established shape of the column and every
  existing row, so changing it is its own migration: add an algorithm marker, verify against the
  old scheme, and re-hash on next login. 變更密碼 deliberately did not start that.
- **A password change does not invalidate tokens** — not the caller's, and not any other device's.
  There is no revocation to hook into: one global signing secret, and rotating it would sign
  *everyone* out. A per-user token version in dbo.AppUser, checked during validation, is the
  shape that would fix it.
- **A rename leaves the token's `userName` claim stale.** Nothing reads it — the app takes the name
  from the stored profile, and the API takes the account from `userId` — so the claim is simply out
  of date until the next login. Re-issuing a token on rename would fix it and is not worth the
  extra endpoint contract today.
- **AppUser has no CRUD feature**, so users and their roles are still maintained in SQL. 個人資料 is
  the only page that writes to `dbo.AppUser` at all, and only ever the caller's own row.
