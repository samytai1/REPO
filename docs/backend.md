# Backend reference

Read **before** writing a model, repository or controller. `CLAUDE.md` indexes the docs and holds the cross-cutting invariants; the conventions themselves are here.

**Stack:** .NET 9, Dapper 2.1.66 (no EF), Microsoft.Data.SqlClient 6.0.2, Swashbuckle 7.2.0.

**Reference implementations:** `AppRole` — string key, n-n junction, FK-target lookup. `PublishStatus` — numeric key the user supplies, no FKs, no n-n, and the FK-referenced delete path. `Partner` — numeric **IDENTITY** key, a nullable column, and a derived (non-`bit`) tri-state filter. `Course` — three FK nav objects via Dapper multi-map (one nullable), two n-n junctions, `date` / `decimal` / `bit` columns, and lookup-only repositories for FK targets that are not features yet.

## Files per table

- `Models\{Table}.cs` — response model; nav objects for FKs, subquery counts for n-n (`UserCount`).
- `Models\{Table}Request.cs` — write DTO; FK pkids only, n-n as a `List<T>` of keys. Validated with data annotations. When the PK is IDENTITY this file also holds `{Table}UpdateRequest : {Table}Request`, which adds the key for the PUT body.
- `Models\{Table}Query.cs` — search DTO (`Keyword?`, FK pkids?, bools?, ranges?).
- `Repositories\I{Table}Repository.cs` + `{Table}Repository.cs` — Dapper only.

## Repository rules

- One `private const string SelectSql` shared by all reads; `QueryAsync` appends `WHERE` clauses built into a `List<string>` + `DynamicParameters` — never string-concatenate values.
- `nchar` columns: always `RTRIM()` in SELECTs (`Certification.Title`).
- Writes run in a transaction; the post-write re-read happens **inside** the transaction (pass the `IDbTransaction` through to a private `GetByIdAsync` overload) so the returned row reflects uncommitted state.
- n-n: delete-then-reinsert the junction rows. String keys: trim, de-duplicate case-insensitively, skip blanks. Numeric keys: `Distinct()` only — the junction PK is composite, so a repeat would violate it.
- **A PK that is not an IDENTITY column** (`PublishStatus.pkid`, `tinyint`) is written explicitly in the INSERT column list — no `SCOPE_IDENTITY()` — and belongs in the request DTO, range-validated.
- **A PK that *is* an IDENTITY column** (`Partner.pkid` `smallint`, `Course.pkid` `int`) is omitted from the INSERT column list and read back with `SELECT CAST(SCOPE_IDENTITY() AS <type>)`. It must not appear on the create DTO — but `PUT /api/{plural}` still needs it, so the update body is a derived `{Table}UpdateRequest : {Table}Request` that adds the key.
- **Deleting a row other tables FK into:** wrap the DELETE in `catch (SqlException ex) when (ex.Number == 547)` and return a three-state result (`Deleted` / `NotFound` / `InUse`) instead of letting a 500 escape. `PublishStatusDeleteResult` is the pattern. When the delete also clears junction rows, run the whole block in a transaction and roll back in the catch (`CourseRepository.DeleteAsync`).
- Normalize a nullable string column to `null` when the request sends blanks or whitespace — never store `''`.
- Never reference a table another sub-system owns (no `CourseCount` subquery on `PublishStatus`) — the repository would break wherever that table is absent.
- **FK nav objects** are slim `{Table}{Target}Ref` classes (`CoursePartnerRef { Pkid, Name }`) declared in the parent's model file — never the full target model; a list read carries hundreds of rows. Fill them with Dapper multi-map: the nav blocks are the **last** columns of `SelectSql`, each block starts with its `splitOn` marker column, and Dapper scans forward from the previous split, so `splitOn: "Name,Description,Description"` resolves left-to-right even with a repeated name. A `LEFT JOIN` miss still materialises an all-null ref, so the map lambda sets the nav to `null` off the FK itself (`course.CourseGroupPkid.HasValue ? cg : null`).
- **An FK target that is not a feature yet** (`CourseGroup`, `JobCategory`, `Certification`) gets a lookup-only `I{Table}Repository.GetLookupAsync()` + `{Table}Lookup` DTO — the `AppUser` pattern — so the form and filter drawer still have options. When that table's own `/crud` lands, repoint the lookup endpoint at the new repository and delete the lookup-only one.
- Bracket `Course.[Hour]` in every SQL statement — `HOUR` is a T-SQL datepart name.
- **A user-supplied natural key** (`FeaturedPromoItem` UNIQUE (`ScheduleOn`, `TrainingCenter_pkid`, `Slot`)) gets an `Is…TakenAsync(…, excludePkid)` check the controller runs before INSERT / UPDATE to return 409, mirroring `AppRole.ExistsAsync`. Re-ordering rows under such a key needs a swap through a parking value inside one transaction (`FeaturedPromoItemRepository.MoveSlotAsync`).
- A search lookup (`IPromotion2Repository.SearchLookupAsync(keyword, take)`) is still lookup-only: `TOP (@Take)`, `LIKE @Prefix` with the wildcard appended in C#, never concatenated.
- `date` columns are `DateOnly`, `time` columns `TimeOnly`; `DapperConfig.Register()` in `Program.cs` installs the handlers before the host is built. No per-feature work needed.
- Connections come from `IDbConnectionFactory` (`Infrastructure\`), registered as a singleton; repositories are scoped and own the connection lifetime.

## Controller rules

`Controllers\AppRolesController.cs` is the template.

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/{plural}` | all records |
| POST | `/api/{plural}/query` | filtered search |
| GET | `/api/{plural}/{id}` | 404 when missing |
| POST | `/api/{plural}` | 201 + `CreatedAtAction`; 409 on duplicate key **only when the user supplies the key** — unreachable for an IDENTITY PK with no other UNIQUE column |
| PUT | `/api/{plural}` | **key comes from the body — no route param** |
| DELETE | `/api/{plural}/{id}` | 204 / 404; 409 when a child table still references the row |
| GET | `/api/lookups/{plural}` | slim list for FK / n-n option controls (`LookupsController`) |

- Route segment is kebab-case plural: `AppRole` → `/api/app-roles`, `PublishStatus` → `/api/publish-statuses`.
- String (`nvarchar`) PKs use a bare `{id}` — **never** `{id:int}`.
- Numeric PKs use `{id:int}` and bind to the column's own CLR type (`tinyint` → `byte`, `smallint` → `short`, `int` → `int`), so an out-of-range key fails model binding with a 400. `CreatedAtAction` route values need a cast for the narrow types (`new { id = (int)created.Pkid }`).
- Use `BadRequest(ModelState)`, not `ValidationProblem(...)`: the latter needs a `ProblemDetailsFactory` from `HttpContext`, which unit-tested controllers constructed directly do not have.
- The 409 body is a `ProblemDetails` whose `Detail` names, in Chinese, the child tables that block the delete — the frontend shows that text.
- `Program.cs` registers every repository as scoped, enables Swagger unconditionally at `/swagger`, and allows CORS from any loopback origin. XML doc comments are compiled (`GenerateDocumentationFile`) and fed to Swagger; `NoWarn` includes 1591 — so every public member still gets a `///` summary.

## Authentication and authorization

`AuthController` holds the endpoints that are not a CRUD table, so it is routed at `api/auth`
rather than a kebab-case plural: `POST /api/auth/login`, `PUT /api/auth/profile` and
`PUT /api/auth/password`. It is the reference for anything that reads credentials or config, and
for anything that acts **on the caller**.

### Issuing a token

- `dbo.AppUser.PasswordHash` is the **lowercase hex SHA-256** of the plain password (`PasswordHasher`
  in `Infrastructure\`). `Matches` decodes both sides and compares with
  `CryptographicOperations.FixedTimeEquals`, so hex casing is irrelevant and the comparison does not
  leak a prefix; a stored value that is not 32 bytes of hex never matches.
- Unknown UserId, `IsActive = 0` and a wrong password all return the **same** 401 `ProblemDetails`
  (`帳號或密碼錯誤。`). `AuthRepository` deliberately does **not** filter on `IsActive` — the
  controller checks it, so every failure takes one code path and cannot diverge in message or timing.
- `AppUserCredential` is internal: it carries `PasswordHash` and must never be returned. The wire
  shape is `LoginResponse` (`userId`, `userName`, `accessToken`) and nothing else.
- The JWT signing secret is the `symmetricSecurityKey` property of the JSON in
  `dbo.SysConfig` where `configKey = 'appConfig'`, read through `ISysConfigRepository` on **every**
  login so a rotated secret needs no restart. Never hard-code it and never move it into
  `appsettings.json`. HS256 needs ≥ 32 bytes; `JwtTokenService` throws a named
  `InvalidOperationException` when the row, the property or the length is wrong.
- Claims: `sub` / `userId` / `userName`, a `jti`, and one **`role`** claim per `dbo.AppUserRole` row.
  The short `role` name is kept as-is on validation (see below), so `User.IsInRole("Admin")` reads
  exactly what was issued. Lifetime is `JwtTokenService.TokenLifetime` (24 h) — assert against that
  constant, never a literal.

### Validating one

- **Closed by default.** `Program.cs` sets an `AuthorizationOptions.FallbackPolicy` of
  `RequireAuthenticatedUser()`, which applies to every endpoint that declares no policy of its own.
  A new controller is therefore protected the moment it is routed — there is no `[Authorize]` to
  remember, and no list of protected routes to keep in step.
- `AuthController.Login` is the **only** `[AllowAnonymous]` action, and **no type carries the
  attribute at all**. That distinction matters: a class-level `[AllowAnonymous]` beats an
  action-level `[Authorize]`, so putting it on the controller would have quietly opened up
  `PUT /api/auth/profile` the moment it was added. `AuthorizationConventionTests` pins both halves —
  no anonymous type, and exactly one anonymous action — so opening an endpoint up is a deliberate
  act.
- `JwtBearerSetup.Configure` holds the validation parameters: signature and lifetime are checked,
  issuer and audience are not (the API issues and consumes its own tokens), `ClockSkew` is one
  minute, and `MapInboundClaims = false` + `RoleClaimType = "role"` keep the short claim names.
- The validation key is the **same** `symmetricSecurityKey` used to sign, read per request through
  `IJwtTokenService.GetSigningKeyAsync` — never a hard-coded `IssuerSigningKey`, so rotating the
  SysConfig row invalidates outstanding tokens without a restart. Because
  `IssuerSigningKeyResolver` is synchronous and the read is not, `OnMessageReceived` awaits the key
  and stashes it in `HttpContext.Items`; the resolver hands that back. An anonymous request carries
  no `Authorization: Bearer` header and so triggers no read at all.
- Middleware order in `Program.cs`: `UseSwagger` → `UseCors` → `UseAuthentication` →
  `UseAuthorization` → `MapControllers`. Swagger sits ahead of authorization on purpose — the docs
  are how a developer gets a token in the first place — and its `AddSecurityDefinition` lets the UI
  send one.
- No endpoint gates on a **role** yet. `[Authorize(Roles = "Admin")]` works if one needs to; today
  the roles only drive what the Angular sidebar shows.

### Acting on the caller

`PUT /api/auth/profile` (個人資料) is the pattern for an endpoint that writes the **signed-in user's
own** row. Copy it rather than inventing a shape.

- The key comes from `User.FindFirstValue(JwtTokenService.UserIdClaimType)` — the validated token's
  `userId` claim, with `sub` as a fallback — and **never** from the request. No route param either:
  a caller has nothing to pass.
- The request DTO carries **only the editable columns**. `UpdateProfileRequest` has one property,
  `UserName`; there is no `UserId` and no role list, so a body that sends them has nothing to bind
  to and System.Text.Json drops them. That is what makes "a userId in the body is ignored" true, and
  it is worth a reflection test (`AuthRoutingConventionTests`) so a later property cannot undo it.
- `[Required]` rejects `null` and `""` but **not** `"   "`, so the action trims and re-checks before
  it reaches SQL; both paths answer 400 with `ModelState`.
- The repository's UPDATE names one column. `AuthRepository.UpdateUserNameAsync` sets `UserName` and
  nothing else — not the key, not `IsActive`, not `PasswordHash` — and never touches
  `dbo.AppUserRole`, so 個人資料 cannot become a privilege-escalation path. It returns the row
  re-read **inside** the transaction, or `null` when no row matched (→ 404: the token validated but
  its account is gone).
- The response is its own narrow model. `UserProfileResponse` is `{ UserId, UserName }`: no
  PasswordHash, and no roles either — those ride in the token's `role` claims, so a rename does not
  re-issue one and nothing about the caller's authority changes.

### 變更密碼 — and the status code it lives or dies by

`PUT /api/auth/password` follows every rule above, plus these.

- **A rejected password is a 400, never a 401.** The Angular `authErrorInterceptor` treats any 401
  outside `/auth/login` as "your session expired": it clears the session and redirects to `/login`.
  Answering a mistyped 目前密碼 with 401 would therefore sign the user out mid-change instead of
  showing them the message. `AuthController.PasswordRejected` is the one helper for these, and its
  `ProblemDetails.Detail` is written to be shown verbatim. The only 401 left is a token with no
  `userId` claim at all, where signing out is the right answer.
- **Knowing the current password is the gate.** `PasswordHasher.Matches` checks it before anything
  is written, and the new password must differ from it.
- **Strength is `IPasswordPolicyService`, not a data annotation.** `PasswordPolicyService` reads
  `enforcePasswordPolicy` out of the same SysConfig JSON as the signing key, on every call, so
  flipping it needs no restart. It **fails closed**: a missing row, unreadable JSON or an absent
  property all mean "enforced", because a configuration mistake must not quietly switch the rules
  off. When on: `MinimumLength` (8) plus an upper-case letter, a lower-case letter, a digit and a
  symbol. When off: non-empty. Assert against `PasswordPolicyService.PolicyMessage`, never a
  literal.
- **Passwords are never trimmed**, on either side of the wire — trimming would make a legitimate
  password that starts or ends with a space impossible to type again. Contrast `UserName`, which is
  trimmed.
- **The success answer is 204 with no body**, which is the strongest form of "no password, hashed
  or not, travels back". The repository takes an already-hashed value, so a plain password never
  reaches a `DynamicParameters`, a log or a profiler trace, and `UpdatePasswordAsync` stamps
  `PasswordUpdatedTime = SYSDATETIME()` alongside the hash.
- **The session deliberately survives.** Tokens are signed with one global secret and there is no
  revocation, so signing the user out here would only imply an invalidation that does not actually
  happen on their other devices.
