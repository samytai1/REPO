namespace CMS.API.Models;

/// <summary>
/// Slim lookup row for dbo.Promotion2 (促銷), used by the PromoCode autocomplete on the 上稿作業
/// form. Topic and Description ride along so the form can pre-fill its own copies of them.
/// Promotion2 is not a generated feature yet — lookup-only, the <see cref="AppUserLookup"/> pattern.
/// </summary>
public class Promotion2Lookup
{
    /// <summary>主代碼 — dbo.Promotion2.pkid (int IDENTITY).</summary>
    public int Pkid { get; set; }

    /// <summary>促銷代碼 — dbo.Promotion2.PromoCode (UNIQUE).</summary>
    public string PromoCode { get; set; } = string.Empty;

    /// <summary>主題 — dbo.Promotion2.Topic.</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明 — dbo.Promotion2.Description.</summary>
    public string Description { get; set; } = string.Empty;
}
