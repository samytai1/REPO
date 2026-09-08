using System.Reflection;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
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
///
/// The same rule now has a second half. That policy also refuses a caller still on the configured
/// 預設密碼, and <c>AuthController.ChangePassword</c> is the only action allowed to name
/// <see cref="AuthPolicies.PasswordChangeExempt"/> and step around it.
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
    public void UpdateProfile_RequiresAnAuthenticatedUser_AndNamesTheFullPolicy()
    {
        var action = typeof(AuthController).GetMethod(nameof(AuthController.UpdateProfile))!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        // An attribute of *any* kind opts an endpoint out of the fallback policy, so a bare
        // [Authorize] here would be leaning on DefaultPolicy instead. Naming the policy outright
        // says which rules apply at the one endpoint where getting it wrong would be worst.
        Assert.NotNull(authorize);
        Assert.Equal(AuthPolicies.PasswordNotDefault, authorize!.Policy);
        Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void ChangePassword_RequiresAnAuthenticatedUser_ButNotTheDefaultPasswordCheck()
    {
        var action = typeof(AuthController).GetMethod(nameof(AuthController.ChangePassword))!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthPolicies.PasswordChangeExempt, authorize!.Policy);
        Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    // ---------- 預設密碼 enforcement ----------

    [Fact]
    public void DefaultAndFallbackPolicies_BothCarryTheMustChangePasswordRequirement()
    {
        var options = Resolve<IOptions<AuthorizationOptions>>().Value;

        // Both, deliberately. FallbackPolicy covers an unattributed endpoint; DefaultPolicy covers a
        // bare [Authorize], which bypasses the fallback entirely. Dropping either one silently
        // reopens half the API to a flagged caller.
        foreach (var policy in new[] { options.DefaultPolicy, options.FallbackPolicy })
        {
            Assert.NotNull(policy);
            Assert.Contains(policy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
            Assert.Contains(policy.Requirements, r => r is MustChangePasswordRequirement);
        }
    }

    [Fact]
    public void ThePasswordNotDefaultPolicy_IsRegisteredByName_SoAnAttributeCanNameIt()
    {
        var policy = Resolve<IOptions<AuthorizationOptions>>().Value.GetPolicy(AuthPolicies.PasswordNotDefault);

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(policy.Requirements, r => r is MustChangePasswordRequirement);
    }

    [Fact]
    public void ThePasswordChangeExemptPolicy_RequiresAuthenticationButNotTheFlag()
    {
        var policy = Resolve<IOptions<AuthorizationOptions>>().Value.GetPolicy(AuthPolicies.PasswordChangeExempt);

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        Assert.DoesNotContain(policy.Requirements, r => r is MustChangePasswordRequirement);
    }

    [Fact]
    public void ChangePasswordIsTheOnlyPasswordChangeExemptAction_InTheWholeApi()
    {
        // Shaped exactly like LoginIsTheOnlyAllowAnonymousAction: exact set equality, so a second
        // exemption added later fails the build rather than passing unnoticed.
        var exempt = Controllers()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<AuthorizeAttribute>()?.Policy == AuthPolicies.PasswordChangeExempt)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.Equal(new[] { $"{nameof(AuthController)}.{nameof(AuthController.ChangePassword)}" }, exempt);
    }

    /// <summary>
    /// The trap this design is shaped around. <c>[Authorize(Roles = …)]</c> sets
    /// <c>useDefaultPolicy = false</c> just as <c>[Authorize(Policy = …)]</c> does, so a future role
    /// gate would escape the fallback **and** the default policy and quietly become the one endpoint
    /// a flagged user can still reach. Naming a policy alongside the roles is the fix.
    /// </summary>
    [Fact]
    public void NoAuthorizeAttribute_EscapesTheDefaultPolicy_ByNamingRolesOrAnUnknownPolicy()
    {
        var known = new[] { AuthPolicies.PasswordNotDefault, AuthPolicies.PasswordChangeExempt };

        var offenders = Controllers()
            .SelectMany(t => t
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>()
                    .Select(a => (Name: $"{t.Name}.{m.Name}", Attribute: a)))
                .Concat(t.GetCustomAttributes<AuthorizeAttribute>().Select(a => (Name: t.Name, Attribute: a))))
            .Where(entry =>
                (entry.Attribute.Policy is not null && !known.Contains(entry.Attribute.Policy))
                || (entry.Attribute.Roles is not null && entry.Attribute.Policy is null))
            .Select(entry => $"{entry.Name} [Policy={entry.Attribute.Policy}, Roles={entry.Attribute.Roles}]")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "these opt out of the default policy without naming one — name "
            + $"AuthPolicies.PasswordNotDefault alongside the roles instead: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void TheAuthorizationResultHandler_IsTheOneThatExplainsThe403()
    {
        // Registered after AddAuthorization so it wins the TryAdd. A regression here would restore
        // the framework's bodyless 403 without failing anything else.
        Assert.IsType<PasswordChangeRequiredResultHandler>(Resolve<IAuthorizationMiddlewareResultHandler>());
    }

    [Fact]
    public void TheMustChangePasswordHandler_IsRegistered()
    {
        // Without it the requirement can never be satisfied and *every* request 403s.
        Assert.Contains(
            _factory.Services.GetServices<IAuthorizationHandler>(),
            handler => handler is MustChangePasswordHandler);
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
