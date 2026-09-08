using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class CourseGroupRepository : ICourseGroupRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CourseGroupRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<CourseGroupLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<CourseGroupLookup>(new CommandDefinition(@"
SELECT  cg.pkid         AS Pkid,
        cg.Description  AS Description
FROM    dbo.CourseGroup cg
ORDER BY cg.Description ASC",
            cancellationToken: cancellationToken));
    }
}
