using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Lookup-only data access for dbo.AppUser (使用者).</summary>
public interface IAppUserRepository
{
    /// <summary>Slim user list for FK / n-n option lists, ordered by UserName ASC.</summary>
    Task<IEnumerable<AppUserLookup>> GetLookupAsync(CancellationToken cancellationToken = default);
}
