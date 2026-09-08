using System.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class AppRoleRepository : IAppRoleRepository
{
    /// <summary>Table this repository owns, as it is written to dbo.RowAudit.TableName.</summary>
    private const string AuditTableName = "AppRole";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _rowAudit;

    /// <summary>Shared projection. UserCount is a subquery over the n-n junction table.</summary>
    private const string SelectSql = @"
SELECT  r.pkid              AS Pkid,
        r.RoleId            AS RoleId,
        r.RoleName          AS RoleName,
        r.PermissionLevel   AS PermissionLevel,
        r.Description       AS Description,
        (SELECT COUNT(1) FROM dbo.AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount
FROM    dbo.AppRole r";

    public AppRoleRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter rowAudit)
    {
        _connectionFactory = connectionFactory;
        _rowAudit = rowAudit;
    }

    public async Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<AppRole>(new CommandDefinition(
            SelectSql + "\nORDER BY r.RoleId ASC",
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("(r.RoleId LIKE @Keyword OR r.RoleName LIKE @Keyword OR r.Description LIKE @Keyword)");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        if (query.PermissionLevelFrom.HasValue)
        {
            where.Add("r.PermissionLevel >= @PermissionLevelFrom");
            parameters.Add("PermissionLevelFrom", query.PermissionLevelFrom.Value);
        }

        if (query.PermissionLevelTo.HasValue)
        {
            where.Add("r.PermissionLevel <= @PermissionLevelTo");
            parameters.Add("PermissionLevelTo", query.PermissionLevelTo.Value);
        }

        var sql = SelectSql
            + (where.Count > 0 ? "\nWHERE " + string.Join(" AND ", where) : string.Empty)
            + "\nORDER BY r.RoleId ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<AppRole>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, null, roleId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<AppRole> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.AppRole (RoleId, RoleName, PermissionLevel, Description)
VALUES (@RoleId, @RoleName, @PermissionLevel, @Description)",
            new
            {
                request.RoleId,
                request.RoleName,
                request.PermissionLevel,
                request.Description
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceUsersAsync(connection, transaction, request, cancellationToken);

        var created = await GetByIdAsync(connection, transaction, request.RoleId, cancellationToken);

        // Inside the transaction: a failed INSERT leaves no row and no audit row.
        await _rowAudit.LogInsertAsync(connection, transaction, AuditTableName, created!, cancellationToken);

        transaction.Commit();

        return created!;
    }

    public async Task<AppRole?> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Read first, and read the members with it: rewriting dbo.AppUserRole is the most
        // consequential thing this endpoint does — it can grant Admin — so `Users` has to be part of
        // the before-picture or the trail would show a role change as touching nothing.
        var before = await GetByIdAsync(connection, transaction, request.RoleId, cancellationToken);

        if (before is null)
        {
            transaction.Rollback();
            return null;
        }

        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE  dbo.AppRole
SET     RoleName        = @RoleName,
        PermissionLevel = @PermissionLevel,
        Description     = @Description
WHERE   RoleId = @RoleId",
            new
            {
                request.RoleId,
                request.RoleName,
                request.PermissionLevel,
                request.Description
            },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return null;
        }

        await ReplaceUsersAsync(connection, transaction, request, cancellationToken);

        var updated = await GetByIdAsync(connection, transaction, request.RoleId, cancellationToken);

        await _rowAudit.LogUpdateAsync(connection, transaction, AuditTableName, before, updated!, cancellationToken);

        transaction.Commit();

        return updated;
    }

    public async Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Read before deleting; once the row is gone its RoleId is the only trace the trail keeps.
        var deleting = await GetByIdAsync(connection, transaction, roleId, cancellationToken);

        if (deleting is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        await _rowAudit.LogDeleteAsync(connection, transaction, AuditTableName, deleting, cancellationToken);

        transaction.Commit();

        return true;
    }

    /// <summary>Reads the role plus its n-n members on an existing connection.</summary>
    private static async Task<AppRole?> GetByIdAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        string roleId,
        CancellationToken cancellationToken)
    {
        var role = await connection.QuerySingleOrDefaultAsync<AppRole>(new CommandDefinition(
            SelectSql + "\nWHERE r.RoleId = @RoleId",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        if (role is null) return null;

        var users = await connection.QueryAsync<AppUserLookup>(new CommandDefinition(@"
SELECT  u.UserId    AS UserId,
        u.UserName  AS UserName,
        u.IsActive  AS IsActive
FROM    dbo.AppUserRole ur
        INNER JOIN dbo.AppUser u ON u.UserId = ur.UserId
WHERE   ur.RoleId = @RoleId
ORDER BY u.UserName ASC",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        role.Users = users.ToList();

        return role;
    }

    /// <summary>n-n: delete-then-reinsert the AppUserRole rows for this role.</summary>
    private static async Task ReplaceUsersAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        AppRoleRequest request,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.AppUserRole WHERE RoleId = @RoleId",
            new { request.RoleId },
            transaction,
            cancellationToken: cancellationToken));

        var userIds = request.UserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (userIds.Count == 0) return;

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)",
            userIds.Select(userId => new { UserId = userId, request.RoleId }),
            transaction,
            cancellationToken: cancellationToken));
    }
}
