namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.Course (課程).
/// </summary>
public class Course
{
    /// <summary>主代碼 — dbo.Course.pkid (int IDENTITY, assigned by the database).</summary>
    public int Pkid { get; set; }

    /// <summary>課程名稱 — dbo.Course.Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>官方課程名稱 — dbo.Course.OfficialTitle (nullable).</summary>
    public string? OfficialTitle { get; set; }

    /// <summary>簡介代碼 — dbo.Course.CourseId.</summary>
    public string CourseId { get; set; } = string.Empty;

    /// <summary>科目代碼 — dbo.Course.ProdCourseId.</summary>
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>網址代稱 — dbo.Course.FriendlyUrl.</summary>
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>顯示順序 — dbo.Course.DisplayOrder.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>原廠 — dbo.Course.Partner_pkid.</summary>
    public short PartnerPkid { get; set; }

    /// <summary>課程群組 — dbo.Course.CourseGroup_pkid (nullable).</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態 — dbo.Course.PublishStatus_pkid.</summary>
    public byte PublishStatusPkid { get; set; }

    /// <summary>上架日期 — dbo.Course.ScheduleOn.</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>下架日期 — dbo.Course.ScheduleOff.</summary>
    public DateOnly ScheduleOff { get; set; }

    /// <summary>時數 — dbo.Course.Hour (bracketed as [Hour] in SQL, since HOUR is a datepart name).</summary>
    public short Hour { get; set; }

    /// <summary>定價 — dbo.Course.ListPrice, decimal(9,0).</summary>
    public decimal ListPrice { get; set; }

    /// <summary>點數 — dbo.Course.LearningCredit, decimal(9,1).</summary>
    public decimal LearningCredit { get; set; }

    /// <summary>教材 — dbo.Course.Material (nullable).</summary>
    public string? Material { get; set; }

    /// <summary>課程目標 — dbo.Course.Objective (nullable).</summary>
    public string? Objective { get; set; }

    /// <summary>適合對象 — dbo.Course.Target (nullable).</summary>
    public string? Target { get; set; }

    /// <summary>先備知識 — dbo.Course.Prerequisites (nullable).</summary>
    public string? Prerequisites { get; set; }

    /// <summary>課程大綱 — dbo.Course.Outline, nvarchar(max) (nullable).</summary>
    public string? Outline { get; set; }

    /// <summary>對應認證／考試 — dbo.Course.TowardCertOrExam, nvarchar(max) (nullable).</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>備註 — dbo.Course.Note (nullable).</summary>
    public string? Note { get; set; }

    /// <summary>其他資訊 — dbo.Course.OtherInfo (nullable).</summary>
    public string? OtherInfo { get; set; }

    /// <summary>允許重聽 — dbo.Course.CanRepeat.</summary>
    public bool CanRepeat { get; set; }

    /// <summary>原廠 — FK nav object, JOINed from dbo.Partner.</summary>
    public CoursePartnerRef? Partner { get; set; }

    /// <summary>課程群組 — FK nav object, LEFT JOINed from dbo.CourseGroup. Null when the FK is null.</summary>
    public CourseGroupRef? CourseGroup { get; set; }

    /// <summary>上架狀態 — FK nav object, JOINed from dbo.PublishStatus.</summary>
    public CoursePublishStatusRef? PublishStatus { get; set; }

    /// <summary>職務類別數 — subquery count over dbo.CourseJobCategories.</summary>
    public int JobCategoryCount { get; set; }

    /// <summary>對應認證數 — subquery count over dbo.CourseInCertification.</summary>
    public int CertificationCount { get; set; }

    /// <summary>職務類別 — n-n members from dbo.CourseJobCategories (detail reads only).</summary>
    public List<JobCategoryLookup> JobCategories { get; set; } = new();

    /// <summary>對應認證 — n-n members from dbo.CourseInCertification (detail reads only).</summary>
    public List<CertificationLookup> Certifications { get; set; } = new();
}

/// <summary>
/// Slim FK reference to dbo.Partner — only the columns a course list labels its 原廠 column with.
/// Deliberately not the full <see cref="Partner"/> model: a course list runs to hundreds of rows.
/// </summary>
public class CoursePartnerRef
{
    /// <summary>主代碼 — dbo.Partner.pkid.</summary>
    public short Pkid { get; set; }

    /// <summary>名稱 — dbo.Partner.Name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>Slim FK reference to dbo.CourseGroup — the 課程群組 label column.</summary>
public class CourseGroupRef
{
    /// <summary>主代碼 — dbo.CourseGroup.pkid.</summary>
    public short Pkid { get; set; }

    /// <summary>群組說明 — dbo.CourseGroup.Description.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>Slim FK reference to dbo.PublishStatus — the 上架狀態 label column.</summary>
public class CoursePublishStatusRef
{
    /// <summary>主代碼 — dbo.PublishStatus.pkid.</summary>
    public byte Pkid { get; set; }

    /// <summary>狀態說明 — dbo.PublishStatus.Description.</summary>
    public string Description { get; set; } = string.Empty;
}
