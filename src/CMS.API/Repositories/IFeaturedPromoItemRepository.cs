using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for dbo.FeaturedPromoItem (上稿作業).</summary>
public interface IFeaturedPromoItemRepository
{
    /// <summary>All items, ScheduleOn ASC, TrainingCenter_pkid ASC, Slot ASC. Tens of thousands of rows — the grid never calls this.</summary>
    Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>One training center's items for the Monday-to-Sunday week containing <see cref="FeaturedPromoItemQuery.WeekOf"/>.</summary>
    Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken cancellationToken = default);

    /// <summary>Single item by pkid. Null when not found.</summary>
    Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another row already occupies (scheduleOn, trainingCenterPkid, slot) — the UNIQUE
    /// key. <paramref name="excludePkid"/> lets an update ignore its own row.
    /// </summary>
    Task<bool> IsSlotTakenAsync(
        DateOnly scheduleOn,
        short trainingCenterPkid,
        byte slot,
        int? excludePkid,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts the item; returns the created record with its assigned pkid.</summary>
    Task<FeaturedPromoItem> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates the item. Null when the row is missing.</summary>
    Task<FeaturedPromoItem?> UpdateAsync(FeaturedPromoItemUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes the item. False when the row is missing. Nothing FKs into this table.</summary>
    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the item into <paramref name="targetSlot"/> on the same day and training center. If
    /// that slot is occupied the two rows swap. Returns the moved item, or null when it is missing.
    /// </summary>
    Task<FeaturedPromoItem?> MoveSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default);
}
