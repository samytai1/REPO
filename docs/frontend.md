# Frontend reference

Read **before** writing a list / detail / form page or a service. `CLAUDE.md` indexes the docs and holds the cross-cutting invariants; the conventions themselves are here.

**Stack:** Angular 20 standalone (no NgModules), PrimeNG 20.4.0 + `@primeuix/themes` Aura + primeicons, Reactive Forms, signals for component state.

**Path aliases** (`tsconfig.json`): `@environments/*`, `@env`, `@core/*`, `@shared/*`, `@features/*`.

**Environments — no dev-server proxy.** `environment.development.ts` points at `http://localhost:5000/api`; `environment.ts` (production) uses `/api`. `fileReplacements` is configured for both the `development` build config **and** the `test` target, so specs read the dev API base URL.

## Three pages per feature

Under `features\{table-plural}\`:

- `{table}-list\` — `p-table` (sortable, paginated, `paginatorPosition="top"`), filter drawer (`p-drawer`), row actions view/edit/delete with `ConfirmationService`.
- `{table}-detail\` — read-only `dl.detail-grid`.
- `{table}-form\` — one component for add **and** edit; mode derived from the route `:id`.

Templates to copy: `partners\*` (IDENTITY key, no lookups), `app-roles\*` (n-n multiselect), `courses\*` (FK selects, dates, many fields, grouped sections).

## List page rules

- Session-storage keys `{table}-list-filters`, `{table}-list-sort`, `{table}-list-page`, read in `ngOnInit`, written on apply/sort/page. All access goes through `ListStateService`, which swallows storage errors. Merge the stored filters over `EMPTY_{TABLE}_QUERY` so a filter added later still reads as `null`.
- The drawer edits `draftFilters`; only 套用 copies it into `filters` and reloads. 清除 resets both and clears the storage key.
- `p-select` in a drawer: always `appendTo="body"`; `[filter]="true"` at 10+ options; add `[virtualScroll]="true" [virtualScrollItemSize]="43"` at 100+ (`courseGroups().length >= 100`).
- Filter-drawer FK selects lead with a `不限` (`null`) option instead of `showClear`.
- `bit` columns filter tri-state through a `p-select` of 不限(`null`)／是(`true`)／否(`false`). `hasActiveFilters` must test `!== null`, not truthiness — a `false` filter is still a filter. The same tri-state applies to a *derived* `bool?` filter over a nullable column (`Partner.hasImage` → 不限／有／無), which the SQL resolves as an `IS NULL` / blank test rather than a column comparison.
- `bit` columns render as a 是／否 `p-tag` in the list and detail, never as a raw boolean.
- A drawer that filters on dates keeps a `DraftFilters` copy with `Date` fields for `p-datepicker` and converts to ISO on 套用 (`course-list.ts` `toDraft` / `toQuery`).
- FK label columns read the row's nav object (`course.partner?.name`); the drawer lookups load through `forkJoin` in parallel with the query and only feed the option lists.
- Wide tables: `[scrollable]="true"` with a `[tableStyle]` min-width, and the 操作 column frozen right (`pFrozenColumn alignFrozen="right"`).
- **In-place cell editing** (`course-list` is the reference; `spec\course\Course.md` has the full rules). PrimeNG's `pEditableColumn` opens on a **single** click and has no double-click mode, so a dblclick-only page hand-rolls the cell shell — `data-field` + `(dblclick)="startEdit(row, field)"` + an `@if` that swaps the text for the matching PrimeNG widget — and keeps an `EDITABLE_FIELDS` guard inside `startEdit` so a read-only column cannot be opened programmatically either. Commit on the widget's `(blur)` / `(onBlur)`, cancel on `Esc`. Validate **before** the request: a failure sets one shared `editError` and leaves the cell open; a save failure closes it, which restores the previous value because the row was never mutated. A cell save must send the **whole** record — re-read it with `getById` first when the list projection omits anything the PUT rewrites, n-n keys above all (the API replaces junctions from the request, so a list row PUT straight back wipes them).
- Delete confirmation names the record: `確定要刪除主代碼 ${pkid}「${name}」？`; a 409 shows the Chinese "still used by …" message.

## Detail page rules

- One `page-card` per section with an `<h2 class="form-section__title">`; `dl.detail-grid` inside.
- Nullable values render `—` with `empty-text`; long text gets `white-space: pre-wrap`.
- n-n members render as `p-tag` chips with the count in the heading (`職務類別（{{ count }}）`).
- A QR code for an external page is the shared `app-qr-code` (`shared\qr-code\`): signal inputs `data` (the URL), `caption` (the label above the code, its `alt` text and the download's file name) and `size`. It encodes with the `qrcode` package into a `data:image/png` URL — asynchronous, so a spec awaits `fixture.whenStable()` — and its 下載 button clicks a throw-away anchor carrying that same URL, so the saved PNG is exactly what is on screen. `qrcode` is CommonJS and is listed in `allowedCommonJsDependencies`. `course-detail` is its one user (`COURSE_SHOW_URL_BASE`).
- FK values link to the target's detail page **only when that route exists** (`/partners/:id`, `/publish-statuses/:id`); otherwise plain text. Child-table link buttons (`對應…`) are deferred until the child feature exists — record what to add in the spec.

## Form page rules

- `forkJoin` for lookups + the edited record in parallel. With no lookups to load (no FKs, no n-n), call `getById` directly instead — a one-key `forkJoin` is noise.
- The record key is `disable()`d in edit mode — read values with `form.getRawValue()`, not `form.value`. This applies to numeric keys too, not just string ones. An IDENTITY key has no meaning before insert, so its control is rendered **only** in edit mode (`@if (isEditMode)`); a user-supplied key is shown in both modes.
- Trim strings on save; send `null` (not `''`) for empty nullable columns. Required FK selects and dates are typed `T | null` in the form; after `form.invalid` a small guard narrows them before building the request.
- n-n `p-multiselect`: `appendTo="body"`, `[maxSelectedLabels]="9999"`, wrapped in `.wrap-chips` (the `::ng-deep` chip-wrap rule lives in `src\styles.scss`).
- `p-datepicker` for `date` columns (ISO string ↔ `Date` on load/save); `[timeOnly]` for `time`. The conversions live in `core\utils\date.util.ts` (`toIso` / `fromIso`) and work in **local** time — never `toISOString().split('T')[0]`, which shifts a UTC+8 evening back a day.
- A nullable FK `p-select` carries an explicit `無` (`null`) option; a required FK `p-select` has none.
- A form-level default derived from another control (`scheduleOff = scheduleOn + 10 years`) subscribes to `valueChanges` in the constructor with `takeUntilDestroyed()`, writes with `{ emitEvent: false }`, and bails out in edit mode or once the target control is `dirty` (`course-form.ts`).
- Numeric widgets: `p-inputnumber` with `[useGrouping]="false"` for keys and orders; `[maxFractionDigits]` / `[minFractionDigits]` matching the column's `decimal` scale.
- Long text: `textarea pTextarea`; `maxlength` matches the column, none for `nvarchar(max)`.
- A form long enough to scroll pins its 取消／儲存 header (`course-form__toolbar`: `position: sticky; top: 0; z-index: 20`). The page scrolls on the **document** — `.app-content` sets no `overflow`, and an `overflow` anywhere up the ancestor chain silently kills `sticky` — so the bar pins to the viewport but only ever spans the content grid column, never the sidebar. Keep the z-index low: PrimeNG's `appendTo="body"` overlays and the toast must still stack above it. A spec asserts the **computed** `position`, not the class; a bar that stopped sticking keeps its class.

## Service rules

`core\services\app-role.service.ts` is the template: `update()` PUTs to the collection route with the key in the body. Single-record routes wrap a **string** id in `encodeURIComponent`; numeric ids are interpolated as-is (`publish-status.service.ts`). With an IDENTITY key the two write methods take different types — `create(PartnerRequest)` sends no key, `update(PartnerUpdateRequest)` sends one (`partner.service.ts`).

Lookups all live in `lookup.service.ts` (`getPartners`, `getCourseGroups`, …); models in `core\models\` with a barrel `index.ts` — add the new model there. A lookup DTO for a not-yet-built table is its own model file (`course-group.model.ts`), mirroring `app-user.model.ts`; a label helper for a nullable label (`certificationLabel`) lives beside it.

## Sidebar and routing

- The root router lazy-loads each feature's `{table-plural}.routes.ts`. Order matters: `new` and `:id/edit` must precede `:id`.
- Add the entry to `NAV_GROUPS` in `src\app\shared\layout\nav-menu.ts`. That is the only place the menu is defined; `app.html` renders it. Current entries: `首頁 Home` → `上稿作業 FeaturedPromoItem` → `/featured-promo-items`; `系統管理 Admin` → `角色 AppRole` → `/app-roles`, `發布狀態 PublishStatus` → `/publish-statuses`; `課程管理 Course` → `課程 Course` → `/courses`, `合作夥伴 Partner` → `/partners`.
- Every feature route carries `canActivate: [authGuard]`; only `login` is public. A new feature adds the guard alongside its `loadChildren` — see below. `profile` is a `loadComponent` route rather than a feature triple, but it carries the guard like everything else.
- A `NavGroup` may carry `roles`. Omit it and everyone signed in sees the group; list roles and `App` filters the group out entirely for anyone else. `系統管理 Admin` lists `Admin`.

## Login, 個人資料 and the signed-in session

The two pages outside the feature triple pattern. `spec\auth\Auth.md` is their build spec.

- The profile (`userId`, `userName`, `accessToken`) lives in **session** storage under `cms-auth`, so it dies with the tab. `AuthService` (`core\services\auth.service.ts`) owns it; `LOGIN_ROUTE`, `PROFILE_ROUTE` and `DEFAULT_ROUTE` are exported from there so nothing hard-codes `'/login'`.
- **Storage is the source of truth for "is there a token?"** — `token()` / `hasToken()` re-read it on every call, because another tab may have signed out. The `profile` / `userName` / `roles` signals mirror it for the templates.
- **Roles come from the token**, decoded (not verified) by `core\utils\jwt.util.ts`: `role` claims serialise as a bare string when there is one and an array when there are several, and both shapes flatten to a list. Never add an API call to fetch roles. This gates what the menu *shows*; the API re-validates every request regardless.
- `authTokenInterceptor` attaches `Authorization: Bearer …` to requests whose URL starts with `environment.apiBaseUrl` — and to nothing else, so the token never leaks to a third party.
- `authErrorInterceptor` turns a 401 into a sign-out: clear the session, navigate to `/login` with the current URL as `returnUrl`, and re-throw so the caller still sees the error. `/auth/login` itself is exempt — its 401 means "wrong password", and the login page shows that message.
- `authGuard` (`core\guards\auth.guard.ts`) returns a `UrlTree` to `/login?returnUrl=…` rather than a boolean, so the redirect is part of the same navigation.
- **Signing out clears *all* of session storage**, list filters included — the next user must not inherit the previous one's view. `AuthService.clearSession()` is the single place that happens, and both the 登出 button and the 401 path go through it.
- A `returnUrl` is honoured only when it is a path inside this app: it must start with a single `/` and must not be the login page. Anything else lands on `DEFAULT_ROUTE`.
- The shell chrome (sidebar + header) renders only when signed in — `app.html` guards both on `signedIn()`, and `.app-shell--anonymous` drops the sidebar column so the login page fills the viewport.
- The header's user name is the trigger for a **user menu** — a `p-menu` in `[popup]` mode whose model is `App.userMenuItems`: 個人資料 as a `routerLink`, 登出 as a `command` (signing out has to clear the session *before* the navigation). It deliberately carries **no** `appendTo`, unlike the drawer selects: rendering inline means the overlay dies with the shell instead of being left behind in `document.body` for the next spec to find.
- **個人資料 My Profile** (`features\profile\`) is a single component on `/profile`, not a list / detail / form triple, because everything it shows is already in the session. 帳號 and the roles render read-only — 帳號 from `AuthService.userId`, the roles as `p-tag` chips from `AuthService.roles` — with **no GET endpoint and no second request**, the same rule the sidebar's role gate follows. 使用者名稱 is the only control: required, trimmed, and trimmed-to-empty rejected client-side with the same message the API's 400 carries.
- `AuthService.updateUserName(name)` PUTs `{ userName }` — and only that — to `/api/auth/profile`, then merges the returned name into the stored profile. The **token is kept as it is**: the API does not re-issue one, so `userId` and the roles are unchanged, and the header updates because it reads the `userName` signal. Store what the API returned, not what was typed.
- **變更密碼** is a second, independent form on the same page (目前密碼 / 新密碼 / 確認新密碼), so a failure in one card never discards what was typed in the other. `AuthService.changePassword(current, next)` PUTs `{ currentPassword, newPassword }` and **touches the session not at all** — the API answers 204 and keeps the token valid.
- **The server owns the password rules; the form does not restate them.** `enforcePasswordPolicy` lives in SysConfig, which the browser cannot see, so the form validates only "filled in" and "the two new entries match" and renders the policy as hint text. Everything else — wrong current password, too weak, same as the current one — comes back as a **400** whose `ProblemDetails.detail` is shown verbatim. Encoding the rules in the form as well would let the two drift apart.
- A 400 is what makes this work at all: a 401 would reach `authErrorInterceptor` and sign the user out over a typo. Never surface a password failure as 401.
- Passwords are sent exactly as typed, never trimmed, and the boxes are emptied on success so the secrets are not left sitting in the DOM.
- `core\testing\auth.testing.ts` is test-only: `signIn(roles)` writes a session, `fakeAccessToken(roles)` builds a JWT-shaped token. Nothing in the browser verifies a signature, so a correctly *shaped* token is all a spec needs.

## Custom pages

A spec under `spec\custom\` describes a page that is not the list / detail / form triple. Build it
as a feature folder with its own components and a single route, reuse the same service / lookup /
`ListStateService` conventions, and write a build spec in `spec\{sub-system}\` recording the
decisions. `featured-promo-items\` is the reference: a computed grid (`days`) so empty cells render,
an inline child form driven by signal inputs and `output()`s, a `p-autocomplete` FK lookup whose
selection pre-fills sibling fields, and a clipboard signal mirrored to session storage.

## Styling

Shared chrome classes (`page-card`, `page-header`, `form-section__title`, `form-field`, `detail-grid`, `field-error`, `empty-text`, `wrap-chips`) live in `src\styles.scss`; per-component SCSS uses BEM under a single block class (`course-form__row`). Theme tokens are CSS custom properties on `:root` (`--app-accent`). Each component re-declares the `:host ::ng-deep .w-full` helper for PrimeNG `styleClass`.
