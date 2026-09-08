# Build Spec for CourseGroup
- database schema: `.\database\course.sql`

---

## Summary

`CourseGroup` is the 課程群組 lookup table for the `course` sub-system — the smallest table in the
schema, carrying nothing but its key and a single descriptive label. It groups courses into
top-level categories that the public site uses for navigation and filtering.

It has **no foreign key columns and no junction tables**. Two tables point *at* it — `Course` and
`PartnerCourseGroup` — so the delete path must handle the FK-violation case. Its primary key `pkid`
is `smallint` **IDENTITY**, database-assigned: like `Partner` and unlike `PublishStatus`, `pkid` is
*not* part of the create request.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` `smallint` IDENTITY(1,1) — database-assigned, C# `short` |
| Foreign Keys | N/A |
| Required Fields | `Description` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course.CourseGroup_pkid`, `PartnerCourseGroup.CourseGroup_pkid` (neither feature built yet — links deferred) |
| Query Filters | keyword (`Description`), `pkid` range |
| Default Sort | `pkid ASC` |

---

## Localization

### Chinese Table Name

- CourseGroup: 課程群組
- Description: 課程的分類群組，決定課程在網站上的分類歸屬

### Chinese Column Names

- pkid: 主代碼
- Description: 說明

---

## Required Fields

`pkid` is IDENTITY, so it is excluded from the create request entirely and read-only on the edit
form.

Required (NOT NULL):
- `Description` — `nvarchar(100)`

Optional (nullable):
- **None.** `CourseGroup` has no nullable columns, so there is no `null`-vs-empty-string
  normalization to do on this feature.

---

## Foreign Keys

`CourseGroup` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`CourseGroup` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

Two tables reference `CourseGroup.pkid`:

| Child table | FK column | Nullable | On delete | Sub-system | Status |
|-------------|-----------|----------|-----------|------------|--------|
| `Course` | `Course.CourseGroup_pkid` | Yes | **CASCADE** | `course` | Feature not built — link deferred |
| `PartnerCourseGroup` | `PartnerCourseGroup.CourseGroup_pkid` | No | NO ACTION | `course` | Feature not built — link deferred |

**Deferred.** Neither `/courses` nor `/partner-course-groups` exists in `app.routes.ts` yet, so the
list and detail pages ship **without** those link buttons — a `routerLink` to a missing route only
produces a navigation error. When those features are generated, add to the detail page:

- 對應課程 (icon `pi pi-book`) → `/courses?courseGroupPkid={pkid}`
- 對應合作夥伴課程群組 (icon `pi pi-sitemap`) → `/partner-course-groups?courseGroupPkid={pkid}`

For the same reason the projection carries **no** `CourseCount` subquery. Per CLAUDE.md the
repository must not reference a table another feature owns — a `CourseCount` subquery would break
this repository wherever `Course` is absent.

### Delete semantics differ per child table

This is the one genuinely non-obvious thing about `CourseGroup`, and it is worth stating plainly:

- `FK_Course_CourseGroup` is declared **`ON DELETE CASCADE`**. Deleting a course group therefore
  does *not* raise error 547 for courses — SQL Server **deletes every `Course` row in that group**.
  Since `CourseGroup_pkid` is nullable, `SET NULL` would have been the safer schema choice, but the
  schema as delivered cascades, and this spec builds against the schema as delivered.
- `FK_PartnerCourseGroup_CourseGroup` has **no cascade**, so a group still used by any
  `PartnerCourseGroup` row raises `SqlException` 547 → `CourseGroupDeleteResult.InUse` → **409**.

The delete confirmation on the list page therefore carries an explicit cascade warning
(see *Frontend Notes → Delete Confirmation*), because the 409 guard alone will **not** protect
courses.

---

## N-N Relationships

No junction table has a FK to `CourseGroup.pkid`. `PartnerCourseGroup` reads like a junction from
its name, but it carries its own IDENTITY `pkid` plus `DisplayOrder` and `Description` columns — it
is a first-class entity with two FKs, not a junction row. (Same reasoning already recorded in
`spec/course/Partner.md`.)

**N/A**

---

## Query Filters

`CourseGroup` has exactly one non-key column, so the filter set is deliberately small:

- **keyword**: `string?`
  - `LIKE` contains-match on `Description` — the only string column in the table.

- **pkidFrom / pkidTo**: `short?` (inclusive range)
  - `pkid >= @PkidFrom`, `pkid <= @PkidTo`
  - Included because `Description` alone would leave the drawer with a single control; the key range
    is the only other dimension the table offers. Both bind as `short` so an out-of-range value is a
    model-binding 400 rather than a silent overflow.

No `bit` columns, no dates, no FKs — so no tri-state selects, no date pickers and no lookup-backed
dropdowns in the drawer.

---

## Lookup Endpoints Required

`CourseGroup` needs no lookups of its own (no FKs, no n-n). It **is** an FK target for `Course` and
`PartnerCourseGroup`, so it publishes one:

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/course-groups` | New | Full `CourseGroup` list, `pkid ASC` — option label = `Description` |

This matches the ordering `spec/sample1.spec.md` already assumes for the `CourseGroup` dropdown on
the Course form (option label = `Description`, order by `pkid ASC`).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/course-groups` | List all, `pkid ASC` |
| `POST` | `/api/course-groups/query` | Filtered query (body: `CourseGroupQuery`) |
| `GET` | `/api/course-groups/{id:int}` | Get by pkid; 404 when missing |
| `POST` | `/api/course-groups` | Create; 201 + `CreatedAtAction` |
| `PUT` | `/api/course-groups` | Update — pkid comes from the body, no route param; 404 when missing |
| `DELETE` | `/api/course-groups/{id:int}` | 204 / 404 / **409** when still referenced by `PartnerCourseGroup` |
| `GET` | `/api/lookups/course-groups` | Slim lookup list |

Notes:
- Route segment is kebab-case plural: `CourseGroup` → `/api/course-groups`.
- `pkid` is numeric, so the route segment carries the `:int` constraint while the action parameter
  binds to the column's own CLR type (`smallint` → `short`) — a value above 32767 fails model
  binding and returns 400. `CreatedAtAction` route values need the cast:
  `new { id = (int)created.Pkid }`.
- **No 409 on create.** `pkid` is IDENTITY and no other column is UNIQUE, so a duplicate key is
  unreachable. Two groups may legitimately share a `Description`.
- **409 on delete**: only `PartnerCourseGroup` can trigger it (`Course` cascades — see above). The
  repository catches `SqlException` 547 and reports `CourseGroupDeleteResult.InUse`; the controller
  turns that into a 409 with a Chinese explanation instead of letting a 500 escape.
- Use `BadRequest(ModelState)`, not `ValidationProblem(...)` — the latter needs a
  `ProblemDetailsFactory` off `HttpContext`, which directly-constructed controllers in unit tests do
  not have.

Auth exceptions: none — this project has no auth layer yet.

---

## Backend Notes

### Models

```csharp
public class CourseGroup
{
    public short  Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CourseGroupRequest
{
    // Pkid is NOT here on create; the PUT body is CourseGroupUpdateRequest.
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Description { get; set; } = string.Empty;
}

/// <summary>PUT body — the same fields plus the key, which the collection route needs.</summary>
public class CourseGroupUpdateRequest : CourseGroupRequest
{
    [Range(1, short.MaxValue)] public short Pkid { get; set; }
}

public class CourseGroupQuery
{
    public string? Keyword { get; set; }   // LIKE Description
    public short?  PkidFrom { get; set; }
    public short?  PkidTo { get; set; }
}

/// <summary>Outcome of a delete attempt — InUse maps to 409.</summary>
public enum CourseGroupDeleteResult { Deleted, NotFound, InUse }
```

No `nchar`, `date`, `time`, `decimal`, `bit` or computed columns — no `RTRIM()`, no Dapper type
handlers, no `bool?` tri-state anywhere in this feature.

### SQL — SELECT

One shared `SelectSql` for `GetAllAsync` / `QueryAsync` / `GetByIdAsync`:

```sql
SELECT  g.pkid        AS Pkid,
        g.Description AS Description
FROM    dbo.CourseGroup g
```

`QueryAsync` appends `WHERE` clauses built into a `List<string>` with a `DynamicParameters` bag —
never string-concatenated values. Every read ends with `ORDER BY g.pkid ASC`.

### SQL — INSERT

`pkid` is IDENTITY, so it is omitted from the column list and read back:

```sql
INSERT INTO dbo.CourseGroup (Description)
VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

### SQL — UPDATE

```sql
UPDATE  dbo.CourseGroup
SET     Description = @Description
WHERE   pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM dbo.CourseGroup WHERE pkid = @Pkid;
```

Wrapped in `try/catch (SqlException ex) when (ex.Number == 547)` → `CourseGroupDeleteResult.InUse`.

### Transactions

Create and Update run in a transaction and re-read the row **inside** that transaction (the
`IDbTransaction` is passed through to a private `GetByIdAsync` overload) so the returned record
reflects uncommitted state, per the repository rules in CLAUDE.md.

### N-N Sync Pattern

**N/A** — no junction tables.

### Special Column Notes

- `pkid` `smallint` **IDENTITY** — C# `short`; excluded from `CourseGroupRequest`, present on
  `CourseGroupUpdateRequest`, `disable()`d and rendered **only in edit mode** on the form, cast to
  `int` for `CreatedAtAction`.
- `Description` is `nvarchar(100)` NOT NULL — trimmed on save, `[StringLength(100)]` is the guard.
- No nullable columns, so there is no empty-string-to-`null` normalization step in this repository.

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/course-groups` | `CourseGroupList` | 課程群組 CourseGroup |
| `/course-groups/new` | `CourseGroupForm` | 新增課程群組 |
| `/course-groups/:id/edit` | `CourseGroupForm` | 編輯課程群組 |
| `/course-groups/:id` | `CourseGroupDetail` | 檢視課程群組 |

`new` and `:id/edit` are declared before `:id`.

### Angular model

```ts
export interface CourseGroup {
  pkid: number;
  description: string;
}

export interface CourseGroupRequest {
  description: string;
}

export interface CourseGroupUpdateRequest extends CourseGroupRequest { pkid: number; }

export interface CourseGroupQuery {
  keyword?: string | null;
  pkidFrom?: number | null;
  pkidTo?: number | null;
}

export const EMPTY_COURSE_GROUP_QUERY: CourseGroupQuery = {
  keyword: null, pkidFrom: null, pkidTo: null,
};
```

### Service

`core/services/course-group.service.ts`, modelled on `partner.service.ts`. The key is numeric, so no
`encodeURIComponent`; `update()` PUTs to the collection route with the key in the body. The two
write methods take different types — `create(CourseGroupRequest)` sends no key,
`update(CourseGroupUpdateRequest)` sends one.

### List component

- `p-table`, sortable, paginated, `paginatorPosition="top"`, `dataKey="pkid"`, rows 10/20/50/100.
- Columns: 主代碼, 說明, 操作 (檢視／編輯／刪除). No em-dash fallbacks needed — neither column is
  nullable.
- Filter drawer (`p-drawer`, position right) edits `draftFilters`; only 套用 copies it into
  `filters` and reloads:
  - 關鍵字 → `input pInputText` (placeholder 說明)
  - 主代碼（起）／（迄） → `p-inputnumber` `[min]="1"` `[max]="32767"`
  - No `p-select` in this drawer, so no `appendTo="body"` concern on this feature.
- `hasActiveFilters` tests `!== null` on each field (no `bit` filters here, but the rule is uniform).
- Session-storage keys `course-group-list-filters`, `course-group-list-sort`,
  `course-group-list-page`, read in `ngOnInit`, written on apply/sort/page, all through
  `ListStateService`.
- Default sort `{ field: 'pkid', order: 1 }`.

### Detail component

Read-only `dl.detail-grid` with 主代碼 and 說明. 返回 / 編輯 buttons in the header. No
Primary-Foreign link buttons yet (see *Primary-Foreign Links*).

### Form component

- One component for add and edit; mode derived from the route `:id`.
- No lookups to load, so `ngOnInit` calls `getById` directly in edit mode instead of a one-key
  `forkJoin`.
- Controls: `pkid` `p-inputnumber` (**edit mode only**, `@if (isEditMode)`, `disable()`d — read
  values with `form.getRawValue()`), `description` text input with `maxlength="100"`.
- Trim `description` on save.
- Create POSTs `CourseGroupRequest`; edit PUTs `CourseGroupUpdateRequest` (key from
  `getRawValue()`).

### Delete Confirmation

The cascade on `FK_Course_CourseGroup` makes the standard one-liner insufficient — the message must
say what else disappears:

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
此群組底下的課程將一併被刪除。
```

A 409 response shows 「此課程群組已被合作夥伴課程群組使用，無法刪除。」

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `course-group-list-filters` | Last query filter values |
| `course-group-list-sort` | `{ sortField, sortOrder }` |
| `course-group-list-page` | `{ first, rows }` |

No incoming cross-entity query params — nothing navigates *to* this list yet.

### Sidebar

Add to the **existing** `課程管理 Course` group in `src/app/shared/layout/nav-menu.ts`, after the
`合作夥伴 Partner` entry:
`{ label: '課程群組 CourseGroup', icon: 'pi pi-sitemap', route: '/course-groups' }`.

`app.spec.ts` derives its nav-item count from `NAV_GROUPS`, so this needs no test change.

### Date handling

**N/A** — no date or datetime columns.

### Special Form Behaviors

**N/A** — no auto-defaulting, no conditional visibility beyond the edit-mode-only key control.

### Sub-panels

**N/A**

---

## Tests

### Backend — `src/CMS.API.Tests`

- `Fakes/InMemoryCourseGroupRepository.cs` — mirrors the SQL semantics: `pkid ASC` sort, keyword on
  `Description`, the `pkid` range, IDENTITY assignment on create, plus a `MarkInUse(pkid)` seam that
  reproduces the FK-violation path memory cannot.
- `CourseGroupsControllerTests.cs` — list, filtered query (keyword, range, combined), get-by-id
  found and not-found, create (201 + assigned pkid), update, update-missing 404, delete 204,
  delete-missing 404, delete-in-use 409, and invalid `ModelState` → 400.
- `CourseGroupsRoutingConventionTests.cs` — pins `api/course-groups`, the `query` sub-route, the
  `{id:int}` segments and the PUT with no route template.
- `LookupsControllerTests.cs` — extended with the `course-groups` lookup.

### Frontend — `src/CMS.NG`

- `core/services/course-group.service.spec.ts` — every method's URL and verb, plus error passthrough.
- `course-group-list.spec.ts` — loads via POST /query, renders rows, filter apply/reset,
  session-storage persistence and restore, sort/page persistence, navigation, delete confirmation
  accepted and dismissed, 409 message.
- `course-group-detail.spec.ts` — loads the record, renders both fields, not-found empty state,
  navigation.
- `course-group-form.spec.ts` — add-mode has no pkid control, required-field guard, POST body
  (trimmed), edit-mode patch, key rendered and locked in edit mode, PUT body, cancel navigation.

---

## Files to Create / Modify

| File | Action |
|------|--------|
| `src/CMS.API/Models/CourseGroup.cs` | Create |
| `src/CMS.API/Models/CourseGroupRequest.cs` | Create (holds `CourseGroupUpdateRequest` too) |
| `src/CMS.API/Models/CourseGroupQuery.cs` | Create |
| `src/CMS.API/Models/CourseGroupDeleteResult.cs` | Create |
| `src/CMS.API/Repositories/ICourseGroupRepository.cs` | Create |
| `src/CMS.API/Repositories/CourseGroupRepository.cs` | Create |
| `src/CMS.API/Controllers/CourseGroupsController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `course-groups` |
| `src/CMS.API/Program.cs` | Modify — register the repository |
| `src/CMS.API.Tests/Fakes/InMemoryCourseGroupRepository.cs` | Create |
| `src/CMS.API.Tests/CourseGroupsControllerTests.cs` | Create |
| `src/CMS.API.Tests/CourseGroupsRoutingConventionTests.cs` | Create |
| `src/CMS.API.Tests/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/src/app/core/models/course-group.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/index.ts` | Modify |
| `src/CMS.NG/src/app/core/services/course-group.service.ts` (+ `.spec.ts`) | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` (+ `.spec.ts`) | Modify |
| `src/CMS.NG/src/app/core/services/index.ts` | Modify |
| `src/CMS.NG/src/app/features/course-groups/course-group-list/*` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-detail/*` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-form/*` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-groups.routes.ts` | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — lazy route |
| `src/CMS.NG/src/app/shared/layout/nav-menu.ts` | Modify — entry in existing `課程管理 Course` group |
