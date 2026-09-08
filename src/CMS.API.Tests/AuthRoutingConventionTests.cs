using System.Reflection;
using CMS.API.Controllers;
using CMS.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>
/// Pins the shape the frontend auth service will be written against: <c>POST /api/auth/login</c>
/// with the credentials in the body. Auth is not a CRUD table, so it deliberately does not follow
/// the kebab-case-plural convention the feature controllers use.
/// </summary>
public class AuthRoutingConventionTests
{
    private static MethodInfo Action(string name)
        => typeof(AuthController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{name} not found on AuthController.");

    [Fact]
    public void Controller_IsRoutedAtApiAuth()
    {
        var route = typeof(AuthController).GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/auth", route!.Template);
    }

    [Fact]
    public void Login_IsPostedToTheLoginSubRoute()
    {
        var post = Action(nameof(AuthController.Login)).GetCustomAttribute<HttpPostAttribute>();

        Assert.Equal("login", post!.Template);
    }

    [Fact]
    public void Login_TakesTheCredentialsFromTheBody()
    {
        var parameter = Action(nameof(AuthController.Login)).GetParameters()
            .Single(p => p.ParameterType == typeof(LoginRequest));

        Assert.NotNull(parameter.GetCustomAttribute<FromBodyAttribute>());
    }

    [Fact]
    public void Login_DeclaresBothTheSuccessAndTheUnauthorizedResponse()
    {
        var declared = Action(nameof(AuthController.Login))
            .GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(a => a.StatusCode)
            .ToList();

        Assert.Contains(StatusCodes.Status200OK, declared);
        Assert.Contains(StatusCodes.Status401Unauthorized, declared);
    }

    [Fact]
    public void LoginRequest_CarriesOnlyTheCredentials()
    {
        var properties = typeof(LoginRequest).GetProperties().Select(p => p.Name).OrderBy(p => p);

        Assert.Equal(new[] { "Password", "UserId" }, properties);
    }

    // ---------- 個人資料 ----------

    [Fact]
    public void UpdateProfile_IsPutToTheProfileSubRoute()
    {
        var put = Action(nameof(AuthController.UpdateProfile)).GetCustomAttribute<HttpPutAttribute>();

        Assert.Equal("profile", put!.Template);
    }

    [Fact]
    public void UpdateProfile_TakesTheNewNameFromTheBody()
    {
        var parameter = Action(nameof(AuthController.UpdateProfile)).GetParameters()
            .Single(p => p.ParameterType == typeof(UpdateProfileRequest));

        Assert.NotNull(parameter.GetCustomAttribute<FromBodyAttribute>());
    }

    [Fact]
    public void UpdateProfileRequest_CarriesOnlyTheUserName()
    {
        // No UserId and no role list: the account being renamed comes from the bearer token, and a
        // body property is the one thing that could ever override it.
        var properties = typeof(UpdateProfileRequest).GetProperties().Select(p => p.Name);

        Assert.Equal(new[] { "UserName" }, properties);
    }

    [Fact]
    public void UpdateProfile_DeclaresTheSuccessAndTheRejectionResponses()
    {
        var declared = Action(nameof(AuthController.UpdateProfile))
            .GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(a => a.StatusCode)
            .ToList();

        Assert.Contains(StatusCodes.Status200OK, declared);
        Assert.Contains(StatusCodes.Status400BadRequest, declared);
        Assert.Contains(StatusCodes.Status401Unauthorized, declared);
        Assert.Contains(StatusCodes.Status404NotFound, declared);
    }

    // ---------- 變更密碼 ----------

    [Fact]
    public void ChangePassword_IsPutToThePasswordSubRoute()
    {
        var put = Action(nameof(AuthController.ChangePassword)).GetCustomAttribute<HttpPutAttribute>();

        Assert.Equal("password", put!.Template);
    }

    [Fact]
    public void ChangePassword_TakesThePasswordsFromTheBody()
    {
        var parameter = Action(nameof(AuthController.ChangePassword)).GetParameters()
            .Single(p => p.ParameterType == typeof(ChangePasswordRequest));

        Assert.NotNull(parameter.GetCustomAttribute<FromBodyAttribute>());
    }

    [Fact]
    public void ChangePasswordRequest_CarriesOnlyTheCurrentAndNewPassword()
    {
        var properties = typeof(ChangePasswordRequest).GetProperties().Select(p => p.Name).OrderBy(p => p);

        Assert.Equal(new[] { "CurrentPassword", "NewPassword" }, properties);
    }

    [Fact]
    public void ChangePassword_DeclaresNoContentOnSuccess_AndHasNoResponseShapeAtAll()
    {
        var action = Action(nameof(AuthController.ChangePassword));

        var declared = action.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(a => a.StatusCode)
            .ToList();

        Assert.Contains(StatusCodes.Status204NoContent, declared);
        Assert.DoesNotContain(StatusCodes.Status200OK, declared);

        Assert.Equal(typeof(Task<IActionResult>), action.ReturnType);
    }

    [Fact]
    public void ChangePassword_DeclaresA400ForARejectedPassword()
    {
        var declared = Action(nameof(AuthController.ChangePassword))
            .GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(a => a.StatusCode)
            .ToList();

        Assert.Contains(StatusCodes.Status400BadRequest, declared);
    }
}
