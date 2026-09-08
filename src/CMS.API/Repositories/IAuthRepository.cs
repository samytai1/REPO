using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Data access for 登入 Auth over dbo.AppUser / dbo.AppUserRole: the credential read the login
/// endpoint needs, and the one column a signed-in user may write about themselves.
/// </summary>
public interface IAuthRepository
{
    /// <summary>
    /// The credential row for <paramref name="userId"/>, or <c>null</c> when no such user exists.
    /// Returns inactive users too — the caller rejects them with the same generic 401.
    /// </summary>
    Task<AppUserCredential?> GetCredentialAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Every RoleId assigned to the user in dbo.AppUserRole, ordered by RoleId ASC.</summary>
    Task<IEnumerable<string>> GetRoleIdsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes dbo.AppUser.UserName for <paramref name="userId"/> and returns the row as it now
    /// stands, or <c>null</c> when no such user exists.
    ///
    /// UserName is the **only** column this touches: the key, the roles, IsActive and PasswordHash
    /// are all out of reach, so 個人資料 cannot become a privilege-escalation path.
    /// </summary>
    Task<UserProfileResponse?> UpdateUserNameAsync(
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes dbo.AppUser.PasswordHash for <paramref name="userId"/> and stamps
    /// <c>PasswordUpdatedTime</c>. <c>false</c> when no such user exists.
    ///
    /// The caller supplies an already-hashed value: a plain password never reaches the repository,
    /// so it can never reach a SQL parameter, a log or a profiler trace.
    /// </summary>
    Task<bool> UpdatePasswordAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken = default);
}
