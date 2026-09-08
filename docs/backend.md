# Backend reference

Read before writing a model, repository or controller. `CLAUDE.md` has the summary; this is the detail.

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
