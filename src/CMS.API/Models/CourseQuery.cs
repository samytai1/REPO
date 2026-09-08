namespace CMS.API.Models;

/// <summary>
/// Search DTO for dbo.Course.
/// </summary>
public class CourseQuery
{
    /// <summary>LIKE across Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl.</summary>
    /// <remarks>
    /// The long text columns (Objective, Prerequisites, Outline, TowardCertOrExam, Note, OtherInfo,
    /// Material, Target) are deliberately excluded — nvarchar(max)/(4000) scans are slow and rarely
    /// what a keyword search means.
    /// </remarks>
    public string? Keyword { get; set; }

    /// <summary>原廠 — exact match on Partner_pkid.</summary>
    public short? PartnerPkid { get; set; }

    /// <summary>課程群組 — exact match on CourseGroup_pkid.</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態 — exact match on PublishStatus_pkid.</summary>
    public byte? PublishStatusPkid { get; set; }

    /// <summary>允許重聽 — tri-state: null = no filter, true / false = exact match.</summary>
    public bool? CanRepeat { get; set; }

    /// <summary>上架日期 lower bound (inclusive).</summary>
    public DateOnly? ScheduleOnFrom { get; set; }

    /// <summary>上架日期 upper bound (inclusive).</summary>
    public DateOnly? ScheduleOnTo { get; set; }

    /// <summary>下架日期 lower bound (inclusive).</summary>
    public DateOnly? ScheduleOffFrom { get; set; }

    /// <summary>下架日期 upper bound (inclusive).</summary>
    public DateOnly? ScheduleOffTo { get; set; }
}
