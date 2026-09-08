using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Tests;

/// <summary>
/// Hosts the real <c>Program</c> pipeline — authentication, the global fallback authorization
/// policy, routing — over in-memory repositories, so the authorization tests exercise the wiring
/// itself rather than a controller called directly.
///
/// Only the repositories a test actually reaches are replaced; everything else stays registered but
/// is never resolved, because a request without a token is rejected before its controller is built.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The secret seeded into the in-memory dbo.SysConfig. 32 bytes, the HS256 minimum.</summary>
    public const string SigningKey = "cloud4fun#123456cloud4fun#123456";

    /// <summary>A second valid-length secret, for tokens this API must reject.</summary>
    public const string ForeignSigningKey = "a-totally-different-secret-key!!!";

    public const string AdminUserId = "admin@example.com";
    public const string AdminUserName = "Admin User";
    public const string AdminPassword = "CMS4fun#";

    /// <summary>
    /// The shared default seeded into <c>appConfig.defaultPassword</c>. It **must not** equal
    /// <see cref="AdminPassword"/>: admin is the user almost every test here signs in as, and a
    /// flagged token answers 403 to everything but PUT /api/auth/password. Tidying the two back
    /// together takes half this suite down.
    /// </summary>
    public const string DefaultPassword = "CMS-default#1";

    /// <summary>The one seeded account still sitting on <see cref="DefaultPassword"/>. No roles.</summary>
    public const string StaleUserId = "stale@example.com";

    public const string StaleUserName = "Stale User";

    /// <summary>A protected route whose repository this factory seeds, so an authorized call really returns data.</summary>
    public const string ProtectedRoute = "/api/publish-statuses";

    public InMemorySysConfigRepository SysConfig { get; } = new();

    public InMemoryAuthRepository Users { get; } = new InMemoryAuthRepository()
        .Seed(AdminUserId, AdminUserName, AdminPassword, true, "Admin", "User")
        .Seed("helen", "Helen Wu", "helen-pw")
        .Seed(StaleUserId, StaleUserName, DefaultPassword);

    public InMemoryPublishStatusRepository PublishStatuses { get; } = new InMemoryPublishStatusRepository()
        .Seed(1, "草稿", true, false, false)
        .Seed(2, "已發布", false, true, false);

    public TestApiFactory()
    {
        RotateSigningKey(SigningKey);
    }

    /// <summary>
    /// Rewrites dbo.SysConfig['appConfig'] with a new secret. Both issuing and validating read the
    /// row per call, so the change takes effect on the very next request — no restart.
    /// </summary>
    public void RotateSigningKey(string secret)
        => SeedAppConfig(secret, DefaultPassword);

    /// <summary>
    /// Rewrites the whole appConfig row. A separate <paramref name="defaultPassword"/> is what the
    /// 預設密碼 tests rotate, to prove the flag is decided per **login** rather than per request.
    /// </summary>
    public void SeedAppConfig(string secret, string defaultPassword)
        => SysConfig.Seed(JwtTokenService.AppConfigKey, $$"""
        {
          "defaultPassword": "{{defaultPassword}}",
          "symmetricSecurityKey": "{{secret}}"
        }
        """);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISysConfigRepository>();
            services.AddSingleton<ISysConfigRepository>(SysConfig);

            services.RemoveAll<IAuthRepository>();
            services.AddSingleton<IAuthRepository>(Users);

            services.RemoveAll<IPublishStatusRepository>();
            services.AddSingleton<IPublishStatusRepository>(PublishStatuses);
        });
    }

    /// <summary>Signs in through the real endpoint and hands back the issued access token.</summary>
    public async Task<string> LoginAsync(string userId = AdminUserId, string password = AdminPassword)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = userId,
            Password = password
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        return body!.AccessToken;
    }

    /// <summary>A client whose every request carries <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public HttpClient CreateClientWithToken(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>
    /// Mints a token outside the login endpoint — for the cases a real login cannot produce:
    /// a foreign signing key, an already-expired lifetime, or a 預設密碼 flag attached to an
    /// account that does not actually carry the default, which is how the middleware is tested
    /// independently of the login path.
    /// </summary>
    public static string SignToken(
        string secret,
        TimeSpan? lifetime = null,
        string userId = AdminUserId,
        bool mustChangePassword = false)
    {
        var expires = DateTime.UtcNow.Add(lifetime ?? JwtTokenService.TokenLifetime);

        // An already-expired token was, of course, issued before it expired — keep iat/nbf behind
        // exp, or the handler refuses to create the token at all.
        var issuedAt = expires <= DateTime.UtcNow ? expires.AddMinutes(-1) : DateTime.UtcNow;

        List<System.Security.Claims.Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtTokenService.UserIdClaimType, userId)
        ];

        if (mustChangePassword)
        {
            claims.Add(new System.Security.Claims.Claim(JwtTokenService.MustChangePasswordClaimType, "true"));
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256)
        });
    }
}
