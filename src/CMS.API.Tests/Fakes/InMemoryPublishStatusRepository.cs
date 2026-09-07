using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IPublishStatusRepository"/> that mirrors the SQL semantics of
/// <c>PublishStatusRepository</c>: pkid is the key, results sort by pkid ASC, the keyword filter
/// covers Description only, the pkid range and the three bit flags filter exactly, and a delete
/// blocked by a child FK reports <see cref="PublishStatusDeleteResult.InUse"/> — the SQL 547 path.
/// </summary>
public class InMemoryPublishStatusRepository : IPublishStatusRepository
{
    private readonly List<PublishStatus> _statuses = new();
    private readonly HashSet<byte> _inUse = new();

    /// <summary>Seeds a status directly, bypassing the write path.</summary>
    public InMemoryPublishStatusRepository Seed(byte pkid, string description, bool isDraft, bool isPublished, bool isDiscontinued)
    {
        _statuses.Add(new PublishStatus
        {
            Pkid = pkid,
            Description = description,
            IsDraft = isDraft,
            IsPublished = isPublished,
            IsDiscontinued = isDiscontinued
        });

        return this;
    }

    /// <summary>Marks a pkid as still referenced by Course / Promotion2, so deleting it conflicts.</summary>
    public InMemoryPublishStatusRepository MarkInUse(byte pkid)
    {
        _inUse.Add(pkid);
        return this;
    }

    public Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<PublishStatus>>(_statuses
            .OrderBy(s => s.Pkid)
            .Select(Project)
            .ToList());

    public Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<PublishStatus> results = _statuses;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(s => s.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.PkidFrom.HasValue)
        {
            results = results.Where(s => s.Pkid >= query.PkidFrom.Value);
        }

        if (query.PkidTo.HasValue)
        {
            results = results.Where(s => s.Pkid <= query.PkidTo.Value);
        }

        if (query.IsDraft.HasValue)
        {
            results = results.Where(s => s.IsDraft == query.IsDraft.Value);
        }

        if (query.IsPublished.HasValue)
        {
            results = results.Where(s => s.IsPublished == query.IsPublished.Value);
        }

        if (query.IsDiscontinued.HasValue)
        {
            results = results.Where(s => s.IsDiscontinued == query.IsDiscontinued.Value);
        }

        return Task.FromResult<IEnumerable<PublishStatus>>(results
            .OrderBy(s => s.Pkid)
            .Select(Project)
            .ToList());
    }

    public Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        var status = Find(pkid);
        return Task.FromResult(status is null ? null : Project(status));
    }

    public Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken = default)
        => Task.FromResult(Find(pkid) is not null);

    public Task<PublishStatus> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        var status = new PublishStatus
        {
            Pkid = request.Pkid,
            Description = request.Description.Trim(),
            IsDraft = request.IsDraft,
            IsPublished = request.IsPublished,
            IsDiscontinued = request.IsDiscontinued
        };

        _statuses.Add(status);

        return Task.FromResult(Project(status));
    }

    public Task<PublishStatus?> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        var status = Find(request.Pkid);

        if (status is null) return Task.FromResult<PublishStatus?>(null);

        status.Description = request.Description.Trim();
        status.IsDraft = request.IsDraft;
        status.IsPublished = request.IsPublished;
        status.IsDiscontinued = request.IsDiscontinued;

        return Task.FromResult<PublishStatus?>(Project(status));
    }

    public Task<PublishStatusDeleteResult> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        var status = Find(pkid);

        if (status is null) return Task.FromResult(PublishStatusDeleteResult.NotFound);

        if (_inUse.Contains(pkid)) return Task.FromResult(PublishStatusDeleteResult.InUse);

        _statuses.Remove(status);

        return Task.FromResult(PublishStatusDeleteResult.Deleted);
    }

    private PublishStatus? Find(byte pkid) => _statuses.FirstOrDefault(s => s.Pkid == pkid);

    /// <summary>Returns a detached copy, as the SQL projection does.</summary>
    private static PublishStatus Project(PublishStatus status) => new()
    {
        Pkid = status.Pkid,
        Description = status.Description,
        IsDraft = status.IsDraft,
        IsPublished = status.IsPublished,
        IsDiscontinued = status.IsDiscontinued
    };
}
