using System.Text.Json;
using CMS.API.Repositories;

namespace CMS.API.Infrastructure;

/// <inheritdoc />
public class DefaultPasswordService : IDefaultPasswordService
{
    /// <summary>Property inside the appConfig JSON holding the shared default password.</summary>
    public const string DefaultPasswordProperty = "defaultPassword";

    private readonly ISysConfigRepository _sysConfigRepository;

    public DefaultPasswordService(ISysConfigRepository sysConfigRepository)
    {
        _sysConfigRepository = sysConfigRepository;
    }

    /// <summary>
    /// Reads <c>defaultPassword</c> from dbo.SysConfig on every call, so rotating it needs no
    /// restart — the same rule the signing key and the password policy follow. The value takes
    /// effect on the **next login**, because that is the only place the flag is decided.
    ///
    /// **Fails open**, and this is the one place in the auth code that does. A missing row,
    /// unreadable JSON, an absent property or a blank value all mean "nobody is forced": failing
    /// closed here would flag every account at once and answer 403 to the entire API. Note the
    /// deliberate asymmetry with <see cref="PasswordPolicyService"/>, which reads the same JSON row
    /// and fails **closed** — there, a configuration mistake must not switch a protection off; here,
    /// it must not switch a lockout on.
    /// </summary>
    public async Task<bool> IsDefaultAsync(string? passwordHash, CancellationToken cancellationToken = default)
    {
        var defaultPassword = await ReadDefaultPasswordAsync(cancellationToken);

        // The blank check must come first: Hash("") is a real, computable value, and treating a
        // blank configuration as a password would make it a target.
        if (string.IsNullOrWhiteSpace(defaultPassword)) return false;

        return PasswordHasher.Matches(defaultPassword, passwordHash);
    }

    /// <summary>The configured default, or <c>null</c> for every unreadable shape. See the fail-open note above.</summary>
    private async Task<string?> ReadDefaultPasswordAsync(CancellationToken cancellationToken)
    {
        var configValue = await _sysConfigRepository.GetValueAsync(JwtTokenService.AppConfigKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(configValue)) return null;

        try
        {
            using var document = JsonDocument.Parse(configValue);

            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty(DefaultPasswordProperty, out var property)) return null;

            return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
