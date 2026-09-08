using CMS.API.Infrastructure;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class SysConfigRepository : ISysConfigRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SysConfigRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string?> GetValueAsync(string configKey, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ConfigKey", configKey);

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(@"
SELECT  c.configValue
FROM    dbo.SysConfig c
WHERE   c.configKey = @ConfigKey",
            parameters,
            cancellationToken: cancellationToken));
    }
}
