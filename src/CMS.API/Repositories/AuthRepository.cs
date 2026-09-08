using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AppUserCredential?> GetCredentialAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId.Trim());

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<AppUserCredential>(new CommandDefinition(@"
SELECT  u.UserId         AS UserId,
        u.UserName       AS UserName,
        u.IsActive       AS IsActive,
        u.PasswordHash   AS PasswordHash
FROM    dbo.AppUser u
WHERE   u.UserId = @UserId",
            parameters,
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<string>> GetRoleIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Array.Empty<string>();

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId.Trim());

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<string>(new CommandDefinition(@"
SELECT  ur.RoleId
FROM    dbo.AppUserRole ur
WHERE   ur.UserId = @UserId
ORDER BY ur.RoleId ASC",
            parameters,
            cancellationToken: cancellationToken));
    }

    public async Task<UserProfileResponse?> UpdateUserNameAsync(
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId.Trim());
        parameters.Add("UserName", userName.Trim());

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Only UserName is in the SET list: the key, IsActive and PasswordHash stay as they are, and
        // dbo.AppUserRole is not touched at all, so roles cannot be changed through this path.
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE  dbo.AppUser
SET     UserName = @UserName
WHERE   UserId = @UserId",
            parameters,
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return null;
        }

        // Re-read inside the transaction, so the caller sees exactly what was written.
        var updated = await connection.QuerySingleOrDefaultAsync<UserProfileResponse>(new CommandDefinition(@"
SELECT  u.UserId   AS UserId,
        u.UserName AS UserName
FROM    dbo.AppUser u
WHERE   u.UserId = @UserId",
            parameters,
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();

        return updated;
    }

    public async Task<bool> UpdatePasswordAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId.Trim());
        parameters.Add("PasswordHash", passwordHash);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // One statement, so it is atomic on its own and there is nothing to re-read — a password is
        // never returned. PasswordUpdatedTime is stamped here rather than passed in, so the row
        // records when the database actually changed.
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE  dbo.AppUser
SET     PasswordHash        = @PasswordHash,
        PasswordUpdatedTime = SYSDATETIME()
WHERE   UserId = @UserId",
            parameters,
            cancellationToken: cancellationToken));

        return affected > 0;
    }
}
