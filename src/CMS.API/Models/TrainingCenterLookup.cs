namespace CMS.API.Models;

/// <summary>
/// Slim lookup row for dbo.TrainingCenter (教育中心), used as the tab strip on the 上稿作業 grid.
/// TrainingCenter is not a generated feature yet, so this DTO and its lookup-only repository exist
/// purely to feed that control — the <see cref="AppUserLookup"/> pattern.
/// </summary>
public class TrainingCenterLookup
{
    /// <summary>主代碼 — dbo.TrainingCenter.pkid (smallint IDENTITY).</summary>
    public short Pkid { get; set; }

    /// <summary>名稱 — dbo.TrainingCenter.Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>顯示順序 — dbo.TrainingCenter.DisplayOrder.</summary>
    public int DisplayOrder { get; set; }
}
