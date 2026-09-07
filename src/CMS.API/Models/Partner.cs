namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.Partner (合作夥伴).
/// </summary>
public class Partner
{
    /// <summary>主代碼 — dbo.Partner.pkid (smallint IDENTITY, assigned by the database).</summary>
    public short Pkid { get; set; }

    /// <summary>名稱 — dbo.Partner.Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>應用代碼 — dbo.Partner.AppKey.</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>選單顯示名稱 — dbo.Partner.NameOnPartnerMenu.</summary>
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>課程明細頁名稱 — dbo.Partner.NameOnCourseDetailPage.</summary>
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>顯示順序 — dbo.Partner.DisplayOrder.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>圖片檔名 — dbo.Partner.ImageFilename (nullable).</summary>
    public string? ImageFilename { get; set; }
}
