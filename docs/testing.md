# Testing reference

Read before writing tests, or when a test fails for a non-obvious reason.

## Backend — `src\CMS.API.Tests` (194 tests)

Controller tests against hand-written in-memory fakes in `Fakes\`; no mocking library.

- A fake must mirror its SQL repository's semantics — sort order, filter breadth, n-n replace and de-duplication, blank-to-null normalisation, IDENTITY assignment — so the tests stay meaningful. `InMemoryCourseRepository` is the fullest example: it also seeds the FK targets so nav objects resolve, and exposes `JobCategoryLinks(pkid)` / `CertificationLinks(pkid)` so the n-n write tests can read the junction state.
- Give the fake a seam for anything the SQL layer does that memory cannot: `MarkInUse(pkid)` reproduces the FK-violation delete path.
- Lookup-only fakes (`InMemoryCourseGroupRepository`) take `params` in the constructor **and** offer a fluent `Seed(...)`, so `LookupsControllerTests` and feature tests can both use them.
- Every `{Table}sControllerTests` covers: list (sort + projection), each query filter in turn, get-by-id found / not-found, create (201 + `CreatedAtAction` route value + trimming + null normalisation + junctions), update (+ junction replace, + 404), delete (204 / 404 / 409), and invalid `ModelState` → 400.
- `{Table}sRoutingConventionTests` pins the route shape via reflection — `api/{plural}`, the `query` sub-route, `{id:int}` (or bare `{id}`), PUT with no template, the id parameter's CLR type, and that the create DTO has no `Pkid` when the key is IDENTITY — so a controller that drifts from the convention fails the build rather than the frontend.
- `LookupsControllerTests` builds one controller with every fake and asserts each lookup's sort order.

## Frontend — `src\CMS.NG` (302 tests)

Default Karma + Jasmine. Services use `provideHttpClientTesting` with `httpMock.verify()` in `afterEach`. Components are tested through their real templates with `provideNoopAnimations()`; protected members are reached via a locally declared `…Internals` type alias rather than `as any`.

- Service specs assert every method's URL and verb, that `create` sends no `pkid`, that `update` PUTs to the collection URL with `pkid` in the body, and error passthrough (404, 409).
- List specs: a `flushInitialLoad()` helper answers the lookups **and** the initial `POST /query`. Cover rendering (each requested column, null fallbacks, `p-tag` for bits), filters (apply → body, `false` counts as active, persist, restore, reset), sort/page persistence, navigation, and delete (accepted, dismissed, 409 message).
- In-place cell editing (`course-list.spec.ts`): load a **single** row so `tbody tr:first-child` is deterministic, then address cells by `td[data-field="…"]`. Drive the real DOM for the gesture — a `MouseEvent('dblclick')` opens the editor, a `click` must not — and for at least one full round trip (set `input.value`, dispatch `input` then `blur`). Validation cases go through the component (`startEdit` → `editValue` → `commitEdit`) and assert all four halves: the message, the cell **still open**, and `expectNone` on both the GET and the PUT. Every save flushes two requests, the `getById` re-read and the PUT, and the PUT body is worth asserting on the fields the list never shows (`friendlyUrl`, the n-n key arrays).
- Form specs: a `setup(pkid)` helper flushes every lookup, then the record in edit mode. Cover add-mode defaults, option mapping, required-field guard, derived defaults, the POST body (trimmed, nulls, ISO dates, n-n arrays, no `pkid`), edit-mode patching (dates parsed, n-n keys), the locked key, the PUT body, cancel in both modes.
- Detail specs: loads, renders every section, null fallbacks, chips, FK links by `href`, no request without a usable key, 404 empty state, navigation.

## Gotchas that already bit once

- Every spec must contain a real `expect(...)` — `httpMock.expectNone(...)` alone logs "has no expectations".
- `app.spec.ts` derives its nav-item count from `NAV_GROUPS`. Never hard-code it — a literal count breaks on the next sidebar entry. Its group-collapse test subtracts only `NAV_GROUPS[0].items.length`: with more than one group, collapsing the first no longer empties the menu. Find a menu link by its label, never by position — the first group changed once already (`首頁 Home`).
- A child component driven by signal inputs is set up with `fixture.componentRef.setInput(...)` before the first `detectChanges()`; its `output()`s are observed with `.subscribe` on the instance (`featured-promo-item-form.spec.ts`).
- Specs that depend on "today" (the default week) assert shape — a Monday within the last seven days — rather than a literal date; a restored session-storage state gives the deterministic path.
- `p-table` emits `onPage` during first render, so a persisted `first` offset beyond the result set is clamped to 0. Don't assert an out-of-range restored offset.
- Give shell specs a matching route (`provideRouter([{ path: '**', children: [] }])`) or `routerLink` logs navigation errors.
- `forkJoin` cancels its sibling requests the moment one errors. A spec that fails one lookup must flush the healthy ones **first** — a cancelled `TestRequest` cannot be flushed.
- `dotnet build` cannot overwrite `bin\Debug\net9.0\CMS.API.exe` / `.dll` while `dotnet run` holds them. To run the backend tests without stopping the API, redirect output: `dotnet test CMS.API.Tests/CMS.API.Tests.csproj -p:OutDir=<scratch dir>/`.
- `DecimalPipe` in specs formats with the default `en-US` locale: `24000 | number:'1.0-0'` renders `24,000`, `30 | number:'1.1-1'` renders `30.0`.
