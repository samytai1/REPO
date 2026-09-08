# Frontend reference

Read before writing a list / detail / form page or a service. `CLAUDE.md` has the summary; this is the detail.

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
- Delete confirmation names the record: `確定要刪除主代碼 ${pkid}「${name}」？`; a 409 shows the Chinese "still used by …" message.

## Detail page rules

- One `page-card` per section with an `<h2 class="form-section__title">`; `dl.detail-grid` inside.
- Nullable values render `—` with `empty-text`; long text gets `white-space: pre-wrap`.
- n-n members render as `p-tag` chips with the count in the heading (`職務類別（{{ count }}）`).
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

## Service rules

`core\services\app-role.service.ts` is the template: `update()` PUTs to the collection route with the key in the body. Single-record routes wrap a **string** id in `encodeURIComponent`; numeric ids are interpolated as-is (`publish-status.service.ts`). With an IDENTITY key the two write methods take different types — `create(PartnerRequest)` sends no key, `update(PartnerUpdateRequest)` sends one (`partner.service.ts`).

Lookups all live in `lookup.service.ts` (`getPartners`, `getCourseGroups`, …); models in `core\models\` with a barrel `index.ts` — add the new model there. A lookup DTO for a not-yet-built table is its own model file (`course-group.model.ts`), mirroring `app-user.model.ts`; a label helper for a nullable label (`certificationLabel`) lives beside it.

## Sidebar and routing

- Add the entry to `NAV_GROUPS` in `src\app\shared\layout\nav-menu.ts`. That is the only place the menu is defined; `app.html` renders it. Current entries: `首頁 Home` → `上稿作業 FeaturedPromoItem` → `/featured-promo-items`; `系統管理 Admin` → `角色 AppRole` → `/app-roles`, `發布狀態 PublishStatus` → `/publish-statuses`; `課程管理 Course` → `課程 Course` → `/courses`, `合作夥伴 Partner` → `/partners`.

## Custom pages

A spec under `spec\custom\` describes a page that is not the list / detail / form triple. Build it
as a feature folder with its own components and a single route, reuse the same service / lookup /
`ListStateService` conventions, and write a build spec in `spec\{sub-system}\` recording the
decisions. `featured-promo-items\` is the reference: a computed grid (`days`) so empty cells render,
an inline child form driven by signal inputs and `output()`s, a `p-autocomplete` FK lookup whose
selection pre-fills sibling fields, and a clipboard signal mirrored to session storage.
- The root router lazy-loads each feature's `{table-plural}.routes.ts`. Order matters: `new` and `:id/edit` must precede `:id`.

## Styling

Shared chrome classes (`page-card`, `page-header`, `form-section__title`, `form-field`, `detail-grid`, `field-error`, `empty-text`, `wrap-chips`) live in `src\styles.scss`; per-component SCSS uses BEM under a single block class (`course-form__row`). Theme tokens are CSS custom properties on `:root` (`--app-accent`). Each component re-declares the `:host ::ng-deep .w-full` helper for PrimeNG `styleClass`.
