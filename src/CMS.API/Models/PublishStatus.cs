namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.PublishStatus (發布狀態).
/// </summary>
public class PublishStatus
{
    /// <summary>主代碼 — dbo.PublishStatus.pkid (tinyint, user-supplied — not an IDENTITY column).</summary>
    public byte Pkid { get; set; }

    /// <summary>狀態說明 — dbo.PublishStatus.Description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿 — dbo.PublishStatus.IsDraft.</summary>
    public bool IsDraft { get; set; }

    /// <summary>已發布 — dbo.PublishStatus.IsPublished.</summary>
    public bool IsPublished { get; set; }

    /// <summary>已下架 — dbo.PublishStatus.IsDiscontinued.</summary>
    public bool IsDiscontinued { get; set; }
}
