# CMS — Full-Stack Project Guide

Angular 20 + .NET 9 Web API + Dapper over MS SQL Server (`SQLEXPRESS`).
Code is generated from the SQL schema in `database\` following `spec\code-gen.convention.md`.
`spec\ui-sample-*.png` are **style** references only — never copy their data.

---

## Layout

```
database\            auth.sql (AppRole/AppUser/AppUserRole/SysConfig), admin.sql (those four plus PublishStatus/RowAudit), course.sql, promotion.sql
spec\                code-gen.convention.md (authoritative), feature-spec.template.md, sample1/2.spec.md, ui-sample-*.png
spec\{sub-system}\   per-feature build specs, e.g. spec\admin\PublishStatus.md, spec\course\Partner.md (written by the /crud skill)
global.json          pins .NET SDK 9.0.316 (SDK 10 is also installed — do not let templates target net10.0)
src\CMS.sln          CMS.API + CMS.API.Tests
src\CMS.API\         Models\ Repositories\ Controllers\ Infrastructure\
src\CMS.API.Tests\   xUnit; Fakes\ holds in-memory repositories
src\CMS.NG\          Angular app — src\app\{core,shared,features}\
```

## Commands

| Task | Command |
|------|---------|
| Run API (http://localhost:5000, Swagger at `/swagger`) | `cd src\CMS.API && dotnet run` |
| Build backend | `cd src && dotnet build CMS.sln` |
| Backend tests | `cd src && dotnet test CMS.sln` |
| Run frontend (http://localhost:4200) | `cd src\CMS.NG && ng serve` |
| Frontend tests | `cd src\CMS.NG && ng test --watch=false --browsers=ChromeHeadless` |
| Frontend build | `cd src\CMS.NG && npm run build` |

Karma needs a browser: `$env:CHROME_BIN = "C:\Program Files\Google\Chrome\Application\chrome.exe"`.

---

## Backend

**Stack:** .NET 9, Dapper 2.1.66 (no EF), Microsoft.Data.SqlClient 6.0.2, Swashbuckle 7.2.0. Every data method is `async` and takes a `CancellationToken`.

**Reference implementations:** `AppRole` — string key, n-n junction, FK-target lookup. `PublishStatus` — numeric key the user supplies, no FKs, no n-n, and the FK-referenced delete path. `Partner` — numeric **IDENTITY** key, a nullable column, and a derived (non-`bit`) tri-state filter.

**Per table, four files:**

- `Models\{Table}.cs` — response model; nav objects for FKs, subquery counts for n-n (`UserCount`).
- `Models\{Table}Request.cs` — write DTO; FK pkids only, n-n as a `List<T>` of keys. Validated with data annotations. When the PK is IDENTITY this file also holds `{Table}UpdateRequest : {Table}Request`, which adds the key for the PUT body.
- `Models\{Table}Query.cs` — search DTO (`Keyword?`, FK pkids?, bools?, ranges?).
- `Repositories\I{Table}Repository.cs` + `{Table}Repository.cs` — Dapper only.

**Repository rules**

- One `private const string SelectSql` shared by all reads; `QueryAsync` appends `WHERE` clauses built into a `List<string>` + `DynamicParameters` — never string-concatenate values.
- `nchar` columns: always `RTRIM()` in SELECTs.
- Writes run in a transaction; the post-write re-read happens **inside** the transaction (pass the `IDbTransaction` through) so the returned row reflects uncommitted state.
- n-n: delete-then-reinsert the junction rows; trim, de-duplicate case-insensitively, skip blanks.
- **A PK that is not an IDENTITY column** (`PublishStatus.pkid`, `tinyint`) is written explicitly in the INSERT column list — no `SCOPE_IDENTITY()` — and belongs in the request DTO, range-validated.
- **A PK that *is* an IDENTITY column** (`Partner.pkid`, `smallint`) is omitted from the INSERT column list and read back with `SELECT CAST(SCOPE_IDENTITY() AS <type>)`. It must not appear on the create DTO — but `PUT /api/{plural}` still needs it, so the update body is a derived `{Table}UpdateRequest : {Table}Request` that adds the key.
- **Deleting a row other tables FK into:** wrap the DELETE in `catch (SqlException ex) when (ex.Number == 547)` and return a three-state result (`Deleted` / `NotFound` / `InUse`) instead of letting a 500 escape. `PublishStatusDeleteResult` is the pattern.
- Normalize a nullable string column to `null` when the request sends blanks or whitespace — never store `''`.
- Never reference a table another sub-system owns (no `CourseCount` subquery on `PublishStatus`) — the repository would break wherever that table is absent.
- Connections come from `IDbConnectionFactory` (`Infrastructure\`), registered as a singleton; repositories are scoped and own the connection lifetime.

**Controller rules** — `Controllers\AppRolesController.cs` is the template:

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/{plural}` | all records |
| POST | `/api/{plural}/query` | filtered search |
| GET | `/api/{plural}/{id}` | 404 when missing |
| POST | `/api/{plural}` | 201 + `CreatedAtAction`; 409 on duplicate key **only when the user supplies the key** — unreachable for an IDENTITY PK with no other UNIQUE column |
| PUT | `/api/{plural}` | **key comes from the body — no route param** |
| DELETE | `/api/{plural}/{id}` | 204 / 404; 409 when a child table still references the row |
| GET | `/api/lookups/{plural}` | slim list for FK / n-n option controls |

- Route segment is kebab-case plural: `AppRole` → `/api/app-roles`, `PublishStatus` → `/api/publish-statuses`.
- String (`nvarchar`) PKs use a bare `{id}` — **never** `{id:int}`.
- Numeric PKs use `{id:int}` and bind to the column's own CLR type (`tinyint` → `byte`, `smallint` → `short`), so an out-of-range key fails model binding with a 400. `CreatedAtAction` route values need a cast (`new { id = (int)created.Pkid }`).
- Use `BadRequest(ModelState)`, not `ValidationProblem(...)`: the latter needs a `ProblemDetailsFactory` from `HttpContext`, which unit-tested controllers constructed directly do not have.
- `Program.cs` registers the Dapper `DateOnly`/`TimeOnly` handlers (`DapperConfig.Register()`) before the host is built, enables Swagger unconditionally at `/swagger`, and allows CORS from any loopback origin. XML doc comments are compiled (`GenerateDocumentationFile`) and fed to Swagger; `NoWarn` includes 1591.

---

## Frontend

**Stack:** Angular 20 standalone (no NgModules), PrimeNG 20.4.0 + `@primeuix/themes` Aura + primeicons, Reactive Forms, signals for component state.

**Path aliases** (`tsconfig.json`): `@environments/*`, `@env`, `@core/*`, `@shared/*`, `@features/*`.

**Environments — no dev-server proxy.** `environment.development.ts` points at `http://localhost:5000/api`; `environment.ts` (production) uses `/api`. `fileReplacements` is configured for both the `development` build config **and** the `test` target, so specs read the dev API base URL.

**Per feature, three pages** under `features\{table-plural}\`:

- `{table}-list\` — `p-table` (sortable, paginated, `paginatorPosition="top"`), filter drawer (`p-drawer`), row actions view/edit/delete with `ConfirmationService`.
- `{table}-detail\` — read-only `dl.detail-grid`.
- `{table}-form\` — one component for add **and** edit; mode derived from the route `:id`.

**List page rules**

- Session-storage keys `{table}-list-filters`, `{table}-list-sort`, `{table}-list-page`, read in `ngOnInit`, written on apply/sort/page. All access goes through `ListStateService`, which swallows storage errors.
- The drawer edits `draftFilters`; only 套用 copies it into `filters` and reloads.
- `p-select` in a drawer: always `appendTo="body"`; `[filter]="true"` at 10+ options; add `[virtualScroll]="true" [virtualScrollItemSize]="43"` at 100+.
- `bit` columns filter tri-state through a `p-select` of 不限(`null`)／是(`true`)／否(`false`). `hasActiveFilters` must test `!== null`, not truthiness — a `false` filter is still a filter. The same tri-state applies to a *derived* `bool?` filter over a nullable column (`Partner.hasImage` → 不限／有／無), which the SQL resolves as an `IS NULL` / blank test rather than a column comparison.
- `bit` columns render as a 是／否 `p-tag` in the list and detail, never as a raw boolean.

**Form page rules**

- `forkJoin` for lookups + the edited record in parallel. With no lookups to load (no FKs, no n-n), call `getById` directly instead — a one-key `forkJoin` is noise.
- The record key is `disable()`d in edit mode — read values with `form.getRawValue()`, not `form.value`. This applies to numeric keys too, not just string ones. An IDENTITY key has no meaning before insert, so its control is rendered **only** in edit mode (`@if (isEditMode)`); a user-supplied key is shown in both modes.
- Trim strings on save; send `null` (not `''`) for empty nullable columns.
- n-n `p-multiselect`: `appendTo="body"`, `[maxSelectedLabels]="9999"`, wrapped in `.wrap-chips` (the `::ng-deep` chip-wrap rule lives in `src\styles.scss`).
- `p-datepicker` for `date` columns (ISO string ↔ `Date` on load/save); `[timeOnly]` for `time`.

**Service rules** — `core\services\app-role.service.ts` is the template: `update()` PUTs to the collection route with the key in the body. Single-record routes wrap a **string** id in `encodeURIComponent`; numeric ids are interpolated as-is (`publish-status.service.ts`). With an IDENTITY key the two write methods take different types — `create(PartnerRequest)` sends no key, `update(PartnerUpdateRequest)` sends one (`partner.service.ts`).

**Sidebar** — add the entry to `NAV_GROUPS` in `src\app\shared\layout\nav-menu.ts`. That is the only place the menu is defined; `app.html` renders it. Current entries: `系統管理 Admin` → `角色 AppRole` → `/app-roles`, `發布狀態 PublishStatus` → `/publish-statuses`; `課程管理 Course` → `合作夥伴 Partner` → `/partners`.

**Routing** — the root router lazy-loads each feature's `{table-plural}.routes.ts`. Order matters: `new` and `:id/edit` must precede `:id`.

**Styling** — shared chrome classes (`page-card`, `page-header`, `form-field`, `detail-grid`, `field-error`, `empty-text`, `wrap-chips`) live in `src\styles.scss`; per-component SCSS uses BEM under a single block class. Theme tokens are CSS custom properties on `:root`.

---

## Testing

**Backend** (`src\CMS.API.Tests`, 97 tests) — controller tests against hand-written in-memory fakes in `Fakes\`; no mocking library. A fake must mirror its SQL repository's semantics (sort order, filter breadth, n-n replace) so the tests stay meaningful. `AppRolesRoutingConventionTests` pins the route shape via reflection, so a controller that drifts from the convention fails the build rather than the frontend.

**Frontend** (`src\CMS.NG`, 146 tests) — default Karma + Jasmine. Services use `provideHttpClientTesting` with `httpMock.verify()` in `afterEach`. Components are tested through their real templates with `provideNoopAnimations()`; protected members are reached via a locally declared `…Internals` type alias rather than `as any`.

Gotchas that already bit once:
- Every spec must contain a real `expect(...)` — `httpMock.expectNone(...)` alone logs "has no expectations".
- `app.spec.ts` derives its nav-item count from `NAV_GROUPS`. Never hard-code it — a literal count breaks on the next sidebar entry. Its group-collapse test subtracts only `NAV_GROUPS[0].items.length`: with more than one group, collapsing the first no longer empties the menu.
- `p-table` emits `onPage` during first render, so a persisted `first` offset beyond the result set is clamped to 0. Don't assert an out-of-range restored offset.
- Give shell specs a matching route (`provideRouter([{ path: '**', children: [] }])`) or `routerLink` logs navigation errors.

---

## Environment gotchas

- **PrimeNG must not be an `-lts` version.** The LTS builds render a red "invalid license" banner over every page. Pinned to `20.4.0`.
- PrimeNG 20 peers need `@angular/animations` and `@angular/cdk` installed explicitly; `ng new` does not add them.
- After changing an npm dependency, **restart `ng serve`** — Vite serves a stale bundle and reports phantom module-resolution errors otherwise.
- Production bundle budgets were raised to 1 MB / 2 MB; PrimeNG + Aura puts the baseline near 670 kB.
- Connection string lives in `src\CMS.API\appsettings.json` under `ConnectionStrings:CMS`.

---

## Adding the next feature

1. Read the table in `database\*.sql`; note the PK type (**and whether it is IDENTITY**), FKs, n-n junctions, nullable columns, and which tables FK into it. `/crud TABLE=… SUB_SYSTEM=…` writes this up as `spec\{sub-system}\{Table}.md` first, then builds from it.
2. Backend: models → repository (+ interface) → controller → register both in `Program.cs` → add lookup endpoint if the table is an FK target.
3. Backend tests: an in-memory fake + controller tests covering list, filter, view, add, edit, delete, and the not-found / duplicate / still-referenced paths. Give the fake a seam for anything the SQL layer does that memory cannot (e.g. `MarkInUse` for the FK violation).
4. Frontend: models → service → list/detail/form → `{table-plural}.routes.ts` → lazy route in `app.routes.ts` → `NAV_GROUPS` entry. Defer cross-entity link buttons whose target route does not exist yet, and record in the spec what to add when it does.
5. Frontend tests: service HTTP contract + one spec per component.
6. Verify: `dotnet test`, `ng test --watch=false`, then run both apps and exercise the real CRUD path against the database.
