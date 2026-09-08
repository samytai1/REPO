# CMS — Full-Stack Project Guide

Angular 20 + .NET 9 Web API + Dapper over MS SQL Server (`SQLEXPRESS`). UI text is Chinese.
Code is generated from the SQL schema in `database\` following `spec\code-gen.convention.md`.
`spec\ui-sample-*.png` are **style** references only — never copy their data.

This file is the index. Every convention lives in `docs\` — **read the relevant file before writing
code in that area**, not after something breaks.

| Read | Before |
|------|--------|
| `docs\backend.md` | a model, repository or controller — PK kinds (IDENTITY vs user-supplied), FK nav objects / multi-map, n-n junctions, delete-409, lookup-only repositories, routes, model binding, **login / JWT**, **an endpoint that acts on the caller** |
| `docs\frontend.md` | a list / detail / form page or a service — drawer and tri-state filters, in-place cell editing, form defaults, dates, nullable-FK selects, routing, styling, **the signed-in session, the user menu and 個人資料** |
| `docs\testing.md` | tests — fake semantics, per-suite checklists, and the gotchas that already bit once |
| `docs\environment.md` | fighting the toolchain — PrimeNG licence banner, stale Vite bundle, locked `bin\`, budgets, connection string, `sqlcmd` |
| `spec\{sub-system}\{Table}.md` | touching that feature — its build spec, deferred links, migration notes |
| `spec\auth\auth-authz.spec.md` | anything touching identity or access — the **derived** auth/authz reference: token and claim contract, the closed-by-default policy, the 400-vs-401 rule, what is enforced server-side vs only shown, and the residual risks |

## Layout

```
database\            auth.sql, admin.sql (+ PublishStatus/RowAudit), course.sql, promotion.sql
spec\                code-gen.convention.md (authoritative), feature-spec.template.md, sample1/2.spec.md, ui-sample-*.png
spec\{sub-system}\   per-feature build specs (admin\, auth\, course\, promotion\) — written by /crud, or by hand for a custom page
spec\custom\{Table}\ hand-written functional spec + ui-*.spec.png mock-ups for a page that is not the list/detail/form triple
docs\                the conventions (see table above)
.claude\skills\crud\ the /crud skill: schema → spec → build (untracked)
global.json          pins .NET SDK 9.0.316
src\CMS.sln          CMS.API + CMS.API.Tests
src\CMS.API\         Models\ Repositories\ Controllers\ Infrastructure\
src\CMS.API.Tests\   xUnit; Fakes\ holds in-memory repositories
src\CMS.NG\          Angular app — src\app\{core,shared,features}\; core\ holds models, services, guards, interceptors, utils, testing
                     features\ is one folder per feature; auth\login and profile\ are single pages, not list/detail/form triples
```

## Commands

| Task | Command |
|------|---------|
| Run API (http://localhost:5000, Swagger at `/swagger`) | `cd src\CMS.API && dotnet run` |
| Build / test backend | `cd src && dotnet build CMS.sln` · `dotnet test CMS.sln` |
| Run frontend (http://localhost:4200) | `cd src\CMS.NG && ng serve` |
| Frontend build / test | `npm run build` · `ng test --watch=false --browsers=ChromeHeadless` |

Karma needs `$env:CHROME_BIN = "C:\Program Files\Google\Chrome\Application\chrome.exe"`.
`dotnet build` fails while `dotnet run` holds `bin\` — see `docs\environment.md`.

## Reference implementations

Copy the closest one rather than inventing a shape:

| | Backend + frontend + tests |
|---|---|
| `AppRole` | string key, n-n junction, FK-target lookup |
| `PublishStatus` | user-supplied numeric key, no FKs, the delete-409 path |
| `Partner` | IDENTITY key, nullable column, derived tri-state filter |
| `Course` | three FK nav objects via multi-map, two n-n, `date`/`decimal`/`bit`, lookup-only repositories, in-place cell editing |
| `FeaturedPromoItem` | the one **custom** page — weekly grid + inline form instead of list/detail/form (`spec\promotion\FeaturedPromoItem.md`) |
| `Auth` | the **non-CRUD** endpoints and the one public page — `POST /api/auth/login` (SHA-256 credential check, JWT signed with a secret read from `dbo.SysConfig`), `PUT /api/auth/profile` and `PUT /api/auth/password` (個人資料: the signed-in user renames themselves and changes their own password), plus bearer validation, the login page, the interceptors, the guard, the header user menu and the role-gated sidebar. Build history in `spec\auth\Auth.md`; the behavioural contract, enforcement matrix and residual risks in `spec\auth\auth-authz.spec.md` |

## Invariants

Silently wrong if you guess; the details are in `docs\`.

- Dapper only — no EF. Every data method is `async` with a `CancellationToken`; values reach SQL through `DynamicParameters`, never concatenation.
- `PUT /api/{plural}` takes the **key in the body, no route param**. Routes are kebab-case plural — `api/auth` is the only exception, because it is not a table.
- A repository never reads a table another sub-system owns.
- Nullable strings store `null`, never `''` — on both sides of the wire.
- `bit` renders as a 是／否 `p-tag` and filters tri-state; "is a filter set?" tests `!== null`, not truthiness.
- Dates convert in **local** time via `core\utils\date.util.ts` — never `toISOString().split('T')[0]`.
- Secrets live in `dbo.SysConfig` (`configKey = 'appConfig'`) and are read at runtime — never hard-coded, never in `appsettings.json`.
- A login failure is one generic 401 whatever went wrong, and `PasswordHash` never reaches a response model.
- **The API is closed by default**: a global fallback policy requires an authenticated user, and `AuthController.Login` is the only `[AllowAnonymous]` **action**. No *type* carries the attribute — a class-level `[AllowAnonymous]` beats an action-level `[Authorize]`, so it would silently open up every action added beside it. A new controller needs no `[Authorize]` — and must not opt out.
- The bearer validation key is the **same** SysConfig secret used to sign, re-read per request. Never a hard-coded `IssuerSigningKey`.
- The signed-in profile lives in **session** storage (`cms-auth`), never local storage; signing out — or any 401 — clears the whole of it and returns to `/login`.
- Roles come from the token's `role` claims, never a second API call. They gate what the sidebar **shows** — and that is currently their only effect.
- **No endpoint gates on a role.** The fallback policy checks *authentication*, not authority: there is no `[Authorize(Roles = …)]` anywhere, so any signed-in user can call every CRUD route — including `PUT /api/app-roles`, whose `UserIds` rewrites `dbo.AppUserRole` and can therefore grant Admin. Hiding a menu group hides nothing. When you add a role gate, add it **server-side first**; a guard or a hidden menu is not a control. See `spec\auth\auth-authz.spec.md` §5.3.
- An endpoint that acts **on the caller** reads its UserId from the token's `userId` claim, never from the request — and its DTO has no property for a key or a role list, so a body cannot name another account.
- **Only `/api/auth/login` may answer 401 for a bad password.** Everywhere else a 401 means "session expired" to `authErrorInterceptor`, which signs the user out — so a rejected 變更密碼 (wrong current password, weak new one) is a **400** carrying the reason in `ProblemDetails.detail`.
- Password rules live in `enforcePasswordPolicy` (SysConfig), read per request and **failing closed**: an unreadable config keeps the rules on. The server owns them; the browser checks only "filled in" and "both entries match" and shows the server's message.
- A feature is done only when `dotnet test` **and** `ng test --watch=false` both pass.

## Adding the next feature

1. **Read the table** in `database\*.sql`: PK type (**IDENTITY or not**), FKs, n-n junctions, nullable columns, UNIQUE natural keys, tables that FK into it.
2. **Spec first.** `/crud TABLE=… SUB_SYSTEM=…` writes `spec\{sub-system}\{Table}.md`, then builds from it. A custom page starts from `spec\custom\{Table}\` — read its spec and mock-ups, then write the build spec by hand (`spec\promotion\FeaturedPromoItem.md` is the model).
3. **Backend:** models → repository (+ interface) → controller → `Program.cs` → lookup endpoint if it is an FK target.
4. **Backend tests:** in-memory fake + controller tests for list, filter, view, add, edit, delete, not-found / duplicate / still-referenced.
5. **Frontend:** models → service → list/detail/form → `{table-plural}.routes.ts` → `app.routes.ts` (**with `canActivate: [authGuard]`**) → `NAV_GROUPS`. Defer link buttons whose target route does not exist and record them in the spec.
6. **Frontend tests:** service HTTP contract + one spec per component.
7. **Verify for real:** both suites, then run both apps and exercise the CRUD path against the database.
