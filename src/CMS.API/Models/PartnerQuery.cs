namespace CMS.API.Models;

/// <summary>
/// Search DTO for dbo.Partner.
/// </summary>
public class PartnerQuery
{
    /// <summary>LIKE across Name, AppKey, NameOnPartnerMenu and NameOnCourseDetailPage.</summary>
    public string? Keyword { get; set; }

    /// <summary>顯示順序 lower bound (inclusive).</summary>
    public int? DisplayOrderFrom { get; set; }

    /// <summary>顯示順序 upper bound (inclusive).</summary>
    public int? DisplayOrderTo { get; set; }

    /// <summary>
    /// 圖片 — true keeps only rows with a non-blank ImageFilename, false only rows without one,
    /// null leaves the column unfiltered.
    /// </summary>
    public bool? HasImage { get; set; }
}
