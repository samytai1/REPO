using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="ISysConfigRepository"/>: configKey is the primary key and a
/// missing key reads back as <c>null</c>, exactly as the SQL <c>QuerySingleOrDefaultAsync</c> does.
/// </summary>
public class InMemorySysConfigRepository : ISysConfigRepository
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public InMemorySysConfigRepository Seed(string configKey, string configValue)
    {
        _values[configKey] = configValue;
        return this;
    }

    public Task<string?> GetValueAsync(string configKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.TryGetValue(configKey, out var value) ? value : null);
}
