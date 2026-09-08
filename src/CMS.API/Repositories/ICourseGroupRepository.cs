using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Lookup-only data access for dbo.CourseGroup (課程群組).
/// </summary>
/// <remarks>
/// CourseGroup has no generated feature yet; this exists solely to feed the Course form's FK select
/// and the list filter drawer, mirroring <see cref="IAppUserRepository"/>. When /crud is run for
/// CourseGroup, repoint the lookup endpoint at that feature's repository and delete this one.
/// </remarks>
public interface ICourseGroupRepository
{
    /// <summary>Slim course-group list for FK option controls, ordered by Description ASC.</summary>
    Task<IEnumerable<CourseGroupLookup>> GetLookupAsync(CancellationToken cancellationToken = default);
}
