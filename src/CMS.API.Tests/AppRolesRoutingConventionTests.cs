using System.Reflection;
using CMS.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CMS.API.Tests;

/// <summary>
/// Pins the routing shape the frontend data service is written against, per
/// <c>spec/code-gen.convention.md</c>: <c>/api/{tablePlural}</c>, PUT with the key in the body,
/// and an unconstrained <c>{id}</c> segment because RoleId is an nvarchar key.
/// </summary>
public class AppRolesRoutingConventionTests
{
    private static MethodInfo Action(string name)
        => typeof(AppRolesController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{name} not found on AppRolesController.");

    [Fact]
    public void Controller_IsRoutedAtApiAppRoles()
    {
        var route = typeof(AppRolesController).GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/app-roles", route!.Template);
    }

    [Fact]
    public void FilteredSearch_IsPostedToTheQuerySubRoute()
    {
        var post = Action(nameof(AppRolesController.Query)).GetCustomAttribute<HttpPostAttribute>();

        Assert.Equal("query", post!.Template);
    }

    [Theory]
    [InlineData(nameof(AppRolesController.GetById))]
    [InlineData(nameof(AppRolesController.Delete))]
    public void SingleRecordRoutes_UseAnUnconstrainedIdSegment(string actionName)
    {
        var template = Action(actionName).GetCustomAttributes<HttpMethodAttribute>().Single().Template;

        Assert.Equal("{id}", template);
    }

    [Fact]
    public void Update_TakesNoRouteParameter_SoTheKeyComesFromTheBody()
    {
        var put = Action(nameof(AppRolesController.Update)).GetCustomAttribute<HttpPutAttribute>();

        Assert.Null(put!.Template);
    }
}
