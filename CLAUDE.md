# CMS — Full-Stack Project Guide

Angular 20 + .NET 9 Web API + Dapper over MS SQL Server (`SQLEXPRESS`).
Code is generated from the SQL schema in `database\` following `spec\code-gen.convention.md`.
`spec\ui-sample-*.png` are **style** references only — never copy their data.

This file holds only what every task needs. The detail lives in `docs\` — read the relevant file
**before** writing code in that area, not after something breaks.

| Read | When |
|------|------|
| `docs\backend.md` | writing or reviewing a model, repository or controller — PK kinds (IDENTITY vs user-supplied), FK nav objects / Dapper multi-map, n-n junctions, the delete-409 path, lookup-only repositories, model binding |
| `docs\frontend.md` | writing or reviewing a list / detail / form page or a service — drawer and tri-state filters, form defaults, date handling, nullable-FK selects, styling |
| `docs\testing.md` | writing tests, or a test fails for a non-obvious reason — fake semantics, spec conventions, the gotchas that already bit once |
| `docs\environment.md` | the toolchain misbehaves — PrimeNG licence banner, stale Vite bundle, locked `bin\`, bundle budgets, connection string |
| `spec\{sub-system}\{Table}.md` | working on that feature — its build spec, deferred links, migration notes |

---

## Layout

```
database\            auth.sql (AppRole/AppUser/AppUserRole/SysConfig), admin.sql (those four plus PublishStatus/RowAudit), course.sql, promotion.sql
spec\                code-gen.convention.md (authoritative), feature-spec.template.md, sample1/2.spec.md, ui-sample-*.png
spec\{sub-system}\   per-feature build specs, e.g. spec\admin\PublishStatus.md, spec\course\Course.md, spec\promotion\FeaturedPromoItem.md (written by /crud, or by hand for a custom page)
spec\custom\{Table}\ hand-written functional spec + ui-*.spec.png mock-ups for a page that is not the list/detail/form triple — the input to a custom build
docs\                on-demand reference for this guide (see table above)
.claude\skills\crud\ the /crud skill: schema → spec → build (untracked)
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

## Rules that apply to every feature

**Reference implementations** — copy the closest one: `AppRole` (string key, n-n, FK-target lookup), `PublishStatus` (user-supplied numeric key, delete-409), `Partner` (IDENTITY key, nullable column, derived tri-state filter), `Course` (three FK nav objects via multi-map, two n-n, `date`/`decimal`/`bit`, lookup-only repositories). `FeaturedPromoItem` is the one **custom** page — a weekly grid with an inline form instead of list/detail/form; its spec is `spec\promotion\FeaturedPromoItem.md`.

**Backend** (`docs\backend.md` for the how)

- Per table: `Models\{Table}.cs`, `{Table}Request.cs` (+ `{Table}UpdateRequest : {Table}Request` when the PK is IDENTITY), `{Table}Query.cs`, `Repositories\I{Table}Repository.cs` + `{Table}Repository.cs`. Dapper only; every data method is `async` with a `CancellationToken`. Register the repository in `Program.cs`.
- One `SelectSql` per repository; `WHERE` built from `List<string>` + `DynamicParameters`, never concatenated values. `nchar` → `RTRIM()`. Writes run in a transaction and re-read **inside** it. Nullable strings store `null`, never `''`. Never touch a table another sub-system owns.
- Routes, kebab-case plural: `GET /api/{plural}`, `POST …/query`, `GET …/{id}`, `POST …`, `PUT …` (**key in the body, no route param**), `DELETE …/{id}`, `GET /api/lookups/{plural}` for FK targets. Numeric ids `{id:int}`, string ids bare `{id}`. `BadRequest(ModelState)`, not `ValidationProblem`.

**Frontend** (`docs\frontend.md` for the how)

- Per feature under `features\{table-plural}\`: `{table}-list` (`p-table` + `p-drawer` filters), `{table}-detail` (`dl.detail-grid`), `{table}-form` (add **and** edit, mode from the route `:id`).
- List state in session storage `{table}-list-filters` / `-sort` / `-page` through `ListStateService`; the drawer edits `draftFilters`, only 套用 commits. `bit` columns are 是／否 `p-tag`s and filter tri-state; test `!== null`, not truthiness.
- In-place cell editing (`course-list`): `dblclick` opens, blur commits, `Esc` cancels — PrimeNG's `pEditableColumn` opens on a **single** click, so the cell shell is hand-rolled (`data-field` + `(dblclick)` + `@if`) with an `EDITABLE_FIELDS` guard; the key and FK label columns stay read-only. Validate **before** the request — a failure keeps the cell open, a save failure closes it and the untouched row shows the old value. A cell save re-reads the record with `getById` and PUTs the **whole** thing: the list projection omits the n-n keys and the API replaces junctions from the request.
- Form: the key control is `disable()`d in edit mode, so read `form.getRawValue()`; an IDENTITY key renders only in edit mode. Trim strings; send `null` for blanks. `forkJoin` lookups with the record.
- Service: `update()` PUTs to the collection route with the key in the body; `encodeURIComponent` only for string ids.
- Wire-up: `{table-plural}.routes.ts` (`new` and `:id/edit` before `:id`) → lazy route in `app.routes.ts` → entry in `NAV_GROUPS` (`shared\layout\nav-menu.ts`, the only menu definition). Current: `首頁 Home` → `/featured-promo-items`; `系統管理 Admin` → `/app-roles`, `/publish-statuses`; `課程管理 Course` → `/courses`, `/partners`.

**Testing** (`docs\testing.md` for the how) — backend: xUnit against hand-written fakes in `Fakes\` (194 tests). Frontend: Karma + Jasmine (302 tests). Both suites pass before a feature is reported done.

---

## Adding the next feature

1. Read the table in `database\*.sql`: PK type (**IDENTITY or not**), FKs, n-n junctions, nullable columns, UNIQUE natural keys, tables that FK into it. `/crud TABLE=… SUB_SYSTEM=…` writes `spec\{sub-system}\{Table}.md` first, then builds from it. A **custom page** starts from `spec\custom\{Table}\` instead: read its spec and mock-ups, then write the build spec yourself (`spec\promotion\FeaturedPromoItem.md` is the model) before coding.
2. Backend: models → repository (+ interface) → controller → `Program.cs` → lookup endpoint if it is an FK target. Read `docs\backend.md` first.
3. Backend tests: in-memory fake + controller tests for list, filter, view, add, edit, delete, not-found / duplicate / still-referenced. Give the fake a seam for what memory cannot do (`MarkInUse`).
4. Frontend: models → service → list/detail/form → routes → `app.routes.ts` → `NAV_GROUPS`. Read `docs\frontend.md` first. Defer link buttons whose target route does not exist; record them in the spec.
5. Frontend tests: service HTTP contract + one spec per component.
6. Verify: `dotnet test`, `ng test --watch=false`, then run both apps and exercise the real CRUD path against the database.
