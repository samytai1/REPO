using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IAuthRepository"/> mirroring the SQL semantics of
/// <c>AuthRepository</c>: UserId is the key and is matched case-insensitively (the dbo.AppUser
/// primary key uses the database's case-insensitive collation), the credential row is returned
/// for inactive users too, and role ids come back sorted by RoleId ASC.
/// </summary>
public class InMemoryAuthRepository : IAuthRepository
{
    private readonly Dictionary<string, AppUserCredential> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _userRoles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _passwordUpdatedTimes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Seeds a user whose PasswordHash is the SHA-256 of <paramref name="password"/>.</summary>
    public InMemoryAuthRepository Seed(string userId, string userName, string password, bool isActive = true, params string[] roleIds)
        => SeedHashed(userId, userName, PasswordHasher.Hash(password), isActive, roleIds);

    /// <summary>Seeds a user with a PasswordHash written verbatim — for rows the hasher would never produce.</summary>
    public InMemoryAuthRepository SeedHashed(string userId, string userName, string passwordHash, bool isActive = true, params string[] roleIds)
    {
        _users[userId] = new AppUserCredential
        {
            UserId = userId,
            UserName = userName,
            IsActive = isActive,
            PasswordHash = passwordHash
        };

        _userRoles[userId] = roleIds.ToList();

        return this;
    }

    public Task<AppUserCredential?> GetCredentialAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult<AppUserCredential?>(null);

        return Task.FromResult(_users.TryGetValue(userId.Trim(), out var user)
            ? new AppUserCredential
            {
                UserId = user.UserId,
                UserName = user.UserName,
                IsActive = user.IsActive,
                PasswordHash = user.PasswordHash
            }
            : null);
    }

    public Task<IEnumerable<string>> GetRoleIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult(Enumerable.Empty<string>());

        var roles = _userRoles.TryGetValue(userId.Trim(), out var assigned)
            ? assigned.OrderBy(r => r, StringComparer.Ordinal).ToList()
            : new List<string>();

        return Task.FromResult<IEnumerable<string>>(roles);
    }

    public Task<UserProfileResponse?> UpdateUserNameAsync(
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult<UserProfileResponse?>(null);

        if (!_users.TryGetValue(userId.Trim(), out var user))
        {
            return Task.FromResult<UserProfileResponse?>(null);
        }

        // Mirrors the SQL UPDATE's SET list exactly: UserName and nothing else. The role list is a
        // separate dictionary here, as it is a separate table there, so it cannot be written by
        // accident.
        user.UserName = userName.Trim();

        return Task.FromResult<UserProfileResponse?>(new UserProfileResponse
        {
            UserId = user.UserId,
            UserName = user.UserName
        });
    }

    /// <summary>The stored row, for tests asserting on columns the wire shape never carries.</summary>
    public AppUserCredential? Stored(string userId)
        => _users.TryGetValue(userId, out var user) ? user : null;

    public Task<bool> UpdatePasswordAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult(false);

        if (!_users.TryGetValue(userId.Trim(), out var user)) return Task.FromResult(false);

        // Mirrors the SQL SET list: the hash and the stamp, nothing else. PasswordUpdatedTime is
        // not on AppUserCredential, so the fake keeps it alongside for the tests that assert it.
        user.PasswordHash = passwordHash;
        _passwordUpdatedTimes[user.UserId] = DateTime.Now;

        return Task.FromResult(true);
    }

    /// <summary>When this user's password was last written, or <c>null</c> if it never was.</summary>
    public DateTime? PasswordUpdatedTime(string userId)
        => _passwordUpdatedTimes.TryGetValue(userId, out var stamp) ? stamp : null;
}
