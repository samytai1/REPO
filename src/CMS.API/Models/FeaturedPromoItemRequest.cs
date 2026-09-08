using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.FeaturedPromoItem. pkid is an IDENTITY column, so it is absent here and
/// carried by <see cref="FeaturedPromoItemUpdateRequest"/> instead.
/// </summary>
public class FeaturedPromoItemRequest
{
    /// <summary>上稿日期.</summary>
    [Required]
    public DateOnly ScheduleOn { get; set; }

    /// <summary>教育中心 — dbo.TrainingCenter.pkid.</summary>
    [Range(1, short.MaxValue)]
    public short TrainingCenterPkid { get; set; }

    /// <summary>版位 — 1, 2 or 3.</summary>
    [Range(FeaturedPromoItemSlots.Min, FeaturedPromoItemSlots.Max)]
    public byte Slot { get; set; }

    /// <summary>促銷 — dbo.Promotion2.pkid, resolved on the form by PromoCode.</summary>
    [Range(1, int.MaxValue)]
    public int PromotionPkid { get; set; }

    /// <summary>主題.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(300)]
    public string Description { get; set; } = string.Empty;
}

/// <summary>PUT body — the same fields plus the key, which the collection route needs.</summary>
public class FeaturedPromoItemUpdateRequest : FeaturedPromoItemRequest
{
    /// <summary>主代碼 — dbo.FeaturedPromoItem.pkid.</summary>
    [Range(1, int.MaxValue)]
    public int Pkid { get; set; }
}

/// <summary>Body of <c>POST /api/featured-promo-items/{id}/move-slot</c> — the slot to move into.</summary>
public class FeaturedPromoItemMoveRequest
{
    /// <summary>目標版位 — 1, 2 or 3. An occupant of that slot swaps into the vacated one.</summary>
    [Range(FeaturedPromoItemSlots.Min, FeaturedPromoItemSlots.Max)]
    public byte TargetSlot { get; set; }
}

/// <summary>The fixed slot range of the 上稿作業 grid.</summary>
public static class FeaturedPromoItemSlots
{
    public const byte Min = 1;
    public const byte Max = 3;

    /// <summary>Slots in display order — 1, 2, 3.</summary>
    public static readonly byte[] All = { 1, 2, 3 };
}
