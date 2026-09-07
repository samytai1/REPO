# Build Spec for Partner
- database schema: `.\database\course.sql`

---

## Summary

`Partner` is the 課程供應商／品牌 lookup table for the `course` sub-system. It holds the partner's
internal name, its application key, the two display names used on the public site (menu bar and
course-detail page), a sort order, and an optional logo filename.

It has **no foreign key columns and no junction tables** — three other tables point *at* it
(`Certification`, `Course`, `PartnerCourseGroup`), so the delete path must handle the FK-violation
case. Its primary key `pkid` is `smallint` **IDENTITY**, so the database assigns it: unlike
`PublishStatus`, `pkid` is *not* part of the create request.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` `smallint` IDENTITY(1,1) — database-assigned, C# `short` |
| Foreign Keys | N/A |
| Required Fields | `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`, `DisplayOrder` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Certification.Partner_pkid`, `Course.Partner_pkid`, `PartnerCourseGroup.Partner_pkid` (none of those features built yet — links deferred) |
| Query Filters | keyword (`Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`), `DisplayOrder` range, `HasImage` (tri-state) |
| Default Sort | `DisplayOrder ASC, pkid ASC` |

---

## Localization

### Chinese Table Name

- Partner: 合作夥伴
- Description: 課程的供應商／品牌，決定課程在選單與課程明細頁上的品牌顯示方式

### Chinese Column Names

- pkid: 主代碼
- Name: 名稱
- AppKey: 應用代碼
- NameOnPartnerMenu: 選單顯示名稱
- NameOnCourseDetailPage: 課程明細頁名稱
- DisplayOrder: 顯示順序
- ImageFilename: 圖片檔名

---

## Required Fields

`pkid` is IDENTITY, so it is excluded from the request entirely (create) and read-only (edit).

Required (NOT NULL):
- `Name` — `nvarchar(50)`
- `AppKey` — `varchar(10)`
- `NameOnPartnerMenu` — `nvarchar(200)`
- `NameOnCourseDetailPage` — `nvarchar(50)`
- `DisplayOrder` — `int`

Optional (nullable):
- `ImageFilename` — `varchar(50)` — send `null`, never `''`, when the field is left blank

---

## Foreign Keys

`Partner` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`Partner` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

Three tables reference `Partner.pkid`:

| Child table | FK column | Sub-system | Status |
|-------------|-----------|------------|--------|
| `Certification` | `Partner_pkid` | `course` | Feature not built — link deferred |
| `Course` | `Partner_pkid` | `course` | Feature not built — link deferred |
| `PartnerCourseGroup` | `Partner_pkid` | `course` | Feature not built — link deferred |

**Deferred.** None of `/certifications`, `/courses`, `/partner-course-groups` exists in
`app.routes.ts` yet, so the list and detail pages ship **without** those link buttons — a
`routerLink` to a missing route only produces a navigation error. When those features are
generated, add to the detail page:

- 對應認證 (icon `pi pi-verified`) → `/certifications?partnerPkid={pkid}`
- 對應課程 (icon `pi pi-book`) → `/courses?partnerPkid={pkid}`
- 對應課程群組 (icon `pi pi-sitemap`) → `/partner-course-groups?partnerPkid={pkid}`

For the same reason the projection carries **no** `CourseCount` / `CertificationCount` subquery.
`Certification` and `Course` happen to live in the same sub-system, but they are separate features
that may not be present, and per CLAUDE.md the repository must not depend on tables it does not own.

---

## N-N Relationships

No junction table has a FK to `Partner.pkid`. `PartnerCourseGroup` looks like a junction from its
name, but it carries its own IDENTITY `pkid` plus `DisplayOrder` and `Description` columns — it is a
first-class entity with two FKs, not a junction row.

**N/A**

---

## Query Filters

- **keyword**: `string?`
  - `LIKE '%…%'` across the four short identifying string columns:
    `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`.
  - `ImageFilename` is excluded — it is a storage detail, not an identifier.

- **displayOrderFrom / displayOrderTo**: `int?` (inclusive range)
  - `DisplayOrder >= @DisplayOrderFrom`, `DisplayOrder <= @DisplayOrderTo`

- **hasImage**: `bool?` — tri-state (null = 不限, true = 有, false = 無)
  - `true` → `ImageFilename IS NOT NULL AND LTRIM(RTRIM(ImageFilename)) <> ''`
  - `false` → `ImageFilename IS NULL OR LTRIM(RTRIM(ImageFilename)) = ''`
  - This is a derived filter, not a `bit` column; the frontend still renders it as the standard
    tri-state `p-select`, and `hasActiveFilters` must test `!== null` so a `false` still counts.

---

## Lookup Endpoints Required

`Partner` needs no lookups of its own (no FKs, no n-n). It **is** an FK target for `Certification`,
`Course` and `PartnerCourseGroup`, so it publishes one:

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | New | Full `Partner` list, `DisplayOrder ASC, pkid ASC` — option label = `Name` |

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/partners` | List all, `DisplayOrder ASC, pkid ASC` |
| `POST` | `/api/partners/query` | Filtered query (body: `PartnerQuery`) |
| `GET` | `/api/partners/{id:int}` | Get by pkid; 404 when missing |
| `POST` | `/api/partners` | Create; 201 + `CreatedAtAction` |
| `PUT` | `/api/partners` | Update — pkid comes from the body, no route param; 404 when missing |
| `DELETE` | `/api/partners/{id:int}` | 204 / 404 / **409** when still referenced |
| `GET` | `/api/lookups/partners` | Slim lookup list |

Notes:
- `pkid` is numeric, so the route segment carries the `:int` constraint. The action parameter binds
  to the column's own CLR type (`smallint` → `short`), so a value above 32767 fails model binding
  and returns 400. `CreatedAtAction` route values need the cast: `new { id = (int)created.Pkid }`.
- **No 409 on create.** `pkid` is IDENTITY and there is no UNIQUE constraint on any other column, so
  a duplicate key is not reachable — unlike `PublishStatus`, whose key the user supplies.
- **409 on delete**: `Certification`, `Course` and `PartnerCourseGroup` all have FK constraints on
  this table. The repository catches `SqlException` 547 and reports `PartnerDeleteResult.InUse`; the
  controller turns that into a 409 with a Chinese explanation instead of letting a 500 escape.
- Use `BadRequest(ModelState)`, not `ValidationProblem(...)` — the latter needs a
  `ProblemDetailsFactory` off `HttpContext`, which directly-constructed controllers in unit tests
  do not have.

Auth exceptions: none — this project has no auth layer yet.

---

## Backend Notes

### Models

```csharp
public class Partner
{
    public short  Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int    DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }
}

public class PartnerRequest
{
    // Pkid is NOT here on create; the PUT body is PartnerUpdateRequest.
    [Required(AllowEmptyStrings = false)] [StringLength(50)]  public string Name { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false)] [StringLength(10)]  public string AppKey { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false)] [StringLength(200)] public string NameOnPartnerMenu { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false)] [StringLength(50)]  public string NameOnCourseDetailPage { get; set; } = string.Empty;
    [Range(0, int.MaxValue)]                                  public int DisplayOrder { get; set; }
    [StringLength(50)]                                        public string? ImageFilename { get; set; }
}

/// <summary>PUT body — the same fields plus the key, which the collection route needs.</summary>
public class PartnerUpdateRequest : PartnerRequest
{
    [Range(1, short.MaxValue)] public short Pkid { get; set; }
}

public class PartnerQuery
{
    public string? Keyword { get; set; }   // LIKE Name / AppKey / NameOnPartnerMenu / NameOnCourseDetailPage
    public int?  DisplayOrderFrom { get; set; }
    public int?  DisplayOrderTo { get; set; }
    public bool? HasImage { get; set; }
}

/// <summary>Outcome of a delete attempt — InUse maps to 409.</summary>
public enum PartnerDeleteResult { Deleted, NotFound, InUse }
```

`PartnerUpdateRequest` is the one shape difference from `PublishStatus`: because the key is
IDENTITY it cannot live on the create DTO, but `PUT /api/partners` still takes the key from the
body. Deriving the update DTO keeps the create DTO clean and the validation rules in one place.

No `nchar`, `date`, `time`, `decimal` or computed columns — no `RTRIM()` and no Dapper type handlers
are involved.

### SQL — SELECT

One shared `SelectSql` for `GetAllAsync` / `QueryAsync` / `GetByIdAsync`:

```sql
SELECT  p.pkid                    AS Pkid,
        p.Name                    AS Name,
        p.AppKey                  AS AppKey,
        p.NameOnPartnerMenu       AS NameOnPartnerMenu,
        p.NameOnCourseDetailPage  AS NameOnCourseDetailPage,
        p.DisplayOrder            AS DisplayOrder,
        p.ImageFilename           AS ImageFilename
FROM    dbo.Partner p
```

`QueryAsync` appends `WHERE` clauses built into a `List<string>` with a `DynamicParameters` bag —
never string-concatenated values. Every read ends with `ORDER BY p.DisplayOrder ASC, p.pkid ASC`.

### SQL — INSERT

`pkid` is IDENTITY, so it is omitted from the column list and read back:

```sql
INSERT INTO dbo.Partner
        (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES  (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

### SQL — UPDATE

```sql
UPDATE  dbo.Partner
SET     Name                   = @Name,
        AppKey                 = @AppKey,
        NameOnPartnerMenu      = @NameOnPartnerMenu,
        NameOnCourseDetailPage = @NameOnCourseDetailPage,
        DisplayOrder           = @DisplayOrder,
        ImageFilename          = @ImageFilename
WHERE   pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM dbo.Partner WHERE pkid = @Pkid;
```

Wrapped in `try/catch (SqlException ex) when (ex.Number == 547)` → `PartnerDeleteResult.InUse`.

### Transactions

Create and Update run in a transaction and re-read the row **inside** that transaction (the
`IDbTransaction` is passed through to a private `GetByIdAsync` overload) so the returned record
reflects uncommitted state, per the repository rules in CLAUDE.md.

### N-N Sync Pattern

**N/A** — no junction tables.

### Special Column Notes

- `pkid` `smallint` **IDENTITY** — C# `short`; excluded from `PartnerRequest`, present on
  `PartnerUpdateRequest`, `disable()`d on the edit form, cast to `int` for `CreatedAtAction`.
- `ImageFilename` is the only nullable column: the frontend must send `null`, not `''`.
- `AppKey` is `varchar(10)` — ASCII by column type; `[StringLength(10)]` is the guard.
- No `nchar`, computed, `date` or `time` columns.

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/partners` | `PartnerList` | 合作夥伴 Partner |
| `/partners/new` | `PartnerForm` | 新增合作夥伴 |
| `/partners/:id/edit` | `PartnerForm` | 編輯合作夥伴 |
| `/partners/:id` | `PartnerDetail` | 檢視合作夥伴 |

`new` and `:id/edit` are declared before `:id`.

### Angular model

```ts
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

export interface PartnerRequest {
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

export interface PartnerUpdateRequest extends PartnerRequest { pkid: number; }

export interface PartnerQuery {
  keyword?: string | null;
  displayOrderFrom?: number | null;
  displayOrderTo?: number | null;
  hasImage?: boolean | null;
}

export const EMPTY_PARTNER_QUERY: PartnerQuery = { /* all null */ };
```

### Service

`core/services/partner.service.ts`, modelled on `publish-status.service.ts`. The key is numeric, so
no `encodeURIComponent`; `update()` PUTs to the collection route with the key in the body.

### List component

- `p-table`, sortable, paginated, `paginatorPosition="top"`, `dataKey="pkid"`, rows 10/20/50/100.
- Columns: 主代碼, 名稱, 應用代碼, 選單顯示名稱, 課程明細頁名稱, 顯示順序, 圖片檔名, 操作
  (檢視／編輯／刪除). Empty `imageFilename` renders `—` with the `empty-text` class.
- Filter drawer (`p-drawer`, position right) edits `draftFilters`; only 套用 copies it into
  `filters` and reloads:
  - 關鍵字 → `input pInputText` (placeholder 名稱／應用代碼／顯示名稱)
  - 顯示順序（起）／（迄） → `p-inputnumber` `[min]="0"`
  - 圖片 → `p-select` with 不限(null)／有(true)／無(false), `appendTo="body"`
    (3 options, so no `[filter]` and no virtual scroll)
- Session-storage keys `partner-list-filters`, `partner-list-sort`, `partner-list-page`, read in
  `ngOnInit`, written on apply/sort/page, all through `ListStateService`.
- Default sort `{ field: 'displayOrder', order: 1 }`.
- Delete confirmation: `確定要刪除主代碼 3「微軟」？`; a 409 response shows
  「此合作夥伴已被認證、課程或課程群組使用，無法刪除。」

### Detail component

Read-only `dl.detail-grid` with all seven fields; empty `imageFilename` shows `—`. 返回 / 編輯
buttons in the header. No Primary-Foreign link buttons yet (see above).

### Form component

- One component for add and edit; mode derived from the route `:id`.
- No lookups to load, so `ngOnInit` calls `getById` directly in edit mode instead of `forkJoin`.
- Controls: `pkid` `p-inputnumber` (**edit mode only, `disable()`d** — read values with
  `form.getRawValue()`), `name` / `appKey` / `nameOnPartnerMenu` / `nameOnCourseDetailPage` /
  `imageFilename` text inputs with matching `maxlength`, `displayOrder` `p-inputnumber` `[min]="0"`.
- Trim every string on save; send `null` (not `''`) for `imageFilename` when blank.
- Create POSTs `PartnerRequest`; edit PUTs `PartnerUpdateRequest` (key from `getRawValue()`).

### Sidebar

Add a **new** nav group `課程管理 Course` to `NAV_GROUPS` in `src/app/shared/layout/nav-menu.ts`,
below the existing `系統管理 Admin` group, with one entry:
`{ label: '合作夥伴 Partner', icon: 'pi pi-briefcase', route: '/partners' }`.

`app.spec.ts` derives its nav-item count from `NAV_GROUPS`, so the new group needs no test change.

### Date handling

**N/A** — no date or datetime columns.

### Sub-panels

**N/A**

---

## Tests

### Backend — `src/CMS.API.Tests`

- `Fakes/InMemoryPartnerRepository.cs` — mirrors the SQL semantics: `DisplayOrder ASC, pkid ASC`
  sort, keyword across all four string columns, the `DisplayOrder` range, the `HasImage` tri-state
  (treating whitespace-only as empty), IDENTITY assignment on create, plus a `MarkInUse(pkid)` seam
  that reproduces the FK-violation path.
- `PartnersControllerTests.cs` — list, filtered query (each filter), get-by-id found and not-found,
  create (201 + assigned pkid), update, update-missing 404, delete 204, delete-missing 404,
  delete-in-use 409, and invalid `ModelState` → 400.
- `PartnersRoutingConventionTests.cs` — pins `api/partners`, the `query` sub-route, the `{id:int}`
  segments and the PUT with no route template.
- `LookupsControllerTests.cs` — extended with the `partners` lookup.

### Frontend — `src/CMS.NG`

- `core/services/partner.service.spec.ts` — every method's URL and verb, plus error passthrough.
- `partner-list.spec.ts` — loads via POST /query, renders rows, filter apply/reset, session-storage
  persistence and restore, sort/page persistence, navigation, delete confirmation accepted and
  dismissed, 409 message.
- `partner-detail.spec.ts` — loads the record, renders every field, null `imageFilename` fallback,
  not-found empty state, navigation.
- `partner-form.spec.ts` — add-mode defaults, required-field guard, POST body (trimmed, `null`
  image), edit-mode patch, key locked in edit mode, PUT body, cancel navigation.

---

## Files to Create / Modify

| File | Action |
|------|--------|
| `src/CMS.API/Models/Partner.cs` | Create |
| `src/CMS.API/Models/PartnerRequest.cs` | Create (holds `PartnerUpdateRequest` too) |
| `src/CMS.API/Models/PartnerQuery.cs` | Create |
| `src/CMS.API/Models/PartnerDeleteResult.cs` | Create |
| `src/CMS.API/Repositories/IPartnerRepository.cs` | Create |
| `src/CMS.API/Repositories/PartnerRepository.cs` | Create |
| `src/CMS.API/Controllers/PartnersController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `partners` |
| `src/CMS.API/Program.cs` | Modify — register the repository |
| `src/CMS.API.Tests/Fakes/InMemoryPartnerRepository.cs` | Create |
| `src/CMS.API.Tests/PartnersControllerTests.cs` | Create |
| `src/CMS.API.Tests/PartnersRoutingConventionTests.cs` | Create |
| `src/CMS.API.Tests/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/src/app/core/models/partner.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/index.ts` | Modify |
| `src/CMS.NG/src/app/core/services/partner.service.ts` (+ `.spec.ts`) | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` (+ `.spec.ts`) | Modify |
| `src/CMS.NG/src/app/core/services/index.ts` | Modify |
| `src/CMS.NG/src/app/features/partners/partner-list/*` | Create |
| `src/CMS.NG/src/app/features/partners/partner-detail/*` | Create |
| `src/CMS.NG/src/app/features/partners/partner-form/*` | Create |
| `src/CMS.NG/src/app/features/partners/partners.routes.ts` | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — lazy route |
| `src/CMS.NG/src/app/shared/layout/nav-menu.ts` | Modify — new `課程管理 Course` group + entry |
