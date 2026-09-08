using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 個人資料 — request body for <c>PUT /api/auth/profile</c>.
///
/// It carries <see cref="UserName"/> and nothing else **on purpose**: the account being edited is
/// the one in the bearer token, so a <c>userId</c> (or a role list) posted alongside has no property
/// to bind to and is silently dropped by the JSON deserializer. That is what stops a signed-in user
/// renaming somebody else.
/// </summary>
public class UpdateProfileRequest
{
    /// <summary>使用者名稱 — required, trimmed by the controller before it reaches SQL.</summary>
    [Required(ErrorMessage = "使用者名稱為必填。")]
    [MaxLength(200)]
    public string UserName { get; set; } = string.Empty;
}
