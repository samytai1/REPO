using System.Text.Json;
using CMS.API.Repositories;

namespace CMS.API.Infrastructure;

/// <inheritdoc />
public class PasswordPolicyService : IPasswordPolicyService
{
    /// <summary>Property inside the appConfig JSON that switches the strength rules on.</summary>
    public const string EnforceProperty = "enforcePasswordPolicy";

    /// <summary>Shortest password the policy accepts when it is enforced.</summary>
    public const int MinimumLength = 8;

    /// <summary>The one message a policy failure produces — it names every rule, not the first to fail.</summary>
    public const string PolicyMessage =
        "新密碼至少 8 個字元，且需包含大寫字母、小寫字母、數字與符號。";

    /// <summary>Used when the policy is switched off and the password is still blank.</summary>
    public const string RequiredMessage = "新密碼為必填。";

    private readonly ISysConfigRepository _sysConfigRepository;

    public PasswordPolicyService(ISysConfigRepository sysConfigRepository)
    {
        _sysConfigRepository = sysConfigRepository;
    }

    public async Task<string?> ValidateAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password)) return RequiredMessage;

        var enforced = await IsEnforcedAsync(cancellationToken);

        if (!enforced) return null;

        return Satisfies(password) ? null : PolicyMessage;
    }

    /// <summary>
    /// The <c>enforcePasswordPolicy</c> flag, read from dbo.SysConfig on every call so flipping it
    /// needs no restart — the same rule the signing key follows.
    ///
    /// **Fails closed.** A missing row, unreadable JSON or an absent property all mean "enforced":
    /// a configuration mistake must not quietly turn the password rules off. Note the deliberate
    /// asymmetry with <see cref="DefaultPasswordService"/>, which reads the same JSON row and fails
    /// **open** — a mistake there must not lock every account out of the API.
    /// </summary>
    private async Task<bool> IsEnforcedAsync(CancellationToken cancellationToken)
    {
        var configValue = await _sysConfigRepository.GetValueAsync(JwtTokenService.AppConfigKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(configValue)) return true;

        try
        {
            using var document = JsonDocument.Parse(configValue);

            if (document.RootElement.ValueKind != JsonValueKind.Object) return true;
            if (!document.RootElement.TryGetProperty(EnforceProperty, out var property)) return true;

            return property.ValueKind switch
            {
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                _ => true
            };
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>Length plus all four character classes. Anything not a letter or digit is a symbol.</summary>
    private static bool Satisfies(string password)
        => password.Length >= MinimumLength
           && password.Any(char.IsUpper)
           && password.Any(char.IsLower)
           && password.Any(char.IsDigit)
           && password.Any(c => !char.IsLetterOrDigit(c));
}
