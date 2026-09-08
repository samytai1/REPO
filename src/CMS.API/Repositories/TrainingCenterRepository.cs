using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class TrainingCenterRepository : ITrainingCenterRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TrainingCenterRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<TrainingCenterLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<TrainingCenterLookup>(new CommandDefinition(@"
SELECT  tc.pkid          AS Pkid,
        tc.Name          AS Name,
        tc.DisplayOrder  AS DisplayOrder
FROM    dbo.TrainingCenter tc
ORDER BY tc.DisplayOrder ASC, tc.pkid ASC",
            cancellationToken: cancellationToken));
    }
}
