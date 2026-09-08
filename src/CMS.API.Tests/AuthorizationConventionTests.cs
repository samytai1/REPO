using System.Reflection;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CMS.API.Tests;

/// <summary>
/// Pins the shape of the authorization wiring, so a controller — or a change to
/// <c>Program.cs</c> — that opens the API back up fails the build rather than the pen test.
///
/// The rule is "closed by default": one global fallback policy, and <c>AuthController.Login</c> the
/// only action that opts out of it — an exception scoped to a method, never to a type.
/// </summary>
public class AuthorizationConventionTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthorizationConventionTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Every controller the API ships.</summary>
    private static IEnumerable<Type> Controllers()
        => typeof(AuthController).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t));

    private T Resolve<T>() where T : notnull => _factory.Services.GetRequiredService<T>();

    // ---------- The global policy ----------

    [Fact]
    public void FallbackPolicy_RequiresAnAuthenticatedUser()
    {
        var options = Resolve<IOptions<AuthorizationOptions>>().Value;

        Assert.NotNull(options.FallbackPolicy);
        Assert.Contains(options.FallbackPolicy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task BearerIsTheDefaultAuthenticationScheme()
    {
        var schemes = Resolve<IAuthenticationSchemeProvider>();

        var defaultScheme = await schemes.GetDefaultAuthenticateSchemeAsync();

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, defaultScheme?.Name);
    }

    [Fact]
    public void BearerValidation_ChecksTheSignatureAndTheLifetime_ButNotIssuerOrAudience()
    {
        var parameters = Resolve<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;

        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.True(parameters.ValidateLifetime);

        // The API issues and consumes its own tokens; there is no issuer or audience to check.
        Assert.False(parameters.ValidateIssuer);
        Assert.False(parameters.ValidateAudience);
    }

    [Fact]
    public void BearerValidation_ReadsTheKeyPerRequest_RatherThanFromAFixedKey()
    {
        var options = Resolve<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        // A hard-coded IssuerSigningKey would survive a secret rotation in dbo.SysConfig.
        Assert.Null(options.TokenValidationParameters.IssuerSigningKey);
        Assert.NotNull(options.TokenValidationParameters.IssuerSigningKeyResolver);
        Assert.NotNull(options.Events?.OnMessageReceived);
    }

    [Fact]
    public void BearerValidation_KeepsTheShortRoleClaimName()
    {
        var options = Resolve<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        // Claim mapping off + RoleClaimType set means User.IsInRole reads exactly the `role` claims
        // JwtTokenService writes — the same names the Angular sidebar gates the Admin group on.
        Assert.False(options.MapInboundClaims);
        Assert.Equal(JwtTokenService.RoleClaimType, options.TokenValidationParameters.RoleClaimType);
    }

    // ---------- The one exception ----------

    [Fact]
    public void NoControllerType_IsAllowAnonymous_NotEvenAuthController()
    {
        // A class-level [AllowAnonymous] beats an action-level [Authorize], so one on AuthController
        // would silently open up every action added beside Login — PUT /api/auth/profile included.
        var anonymous = Controllers()
            .Where(t => t.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(anonymous);
    }

    [Fact]
    public void LoginIsTheOnlyAllowAnonymousAction_InTheWholeApi()
    {
        var anonymous = Controllers()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.Equal(new[] { $"{nameof(AuthController)}.{nameof(AuthController.Login)}" }, anonymous);
    }

    [Fact]
    public void UpdateProfile_RequiresAnAuthenticatedUser()
    {
        var action = typeof(AuthController).GetMethod(nameof(AuthController.UpdateProfile))!;

        // Belt and braces: the global fallback policy already covers it, and this attribute says so
        // at the one endpoint where an accidental anonymous write would be worst.
        Assert.NotNull(action.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void ControllersAreDiscovered_SoTheExceptionListIsMeaningful()
    {
        // Guards the two tests above: an empty reflection query would pass them vacuously.
        var names = Controllers().Select(t => t.Name).ToList();

        Assert.Contains(nameof(AuthController), names);
        Assert.Contains("PublishStatusesController", names);
        Assert.True(names.Count >= 6, $"only found {names.Count} controllers: {string.Join(", ", names)}");
    }
}
