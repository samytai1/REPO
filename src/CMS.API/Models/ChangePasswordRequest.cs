using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 變更密碼 — request body for <c>PUT /api/auth/password</c>.
///
/// Like <see cref="UpdateProfileRequest"/> it names no account: the row that changes is the one the
/// bearer token identifies. There is no confirm-password property either — re-typing the new
/// password is a UI check, and sending it twice would only put the secret on the wire twice.
///
/// Neither value is trimmed anywhere. A password is used exactly as typed, or a legitimate one that
/// begins or ends with a space could never be entered again.
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>目前密碼 — checked against dbo.AppUser.PasswordHash before anything is written.</summary>
    [Required(ErrorMessage = "目前密碼為必填。")]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>新密碼 — must satisfy <c>IPasswordPolicyService</c>.</summary>
    [Required(ErrorMessage = "新密碼為必填。")]
    public string NewPassword { get; set; } = string.Empty;
}
