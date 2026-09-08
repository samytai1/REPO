using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="ICourseRepository"/> that mirrors the SQL semantics of
/// <c>CourseRepository</c>: pkid is assigned like an IDENTITY column, results sort by DisplayOrder
/// ASC then pkid ASC, the keyword filter spans the five short string columns, the three FK filters
/// are exact matches, CanRepeat is tri-state, both date ranges are inclusive, the FK nav objects are
/// resolved from the seeded lookup rows, the two junctions are replaced (and de-duplicated) on every
/// write, and a delete blocked by a child FK reports <see cref="CourseDeleteResult.InUse"/> — the
/// SQL 547 path.
/// </summary>
/// <remarks>
/// The n-n collections are populated by <see cref="GetByIdAsync(int, CancellationToken)"/> only,
/// exactly as the SQL repository does; list reads carry the counts instead.
/// </remarks>
public class InMemoryCourseRepository : ICourseRepository
{
    private readonly List<Course> _courses = new();
    private readonly Dictionary<int, List<short>> _jobCategoryLinks = new();
    private readonly Dictionary<int, List<int>> _certificationLinks = new();
    private readonly HashSet<int> _inUse = new();

    private readonly Dictionary<short, CoursePartnerRef> _partners = new();
    private readonly Dictionary<short, CourseGroupRef> _courseGroups = new();
    private readonly Dictionary<byte, CoursePublishStatusRef> _publishStatuses = new();
    private readonly Dictionary<short, JobCategoryLookup> _jobCategories = new();
    private readonly Dictionary<int, CertificationLookup> _certifications = new();

    private int _nextPkid = 1;

    /// <summary>Seeds a partner the JOIN can resolve.</summary>
    public InMemoryCourseRepository SeedPartner(short pkid, string name)
    {
        _partners[pkid] = new CoursePartnerRef { Pkid = pkid, Name = name };
        return this;
    }

    /// <summary>Seeds a course group the LEFT JOIN can resolve.</summary>
    public InMemoryCourseRepository SeedCourseGroup(short pkid, string description)
    {
        _courseGroups[pkid] = new CourseGroupRef { Pkid = pkid, Description = description };
        return this;
    }

    /// <summary>Seeds a publish status the JOIN can resolve.</summary>
    public InMemoryCourseRepository SeedPublishStatus(byte pkid, string description)
    {
        _publishStatuses[pkid] = new CoursePublishStatusRef { Pkid = pkid, Description = description };
        return this;
    }

    /// <summary>Seeds a job category the n-n read can resolve.</summary>
    public InMemoryCourseRepository SeedJobCategory(short pkid, string description)
    {
        _jobCategories[pkid] = new JobCategoryLookup { Pkid = pkid, Description = description };
        return this;
    }

    /// <summary>Seeds a certification the n-n read can resolve.</summary>
    public InMemoryCourseRepository SeedCertification(int pkid, string? title, short partnerPkid = 1)
    {
        _certifications[pkid] = new CertificationLookup { Pkid = pkid, Title = title, PartnerPkid = partnerPkid };
        return this;
    }

    /// <summary>Seeds a course directly, bypassing the write path.</summary>
    public InMemoryCourseRepository Seed(
        Course course,
        IEnumerable<short>? jobCategoryPkids = null,
        IEnumerable<int>? certificationPkids = null)
    {
        _courses.Add(course);

        _jobCategoryLinks[course.Pkid] = (jobCategoryPkids ?? Enumerable.Empty<short>()).Distinct().ToList();
        _certificationLinks[course.Pkid] = (certificationPkids ?? Enumerable.Empty<int>()).Distinct().ToList();

        if (course.Pkid >= _nextPkid) _nextPkid = course.Pkid + 1;

        return this;
    }

    /// <summary>
    /// Marks a pkid as still referenced by CourseFAQ / CourseRelatedLink / HotCourse, so deleting it
    /// conflicts.
    /// </summary>
    public InMemoryCourseRepository MarkInUse(int pkid)
    {
        _inUse.Add(pkid);
        return this;
    }

    /// <summary>The junction rows currently held for a course — the seam the n-n write tests read.</summary>
    public IReadOnlyList<short> JobCategoryLinks(int pkid)
        => _jobCategoryLinks.TryGetValue(pkid, out var links) ? links : Array.Empty<short>();

    /// <inheritdoc cref="JobCategoryLinks" />
    public IReadOnlyList<int> CertificationLinks(int pkid)
        => _certificationLinks.TryGetValue(pkid, out var links) ? links : Array.Empty<int>();

    public Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Course>>(Sorted(_courses).ToList());

    public Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<Course> results = _courses;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(c =>
                c.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (c.OfficialTitle ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || c.CourseId.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || c.ProdCourseId.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || c.FriendlyUrl.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.PartnerPkid.HasValue)
        {
            results = results.Where(c => c.PartnerPkid == query.PartnerPkid.Value);
        }

        if (query.CourseGroupPkid.HasValue)
        {
            results = results.Where(c => c.CourseGroupPkid == query.CourseGroupPkid.Value);
        }

        if (query.PublishStatusPkid.HasValue)
        {
            results = results.Where(c => c.PublishStatusPkid == query.PublishStatusPkid.Value);
        }

        if (query.CanRepeat.HasValue)
        {
            results = results.Where(c => c.CanRepeat == query.CanRepeat.Value);
        }

        if (query.ScheduleOnFrom.HasValue)
        {
            results = results.Where(c => c.ScheduleOn >= query.ScheduleOnFrom.Value);
        }

        if (query.ScheduleOnTo.HasValue)
        {
            results = results.Where(c => c.ScheduleOn <= query.ScheduleOnTo.Value);
        }

        if (query.ScheduleOffFrom.HasValue)
        {
            results = results.Where(c => c.ScheduleOff >= query.ScheduleOffFrom.Value);
        }

        if (query.ScheduleOffTo.HasValue)
        {
            results = results.Where(c => c.ScheduleOff <= query.ScheduleOffTo.Value);
        }

        return Task.FromResult<IEnumerable<Course>>(Sorted(results).ToList());
    }

    public Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var course = Find(pkid);
        return Task.FromResult(course is null ? null : WithMembers(course));
    }

    public Task<Course> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        var course = new Course { Pkid = _nextPkid++ };

        Apply(course, request);
        _courses.Add(course);
        ReplaceJunctions(course.Pkid, request);

        return Task.FromResult(WithMembers(course));
    }

    public Task<Course?> UpdateAsync(CourseUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var course = Find(request.Pkid);

        if (course is null) return Task.FromResult<Course?>(null);

        Apply(course, request);
        ReplaceJunctions(course.Pkid, request);

        return Task.FromResult<Course?>(WithMembers(course));
    }

    public Task<CourseDeleteResult> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var course = Find(pkid);

        if (course is null) return Task.FromResult(CourseDeleteResult.NotFound);

        if (_inUse.Contains(pkid)) return Task.FromResult(CourseDeleteResult.InUse);

        _courses.Remove(course);
        _jobCategoryLinks.Remove(pkid);
        _certificationLinks.Remove(pkid);

        return Task.FromResult(CourseDeleteResult.Deleted);
    }

    private Course? Find(int pkid) => _courses.FirstOrDefault(c => c.Pkid == pkid);

    private IEnumerable<Course> Sorted(IEnumerable<Course> courses) => courses
        .OrderBy(c => c.DisplayOrder)
        .ThenBy(c => c.Pkid)
        .Select(Project)
        .ToList();

    /// <summary>Writes the request onto the stored row, normalising blanks to null as the SQL does.</summary>
    private static void Apply(Course course, CourseRequest request)
    {
        course.Title = request.Title.Trim();
        course.OfficialTitle = Normalize(request.OfficialTitle);
        course.CourseId = request.CourseId.Trim();
        course.ProdCourseId = request.ProdCourseId.Trim();
        course.FriendlyUrl = request.FriendlyUrl.Trim();
        course.DisplayOrder = request.DisplayOrder;
        course.PartnerPkid = request.PartnerPkid;
        course.CourseGroupPkid = request.CourseGroupPkid;
        course.PublishStatusPkid = request.PublishStatusPkid;
        course.ScheduleOn = request.ScheduleOn;
        course.ScheduleOff = request.ScheduleOff;
        course.Hour = request.Hour;
        course.ListPrice = request.ListPrice;
        course.LearningCredit = request.LearningCredit;
        course.Material = Normalize(request.Material);
        course.Objective = Normalize(request.Objective);
        course.Target = Normalize(request.Target);
        course.Prerequisites = Normalize(request.Prerequisites);
        course.Outline = Normalize(request.Outline);
        course.TowardCertOrExam = Normalize(request.TowardCertOrExam);
        course.Note = Normalize(request.Note);
        course.OtherInfo = Normalize(request.OtherInfo);
        course.CanRepeat = request.CanRepeat;
    }

    /// <summary>n-n: delete-then-reinsert, de-duplicated as the composite junction PK requires.</summary>
    private void ReplaceJunctions(int pkid, CourseRequest request)
    {
        _jobCategoryLinks[pkid] = request.JobCategoryPkids.Distinct().ToList();
        _certificationLinks[pkid] = request.CertificationPkids.Distinct().ToList();
    }

    /// <summary>A nullable column stores null, never an empty or whitespace-only string.</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Returns a detached copy with the FK nav objects and n-n counts, as the SQL projection does.</summary>
    private Course Project(Course course)
    {
        var jobCategoryPkids = JobCategoryLinks(course.Pkid);
        var certificationPkids = CertificationLinks(course.Pkid);

        return new Course
        {
            Pkid = course.Pkid,
            Title = course.Title,
            OfficialTitle = course.OfficialTitle,
            CourseId = course.CourseId,
            ProdCourseId = course.ProdCourseId,
            FriendlyUrl = course.FriendlyUrl,
            DisplayOrder = course.DisplayOrder,
            PartnerPkid = course.PartnerPkid,
            CourseGroupPkid = course.CourseGroupPkid,
            PublishStatusPkid = course.PublishStatusPkid,
            ScheduleOn = course.ScheduleOn,
            ScheduleOff = course.ScheduleOff,
            Hour = course.Hour,
            ListPrice = course.ListPrice,
            LearningCredit = course.LearningCredit,
            Material = course.Material,
            Objective = course.Objective,
            Target = course.Target,
            Prerequisites = course.Prerequisites,
            Outline = course.Outline,
            TowardCertOrExam = course.TowardCertOrExam,
            Note = course.Note,
            OtherInfo = course.OtherInfo,
            CanRepeat = course.CanRepeat,
            Partner = _partners.TryGetValue(course.PartnerPkid, out var partner) ? partner : null,
            CourseGroup = course.CourseGroupPkid.HasValue
                          && _courseGroups.TryGetValue(course.CourseGroupPkid.Value, out var courseGroup)
                ? courseGroup
                : null,
            PublishStatus = _publishStatuses.TryGetValue(course.PublishStatusPkid, out var publishStatus)
                ? publishStatus
                : null,
            JobCategoryCount = jobCategoryPkids.Count,
            CertificationCount = certificationPkids.Count
        };
    }

    /// <summary>The projection plus both n-n collections — what a single-record read returns.</summary>
    private Course WithMembers(Course course)
    {
        var projected = Project(course);

        projected.JobCategories = JobCategoryLinks(course.Pkid)
            .Where(_jobCategories.ContainsKey)
            .Select(pkid => _jobCategories[pkid])
            .OrderBy(jc => jc.Description, StringComparer.Ordinal)
            .ToList();

        projected.Certifications = CertificationLinks(course.Pkid)
            .Where(_certifications.ContainsKey)
            .Select(pkid => _certifications[pkid])
            .OrderBy(ct => ct.Title, StringComparer.Ordinal)
            .ToList();

        return projected;
    }
}
