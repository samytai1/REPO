namespace CMS.API.Models;

/// <summary>
/// Slim lookup row for dbo.AppUser, used as the n-n option list on the AppRole form.
/// </summary>
public class AppUserLookup
{
    /// <summary>使用者代碼 — dbo.AppUser.UserId (primary key).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱 — dbo.AppUser.UserName.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用 — dbo.AppUser.IsActive.</summary>
    public bool IsActive { get; set; }
}
