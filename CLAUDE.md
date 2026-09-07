# CMS — Full-Stack Project Guide

Angular 20 + .NET 9 Web API + Dapper over MS SQL Server (`SQLEXPRESS`).
Code is generated from the SQL schema in `database\` following `spec\code-gen.convention.md`.
`spec\ui-sample-*.png` are **style** references only — never copy their data.

---

## Layout

```
database\            auth.sql (AppRole/AppUser/AppUserRole/SysConfig), admin.sql, course.sql, promotion.sql
spec\                code-gen.convention.md (authoritative), feature-spec.template.md, sample1/2.spec.md, ui-sample-*.png
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

**Per table, four files** (`AppRole` is the reference implementation):

- `Models\{Table}.cs` — response model; nav objects for FKs, subquery counts for n-n (`UserCount`).
- `Models\{Table}Request.cs` — write DTO; FK pkids only, n-n as a `List<T>` of keys. Validated with data annotations.
- `Models\{Table}Query.cs` — search DTO (`Keyword?`, FK pkids?, bools?, ranges?).
- `Repositories\I{Table}Repository.cs` + `{Table}Repository.cs` — Dapper only.

**Repository rules**

- One `private const string SelectSql` shared by all reads; `QueryAsync` appends `WHERE` clauses built into a `List<string>` + `DynamicParameters` — never string-concatenate values.
- `nchar` columns: always `RTRIM()` in SELECTs.
- Writes run in a transaction; the post-write re-read happens **inside** the transaction (pass the `IDbTransaction` through) so the returned row reflects uncommitted state.
- n-n: delete-then-reinsert the junction rows; trim, de-duplicate case-insensitively, skip blanks.
- Connections come from `IDbConnectionFactory` (`Infrastructure\`), registered as a singleton; repositories are scoped and own the connection lifetime.

**Controller rules** — `Controllers\AppRolesController.cs` is the template:

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/{plural}` | all records |
| POST | `/api/{plural}/query` | filtered search |
| GET | `/api/{plural}/{id}` | 404 when missing |
| POST | `/api/{plural}` | 201 + `CreatedAtAction`; 409 on duplicate key |
| PUT | `/api/{plural}` | **key comes from the body — no route param** |
| DELETE | `/api/{plural}/{id}` | 204 / 404 |
| GET | `/api/lookups/{plural}` | slim list for FK / n-n option controls |

- Route segment is kebab-case plural: `AppRole` → `/api/app-roles`.
- String (`nvarchar`) PKs use a bare `{id}` — **never** `{id:int}`.
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

**Form page rules**

- `forkJoin` for lookups + the edited record in parallel.
- The record key is `disable()`d in edit mode — read values with `form.getRawValue()`, not `form.value`.
- Trim strings on save; send `null` (not `''`) for empty nullable columns.
- n-n `p-multiselect`: `appendTo="body"`, `[maxSelectedLabels]="9999"`, wrapped in `.wrap-chips` (the `::ng-deep` chip-wrap rule lives in `src\styles.scss`).
- `p-datepicker` for `date` columns (ISO string ↔ `Date` on load/save); `[timeOnly]` for `time`.

**Service rules** — `core\services\app-role.service.ts` is the template: single-record routes wrap the id in `encodeURIComponent`; `update()` PUTs to the collection route with the key in the body.

**Sidebar** — add the entry to `NAV_GROUPS` in `src\app\shared\layout\nav-menu.ts`. That is the only place the menu is defined; `app.html` renders it. Current entry: `系統管理 Admin` → `角色 AppRole` → `/app-roles`.

**Routing** — the root router lazy-loads each feature's `{table-plural}.routes.ts`. Order matters: `new` and `:id/edit` must precede `:id`.

**Styling** — shared chrome classes (`page-card`, `page-header`, `form-field`, `detail-grid`, `field-error`, `empty-text`, `wrap-chips`) live in `src\styles.scss`; per-component SCSS uses BEM under a single block class. Theme tokens are CSS custom properties on `:root`.

---

## Testing

**Backend** (`src\CMS.API.Tests`, 32 tests) — controller tests against hand-written in-memory fakes in `Fakes\`; no mocking library. A fake must mirror its SQL repository's semantics (sort order, filter breadth, n-n replace) so the tests stay meaningful. `AppRolesRoutingConventionTests` pins the route shape via reflection, so a controller that drifts from the convention fails the build rather than the frontend.

**Frontend** (`src\CMS.NG`, 50 tests) — default Karma + Jasmine. Services use `provideHttpClientTesting` with `httpMock.verify()` in `afterEach`. Components are tested through their real templates with `provideNoopAnimations()`; protected members are reached via a locally declared `…Internals` type alias rather than `as any`.

Gotchas that already bit once:
- Every spec must contain a real `expect(...)` — `httpMock.expectNone(...)` alone logs "has no expectations".
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

1. Read the table in `database\*.sql`; note the PK type, FKs, n-n junctions, nullable columns.
2. Backend: models → repository (+ interface) → controller → register both in `Program.cs` → add lookup endpoint if the table is an FK target.
3. Backend tests: an in-memory fake + controller tests covering list, filter, view, add, edit, delete, and the not-found / duplicate paths.
4. Frontend: models → service → list/detail/form → `{table-plural}.routes.ts` → lazy route in `app.routes.ts` → `NAV_GROUPS` entry.
5. Frontend tests: service HTTP contract + one spec per component.
6. Verify: `dotnet test`, `ng test --watch=false`, then run both apps and exercise the real CRUD path against the database.
