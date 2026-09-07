using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.Partner. pkid is a smallint IDENTITY column, so the database assigns it and it
/// is absent here — the update body carries it instead (see <see cref="PartnerUpdateRequest"/>).
/// </summary>
public class PartnerRequest
{
    /// <summary>名稱.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>應用代碼.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(10)]
    public string AppKey { get; set; } = string.Empty;

    /// <summary>選單顯示名稱.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>課程明細頁名稱.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>顯示順序.</summary>
    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    /// <summary>圖片檔名 — optional; send null (not an empty string) when blank.</summary>
    [StringLength(50)]
    public string? ImageFilename { get; set; }
}

/// <summary>
/// PUT body for dbo.Partner — the create fields plus the key. PUT posts to the collection route, so
/// pkid has to travel in the body.
/// </summary>
public class PartnerUpdateRequest : PartnerRequest
{
    /// <summary>主代碼 — the row to update.</summary>
    [Range(1, short.MaxValue)]
    public short Pkid { get; set; }
}
