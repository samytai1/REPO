using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class Promotion2Repository : IPromotion2Repository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public Promotion2Repository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Promotion2Lookup>> SearchLookupAsync(
        string? keyword,
        int take,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Take", take);

        var where = string.Empty;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // Prefix match: codes are date-led (20260313_OpenShift), so typing a date narrows fast.
            where = "\nWHERE   p.PromoCode LIKE @Prefix";
            parameters.Add("Prefix", keyword.Trim() + "%");
        }

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<Promotion2Lookup>(new CommandDefinition(@"
SELECT  TOP (@Take)
        p.pkid         AS Pkid,
        p.PromoCode    AS PromoCode,
        p.Topic        AS Topic,
        p.Description  AS Description
FROM    dbo.Promotion2 p" + where + @"
ORDER BY p.PromoCode DESC",
            parameters,
            cancellationToken: cancellationToken));
    }
}
