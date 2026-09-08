namespace CMS.API.Models;

/// <summary>
/// 登入結果 — the signed-in user's profile plus the JWT access token.
/// Deliberately carries no password material.
/// </summary>
public class LoginResponse
{
    /// <summary>使用者代碼 — dbo.AppUser.UserId, as stored.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱 — dbo.AppUser.UserName.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>The signed JWT. Valid for 24 hours from issue.</summary>
    public string AccessToken { get; set; } = string.Empty;
}
