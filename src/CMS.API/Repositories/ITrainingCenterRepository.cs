using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Lookup-only data access for dbo.TrainingCenter (教育中心). Feeds the 上稿作業 tab strip; when
/// TrainingCenter gets its own /crud, repoint the lookup endpoint at that repository and delete this.
/// </summary>
public interface ITrainingCenterRepository
{
    /// <summary>Slim training-center list, ordered by DisplayOrder ASC then pkid ASC.</summary>
    Task<IEnumerable<TrainingCenterLookup>> GetLookupAsync(CancellationToken cancellationToken = default);
}
