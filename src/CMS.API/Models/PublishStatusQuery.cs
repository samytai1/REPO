namespace CMS.API.Models;

/// <summary>
/// Search DTO for dbo.PublishStatus.
/// </summary>
public class PublishStatusQuery
{
    /// <summary>LIKE across Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>主代碼 lower bound (inclusive).</summary>
    public byte? PkidFrom { get; set; }

    /// <summary>主代碼 upper bound (inclusive).</summary>
    public byte? PkidTo { get; set; }

    /// <summary>草稿 — null leaves the flag unfiltered.</summary>
    public bool? IsDraft { get; set; }

    /// <summary>已發布 — null leaves the flag unfiltered.</summary>
    public bool? IsPublished { get; set; }

    /// <summary>已下架 — null leaves the flag unfiltered.</summary>
    public bool? IsDiscontinued { get; set; }
}
