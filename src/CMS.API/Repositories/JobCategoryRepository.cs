using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class JobCategoryRepository : IJobCategoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public JobCategoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<JobCategoryLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<JobCategoryLookup>(new CommandDefinition(@"
SELECT  jc.pkid         AS Pkid,
        jc.Description  AS Description
FROM    dbo.JobCategory jc
ORDER BY jc.Description ASC",
            cancellationToken: cancellationToken));
    }
}
