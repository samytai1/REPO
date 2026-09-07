using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>In-memory stand-in for <see cref="IAppUserRepository"/>.</summary>
public class InMemoryAppUserRepository : IAppUserRepository
{
    private readonly List<AppUserLookup> _users;

    public InMemoryAppUserRepository(params AppUserLookup[] users)
    {
        _users = users.ToList();
    }

    public Task<IEnumerable<AppUserLookup>> GetLookupAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<AppUserLookup>>(_users
            .OrderBy(u => u.UserName, StringComparer.Ordinal)
            .ToList());
}
