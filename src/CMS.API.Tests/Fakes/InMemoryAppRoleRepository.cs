using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IAppRoleRepository"/> that mirrors the SQL semantics of
/// <c>AppRoleRepository</c>: RoleId is the key, results sort by RoleId ASC, the keyword filter
/// spans RoleId/RoleName/Description, and the n-n user set is replaced wholesale on write.
/// </summary>
public class InMemoryAppRoleRepository : IAppRoleRepository
{
    private readonly List<AppRole> _roles = new();
    private readonly Dictionary<string, List<string>> _userRoles = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, AppUserLookup> _users;
    private int _nextPkid = 1;

    public InMemoryAppRoleRepository(IEnumerable<AppUserLookup>? users = null)
    {
        _users = (users ?? Enumerable.Empty<AppUserLookup>())
            .ToDictionary(u => u.UserId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Seeds a role directly, bypassing the write path.</summary>
    public InMemoryAppRoleRepository Seed(string roleId, string roleName, int permissionLevel, string? description, params string[] userIds)
    {
        _roles.Add(new AppRole
        {
            Pkid = _nextPkid++,
            RoleId = roleId,
            RoleName = roleName,
            PermissionLevel = permissionLevel,
            Description = description
        });

        _userRoles[roleId] = userIds.ToList();

        return this;
    }

    public Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<AppRole>>(_roles
            .OrderBy(r => r.RoleId, StringComparer.Ordinal)
            .Select(Project)
            .ToList());

    public Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<AppRole> results = _roles;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(r =>
                r.RoleId.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || r.RoleName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (r.Description ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.PermissionLevelFrom.HasValue)
        {
            results = results.Where(r => r.PermissionLevel >= query.PermissionLevelFrom.Value);
        }

        if (query.PermissionLevelTo.HasValue)
        {
            results = results.Where(r => r.PermissionLevel <= query.PermissionLevelTo.Value);
        }

        return Task.FromResult<IEnumerable<AppRole>>(results
            .OrderBy(r => r.RoleId, StringComparer.Ordinal)
            .Select(Project)
            .ToList());
    }

    public Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var role = Find(roleId);
        return Task.FromResult(role is null ? null : Project(role));
    }

    public Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default)
        => Task.FromResult(Find(roleId) is not null);

    public Task<AppRole> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = new AppRole
        {
            Pkid = _nextPkid++,
            RoleId = request.RoleId,
            RoleName = request.RoleName,
            PermissionLevel = request.PermissionLevel,
            Description = request.Description
        };

        _roles.Add(role);
        ReplaceUsers(request);

        return Task.FromResult(Project(role));
    }

    public Task<AppRole?> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = Find(request.RoleId);

        if (role is null) return Task.FromResult<AppRole?>(null);

        role.RoleName = request.RoleName;
        role.PermissionLevel = request.PermissionLevel;
        role.Description = request.Description;

        ReplaceUsers(request);

        return Task.FromResult<AppRole?>(Project(role));
    }

    public Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var role = Find(roleId);

        if (role is null) return Task.FromResult(false);

        _roles.Remove(role);
        _userRoles.Remove(roleId);

        return Task.FromResult(true);
    }

    /// <summary>The user ids currently assigned to a role — lets tests assert the n-n write.</summary>
    public IReadOnlyList<string> AssignedUserIds(string roleId)
        => _userRoles.TryGetValue(roleId, out var ids) ? ids : Array.Empty<string>();

    private AppRole? Find(string roleId)
        => _roles.FirstOrDefault(r => string.Equals(r.RoleId, roleId, StringComparison.OrdinalIgnoreCase));

    private void ReplaceUsers(AppRoleRequest request)
    {
        _userRoles[request.RoleId] = request.UserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Returns a detached copy with UserCount and Users filled in, as the SQL projection does.</summary>
    private AppRole Project(AppRole role)
    {
        var userIds = AssignedUserIds(role.RoleId);

        return new AppRole
        {
            Pkid = role.Pkid,
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            PermissionLevel = role.PermissionLevel,
            Description = role.Description,
            UserCount = userIds.Count,
            Users = userIds
                .Select(id => _users.TryGetValue(id, out var user)
                    ? user
                    : new AppUserLookup { UserId = id, UserName = id, IsActive = true })
                .OrderBy(u => u.UserName, StringComparer.Ordinal)
                .ToList()
        };
    }
}
