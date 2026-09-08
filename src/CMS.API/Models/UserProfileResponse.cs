namespace CMS.API.Models;

/// <summary>
/// 個人資料 — the wire shape of <c>PUT /api/auth/profile</c>.
///
/// Like <see cref="LoginResponse"/> it is a deliberately narrow projection of dbo.AppUser:
/// PasswordHash has no property here and so can never reach a response. There is no
/// <c>roles</c> field either — the roles ride in the access token's <c>role</c> claims, and the
/// browser reads them from there rather than from a second API call.
/// </summary>
public class UserProfileResponse
{
    /// <summary>dbo.AppUser.UserId — read-only, always the account the token names.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>dbo.AppUser.UserName as it now stands in the database.</summary>
    public string UserName { get; set; } = string.Empty;
}
