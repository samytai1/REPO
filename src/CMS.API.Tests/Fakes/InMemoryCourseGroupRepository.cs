using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="ICourseGroupRepository"/>. Like the SQL lookup it sorts by
/// Description ASC.
/// </summary>
public class InMemoryCourseGroupRepository : ICourseGroupRepository
{
    private readonly List<CourseGroupLookup> _courseGroups;

    public InMemoryCourseGroupRepository(params CourseGroupLookup[] courseGroups)
    {
        _courseGroups = courseGroups.ToList();
    }

    /// <summary>Seeds a course group directly.</summary>
    public InMemoryCourseGroupRepository Seed(short pkid, string description)
    {
        _courseGroups.Add(new CourseGroupLookup { Pkid = pkid, Description = description });
        return this;
    }

    public Task<IEnumerable<CourseGroupLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<CourseGroupLookup>>(
            _courseGroups.OrderBy(cg => cg.Description, StringComparer.Ordinal).ToList());
}
