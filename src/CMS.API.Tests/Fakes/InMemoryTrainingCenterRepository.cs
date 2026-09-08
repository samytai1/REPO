using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="ITrainingCenterRepository"/>. Like the SQL lookup it sorts by
/// DisplayOrder ASC then pkid ASC.
/// </summary>
public class InMemoryTrainingCenterRepository : ITrainingCenterRepository
{
    private readonly List<TrainingCenterLookup> _trainingCenters;

    public InMemoryTrainingCenterRepository(params TrainingCenterLookup[] trainingCenters)
    {
        _trainingCenters = trainingCenters.ToList();
    }

    /// <summary>Seeds a training center directly.</summary>
    public InMemoryTrainingCenterRepository Seed(short pkid, string name, int displayOrder)
    {
        _trainingCenters.Add(new TrainingCenterLookup { Pkid = pkid, Name = name, DisplayOrder = displayOrder });
        return this;
    }

    public Task<IEnumerable<TrainingCenterLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<TrainingCenterLookup>>(
            _trainingCenters.OrderBy(tc => tc.DisplayOrder).ThenBy(tc => tc.Pkid).ToList());
}
