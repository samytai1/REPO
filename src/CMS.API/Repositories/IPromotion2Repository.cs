using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Lookup-only data access for dbo.Promotion2 (促銷). Feeds the PromoCode autocomplete on the
/// 上稿作業 form; when Promotion2 gets its own /crud, repoint the lookup endpoint and delete this.
/// </summary>
public interface IPromotion2Repository
{
    /// <summary>
    /// Promotions whose PromoCode starts with <paramref name="keyword"/> (all promotions when blank),
    /// newest code first, at most <paramref name="take"/> rows.
    /// </summary>
    Task<IEnumerable<Promotion2Lookup>> SearchLookupAsync(
        string? keyword,
        int take,
        CancellationToken cancellationToken = default);
}
