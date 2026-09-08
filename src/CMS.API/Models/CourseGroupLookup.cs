namespace CMS.API.Models;

/// <summary>
/// Slim lookup row for dbo.CourseGroup (課程群組), used as the FK option list on the Course form
/// and filter drawer.
/// </summary>
/// <remarks>
/// CourseGroup is not a generated feature yet, so this DTO — and the lookup-only repository behind
/// it — exist purely to feed those option controls, mirroring <see cref="AppUserLookup"/>.
/// </remarks>
public class CourseGroupLookup
{
    /// <summary>主代碼 — dbo.CourseGroup.pkid (smallint IDENTITY).</summary>
    public short Pkid { get; set; }

    /// <summary>群組說明 — dbo.CourseGroup.Description.</summary>
    public string Description { get; set; } = string.Empty;
}
