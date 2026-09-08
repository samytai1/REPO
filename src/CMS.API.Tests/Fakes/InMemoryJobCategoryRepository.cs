using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IJobCategoryRepository"/>. Like the SQL lookup it sorts by
/// Description ASC.
/// </summary>
public class InMemoryJobCategoryRepository : IJobCategoryRepository
{
    private readonly List<JobCategoryLookup> _jobCategories;

    public InMemoryJobCategoryRepository(params JobCategoryLookup[] jobCategories)
    {
        _jobCategories = jobCategories.ToList();
    }

    /// <summary>Seeds a job category directly.</summary>
    public InMemoryJobCategoryRepository Seed(short pkid, string description)
    {
        _jobCategories.Add(new JobCategoryLookup { Pkid = pkid, Description = description });
        return this;
    }

    public Task<IEnumerable<JobCategoryLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<JobCategoryLookup>>(
            _jobCategories.OrderBy(jc => jc.Description, StringComparer.Ordinal).ToList());
}
