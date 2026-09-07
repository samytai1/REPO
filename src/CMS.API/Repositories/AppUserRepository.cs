using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class AppUserRepository : IAppUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AppUserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<AppUserLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<AppUserLookup>(new CommandDefinition(@"
SELECT  u.UserId    AS UserId,
        u.UserName  AS UserName,
        u.IsActive  AS IsActive
FROM    dbo.AppUser u
ORDER BY u.UserName ASC",
            cancellationToken: cancellationToken));
    }
}
