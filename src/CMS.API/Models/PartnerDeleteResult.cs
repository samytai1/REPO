namespace CMS.API.Models;

/// <summary>
/// Outcome of a delete attempt on dbo.Partner.
/// Certification, Course and PartnerCourseGroup hold FK constraints against this table, so a delete
/// can fail for a reason other than "missing" — <see cref="InUse"/> is reported as 409 rather than
/// a 500.
/// </summary>
public enum PartnerDeleteResult
{
    /// <summary>The row was deleted.</summary>
    Deleted,

    /// <summary>No row with that pkid exists.</summary>
    NotFound,

    /// <summary>The row is still referenced by a child table (SQL error 547).</summary>
    InUse
}
