# Build Spec for Course
- database schema: `.\database\course.sql`

---

## Summary

`Course` is the central entity of the `course` sub-system — one row per sellable course. It carries
the identifying codes (`CourseId`, `ProdCourseId`, `FriendlyUrl`), the marketing copy (five short
text columns plus two `nvarchar(max)` bodies), the scheduling window, the commercial figures
(`ListPrice`, `LearningCredit`, `Hour`), and three foreign keys that classify it.

It is the first entity in this project with **both** FK nav objects **and** junction tables, so it
exercises every rule in `CLAUDE.md` at once: multi-map reads, n-n delete-then-reinsert writes, a
nullable FK, `DateOnly` columns, `decimal` columns, a `bit` column, and an FK-referenced delete path.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` `int` IDENTITY(1,1) — database-assigned, C# `int` |
| Foreign Keys | `Partner_pkid` → `Partner.pkid` (NOT NULL); `CourseGroup_pkid` → `CourseGroup.pkid` (**nullable**); `PublishStatus_pkid` → `PublishStatus.pkid` (NOT NULL) |
| Required Fields | `Title`, `CourseId`, `ProdCourseId`, `FriendlyUrl`, `DisplayOrder`, `Partner_pkid`, `PublishStatus_pkid`, `ScheduleOn`, `ScheduleOff`, `Hour`, `ListPrice`, `LearningCredit`, `CanRepeat` |
| N-N Relationships | `CourseJobCategories` → `JobCategory`; `CourseInCertification` → `Certification` |
| Primary-Foreign Links | `CourseFAQ`, `CourseRelatedLink`, `HotCourse` (none built yet — links deferred) |
| Query Filters | keyword, three FK dropdowns, `CanRepeat` tri-state, `ScheduleOn` range, `ScheduleOff` range |
| Default Sort | `DisplayOrder ASC, pkid ASC` |

---

## Localization

### Chinese Table Name

- Course: 課程
- Description: 課程主檔 — 一門可販售課程的識別代碼、分類、上下架排程、費用與所有文案內容

### Chinese Column Names

- pkid: 主代碼
- Title: 課程名稱
- OfficialTitle: 官方課程名稱
- CourseId: 簡介代碼
- ProdCourseId: 科目代碼
- FriendlyUrl: 網址代稱
- DisplayOrder: 顯示順序
- Partner_pkid: 原廠
- CourseGroup_pkid: 課程群組
- PublishStatus_pkid: 上架狀態
- ScheduleOn: 上架日期
- ScheduleOff: 下架日期
- Hour: 時數
- ListPrice: 定價
- LearningCredit: 點數
- Material: 教材
- Objective: 課程目標
- Target: 適合對象
- Prerequisites: 先備知識
- Outline: 課程大綱
- TowardCertOrExam: 對應認證／考試
- Note: 備註
- OtherInfo: 其他資訊
- CanRepeat: 允許重聽

Derived (not columns):
- JobCategories / JobCategoryCount: 職務類別 ／ 職務類別數
- Certifications / CertificationCount: 對應認證 ／ 對應認證數

---

## Required Fields

`pkid` is IDENTITY, so it is excluded from the create request entirely and read-only on edit.

Required (NOT NULL):
- `Title` — `nvarchar(200)`
- `CourseId` — `varchar(50)`
- `ProdCourseId` — `varchar(50)`
- `FriendlyUrl` — `nvarchar(100)`
- `DisplayOrder` — `int`
- `Partner_pkid` — `smallint` → C# `short`
- `PublishStatus_pkid` — `tinyint` → C# `byte`
- `ScheduleOn` — `date` → C# `DateOnly`
- `ScheduleOff` — `date` → C# `DateOnly`
- `Hour` — `smallint` → C# `short` (DB default `0`)
- `ListPrice` — `decimal(9,0)` (DB default `0`)
- `LearningCredit` — `decimal(9,1)` (DB default `0`)
- `CanRepeat` — `bit` (DB default `0`)

Optional (nullable) — every one must be sent as `null`, never `''`:
- `OfficialTitle` — `nvarchar(300)`
- `CourseGroup_pkid` — `smallint` → C# `short?`
- `Material` — `nvarchar(500)`
- `Objective` — `nvarchar(4000)`
- `Target` — `nvarchar(500)`
- `Prerequisites` — `nvarchar(4000)`
- `Outline` — `nvarchar(max)`
- `TowardCertOrExam` — `nvarchar(max)`
- `Note` — `nvarchar(4000)`
- `OtherInfo` — `nvarchar(4000)`

---

## Foreign Keys

| Column | → Table.PK | Nullable | Option label | Lookup order |
|--------|-----------|----------|--------------|--------------|
| `Partner_pkid` (`smallint`) | `Partner.pkid` | No | `Name` | `DisplayOrder ASC, pkid ASC` |
| `CourseGroup_pkid` (`smallint`) | `CourseGroup.pkid` | **Yes** | `Description` | `Description ASC` |
| `PublishStatus_pkid` (`tinyint`) | `PublishStatus.pkid` | No | `Description` | `pkid ASC` |

Notes:

- All three SQL column names carry the `_pkid` suffix and must be aliased for C#:
  `c.Partner_pkid AS PartnerPkid`, `c.CourseGroup_pkid AS CourseGroupPkid`,
  `c.PublishStatus_pkid AS PublishStatusPkid`.
- `CourseGroup_pkid` is the only nullable FK. Its dropdown carries a `無` (`null`) option, and the
  JOIN is a `LEFT JOIN` — the nav object is set to `null` when the key is null.
- `FK_Course_CourseGroup` is declared `ON DELETE CASCADE`: deleting a course group silently deletes
  its courses. That is a property of the `CourseGroup` feature, not this one, but it is worth
  recording — it is the only cascade pointing *into* `Course`.
- `Partner` and `PublishStatus` are already built features with existing lookup endpoints.
  `CourseGroup` is **not** built; see *Lookup Endpoints Required*.

---

## Foreign-Primary Links

| FK column | Primary table | Link target | Status |
|-----------|---------------|-------------|--------|
| `Partner_pkid` | `Partner` | `/partners/{partnerPkid}` | **Available** — `/partners` exists |
| `PublishStatus_pkid` | `PublishStatus` | `/publish-statuses/{publishStatusPkid}` | **Available** — `/publish-statuses` exists |
| `CourseGroup_pkid` | `CourseGroup` | `/course-groups/{courseGroupPkid}` | Deferred — route does not exist |

The **detail page** renders 原廠 and 上架狀態 as `routerLink` buttons to those two detail pages.
課程群組 renders as plain text until the `CourseGroup` feature is generated; at that point add the
third link. The list page keeps all three as plain text — 15 columns is already wide.

---

## Primary-Foreign Links

Tables holding an FK to `Course.pkid`:

| Child table | FK column | Cascade | Sub-system | Status |
|-------------|-----------|---------|------------|--------|
| `CourseFAQ` | `Course_pkid` | No | `course` | Feature not built — link deferred |
| `CourseRelatedLink` | `Course_pkid` | No | `course` | Feature not built — link deferred |
| `HotCourse` | `Course_pkid` | No | `course` | Feature not built — link deferred |
| `CourseInCertification` | `Course_pkid` | `ON DELETE CASCADE` | `course` | Junction — handled as n-n, not a link |
| `CourseJobCategories` | `Course_pkid` | `ON DELETE CASCADE` | `course` | Junction — handled as n-n, not a link |

**Deferred.** `/course-faqs`, `/course-related-links` and `/hot-courses` do not exist in
`app.routes.ts`, so the list and detail pages ship without those buttons — a `routerLink` to a
missing route only produces a navigation error. When those features are generated, add to the detail
page:

- 課程問答 (icon `pi pi-question-circle`) → `/course-faqs?coursePkid={pkid}`
- 相關連結 (icon `pi pi-link`) → `/course-related-links?coursePkid={pkid}`
- 熱門課程 (icon `pi pi-star`) → `/hot-courses?coursePkid={pkid}`

`CourseRecomm` also stores course keys (`CourseId` / `RecommCourseId`, `varchar(50)`) but declares
**no FK constraint** and joins on the business code rather than `pkid`, so it neither blocks a delete
nor earns a link button.

---

## N-N Relationships

Two junction tables have an FK to `Course.pkid`. Both are pure junctions — two FK columns, composite
PK, no payload columns.

### 1. `CourseJobCategories` → `JobCategory` (職務類別)

- Junction columns: `Course_pkid` `int`, `JobCategory_pkid` `smallint`
- Related entity: `JobCategory` (`pkid` `smallint` IDENTITY, `Description` `nvarchar(70)` NOT NULL)
- Lookup endpoint: `GET /api/lookups/job-categories` (**new**), label `Description`, order `Description ASC`
- List view: `JobCategoryCount` (subquery count), rendered as a plain number
- Detail view: `p-tag` chips of each `description`
- Form: `p-multiselect`, `appendTo="body"`, `[maxSelectedLabels]="9999"`, wrapped in `.wrap-chips`
- Request field: `JobCategoryPkids: List<short>`

### 2. `CourseInCertification` → `Certification` (認證)

- Junction columns: `Course_pkid` `int`, `Certification_pkid` `int`
- Related entity: `Certification` (`pkid` `int` IDENTITY, `Partner_pkid` `smallint` NOT NULL,
  `Title` **`nchar(100)` NULL** — needs `RTRIM()`)
- Lookup endpoint: `GET /api/lookups/certifications` (**new**), label `RTRIM(Title)`, order `RTRIM(Title) ASC`
- List view: `CertificationCount` (subquery count)
- Detail view: `p-tag` chips of each `title`
- Form: `p-multiselect`, same options as above
- Request field: `CertificationPkids: List<int>`
- A `Certification` row may have a `NULL` `Title`; the frontend falls back to `(未命名 #{pkid})`
  so the option is still selectable.

### Sync pattern (both, inside the write transaction)

```sql
DELETE FROM dbo.CourseJobCategories WHERE Course_pkid = @Pkid;
INSERT INTO dbo.CourseJobCategories (Course_pkid, JobCategory_pkid) VALUES (@Pkid, @JobCategoryPkid);

DELETE FROM dbo.CourseInCertification WHERE Course_pkid = @Pkid;
INSERT INTO dbo.CourseInCertification (Course_pkid, Certification_pkid) VALUES (@Pkid, @CertificationPkid);
```

Ids are de-duplicated before insert (the junction PK is composite, so a repeated id would violate it).
Unlike `AppRole.UserIds` these are numeric, so there is no trim / blank / case-folding step.

---

## Query Filters

- **keyword**: `string?` — `LIKE '%…%'` across the five short identifying columns:
  `Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`.
  The eight long text columns (`Objective`, `Prerequisites`, `Outline`, `TowardCertOrExam`, `Note`,
  `OtherInfo`, `Material`, `Target`) are **excluded** — `nvarchar(max)` / `nvarchar(4000)` scans are
  slow and rarely what a user means by a keyword.

- **partnerPkid**: `short?` — `c.Partner_pkid = @PartnerPkid`. Options from `GET /api/lookups/partners`.
- **courseGroupPkid**: `short?` — `c.CourseGroup_pkid = @CourseGroupPkid`. Options from `GET /api/lookups/course-groups`.
- **publishStatusPkid**: `byte?` — `c.PublishStatus_pkid = @PublishStatusPkid`. Options from `GET /api/lookups/publish-statuses`.
- **canRepeat**: `bool?` — tri-state (`null` = 不限, `true` = 是, `false` = 否) → `c.CanRepeat = @CanRepeat`.
- **scheduleOnFrom / scheduleOnTo**: `DateOnly?` inclusive — `c.ScheduleOn >= @ScheduleOnFrom`, `c.ScheduleOn <= @ScheduleOnTo`.
- **scheduleOffFrom / scheduleOffTo**: `DateOnly?` inclusive — `c.ScheduleOff >= @ScheduleOffFrom`, `c.ScheduleOff <= @ScheduleOffTo`.

Deliberately **not** included:
- A `DisplayOrder` range — the three FK dropdowns plus two date ranges already make a crowded drawer.
- An n-n filter (`職務類別` / `對應認證`). It would need an `EXISTS` sub-select against the junction;
  worth adding later, but it is not one of the standard filter shapes.

`hasActiveFilters` must test `!== null` for `canRepeat` — a `false` filter is still a filter.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | **Exists** | Full `Partner` list — option label `Name` |
| `GET /api/lookups/publish-statuses` | **Exists** | Full `PublishStatus` list — option label `Description` |
| `GET /api/lookups/course-groups` | **New** | `CourseGroupLookup` — `pkid` + `Description`, `Description ASC` |
| `GET /api/lookups/job-categories` | **New** | `JobCategoryLookup` — `pkid` + `Description`, `Description ASC` |
| `GET /api/lookups/certifications` | **New** | `CertificationLookup` — `pkid` + `RTRIM(Title)` + `PartnerPkid`, `RTRIM(Title) ASC` |

`CourseGroup`, `JobCategory` and `Certification` are **not** built as features. Each gets a
**lookup-only repository** — exactly the `AppUserRepository` / `AppUserLookup` pattern already in the
codebase, which exists solely to feed the `AppRole` n-n multiselect. All three tables live in the
`course` sub-system, the same sub-system as `Course`, so the CLAUDE.md rule against reaching into
another sub-system's tables is not engaged.

> **Migration note.** When `/crud TABLE=CourseGroup` (or `JobCategory`, `Certification`) is run
> later, that feature will introduce a full repository with `GetAllAsync`. At that point repoint the
> lookup endpoint at the new repository and delete the lookup-only one, exactly as `AppUser` will be
> retired when its own feature lands.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses` | List all, `DisplayOrder ASC, pkid ASC` |
| `POST` | `/api/courses/query` | Filtered query (body: `CourseQuery`) |
| `GET` | `/api/courses/{id:int}` | Get by pkid; 404 when missing. Includes the two n-n collections |
| `POST` | `/api/courses` | Create; 201 + `CreatedAtAction` |
| `PUT` | `/api/courses` | Update — pkid comes from the body, no route param; 404 when missing |
| `DELETE` | `/api/courses/{id:int}` | 204 / 404 / **409** when still referenced |
| `GET` | `/api/lookups/course-groups` | New lookup |
| `GET` | `/api/lookups/job-categories` | New lookup |
| `GET` | `/api/lookups/certifications` | New lookup |

Notes:

- `pkid` is `int` IDENTITY, so the route segment carries `{id:int}` and the action parameter binds to
  `int`. No cast is needed for `CreatedAtAction` (unlike `Partner`'s `short`).
- **No 409 on create.** `pkid` is IDENTITY and no other column carries a UNIQUE constraint — a
  duplicate key is unreachable. (`CourseId` and `FriendlyUrl` look like natural keys but the schema
  does not enforce uniqueness on them, and this feature does not invent a constraint the DB lacks.)
- **409 on delete**: `CourseFAQ`, `CourseRelatedLink` and `HotCourse` hold non-cascading FKs against
  `Course.pkid`. The repository catches `SqlException` 547 and reports `CourseDeleteResult.InUse`;
  the controller turns that into 409 rather than letting a 500 escape. The two junction tables
  cascade, and the repository also clears them explicitly first (see *SQL — DELETE*).
- Use `BadRequest(ModelState)`, not `ValidationProblem(...)`.

Auth exceptions: none — this project has no auth layer yet.

---

## Backend Notes

### Models

```csharp
// Models/Course.cs
public class Course
{
    public int      Pkid { get; set; }                    // IDENTITY
    public string   Title { get; set; } = string.Empty;
    public string?  OfficialTitle { get; set; }
    public string   CourseId { get; set; } = string.Empty;
    public string   ProdCourseId { get; set; } = string.Empty;
    public string   FriendlyUrl { get; set; } = string.Empty;
    public int      DisplayOrder { get; set; }

    public short    PartnerPkid { get; set; }
    public short?   CourseGroupPkid { get; set; }
    public byte     PublishStatusPkid { get; set; }

    public DateOnly ScheduleOn { get; set; }
    public DateOnly ScheduleOff { get; set; }
    public short    Hour { get; set; }
    public decimal  ListPrice { get; set; }
    public decimal  LearningCredit { get; set; }

    public string?  Material { get; set; }
    public string?  Objective { get; set; }
    public string?  Target { get; set; }
    public string?  Prerequisites { get; set; }
    public string?  Outline { get; set; }
    public string?  TowardCertOrExam { get; set; }
    public string?  Note { get; set; }
    public string?  OtherInfo { get; set; }
    public bool     CanRepeat { get; set; }

    // FK nav objects — filled by the multi-map read.
    public CoursePartnerRef?       Partner { get; set; }
    public CourseGroupRef?         CourseGroup { get; set; }   // null when CourseGroupPkid is null
    public CoursePublishStatusRef? PublishStatus { get; set; }

    // n-n: counts on every read, full lists on GetById only (mirrors AppRole.UserCount / Users).
    public int JobCategoryCount { get; set; }
    public int CertificationCount { get; set; }
    public List<JobCategoryLookup>   JobCategories { get; set; } = new();
    public List<CertificationLookup> Certifications { get; set; } = new();
}

/// <summary>Slim FK reference — only the columns the UI labels a course with.</summary>
public class CoursePartnerRef       { public short Pkid { get; set; } public string Name { get; set; } = string.Empty; }
public class CourseGroupRef         { public short Pkid { get; set; } public string Description { get; set; } = string.Empty; }
public class CoursePublishStatusRef { public byte  Pkid { get; set; } public string Description { get; set; } = string.Empty; }
```

The nav objects are deliberately slim rather than the full `Partner` / `PublishStatus` models: a
course list carries hundreds of rows and only ever renders the label column.

```csharp
// Models/CourseRequest.cs
public class CourseRequest
{
    // Pkid is NOT here on create; the PUT body is CourseUpdateRequest.
    [Required(AllowEmptyStrings = false)] [StringLength(200)] public string  Title { get; set; } = string.Empty;
    [StringLength(300)]                                       public string? OfficialTitle { get; set; }
    [Required(AllowEmptyStrings = false)] [StringLength(50)]  public string  CourseId { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false)] [StringLength(50)]  public string  ProdCourseId { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false)] [StringLength(100)] public string  FriendlyUrl { get; set; } = string.Empty;
    [Range(0, int.MaxValue)]                                  public int     DisplayOrder { get; set; }

    [Range(1, short.MaxValue)] public short  PartnerPkid { get; set; }
    [Range(1, short.MaxValue)] public short? CourseGroupPkid { get; set; }
    [Range(0, byte.MaxValue)]  public byte   PublishStatusPkid { get; set; }

    [Required] public DateOnly ScheduleOn { get; set; }
    [Required] public DateOnly ScheduleOff { get; set; }

    [Range(0, short.MaxValue)]   public short   Hour { get; set; }
    [Range(0, 999999999)]        public decimal ListPrice { get; set; }
    [Range(0, 99999999.9)]       public decimal LearningCredit { get; set; }

    [StringLength(500)]  public string? Material { get; set; }
    [StringLength(4000)] public string? Objective { get; set; }
    [StringLength(500)]  public string? Target { get; set; }
    [StringLength(4000)] public string? Prerequisites { get; set; }
                         public string? Outline { get; set; }            // nvarchar(max) — no length cap
                         public string? TowardCertOrExam { get; set; }   // nvarchar(max) — no length cap
    [StringLength(4000)] public string? Note { get; set; }
    [StringLength(4000)] public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }

    public List<short> JobCategoryPkids { get; set; } = new();
    public List<int>   CertificationPkids { get; set; } = new();
}

/// <summary>PUT body — the same fields plus the key, which the collection route needs.</summary>
public class CourseUpdateRequest : CourseRequest
{
    [Range(1, int.MaxValue)] public int Pkid { get; set; }
}

// Models/CourseQuery.cs
public class CourseQuery
{
    public string?   Keyword { get; set; }   // LIKE Title / OfficialTitle / CourseId / ProdCourseId / FriendlyUrl
    public short?    PartnerPkid { get; set; }
    public short?    CourseGroupPkid { get; set; }
    public byte?     PublishStatusPkid { get; set; }
    public bool?     CanRepeat { get; set; }
    public DateOnly? ScheduleOnFrom { get; set; }
    public DateOnly? ScheduleOnTo { get; set; }
    public DateOnly? ScheduleOffFrom { get; set; }
    public DateOnly? ScheduleOffTo { get; set; }
}

/// <summary>Outcome of a delete attempt — InUse maps to 409.</summary>
public enum CourseDeleteResult { Deleted, NotFound, InUse }
```

Lookup DTOs (each also the option shape the frontend binds):

```csharp
public class CourseGroupLookup   { public short Pkid; public string  Description; }
public class JobCategoryLookup   { public short Pkid; public string  Description; }
public class CertificationLookup { public int   Pkid; public string? Title; public short PartnerPkid; }
```

`ScheduleOn` / `ScheduleOff` are `DateOnly`; `DapperConfig.Register()` already installs the
`DateOnlyTypeHandler` in `Program.cs`, so no new handler is needed.

### SQL — SELECT

One shared `SelectSql` for `GetAllAsync` / `QueryAsync` / `GetByIdAsync`. Column order matters: the
three nav blocks come last, each starting at its `splitOn` marker.

```sql
SELECT  c.pkid                AS Pkid,
        c.Title               AS Title,
        c.OfficialTitle       AS OfficialTitle,
        c.CourseId            AS CourseId,
        c.ProdCourseId        AS ProdCourseId,
        c.FriendlyUrl         AS FriendlyUrl,
        c.DisplayOrder        AS DisplayOrder,
        c.Partner_pkid        AS PartnerPkid,
        c.CourseGroup_pkid    AS CourseGroupPkid,
        c.PublishStatus_pkid  AS PublishStatusPkid,
        c.ScheduleOn          AS ScheduleOn,
        c.ScheduleOff         AS ScheduleOff,
        c.[Hour]              AS [Hour],
        c.ListPrice           AS ListPrice,
        c.LearningCredit      AS LearningCredit,
        c.Material            AS Material,
        c.Objective           AS Objective,
        c.Target              AS Target,
        c.Prerequisites       AS Prerequisites,
        c.Outline             AS Outline,
        c.TowardCertOrExam    AS TowardCertOrExam,
        c.Note                AS Note,
        c.OtherInfo           AS OtherInfo,
        c.CanRepeat           AS CanRepeat,
        (SELECT COUNT(1) FROM dbo.CourseJobCategories  jx WHERE jx.Course_pkid = c.pkid) AS JobCategoryCount,
        (SELECT COUNT(1) FROM dbo.CourseInCertification cx WHERE cx.Course_pkid = c.pkid) AS CertificationCount,
        p.Name                AS Name,          -- split 1 → CoursePartnerRef
        p.pkid                AS Pkid,
        cg.Description        AS Description,   -- split 2 → CourseGroupRef
        cg.pkid               AS Pkid,
        ps.Description        AS Description,   -- split 3 → CoursePublishStatusRef
        ps.pkid               AS Pkid
FROM    dbo.Course c
        INNER JOIN dbo.Partner       p  ON p.pkid  = c.Partner_pkid
        LEFT  JOIN dbo.CourseGroup   cg ON cg.pkid = c.CourseGroup_pkid
        INNER JOIN dbo.PublishStatus ps ON ps.pkid = c.PublishStatus_pkid
```

- `Hour` collides with the T-SQL `HOUR` datepart name — bracket it: `c.[Hour] AS [Hour]`.
- Multi-map: `QueryAsync<Course, CoursePartnerRef, CourseGroupRef, CoursePublishStatusRef, Course>(…,
  splitOn: "Name,Description,Description")`. Each marker is the first column of its block and Dapper
  scans forward from the previous split, so the repeated `Description` resolves to `cg` then `ps`.
- The map lambda sets `course.CourseGroup = course.CourseGroupPkid.HasValue ? cg : null` — a
  `LEFT JOIN` miss otherwise yields an all-null `CourseGroupRef` instead of a null nav object.
- `ORDER BY c.DisplayOrder ASC, c.pkid ASC` closes every read.
- No `nchar` column on `Course` itself, so no `RTRIM()` here — but the **certification lookup** and
  the n-n read do need it (`Certification.Title` is `nchar(100)`).

n-n read, issued only by `GetByIdAsync` on the same connection/transaction:

```sql
SELECT  jc.pkid AS Pkid, jc.Description AS Description
FROM    dbo.CourseJobCategories jx
        INNER JOIN dbo.JobCategory jc ON jc.pkid = jx.JobCategory_pkid
WHERE   jx.Course_pkid = @Pkid
ORDER BY jc.Description ASC;

SELECT  ct.pkid AS Pkid, RTRIM(ct.Title) AS Title, ct.Partner_pkid AS PartnerPkid
FROM    dbo.CourseInCertification cx
        INNER JOIN dbo.Certification ct ON ct.pkid = cx.Certification_pkid
WHERE   cx.Course_pkid = @Pkid
ORDER BY RTRIM(ct.Title) ASC;
```

### SQL — INSERT

`pkid` is IDENTITY, so it is omitted and read back:

```sql
INSERT INTO dbo.Course
        (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
         Partner_pkid, CourseGroup_pkid, PublishStatus_pkid,
         ScheduleOn, ScheduleOff, [Hour], ListPrice, LearningCredit,
         Material, Objective, Target, Prerequisites, Outline, TowardCertOrExam,
         Note, OtherInfo, CanRepeat)
VALUES  (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
         @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid,
         @ScheduleOn, @ScheduleOff, @Hour, @ListPrice, @LearningCredit,
         @Material, @Objective, @Target, @Prerequisites, @Outline, @TowardCertOrExam,
         @Note, @OtherInfo, @CanRepeat);
SELECT CAST(SCOPE_IDENTITY() AS int);
```

### SQL — UPDATE

The same column set, `WHERE pkid = @Pkid`. Nothing on `Course` is immutable or snapshot-only.

### SQL — DELETE

```sql
DELETE FROM dbo.CourseJobCategories   WHERE Course_pkid = @Pkid;
DELETE FROM dbo.CourseInCertification WHERE Course_pkid = @Pkid;
DELETE FROM dbo.Course                WHERE pkid = @Pkid;
```

All three run in one transaction. The junction deletes are redundant with the declared
`ON DELETE CASCADE`, but stating them keeps the repository's intent explicit and matches
`AppRoleRepository.DeleteAsync`. The whole block is wrapped in
`catch (SqlException ex) when (ex.Number == 547)` → roll back and return `CourseDeleteResult.InUse`
(a `CourseFAQ`, `CourseRelatedLink` or `HotCourse` row still points at the course).

### Transactions

Create and Update run in a transaction: write the row, replace both junctions, then re-read **inside**
the transaction (the `IDbTransaction` is passed to a private `GetByIdAsync` overload) so the returned
record reflects uncommitted state.

### Special Column Notes

- `pkid` `int` **IDENTITY** — excluded from `CourseRequest`, present on `CourseUpdateRequest`,
  `disable()`d and rendered **edit-mode only** on the form.
- `Hour` must be bracketed as `[Hour]` in SQL.
- `Partner_pkid` / `CourseGroup_pkid` / `PublishStatus_pkid` need `AS PartnerPkid` etc. aliases.
- `ScheduleOn` / `ScheduleOff` are `date` → `DateOnly`; handler already registered.
- `ListPrice` `decimal(9,0)` — whole currency units, 0 decimal places in the UI.
  `LearningCredit` `decimal(9,1)` — exactly 1 decimal place in the UI.
- `Outline` / `TowardCertOrExam` are `nvarchar(max)` — **no** `[StringLength]` annotation.
- Ten nullable columns: normalise blank/whitespace to `null` in `ToParameters`, never store `''`.
- `Certification.Title` is `nchar(100)` — `RTRIM()` in the lookup and the n-n read.

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/courses` | `CourseList` | 課程 Course |
| `/courses/new` | `CourseForm` | 新增課程 |
| `/courses/:id/edit` | `CourseForm` | 編輯課程 |
| `/courses/:id` | `CourseDetail` | 檢視課程 |

`new` and `:id/edit` are declared before `:id`.

### Angular model

```ts
export interface CoursePartnerRef       { pkid: number; name: string; }
export interface CourseGroupRef         { pkid: number; description: string; }
export interface CoursePublishStatusRef { pkid: number; description: string; }

export interface Course {
  pkid: number;
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  scheduleOn: string;        // ISO yyyy-MM-dd
  scheduleOff: string;       // ISO yyyy-MM-dd
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;
  partner: CoursePartnerRef | null;
  courseGroup: CourseGroupRef | null;
  publishStatus: CoursePublishStatusRef | null;
  jobCategoryCount: number;
  certificationCount: number;
  jobCategories: JobCategoryLookup[];
  certifications: CertificationLookup[];
}

export interface CourseRequest { /* every writable column + jobCategoryPkids + certificationPkids */ }
export interface CourseUpdateRequest extends CourseRequest { pkid: number; }
export interface CourseQuery { keyword?, partnerPkid?, courseGroupPkid?, publishStatusPkid?,
                               canRepeat?, scheduleOnFrom?, scheduleOnTo?,
                               scheduleOffFrom?, scheduleOffTo? }
export const EMPTY_COURSE_QUERY: CourseQuery = { /* all null */ };
```

Plus `course-group.model.ts`, `job-category.model.ts`, `certification.model.ts` holding
`CourseGroupLookup`, `JobCategoryLookup`, `CertificationLookup` (mirroring `app-user.model.ts`).

### Service

`core/services/course.service.ts`, modelled on `partner.service.ts`: numeric key so no
`encodeURIComponent`; `create(CourseRequest)` omits the key, `update(CourseUpdateRequest)` PUTs to
the collection route with the key in the body.

`lookup.service.ts` gains `getCourseGroups()`, `getJobCategories()`, `getCertifications()`.

### Date handling

New file `core/utils/date.util.ts` — the canonical helpers the convention names:

- `toIso(d: Date | null): string | null` — built from **local** components
  (`getFullYear()` / `getMonth()+1` / `getDate()`). Never `toISOString().split('T')[0]`: that
  converts to UTC first and shifts a UTC+8 date back a day.
- `fromIso(s: string | null): Date | null` — parses `yyyy-MM-dd` into a local `Date`.

`Course` has no `datetime` column, so the `+ 'Z'` display fix does not apply here.

### List component

- `p-table`, sortable, paginated, `paginatorPosition="top"`, `dataKey="pkid"`, rows 10/20/50/100,
  `[scrollable]="true"` with a `min-width` style — 15 columns overflow a normal viewport.
- Columns, in the order requested:
  主代碼 `pkid` · 顯示順序 `displayOrder` · 簡介代碼 `courseId` · 科目代碼 `prodCourseId` ·
  課程名稱 `title` · 原廠 `partner.name` · 課程群組 `courseGroup.description` ·
  上架狀態 `publishStatus.description` · 上架日期 `scheduleOn` · 下架日期 `scheduleOff` ·
  時數 `hour` · 定價 `listPrice` · 點數 `learningCredit` · 允許重聽 `canRepeat` · 操作.
- `courseGroup` is nullable → `—` with the `empty-text` class.
- `canRepeat` renders as a 是／否 `p-tag`, never a raw boolean.
- `listPrice` → `| number:'1.0-0'`; `learningCredit` → `| number:'1.1-1'`; both right-aligned.
- Filter drawer (`p-drawer`, position right) edits `draftFilters`; only 套用 copies into `filters`:
  - 關鍵字 → `input pInputText` (placeholder 課程名稱／簡介代碼／科目代碼／網址代稱)
  - 原廠 → `p-select`, `appendTo="body"`, `[filter]="true"` (partner counts reach 10+)
  - 課程群組 → `p-select`, `appendTo="body"`, `[filter]="true"`
  - 上架狀態 → `p-select`, `appendTo="body"` (few options — no filter)
  - 允許重聽 → `p-select` 不限(null)／是(true)／否(false)
  - 上架日期（起）／（迄） → `p-datepicker` `appendTo="body"` `dateFormat="yy-mm-dd"`
  - 下架日期（起）／（迄） → `p-datepicker` `appendTo="body"` `dateFormat="yy-mm-dd"`
  - Add `[virtualScroll]="true" [virtualScrollItemSize]="43"` to any select whose option list is
    expected past 100 entries (course groups, certifications).
- Lookups (`partners`, `course-groups`, `publish-statuses`) load through `forkJoin` on init;
  saved filters are restored after they resolve.
- Session-storage keys `course-list-filters`, `course-list-sort`, `course-list-page`, all via
  `ListStateService`.
- Default sort `{ field: 'displayOrder', order: 1 }`.
- Delete confirmation: `確定要刪除主代碼 <b>12</b>「Azure 基礎架構」？`; a 409 shows
  「此課程已被課程問答、相關連結或熱門課程使用，無法刪除。」

### Detail component

Read-only `dl.detail-grid`, grouped with `<h3>` sub-headings:

1. **基本資料** — 主代碼, 課程名稱, 官方課程名稱, 簡介代碼, 科目代碼, 網址代稱, 顯示順序
2. **分類與狀態** — 原廠 (link → `/partners/:id`), 課程群組 (plain text for now),
   上架狀態 (link → `/publish-statuses/:id`), 允許重聽 (`p-tag`)
3. **排程與費用** — 上架日期, 下架日期, 時數, 定價, 點數
4. **關聯** — 職務類別 chips, 對應認證 chips (`(未命名 #n)` fallback for a null title)
5. **課程內容** — 教材, 課程目標, 適合對象, 先備知識, 課程大綱, 對應認證／考試, 備註, 其他資訊
   (long text preserves newlines via `white-space: pre-wrap`)

Null values render `—` with `empty-text`. 返回 / 編輯 buttons in the header.

### Form component

- One component for add and edit; mode from the route `:id`.
- `forkJoin` on init: `partners`, `course-groups`, `publish-statuses`, `job-categories`,
  `certifications` — plus `getById` in edit mode.
- `pkid` — `p-inputnumber`, **edit mode only** (`@if (isEditMode)`), `disable()`d; read values with
  `form.getRawValue()`.
- Sections mirror the detail page. Controls:
  - `title` / `officialTitle` / `courseId` / `prodCourseId` / `friendlyUrl` — `input pInputText` with matching `maxlength`
  - `displayOrder` / `hour` — `p-inputnumber` `[min]="0"`
  - `listPrice` — `p-inputnumber` `[min]="0"` `[maxFractionDigits]="0"`
  - `learningCredit` — `p-inputnumber` `[min]="0"` `[minFractionDigits]="1"` `[maxFractionDigits]="1"`
  - `partnerPkid` / `publishStatusPkid` — required `p-select`, `appendTo="body"`, `[filter]="true"`
  - `courseGroupPkid` — `p-select` with a `無` (`null`) option, `[showClear]="true"`
  - `scheduleOn` / `scheduleOff` — `p-datepicker`, `dateFormat="yy-mm-dd"`, `appendTo="body"`
  - `canRepeat` — `p-toggleswitch`
  - `jobCategoryPkids` / `certificationPkids` — `p-multiselect`, `appendTo="body"`,
    `[maxSelectedLabels]="9999"`, wrapped in `.wrap-chips`
  - `material` / `target` — `textarea pTextarea` rows 2
  - `objective` / `prerequisites` / `note` / `otherInfo` — `textarea pTextarea` rows 4, maxlength 4000
  - `outline` / `towardCertOrExam` — `textarea pTextarea` rows 8, **no maxlength**
- Trim every string on save; send `null` (not `''`) for all ten nullable columns.
- Dates serialise through `toIso()` from `core/utils/date.util.ts`.

### Special Form Behaviors

- **`scheduleOff` auto-default (add mode only).** `ScheduleOn` and `ScheduleOff` are both NOT NULL
  and in practice a course stays published for a decade. On `scheduleOn.valueChanges`, if the form
  is in add mode **and** `scheduleOff` is still pristine, set `scheduleOff` to
  `scheduleOn + 10 years` with `{ emitEvent: false }` (preventing a subscription loop). Once the
  user touches `scheduleOff` the default never fires again, and edit mode never applies it.
- No conditional visibility beyond the edit-mode-only `pkid` control.

### Sub-panels (edit mode only)

**N/A** — `CourseFAQ` and `CourseRelatedLink` would be the candidates, but neither feature exists.

### Sidebar

Add one entry to the **existing** `課程管理 Course` group in
`src/app/shared/layout/nav-menu.ts`, above `合作夥伴 Partner` (the course itself is the headline
entity): `{ label: '課程 Course', icon: 'pi pi-book', route: '/courses' }`.

`app.spec.ts` derives its nav-item count from `NAV_GROUPS`, so no test change is needed.

---

## Tests

### Backend — `src/CMS.API.Tests`

- `Fakes/InMemoryCourseRepository.cs` — mirrors the SQL semantics: `DisplayOrder ASC, pkid ASC`
  sort, keyword across the five short string columns, the three FK equality filters, the `CanRepeat`
  tri-state, both inclusive date ranges, IDENTITY assignment on create, n-n replace (dedupe) on
  create/update, nav objects resolved from seeded lookup rows, and a `MarkInUse(pkid)` seam that
  reproduces the FK-violation delete path.
- `Fakes/InMemoryCourseGroupRepository.cs`, `InMemoryJobCategoryRepository.cs`,
  `InMemoryCertificationRepository.cs` — lookup-only, sorted like their SQL counterparts.
- `CoursesControllerTests.cs` — list, each filter in turn, get-by-id found and not-found (n-n
  collections populated), create (201 + assigned pkid + junction rows), update (junction replace),
  update-missing 404, delete 204, delete-missing 404, delete-in-use 409, invalid `ModelState` → 400.
- `CoursesRoutingConventionTests.cs` — pins `api/courses`, the `query` sub-route, the `{id:int}`
  segments and the PUT with no route template.
- `LookupsControllerTests.cs` — extended with `course-groups`, `job-categories`, `certifications`.

### Frontend — `src/CMS.NG`

- `core/utils/date.util.spec.ts` — `toIso` uses local components (a UTC+8 evening `Date` must not
  roll back a day) and round-trips through `fromIso`.
- `core/services/course.service.spec.ts` — every method's URL and verb, plus error passthrough.
- `core/services/lookup.service.spec.ts` — extended with the three new endpoints.
- `course-list.spec.ts` — loads via POST /query, renders rows and the FK label columns, null
  `courseGroup` fallback, `canRepeat` tag, filter apply/reset, session-storage persistence and
  restore, sort/page persistence, navigation, delete confirmation accepted and dismissed, 409 message.
- `course-detail.spec.ts` — loads the record, renders every section, null fallbacks, n-n chips,
  the two FK link buttons, not-found empty state, navigation.
- `course-form.spec.ts` — add-mode defaults, required-field guard, the `scheduleOff` +10-year
  auto-default (and that it stops once `scheduleOff` is dirty), POST body (trimmed, nulls, ISO dates,
  n-n arrays), edit-mode patch, key locked in edit mode, PUT body, cancel navigation.

---

## Files to Create / Modify

| File | Action |
|------|--------|
| `src/CMS.API/Models/Course.cs` | Create (holds the three nav ref classes) |
| `src/CMS.API/Models/CourseRequest.cs` | Create (holds `CourseUpdateRequest` too) |
| `src/CMS.API/Models/CourseQuery.cs` | Create |
| `src/CMS.API/Models/CourseDeleteResult.cs` | Create |
| `src/CMS.API/Models/CourseGroupLookup.cs` | Create |
| `src/CMS.API/Models/JobCategoryLookup.cs` | Create |
| `src/CMS.API/Models/CertificationLookup.cs` | Create |
| `src/CMS.API/Repositories/ICourseRepository.cs` | Create |
| `src/CMS.API/Repositories/CourseRepository.cs` | Create |
| `src/CMS.API/Repositories/ICourseGroupRepository.cs` + `CourseGroupRepository.cs` | Create (lookup-only) |
| `src/CMS.API/Repositories/IJobCategoryRepository.cs` + `JobCategoryRepository.cs` | Create (lookup-only) |
| `src/CMS.API/Repositories/ICertificationRepository.cs` + `CertificationRepository.cs` | Create (lookup-only) |
| `src/CMS.API/Controllers/CoursesController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — three new lookups |
| `src/CMS.API/Program.cs` | Modify — register four repositories |
| `src/CMS.API.Tests/Fakes/InMemoryCourseRepository.cs` | Create |
| `src/CMS.API.Tests/Fakes/InMemoryCourseGroupRepository.cs` | Create |
| `src/CMS.API.Tests/Fakes/InMemoryJobCategoryRepository.cs` | Create |
| `src/CMS.API.Tests/Fakes/InMemoryCertificationRepository.cs` | Create |
| `src/CMS.API.Tests/CoursesControllerTests.cs` | Create |
| `src/CMS.API.Tests/CoursesRoutingConventionTests.cs` | Create |
| `src/CMS.API.Tests/LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/src/app/core/models/course.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/course-group.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/job-category.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/certification.model.ts` | Create |
| `src/CMS.NG/src/app/core/models/index.ts` | Modify |
| `src/CMS.NG/src/app/core/utils/date.util.ts` (+ `.spec.ts`) | Create |
| `src/CMS.NG/src/app/core/services/course.service.ts` (+ `.spec.ts`) | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` (+ `.spec.ts`) | Modify |
| `src/CMS.NG/src/app/core/services/index.ts` | Modify |
| `src/CMS.NG/src/app/features/courses/course-list/*` | Create |
| `src/CMS.NG/src/app/features/courses/course-detail/*` | Create |
| `src/CMS.NG/src/app/features/courses/course-form/*` | Create |
| `src/CMS.NG/src/app/features/courses/courses.routes.ts` | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — lazy route |
| `src/CMS.NG/src/app/shared/layout/nav-menu.ts` | Modify — entry in the existing 課程管理 Course group |
