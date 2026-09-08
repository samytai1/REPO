namespace CMS.API.Models;

/// <summary>
/// Slim lookup row for dbo.JobCategory (職務類別), used as the n-n option list on the Course form.
/// </summary>
public class JobCategoryLookup
{
    /// <summary>主代碼 — dbo.JobCategory.pkid (smallint IDENTITY).</summary>
    public short Pkid { get; set; }

    /// <summary>類別說明 — dbo.JobCategory.Description.</summary>
    public string Description { get; set; } = string.Empty;
}
