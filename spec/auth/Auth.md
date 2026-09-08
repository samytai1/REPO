# Build Spec for Auth (登入／授權)
- database schema: `.\database\auth.sql` (`AppUser`, `AppRole`, `AppUserRole`, `SysConfig`)

Auth is **not** a CRUD feature. There is no `Auth` table, no list / detail / form triple and no
`/api/lookups` entry. It is two endpoints plus the request-pipeline and app-shell wiring that makes
every other feature private.

This spec covers three rounds of work, in the order they were built:

1. **Issuing** — `POST /api/auth/login`: credential check + JWT signed with the secret in
   `dbo.SysConfig`.
2. **Locking down** — bearer validation, global authorization, the login page, the HTTP
   interceptors, the route guard, logout, and the role-gated sidebar.
3. **個人資料 My Profile** — `PUT /api/auth/profile`, the header user menu, and the `/profile` page:
   the signed-in user renames themselves, and nothing else.

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

## Authorization (backend)

- **Closed by default.** `Program.cs` sets an `AuthorizationOptions.FallbackPolicy` of
  `RequireAuthenticatedUser()`. It applies to every endpoint that declares no policy of its own, so
  a new controller is protected the moment it is routed — nothing to remember, no list of routes to
  keep in step. `AuthController.Login` opts back out with `[AllowAnonymous]`, and is the only
  **action** that may — **no type** carries the attribute. That distinction is load-bearing: a
  class-level `[AllowAnonymous]` beats an action-level `[Authorize]`, so leaving it on the controller
  would have served `PUT /api/auth/profile` to anyone the moment it was added. `UpdateProfile` also
  carries an explicit `[Authorize]` — redundant against the fallback policy, but it states the
  intent at the endpoint where getting it wrong would be worst.
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
- No endpoint gates on a **role** yet. `[Authorize(Roles = "Admin")]` would work; today the roles
  only drive the sidebar.

## Frontend

- Route `/login` (public, `loadComponent`) and `/profile` (`loadComponent` + `authGuard`); every
  other route carries `canActivate: [authGuard]`. The empty path and the wildcard both redirect to
  `/featured-promo-items` — the 首頁 Home landing page — rather than `/app-roles`, which is
  Admin-only in the menu.
- `core/models/auth.model.ts` — `LoginRequest`, `AuthProfile`, `UpdateProfileRequest`,
  `UserProfile`. None of them has a `roles` field: the roles ride inside the token.
- `core/services/auth.service.ts` — owns the session. The profile lives in **session** storage under
  `cms-auth`, so it dies with the tab. Exports `AUTH_SESSION_KEY`, `LOGIN_ROUTE` (`/login`),
  `PROFILE_ROUTE` (`/profile`) and `DEFAULT_ROUTE` (`/featured-promo-items`) so nothing hard-codes
  a path.
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
- `core/utils/jwt.util.ts` — decodes (never verifies) the payload. `rolesFromToken` flattens both
  claim shapes: one role serialises as a bare string, several as an array. Base64url is padded and
  percent-unescaped so a Chinese `userName` survives the round trip.
- `core/interceptors/auth-token.interceptor.ts` — attaches `Authorization: Bearer …` to requests
  whose URL starts with `environment.apiBaseUrl`, and to nothing else, so the token cannot leak to a
  third party.
- `core/interceptors/auth-error.interceptor.ts` — a 401 clears the session, navigates to `/login`
  with the current URL as `returnUrl`, and **re-throws** so the caller still sees the error.
  `/auth/login` is exempt: its 401 means "wrong password", not "session expired", and the login page
  shows that itself.
- `core/guards/auth.guard.ts` — returns a `UrlTree` to `/login?returnUrl=…` rather than `false`, so
  the redirect is part of the same navigation.
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
- `app.ts` / `app.html` — the shell chrome renders only when signed in. The header's `userName` is
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

- Backend `AuthControllerTests` (32, counting the theory cases): the credential paths, one identical
  401 for every cause, the claims and 24-hour lifetime of a genuinely signed token, the SysConfig
  failures, and that `PasswordHash` reaches neither the response nor the token. Its 個人資料 half
  builds the controller under a hand-made `ClaimsPrincipal` (`SignedInAs`) and covers: the token's
  user is the row that changes, every other account is untouched, the key / roles / IsActive /
  PasswordHash are all unchanged, the name is trimmed, empty and whitespace both 400 without
  writing, no `userId` claim → 401, a vanished account → 404, and the two DTO shapes.
  `AuthRoutingConventionTests` pins `api/auth` + `login` + `profile` + `[FromBody]`, and that
  `UpdateProfileRequest` carries `UserName` and nothing else.
- Backend `AuthorizationTests` (25, counting the theory cases) hosts the real pipeline through
  `TestApiFactory : WebApplicationFactory<Program>` — a `[Fact]` calling an action directly would
  never reach the middleware. Covers: 401 with no token and a `Bearer` challenge header; a `[Theory]`
  over twelve route/verb pairs — `PUT /api/auth/profile` among them, because it shares a controller
  foreign-signed, tampered and wrong-scheme tokens; login working with no token and with a stale
  one; and that rotating the SysConfig secret invalidates outstanding tokens while a re-issued one
  is accepted at once (its own factory, so the rotation cannot leak into the class fixture).
  Swagger stays reachable unauthenticated. 個人資料 is exercised here too, because a **raw JSON**
  body carrying a foreign `userId` and a role list is a request the typed DTO cannot express: helen
  renames herself, admin is untouched, helen gains no role. Those tests build their own factory —
  they mutate the seeded user.
- Backend `AuthorizationConventionTests` (8) pins the wiring: the fallback policy carries
  `DenyAnonymousAuthorizationRequirement`, Bearer is the default scheme, `IssuerSigningKey` is null
  while the resolver is not, `MapInboundClaims` is off with `RoleClaimType = "role"`, **no
  controller type** is `[AllowAnonymous]`, `AuthController.Login` is the only `[AllowAnonymous]`
  action in the assembly, and `UpdateProfile` carries `[Authorize]` — plus a guard that the
  reflection query actually found the controllers, so "only one" cannot pass vacuously.
- Frontend: `jwt.util.spec` (both claim shapes, multi-byte values, every unreadable input),
  `auth.service.spec` (login body, session-not-local storage, restore, corrupt session, storage read
  per call, roles from the token with **no** second request, case-insensitive `hasRole`,
  `clearSession` dropping list state, storage unavailable, and `updateUserName`: the PUT body is
  `{ userName }` alone, the stored name is refreshed while the token / userId / roles survive, the
  API's name wins over the typed one, and a failure leaves the session exactly as it was),
  `auth-token.interceptor.spec` (header
  attached on every verb, absent when signed out, picked up mid-session, never sent off-API),
  `auth-error.interceptor.spec` (401 clears + redirects, `returnUrl`, no self-referencing
  `returnUrl`, error re-thrown, login exempt, other statuses ignored), `auth.guard.spec`
  (allow / `UrlTree` / `returnUrl` / blocks after a clear / blocks on an empty token),
  `login.spec` (validation, trimmed userId and untrimmed password, session stored, `returnUrl`
  honoured and rejected, 401 message), `app.spec` (Admin group shown only to Admin — group, links
  and item count — hidden for `User` and for no roles at all, case-insensitive, the header
  `userName` and its refresh after a rename, the user menu offering 個人資料 → `/profile` and 登出,
  登出 clearing the session and returning to `/login`, and no chrome when signed out), and
  `profile.spec` (帳號 and 角色 read-only — asserted by the **absence** of a control, with exactly
  one `<input>` on the page — the 未設定角色 fallback, the pre-filled name, a save that PUTs the
  trimmed `{ userName }` and lands in all three places at once, userId / roles / token unchanged
  across it, empty and whitespace refused with no request, a failed save leaving the session alone,
  and 取消 restoring the stored name).

## Deferred

- **No refresh token and no expiry countdown.** A 24-hour token simply stops working and the next
  API call's 401 sends the user back to `/login`. The browser never checks `exp` itself.
- **No role gate on routes.** A non-Admin who types `/app-roles` still reaches the page; the API
  serves it, because no endpoint gates on a role yet. Add `[Authorize(Roles = …)]` server-side
  first, then a role guard, if that becomes a requirement.
- **No password change / reset.** `SysConfig.defaultPassword` and `enforcePasswordPolicy` are read
  by nothing; `AppUser.PasswordUpdatedTime` is never written. 個人資料 renames only — a 變更密碼
  section would be the natural place for it.
- **A rename leaves the token's `userName` claim stale.** Nothing reads it — the app takes the name
  from the stored profile, and the API takes the account from `userId` — so the claim is simply out
  of date until the next login. Re-issuing a token on rename would fix it and is not worth the
  extra endpoint contract today.
- **AppUser has no CRUD feature**, so users and their roles are still maintained in SQL. 個人資料 is
  the only page that writes to `dbo.AppUser` at all, and only ever the caller's own row.
