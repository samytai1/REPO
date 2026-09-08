namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.FeaturedPromoItem (上稿作業) — one promotion pinned to a slot on a day for
/// one training center. (ScheduleOn, TrainingCenter_pkid, Slot) is UNIQUE.
/// </summary>
public class FeaturedPromoItem
{
    /// <summary>主代碼 — dbo.FeaturedPromoItem.pkid (int IDENTITY, assigned by the database).</summary>
    public int Pkid { get; set; }

    /// <summary>上稿日期 — dbo.FeaturedPromoItem.ScheduleOn.</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>教育中心 — dbo.FeaturedPromoItem.TrainingCenter_pkid.</summary>
    public short TrainingCenterPkid { get; set; }

    /// <summary>版位 — dbo.FeaturedPromoItem.Slot (1–3).</summary>
    public byte Slot { get; set; }

    /// <summary>促銷 — dbo.FeaturedPromoItem.Promotion_pkid (→ dbo.Promotion2).</summary>
    public int PromotionPkid { get; set; }

    /// <summary>主題 — dbo.FeaturedPromoItem.Topic.</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明 — dbo.FeaturedPromoItem.Description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>促銷 — FK nav object, JOINed from dbo.Promotion2 (the PromoCode the grid shows).</summary>
    public FeaturedPromoItemPromotionRef? Promotion { get; set; }

    /// <summary>教育中心 — FK nav object, JOINed from dbo.TrainingCenter.</summary>
    public FeaturedPromoItemTrainingCenterRef? TrainingCenter { get; set; }
}

/// <summary>Slim FK reference to dbo.Promotion2 — the 促銷代碼 label column.</summary>
public class FeaturedPromoItemPromotionRef
{
    /// <summary>主代碼 — dbo.Promotion2.pkid.</summary>
    public int Pkid { get; set; }

    /// <summary>促銷代碼 — dbo.Promotion2.PromoCode.</summary>
    public string PromoCode { get; set; } = string.Empty;
}

/// <summary>Slim FK reference to dbo.TrainingCenter — the tab label.</summary>
public class FeaturedPromoItemTrainingCenterRef
{
    /// <summary>主代碼 — dbo.TrainingCenter.pkid.</summary>
    public short Pkid { get; set; }

    /// <summary>名稱 — dbo.TrainingCenter.Name.</summary>
    public string Name { get; set; } = string.Empty;
}
