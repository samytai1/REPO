using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Infrastructure;

/// <summary>Issues — and supplies the validation key for — the signed JWT returned by <c>POST /api/auth/login</c>.</summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Signs a token carrying the user's id, name and one <c>role</c> claim per assigned RoleId.
    /// The signing secret is read at runtime from dbo.SysConfig.
    /// </summary>
    Task<string> CreateAccessTokenAsync(
        string userId,
        string userName,
        IEnumerable<string> roleIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The HS256 key an incoming bearer token must be signed with — the same secret
    /// <see cref="CreateAccessTokenAsync"/> issues with, read from dbo.SysConfig on every call so a
    /// rotated secret needs no restart.
    /// </summary>
    Task<SymmetricSecurityKey> GetSigningKeyAsync(CancellationToken cancellationToken = default);
}
