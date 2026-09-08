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

## 異動紀錄 — the RowAudit trail

Every write to a business table leaves one `dbo.RowAudit` row. The mechanism is one service,
`Infrastructure\RowAuditWriter`, and it is entity-agnostic: it works out what to record by
reflection, so a new table needs three call sites and no new audit code.

- **Take `IRowAuditWriter` in the constructor** and declare `private const string AuditTableName = "…"` — the **database** table name (`"FeaturedPromoItem"`, not the model or the route), because that string is what someone reads the trail by.
- **Always the overload that takes the connection and the transaction.** It enlists the audit INSERT in the caller's own transaction, which is the entire guarantee: a change that rolls back takes its audit row with it. The short overload opens its own connection and commits on its own — it is for a caller with no transaction to join, and a repository is never that caller.
- **Insert:** after the post-write re-read, before the commit — `LogInsertAsync(connection, transaction, AuditTableName, created!, ct)`. `ActionDesc` becomes the entity's **first string property in declaration order** (`Course.CourseId`, `AppRole.RoleId`, `Partner.Name`), so put the identifying column early in the model.
- **Update: read the row *before* you write it.** `ActionDesc` is the list of properties that differ between the two snapshots, and that difference cannot be reconstructed afterwards. The pre-read doubles as the 404 check, replacing the `affected == 0` guard as the reason to roll back and return `null`.
- **Delete: read the row *before* you delete it.** Its first string property is the only trace of it the trail keeps. `PublishStatus`, `Partner` and `FeaturedPromoItem` grew a transaction they did not previously need so the DELETE and its audit row land together.
- **A delete that answers 409 rolls back**, so a row still referenced by a child table leaves no audit row either. Same for a failed insert: the exception escapes with the transaction uncommitted.
- **A no-op update still writes a row**, with an empty `ActionDesc`. "Someone saved this and changed nothing" is a fact about the session; a missing row would be indistinguishable from a missing audit call.
- **Comparison is structural, not by reference.** `before` and `after` are two separate reads, so every nav object (`Course.Partner`), members list (`AppRole.Users`) and key list is a fresh instance — compared by reference they would appear in `ActionDesc` on *every* edit and bury the columns that really moved. `RowAuditWriter` compares collections element-wise and other reference types property-by-property, recursively.
- **`UserName` comes from the token's `userName` claim**, via `IHttpContextAccessor`, falling back to `"system"` when nothing is signed in. Never `User.Identity.Name` — `JwtBearerSetup` maps `NameClaimType` to `userId`, so that property would sign every row with a number.
- **`ActionDesc` is `varchar(1000)` — 1000 *bytes*, not characters.** The database collates `Chinese_Taiwan_Stroke_CI_AS` (CP950), where a Chinese character costs two, so a 1000-character 課程名稱 is 2000 bytes and SQL Server rejects the INSERT outright. The writer truncates on a byte budget; do not "simplify" it back to `value[..1000]`.
- A move is an update: `FeaturedPromoItemRepository.MoveSlotAsync` writes one row for the item and one for the occupant it swapped with. The parking slot never reaches the trail — only the before and after states do.

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
- Claims: `sub` / `userId` / `userName`, a `jti`, one **`role`** claim per `dbo.AppUserRole` row,
  and `mustChangePassword` when the account is still on the 預設密碼 (below).
  The short `role` name is kept as-is on validation (see below), so `User.IsInRole("Admin")` reads
  exactly what was issued. Lifetime is `JwtTokenService.TokenLifetime` (24 h) — assert against that
  constant, never a literal.
- `CreateAccessTokenAsync` takes `bool mustChangePassword` as a **required** parameter, not a
  defaulted one: a default of `false` would let a new call site fail open without saying so. The
  claim is written **only when true**, and as the string `"true"` rather than a JSON boolean, so an
  unflagged token is byte-identical to one issued before the flag existed and the browser's decoder
  has one shape to read.

### 預設密碼 — forcing a password change

- `DefaultPasswordService.IsDefaultAsync` compares the **stored hash** to
  `Hash(appConfig.defaultPassword)`. The stored hash, not the submitted password: the credential
  check has already proved they match, and `PasswordHasher.Matches` is fixed-time and
  hex-case-insensitive, so it is correct for free. Call it **after** the credential guard, so a
  rejected login pays for no extra config read.
- It **fails open**, and it is the only thing in the auth code that does: a missing row, unreadable
  JSON, an absent, non-string or blank value all mean "nobody is forced". Failing closed would flag
  every account at once and 403 the entire API. `PasswordPolicyService` reads the *same* JSON row
  and fails **closed** — two opposite failure modes over one document is the confusing part, so both
  files name each other. The blank check runs **before** the comparison, or `Hash("")` becomes a
  real target.
- The flag rides in the token, never in `LoginResponse`. One source of truth, and it survives a
  page reload exactly as the roles do.

### Validating one

- **Closed by default.** `Program.cs` builds `AuthPolicies.PasswordNotDefault` —
  `RequireAuthenticatedUser()` + `MustChangePasswordRequirement` — and sets it as **both**
  `AuthorizationOptions.FallbackPolicy` and `AuthorizationOptions.DefaultPolicy`. A new controller
  is therefore protected the moment it is routed — there is no `[Authorize]` to remember, and no
  list of protected routes to keep in step.
- **Why both.** The *fallback* policy applies only to an endpoint carrying no `IAuthorizeData` at
  all. An endpoint with a **bare `[Authorize]`** bypasses it and combines the *default* policy
  instead — and so does `[Authorize(Roles = …)]`, which sets `useDefaultPolicy = false` just as
  `[Authorize(Policy = …)]` does. Setting only the fallback would have left `UpdateProfile` exempt
  from the password check, and would silently exempt the first role gate anyone adds. Write a future
  role gate as `[Authorize(Policy = AuthPolicies.PasswordNotDefault, Roles = "Admin")]`;
  `AuthorizationConventionTests` fails the build otherwise.
- **`ChangePassword` is the one exemption**, `[Authorize(Policy = AuthPolicies.PasswordChangeExempt)]`
  — authenticated, requirement lifted. Without it a flagged user would be 403'd out of the endpoint
  that clears the flag. Like `[AllowAnonymous]`, it is scoped to a single **action** and pinned by
  exact set equality: exactly one exempt action in the whole API.
- **A flagged caller gets 403, never 401.** A failed requirement on an already-authenticated
  principal forbids rather than challenges, which is what makes this safe to add: the Angular
  `authErrorInterceptor` signs the user out on any 401 outside the login call and explicitly ignores
  a 403. `PasswordChangeRequiredResultHandler` gives that 403 a `ProblemDetails` body — the
  framework's own is empty, which would be the one refusal in this API that does not say why. Its
  handler must **not** call `context.Fail()`: an explicit fail leaves `FailedRequirements` empty,
  and the result handler could then not tell this rejection from any other.
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
- No endpoint gates on a **role** yet; today the roles only drive what the Angular sidebar shows.
  When one is added, name a policy beside the roles — see "Why both" above.

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
- **The forced flow needs no extra check here.** "The new password must differ from the current one"
  already refuses it: for a user flagged `mustChangePassword`, the stored hash *is* the default's
  hash, so "same as current" is exactly "still the default". The token is not re-issued either, so
  the caller stays flagged until they sign in again — which is why the browser signs them out on
  this one page, and only on this one.
