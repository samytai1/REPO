using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IPartnerRepository"/> that mirrors the SQL semantics of
/// <c>PartnerRepository</c>: pkid is assigned like an IDENTITY column, results sort by
/// DisplayOrder ASC then pkid ASC, the keyword filter spans the four short string columns, the
/// DisplayOrder range is inclusive, HasImage treats a whitespace-only filename as no image, and a
/// delete blocked by a child FK reports <see cref="PartnerDeleteResult.InUse"/> — the SQL 547 path.
/// </summary>
public class InMemoryPartnerRepository : IPartnerRepository
{
    private readonly List<Partner> _partners = new();
    private readonly HashSet<short> _inUse = new();

    private short _nextPkid = 1;

    /// <summary>Seeds a partner directly, bypassing the write path.</summary>
    public InMemoryPartnerRepository Seed(
        short pkid,
        string name,
        string appKey,
        string nameOnPartnerMenu,
        string nameOnCourseDetailPage,
        int displayOrder,
        string? imageFilename = null)
    {
        _partners.Add(new Partner
        {
            Pkid = pkid,
            Name = name,
            AppKey = appKey,
            NameOnPartnerMenu = nameOnPartnerMenu,
            NameOnCourseDetailPage = nameOnCourseDetailPage,
            DisplayOrder = displayOrder,
            ImageFilename = imageFilename
        });

        if (pkid >= _nextPkid) _nextPkid = (short)(pkid + 1);

        return this;
    }

    /// <summary>
    /// Marks a pkid as still referenced by Certification / Course / PartnerCourseGroup, so deleting
    /// it conflicts.
    /// </summary>
    public InMemoryPartnerRepository MarkInUse(short pkid)
    {
        _inUse.Add(pkid);
        return this;
    }

    public Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Partner>>(Sorted(_partners).ToList());

    public Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<Partner> results = _partners;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(p =>
                p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || p.AppKey.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || p.NameOnPartnerMenu.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || p.NameOnCourseDetailPage.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.DisplayOrderFrom.HasValue)
        {
            results = results.Where(p => p.DisplayOrder >= query.DisplayOrderFrom.Value);
        }

        if (query.DisplayOrderTo.HasValue)
        {
            results = results.Where(p => p.DisplayOrder <= query.DisplayOrderTo.Value);
        }

        if (query.HasImage.HasValue)
        {
            results = results.Where(p => !string.IsNullOrWhiteSpace(p.ImageFilename) == query.HasImage.Value);
        }

        return Task.FromResult<IEnumerable<Partner>>(Sorted(results).ToList());
    }

    public Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var partner = Find(pkid);
        return Task.FromResult(partner is null ? null : Project(partner));
    }

    public Task<Partner> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        var partner = new Partner
        {
            Pkid = _nextPkid++,
            Name = request.Name.Trim(),
            AppKey = request.AppKey.Trim(),
            NameOnPartnerMenu = request.NameOnPartnerMenu.Trim(),
            NameOnCourseDetailPage = request.NameOnCourseDetailPage.Trim(),
            DisplayOrder = request.DisplayOrder,
            ImageFilename = Normalize(request.ImageFilename)
        };

        _partners.Add(partner);

        return Task.FromResult(Project(partner));
    }

    public Task<Partner?> UpdateAsync(PartnerUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var partner = Find(request.Pkid);

        if (partner is null) return Task.FromResult<Partner?>(null);

        partner.Name = request.Name.Trim();
        partner.AppKey = request.AppKey.Trim();
        partner.NameOnPartnerMenu = request.NameOnPartnerMenu.Trim();
        partner.NameOnCourseDetailPage = request.NameOnCourseDetailPage.Trim();
        partner.DisplayOrder = request.DisplayOrder;
        partner.ImageFilename = Normalize(request.ImageFilename);

        return Task.FromResult<Partner?>(Project(partner));
    }

    public Task<PartnerDeleteResult> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var partner = Find(pkid);

        if (partner is null) return Task.FromResult(PartnerDeleteResult.NotFound);

        if (_inUse.Contains(pkid)) return Task.FromResult(PartnerDeleteResult.InUse);

        _partners.Remove(partner);

        return Task.FromResult(PartnerDeleteResult.Deleted);
    }

    private Partner? Find(short pkid) => _partners.FirstOrDefault(p => p.Pkid == pkid);

    private static IEnumerable<Partner> Sorted(IEnumerable<Partner> partners) => partners
        .OrderBy(p => p.DisplayOrder)
        .ThenBy(p => p.Pkid)
        .Select(Project);

    /// <summary>A blank filename is stored as null, exactly as the repository writes it.</summary>
    private static string? Normalize(string? imageFilename)
        => string.IsNullOrWhiteSpace(imageFilename) ? null : imageFilename.Trim();

    /// <summary>Returns a detached copy, as the SQL projection does.</summary>
    private static Partner Project(Partner partner) => new()
    {
        Pkid = partner.Pkid,
        Name = partner.Name,
        AppKey = partner.AppKey,
        NameOnPartnerMenu = partner.NameOnPartnerMenu,
        NameOnCourseDetailPage = partner.NameOnCourseDetailPage,
        DisplayOrder = partner.DisplayOrder,
        ImageFilename = partner.ImageFilename
    };
}
