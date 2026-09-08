using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 登入 — request body for <c>POST /api/auth/login</c>.
/// </summary>
public class LoginRequest
{
    /// <summary>使用者代碼 — matched against dbo.AppUser.UserId.</summary>
    [Required]
    [StringLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>密碼 — plain text; hashed with SHA-256 before it is compared.</summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}
