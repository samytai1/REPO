using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IPromotion2Repository"/>. Mirrors the SQL search: a
/// case-insensitive PromoCode prefix match (the database collation is case-insensitive), newest
/// code first, capped at <c>take</c> rows.
/// </summary>
public class InMemoryPromotion2Repository : IPromotion2Repository
{
    private readonly List<Promotion2Lookup> _promotions;

    public InMemoryPromotion2Repository(params Promotion2Lookup[] promotions)
    {
        _promotions = promotions.ToList();
    }

    /// <summary>Seeds a promotion directly.</summary>
    public InMemoryPromotion2Repository Seed(int pkid, string promoCode, string topic = "", string description = "")
    {
        _promotions.Add(new Promotion2Lookup
        {
            Pkid = pkid,
            PromoCode = promoCode,
            Topic = topic,
            Description = description
        });
        return this;
    }

    public Task<IEnumerable<Promotion2Lookup>> SearchLookupAsync(
        string? keyword,
        int take,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Promotion2Lookup> results = _promotions;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var prefix = keyword.Trim();
            results = results.Where(p => p.PromoCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IEnumerable<Promotion2Lookup>>(
            results.OrderByDescending(p => p.PromoCode, StringComparer.OrdinalIgnoreCase).Take(take).ToList());
    }
}
