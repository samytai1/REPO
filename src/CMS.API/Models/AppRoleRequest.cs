using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.AppRole. RoleId is the key: supplied on create, immutable on update.
/// </summary>
public class AppRoleRequest
{
    /// <summary>角色代碼 — key. Required on both create and update.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>角色名稱.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>權限等級.</summary>
    [Range(0, int.MaxValue)]
    public int PermissionLevel { get; set; } = 100;

    /// <summary>描述.</summary>
    [StringLength(400)]
    public string? Description { get; set; }

    /// <summary>使用者 — n-n member ids for dbo.AppUserRole.</summary>
    public List<string> UserIds { get; set; } = new();
}
