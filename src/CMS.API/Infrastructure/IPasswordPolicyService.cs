namespace CMS.API.Infrastructure;

/// <summary>
/// 密碼原則 — decides whether a proposed password is strong enough, per the
/// <c>enforcePasswordPolicy</c> flag in dbo.SysConfig['appConfig'].
/// </summary>
public interface IPasswordPolicyService
{
    /// <summary>
    /// <c>null</c> when <paramref name="password"/> is acceptable, otherwise the Chinese message
    /// explaining what it failed — the text the API returns and the browser shows verbatim.
    /// </summary>
    Task<string?> ValidateAsync(string password, CancellationToken cancellationToken = default);
}
