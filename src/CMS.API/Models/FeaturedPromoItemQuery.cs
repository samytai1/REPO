namespace CMS.API.Models;

/// <summary>
/// Search DTO for dbo.FeaturedPromoItem. The grid always shows one training center for one
/// Monday-to-Sunday week, so those are the only two filters.
/// </summary>
public class FeaturedPromoItemQuery
{
    /// <summary>教育中心 — exact match on TrainingCenter_pkid (the active tab).</summary>
    public short? TrainingCenterPkid { get; set; }

    /// <summary>
    /// Any date inside the week to show. The repository snaps it to that week's Monday and filters
    /// ScheduleOn from Monday to Sunday inclusive — a Wednesday and its Monday return the same rows.
    /// </summary>
    public DateOnly? WeekOf { get; set; }
}
