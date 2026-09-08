using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="ICertificationRepository"/>. Like the SQL lookup it sorts by
/// Title ASC; Title is nchar in the database, so the seeded values are already RTRIMmed.
/// </summary>
public class InMemoryCertificationRepository : ICertificationRepository
{
    private readonly List<CertificationLookup> _certifications;

    public InMemoryCertificationRepository(params CertificationLookup[] certifications)
    {
        _certifications = certifications.ToList();
    }

    /// <summary>Seeds a certification directly.</summary>
    public InMemoryCertificationRepository Seed(int pkid, string? title, short partnerPkid)
    {
        _certifications.Add(new CertificationLookup { Pkid = pkid, Title = title, PartnerPkid = partnerPkid });
        return this;
    }

    public Task<IEnumerable<CertificationLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<CertificationLookup>>(
            _certifications.OrderBy(ct => ct.Title, StringComparer.Ordinal).ToList());
}
