namespace CMS.API.Models;

/// <summary>
/// Internal credential row read from dbo.AppUser during login. Never returned from a controller —
/// <see cref="LoginResponse"/> is the wire shape, and it has no <c>PasswordHash</c>.
/// </summary>
public class AppUserCredential
{
    /// <summary>使用者代碼 — dbo.AppUser.UserId (primary key).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱 — dbo.AppUser.UserName.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用 — dbo.AppUser.IsActive. An inactive user may not sign in.</summary>
    public bool IsActive { get; set; }

    /// <summary>Lowercase hex SHA-256 of the password — dbo.AppUser.PasswordHash.</summary>
    public string PasswordHash { get; set; } = string.Empty;
}
