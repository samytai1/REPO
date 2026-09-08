using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.Course. pkid is an IDENTITY column, so it is absent here and carried by
/// <see cref="CourseUpdateRequest"/> instead.
/// </summary>
public class CourseRequest
{
    /// <summary>課程名稱.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>官方課程名稱 (nullable).</summary>
    [StringLength(300)]
    public string? OfficialTitle { get; set; }

    /// <summary>簡介代碼.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string CourseId { get; set; } = string.Empty;

    /// <summary>科目代碼.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>網址代稱.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>顯示順序.</summary>
    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    /// <summary>原廠 — dbo.Partner.pkid. Required.</summary>
    [Range(1, short.MaxValue)]
    public short PartnerPkid { get; set; }

    /// <summary>課程群組 — dbo.CourseGroup.pkid. Nullable — the only optional FK.</summary>
    [Range(1, short.MaxValue)]
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態 — dbo.PublishStatus.pkid. Required.</summary>
    [Range(0, byte.MaxValue)]
    public byte PublishStatusPkid { get; set; }

    /// <summary>上架日期.</summary>
    [Required]
    public DateOnly ScheduleOn { get; set; }

    /// <summary>下架日期.</summary>
    [Required]
    public DateOnly ScheduleOff { get; set; }

    /// <summary>時數.</summary>
    [Range(0, short.MaxValue)]
    public short Hour { get; set; }

    /// <summary>定價 — decimal(9,0), so nine whole digits.</summary>
    [Range(0, 999999999)]
    public decimal ListPrice { get; set; }

    /// <summary>點數 — decimal(9,1), so eight whole digits and one decimal.</summary>
    [Range(0, 99999999.9)]
    public decimal LearningCredit { get; set; }

    /// <summary>教材 (nullable).</summary>
    [StringLength(500)]
    public string? Material { get; set; }

    /// <summary>課程目標 (nullable).</summary>
    [StringLength(4000)]
    public string? Objective { get; set; }

    /// <summary>適合對象 (nullable).</summary>
    [StringLength(500)]
    public string? Target { get; set; }

    /// <summary>先備知識 (nullable).</summary>
    [StringLength(4000)]
    public string? Prerequisites { get; set; }

    /// <summary>課程大綱 — nvarchar(max), so no length cap (nullable).</summary>
    public string? Outline { get; set; }

    /// <summary>對應認證／考試 — nvarchar(max), so no length cap (nullable).</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>備註 (nullable).</summary>
    [StringLength(4000)]
    public string? Note { get; set; }

    /// <summary>其他資訊 (nullable).</summary>
    [StringLength(4000)]
    public string? OtherInfo { get; set; }

    /// <summary>允許重聽.</summary>
    public bool CanRepeat { get; set; }

    /// <summary>職務類別 — n-n member keys for dbo.CourseJobCategories.</summary>
    public List<short> JobCategoryPkids { get; set; } = new();

    /// <summary>對應認證 — n-n member keys for dbo.CourseInCertification.</summary>
    public List<int> CertificationPkids { get; set; } = new();
}

/// <summary>PUT body — the same fields plus the key, which the collection route needs.</summary>
public class CourseUpdateRequest : CourseRequest
{
    /// <summary>主代碼 — dbo.Course.pkid.</summary>
    [Range(1, int.MaxValue)]
    public int Pkid { get; set; }
}
