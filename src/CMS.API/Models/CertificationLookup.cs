namespace CMS.API.Models;

/// <summary>
/// Slim lookup row for dbo.Certification (認證), used as the n-n option list on the Course form.
/// </summary>
public class CertificationLookup
{
    /// <summary>主代碼 — dbo.Certification.pkid (int IDENTITY).</summary>
    public int Pkid { get; set; }

    /// <summary>認證名稱 — dbo.Certification.Title. nchar(100) and nullable, so always RTRIM()ed in SQL.</summary>
    public string? Title { get; set; }

    /// <summary>原廠 — dbo.Certification.Partner_pkid.</summary>
    public short PartnerPkid { get; set; }
}
