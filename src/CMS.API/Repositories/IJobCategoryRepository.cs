using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Lookup-only data access for dbo.JobCategory (職務類別).
/// </summary>
/// <remarks>
/// Feeds the Course form's 職務類別 n-n multiselect. See <see cref="ICourseGroupRepository"/> for
/// why this is lookup-only.
/// </remarks>
public interface IJobCategoryRepository
{
    /// <summary>Slim job-category list for n-n option controls, ordered by Description ASC.</summary>
    Task<IEnumerable<JobCategoryLookup>> GetLookupAsync(CancellationToken cancellationToken = default);
}
