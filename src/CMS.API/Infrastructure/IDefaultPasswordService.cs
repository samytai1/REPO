namespace CMS.API.Infrastructure;

/// <summary>
/// 預設密碼 — decides whether an account is still sitting on the shared default password held in
/// <c>defaultPassword</c> inside dbo.SysConfig['appConfig'].
///
/// A <c>true</c> answer at login marks the issued token, and the token's
/// <see cref="JwtTokenService.MustChangePasswordClaimType"/> claim makes every endpoint but
/// <c>PUT /api/auth/password</c> answer 403 until the user picks a new one.
/// </summary>
public interface IDefaultPasswordService
{
    /// <summary>
    /// True when <paramref name="passwordHash"/> is the hash of the configured default password.
    ///
    /// The **stored hash** is compared rather than the submitted password, which is what makes this
    /// correct for free: login has already proved the two match, and
    /// <see cref="PasswordHasher.Matches"/> is hex-case-insensitive and fixed-time.
    /// </summary>
    Task<bool> IsDefaultAsync(string? passwordHash, CancellationToken cancellationToken = default);
}
