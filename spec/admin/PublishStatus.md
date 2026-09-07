# Build Spec for PublishStatus
- database schema: `.\database\admin.sql`

---

## Summary

`PublishStatus` is a small lookup table describing the publication lifecycle state of content
(draft → published → discontinued). It has no foreign keys and no junction tables; other
sub-systems point *at* it (`Course.PublishStatus_pkid`, `Promotion2.PublishStatus_pkid`).

Its primary key `pkid` is **`tinyint` and NOT an IDENTITY column** — the value is supplied by the
user on create and is immutable on edit, exactly like `AppRole.RoleId` but numeric.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` `tinyint` NOT NULL — **user-supplied, not IDENTITY**, range 0–255 |
| Foreign Keys | N/A |
| Required Fields | `pkid`, `Description`, `IsDraft`, `IsPublished`, `IsDiscontinued` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course.PublishStatus_pkid`, `Promotion2.PublishStatus_pkid` (both features not built yet — links deferred) |
| Query Filters | keyword (`Description`), pkid range, `IsDraft`, `IsPublished`, `IsDiscontinued` (tri-state) |
| Default Sort | `pkid ASC` |

---

## Localization

### Chinese Table Name

- PublishStatus: 發布狀態
- Description: 內容的發布生命週期狀態（草稿／已發布／已下架）

### Chinese Column Names

- pkid: 主代碼
- Description: 狀態說明
- IsDraft: 草稿
- IsPublished: 已發布
- IsDiscontinued: 已下架

---

## Required Fields

Every column is NOT NULL, and the PK is not IDENTITY, so `pkid` is required on create too.

Required (NOT NULL):
- `pkid` — `tinyint`, 0–255, supplied on create, **immutable on update**
- `Description` — `nvarchar(50)`
- `IsDraft` — `bit`
- `IsPublished` — `bit`
- `IsDiscontinued` — `bit`

Optional (nullable): none.

---

## Foreign Keys

`PublishStatus` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`PublishStatus` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

Two tables reference `PublishStatus.pkid`:

| Child table | FK column | Sub-system | Status |
|-------------|-----------|------------|--------|
| `Course` | `PublishStatus_pkid` | `course` | Feature not built — link deferred |
| `Promotion2` | `PublishStatus_pkid` | `promotion` | Feature not built — link deferred |

**Deferred.** Neither `/courses` nor `/promotion2s` exists in `app.routes.ts` yet, so the list and
detail pages ship **without** 對應課程 / 對應促銷 buttons — a link to a missing route only produces a
navigation error. When those features are generated, add:

- 對應課程 (icon `pi pi-book`) → `/courses?publishStatusPkid={pkid}`
- 對應促銷 (icon `pi pi-megaphone`) → `/promotion2s?publishStatusPkid={pkid}`

For the same reason the projection carries **no** `CourseCount` / `PromotionCount` subquery: the
repository must not depend on tables another sub-system owns.

---

## N-N Relationships

No junction table has a FK to `PublishStatus.pkid`.

**N/A**

---

## Query Filters

- **keyword**: `string?`
  - `LIKE` on `Description` only. `pkid` is numeric and covered by its own range filter.

- **pkidFrom / pkidTo**: `byte?` (inclusive range)
  - `pkid >= @PkidFrom`, `pkid <= @PkidTo`

- **isDraft**: `bool?` — tri-state (null = 不限, true = 是, false = 否), exact match on `IsDraft`
- **isPublished**: `bool?` — tri-state, exact match on `IsPublished`
- **isDiscontinued**: `bool?` — tri-state, exact match on `IsDiscontinued`

---

## Lookup Endpoints Required

`PublishStatus` needs no lookups of its own (no FKs, no n-n). It **is** an FK target, so it
publishes one:

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/publish-statuses` | New | Full `PublishStatus` list, `pkid ASC` — option label = `Description` |

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/publish-statuses` | List all, `pkid ASC` |
| `POST` | `/api/publish-statuses/query` | Filtered query (body: `PublishStatusQuery`) |
| `GET` | `/api/publish-statuses/{id:int}` | Get by pkid; 404 when missing |
| `POST` | `/api/publish-statuses` | Create; **409** when `pkid` already exists |
| `PUT` | `/api/publish-statuses` | Update — pkid comes from the body, no route param |
| `DELETE` | `/api/publish-statuses/{id:int}` | 204 / 404 / **409** when still referenced |
| `GET` | `/api/lookups/publish-statuses` | Slim lookup list |

Notes:
- `pkid` is numeric, so the route segment carries the `:int` constraint (per CLAUDE.md, only
  `nvarchar` keys use a bare `{id}`). The action parameter is `byte`; a value above 255 fails model
  binding and returns 400, which is the right answer for an out-of-range key.
- **409 on delete**: `Course` and `Promotion2` have FK constraints on this table. The repository
  catches `SqlException` 547 and reports `PublishStatusDeleteResult.InUse`; the controller turns
  that into a 409 with a Chinese explanation instead of letting a 500 escape.

Auth exceptions: none — this project has no auth layer yet.

---

## Backend Notes

### Models

```csharp
public class PublishStatus
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

public class PublishStatusRequest
{
    [Range(0, 255)]                       public byte Pkid { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]                    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

public class PublishStatusQuery
{
    public string? Keyword { get; set; }        // LIKE Description
    public byte? PkidFrom { get; set; }
    public byte? PkidTo { get; set; }
    public bool? IsDraft { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}

/// <summary>Outcome of a delete attempt — InUse maps to 409.</summary>
public enum PublishStatusDeleteResult { Deleted, NotFound, InUse }
```

No `DateOnly` / `TimeOnly` / `nchar` columns, so no `RTRIM()` and no type handlers are involved.

### SQL — SELECT

One shared `SelectSql` for `GetAllAsync` / `QueryAsync` / `GetByIdAsync`:

```sql
SELECT  s.pkid            AS Pkid,
        s.Description     AS Description,
        s.IsDraft         AS IsDraft,
        s.IsPublished     AS IsPublished,
        s.IsDiscontinued  AS IsDiscontinued
FROM    dbo.PublishStatus s
```

`QueryAsync` appends `WHERE` clauses built into a `List<string>` with a `DynamicParameters` bag —
never string-concatenated values. Every read ends with `ORDER BY s.pkid ASC`.

### SQL — INSERT

`pkid` is **not** IDENTITY, so it is written explicitly and there is no `SCOPE_IDENTITY()`:

```sql
INSERT INTO dbo.PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
```

### SQL — UPDATE

`pkid` is the key and immutable:

```sql
UPDATE  dbo.PublishStatus
SET     Description    = @Description,
        IsDraft        = @IsDraft,
        IsPublished    = @IsPublished,
        IsDiscontinued = @IsDiscontinued
WHERE   pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM dbo.PublishStatus WHERE pkid = @Pkid;
```

Wrapped in `try/catch (SqlException ex) when (ex.Number == 547)` → `PublishStatusDeleteResult.InUse`.

### Transactions

Create and Update run in a transaction and re-read the row **inside** that transaction (the
`IDbTransaction` is passed through to a private `GetByIdAsync` overload) so the returned record
reflects uncommitted state, per the repository rules in CLAUDE.md.

### N-N Sync Pattern

**N/A** — no junction tables.

### Special Column Notes

- `pkid` `tinyint` **NOT IDENTITY** — the one non-obvious thing about this table. It is part of the
  create request, validated `[Range(0, 255)]`, and `disable()`d on the edit form.
- No `nchar`, computed, `date`, or `time` columns.

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/publish-statuses` | `PublishStatusList` | 發布狀態 PublishStatus |
| `/publish-statuses/new` | `PublishStatusForm` | 新增發布狀態 |
| `/publish-statuses/:id/edit` | `PublishStatusForm` | 編輯發布狀態 |
| `/publish-statuses/:id` | `PublishStatusDetail` | 檢視發布狀態 |

`new` and `:id/edit` are declared before `:id`.

### Angular model

```ts
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

export interface PublishStatusRequest { /* the same five fields */ }

export interface PublishStatusQuery {
  keyword?: string | null;
  pkidFrom?: number | null;
  pkidTo?: number | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}

export const EMPTY_PUBLISH_STATUS_QUERY: PublishStatusQuery = { /* all null */ };
```

### Service

`core/services/publish-status.service.ts`, modelled on `app-role.service.ts`. The key is numeric, so
no `encodeURIComponent` is needed; `update()` still PUTs to the collection route with the key in the
body.

### List component

- `p-table`, sortable, paginated, `paginatorPosition="top"`, `dataKey="pkid"`, rows 10/20/50/100.
- Columns: 主代碼, 狀態說明, 草稿, 已發布, 已下架, 操作 (檢視／編輯／刪除).
- Boolean cells render a `p-tag` — `是` (success) / `否` (secondary) — not raw true/false.
- Filter drawer (`p-drawer`, position right) edits `draftFilters`; only 套用 copies it into
  `filters` and reloads:
  - 關鍵字 → `input pInputText` (placeholder 狀態說明)
  - 主代碼（起）／（迄） → `p-inputnumber` `[min]="0" [max]="255"`
  - 草稿／已發布／已下架 → `p-select` with 不限(null)／是(true)／否(false), `appendTo="body"`
    (3 options, so no `[filter]` and no virtual scroll)
- Session-storage keys `publish-status-list-filters`, `publish-status-list-sort`,
  `publish-status-list-page`, read in `ngOnInit`, written on apply/sort/page, all through
  `ListStateService`.
- Default sort `{ field: 'pkid', order: 1 }`.
- Delete confirmation: `確定要刪除主代碼 3「已下架」？`; a 409 response shows
  「此發布狀態已被課程或促銷使用，無法刪除。」

### Detail component

Read-only `dl.detail-grid` with all five fields; booleans as `p-tag`. 返回 / 編輯 buttons in the
header. No Primary-Foreign link buttons yet (see above).

### Form component

- One component for add and edit; mode derived from the route `:id`.
- No lookups to load, so `ngOnInit` calls `getById` directly in edit mode instead of `forkJoin`.
- Controls: `pkid` `p-inputnumber` `[min]="0" [max]="255"` (**`disable()`d in edit mode** — read
  values with `form.getRawValue()`), `description` text input `maxlength="50"`, and three
  `p-checkbox [binary]="true"` toggles.
- Trim `description` on save. Nothing is nullable, so no `'' → null` conversion is needed.
- 409 on create → 「主代碼已存在，請改用其他代碼。」

### Sidebar

Add to `NAV_GROUPS` in `src/app/shared/layout/nav-menu.ts` under the existing `系統管理 Admin`
group: `{ label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' }`.

### Date handling

**N/A** — no date or datetime columns.

### Sub-panels

**N/A**

---

## Tests

### Backend — `src/CMS.API.Tests`

- `Fakes/InMemoryPublishStatusRepository.cs` — mirrors the SQL semantics: `pkid ASC` sort, keyword
  over `Description`, the pkid range and the three tri-state bool filters, plus a `MarkInUse(pkid)`
  seam that reproduces the FK-violation path.
- `PublishStatusesControllerTests.cs` — list, filtered query (each filter), get-by-id found and
  not-found, create, duplicate-key 409, update, update-missing 404, delete 204, delete-missing 404,
  delete-in-use 409, and invalid `ModelState` → 400.
- `PublishStatusesRoutingConventionTests.cs` — pins `api/publish-statuses`, the `query` sub-route,
  the `{id:int}` segments and the PUT with no route template.
- `LookupsControllerTests.cs` — extended with the `publish-statuses` lookup.

### Frontend — `src/CMS.NG`

- `core/services/publish-status.service.spec.ts` — every method's URL and verb, plus error passthrough.
- `publish-status-list.spec.ts` — loads via POST /query, renders rows, filter apply/reset,
  session-storage persistence and restore, sort/page persistence, navigation, delete confirmation
  accepted and dismissed.
- `publish-status-detail.spec.ts` — loads the record, renders every field, not-found empty state,
  navigation.
- `publish-status-form.spec.ts` — add-mode defaults, required-field guard, POST body, edit-mode
  patch, key locked in edit mode, PUT body, 409 handling, cancel navigation.

---

## Files to Create / Modify

| File | Action |
|------|--------|
| `src/CMS.API/Models/PublishStatus.cs` | Create |
| `src/CMS.API/Models/PublishStatusRequest.cs` | Create |
| `src/CMS.API/Models/PublishStatusQuery.cs` | Create |
| `src/CMS.API/Models/PublishStatusDeleteResult.cs` | Create |
| `src/CMS.API/Repositories/IPublishStatusRepository.cs` | Create |
| `src/CMS.API/Repositories/PublishStatusRepository.cs` | Create |
| `src/CMS.API/Controllers/PublishStatusesController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `publish-statuses` |
| `src/CMS.API/Program.cs` | Modify — register the repository |
| `src/CMS.API.Tests/Fakes/InMemoryPublishStatusRepository.cs` | Create |
| `src/CMS.API.Tests/PublishStatusesControllerTests.cs` | Create |
| `src/CMS.API.Tests/PublishStatusesRoutingConventionTests.cs` | Create |
| `src/CMS.API.Tests/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/src/app/core/models/publish-status.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/index.ts` | Modify |
| `src/CMS.NG/src/app/core/services/publish-status.service.ts` (+ `.spec.ts`) | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` (+ `.spec.ts`) | Modify |
| `src/CMS.NG/src/app/core/services/index.ts` | Modify |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-list/*` | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-detail/*` | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-form/*` | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-statuses.routes.ts` | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — lazy route |
| `src/CMS.NG/src/app/shared/layout/nav-menu.ts` | Modify — nav entry |
