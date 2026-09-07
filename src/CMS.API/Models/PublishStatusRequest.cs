using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.PublishStatus. pkid is the key: supplied on create (the column is not an
/// IDENTITY), immutable on update.
/// </summary>
public class PublishStatusRequest
{
    /// <summary>主代碼 — key. Required on both create and update.</summary>
    [Range(0, 255)]
    public byte Pkid { get; set; }

    /// <summary>狀態說明.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿.</summary>
    public bool IsDraft { get; set; }

    /// <summary>已發布.</summary>
    public bool IsPublished { get; set; }

    /// <summary>已下架.</summary>
    public bool IsDiscontinued { get; set; }
}
