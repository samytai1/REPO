using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for dbo.AppRole (角色).</summary>
public interface IAppRoleRepository
{
    /// <summary>All roles, default sort RoleId ASC.</summary>
    Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Filtered search.</summary>
    Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default);

    /// <summary>Single role by RoleId, including its n-n user members. Null when not found.</summary>
    Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>True when a role with the given RoleId already exists.</summary>
    Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>Inserts the role and its AppUserRole rows; returns the created record.</summary>
    Task<AppRole> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates the role and rewrites its AppUserRole rows. Null when the role is missing.</summary>
    Task<AppRole?> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes the role and its AppUserRole rows. False when the role is missing.</summary>
    Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default);
}
