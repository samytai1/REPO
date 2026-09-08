using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Lookup-only data access for dbo.Certification (認證).
/// </summary>
/// <remarks>
/// Feeds the Course form's 對應認證 n-n multiselect. See <see cref="ICourseGroupRepository"/> for
/// why this is lookup-only.
/// </remarks>
public interface ICertificationRepository
{
    /// <summary>Slim certification list for n-n option controls, ordered by Title ASC.</summary>
    Task<IEnumerable<CertificationLookup>> GetLookupAsync(CancellationToken cancellationToken = default);
}
