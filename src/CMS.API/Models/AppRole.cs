namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.AppRole (角色).
/// </summary>
public class AppRole
{
    /// <summary>主代碼 — dbo.AppRole.pkid (IDENTITY, surrogate).</summary>
    public int Pkid { get; set; }

    /// <summary>角色代碼 — dbo.AppRole.RoleId (business primary key).</summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>角色名稱 — dbo.AppRole.RoleName.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>權限等級 — dbo.AppRole.PermissionLevel (lower = higher privilege).</summary>
    public int PermissionLevel { get; set; }

    /// <summary>描述 — dbo.AppRole.Description.</summary>
    public string? Description { get; set; }

    /// <summary>使用者數 — subquery count over dbo.AppUserRole.</summary>
    public int UserCount { get; set; }

    /// <summary>使用者 — n-n members from dbo.AppUserRole (detail reads only).</summary>
    public List<AppUserLookup> Users { get; set; } = new();
}
