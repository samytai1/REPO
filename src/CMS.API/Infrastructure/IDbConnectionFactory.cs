using System.Data;

namespace CMS.API.Infrastructure;

/// <summary>
/// Creates open connections to the CMS database. Repositories own the connection lifetime.
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
