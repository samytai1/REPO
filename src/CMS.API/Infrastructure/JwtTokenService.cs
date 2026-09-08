using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CMS.API.Repositories;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Infrastructure;

/// <inheritdoc />
public class JwtTokenService : IJwtTokenService
{
    /// <summary>A login token is good for 24 hours from issue.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    /// <summary>dbo.SysConfig row that holds the application JSON settings.</summary>
    public const string AppConfigKey = "appConfig";

    /// <summary>Property inside that JSON that holds the HMAC signing secret.</summary>
    public const string SecurityKeyProperty = "symmetricSecurityKey";

    /// <summary>Claim type carrying dbo.AppUser.UserId.</summary>
    public const string UserIdClaimType = "userId";

    /// <summary>Claim type carrying dbo.AppUser.UserName.</summary>
    public const string UserNameClaimType = "userName";

    /// <summary>Claim type carrying each dbo.AppUserRole.RoleId. Maps to <see cref="ClaimTypes.Role"/> on validation.</summary>
    public const string RoleClaimType = "role";

    /// <summary>HMAC-SHA256 needs at least a 256-bit secret.</summary>
    private const int MinimumKeyBytes = 32;

    private readonly ISysConfigRepository _sysConfigRepository;

    public JwtTokenService(ISysConfigRepository sysConfigRepository)
    {
        _sysConfigRepository = sysConfigRepository;
    }

    public async Task<string> CreateAccessTokenAsync(
        string userId,
        string userName,
        IEnumerable<string> roleIds,
        CancellationToken cancellationToken = default)
    {
        var signingKey = await GetSigningKeyAsync(cancellationToken);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(UserIdClaimType, userId),
            new(UserNameClaimType, userName)
        };

        // One role claim per assignment; blanks and repeats would only bloat the token.
        claims.AddRange((roleIds ?? Enumerable.Empty<string>())
            .Where(roleId => !string.IsNullOrWhiteSpace(roleId))
            .Select(roleId => roleId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(roleId => new Claim(RoleClaimType, roleId)));

        var issuedAt = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.Add(TokenLifetime),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>
    /// Pulls <c>symmetricSecurityKey</c> out of the appConfig JSON. Read on every login — and on
    /// every bearer-token validation — so a rotated secret takes effect without a restart; the
    /// secret is never compiled in or held in appsettings.
    /// </summary>
    public async Task<SymmetricSecurityKey> GetSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        var configValue = await _sysConfigRepository.GetValueAsync(AppConfigKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(configValue))
        {
            throw new InvalidOperationException(
                $"dbo.SysConfig has no '{AppConfigKey}' row, so the JWT signing key cannot be resolved.");
        }

        string? secret;
        try
        {
            using var document = JsonDocument.Parse(configValue);
            secret = document.RootElement.ValueKind == JsonValueKind.Object
                     && document.RootElement.TryGetProperty(SecurityKeyProperty, out var property)
                     && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"dbo.SysConfig['{AppConfigKey}'] is not valid JSON, so the JWT signing key cannot be resolved.", ex);
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"dbo.SysConfig['{AppConfigKey}'] has no '{SecurityKeyProperty}' string property.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);

        if (keyBytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"'{SecurityKeyProperty}' is {keyBytes.Length} bytes; HMAC-SHA256 needs at least {MinimumKeyBytes}.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }
}
