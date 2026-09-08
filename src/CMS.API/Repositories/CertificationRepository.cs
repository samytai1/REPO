using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class CertificationRepository : ICertificationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CertificationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<CertificationLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Title is nchar(100), so it always needs RTRIM.
        return await connection.QueryAsync<CertificationLookup>(new CommandDefinition(@"
SELECT  ct.pkid          AS Pkid,
        RTRIM(ct.Title)  AS Title,
        ct.Partner_pkid  AS PartnerPkid
FROM    dbo.Certification ct
ORDER BY RTRIM(ct.Title) ASC",
            cancellationToken: cancellationToken));
    }
}
