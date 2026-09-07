namespace CMS.API.Models;

/// <summary>
/// Search DTO for dbo.AppRole.
/// </summary>
public class AppRoleQuery
{
    /// <summary>LIKE across RoleId, RoleName, Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>權限等級 lower bound (inclusive).</summary>
    public int? PermissionLevelFrom { get; set; }

    /// <summary>權限等級 upper bound (inclusive).</summary>
    public int? PermissionLevelTo { get; set; }
}
