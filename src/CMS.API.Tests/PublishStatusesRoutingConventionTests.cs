using System.Reflection;
using CMS.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CMS.API.Tests;

/// <summary>
/// Pins the routing shape the frontend data service is written against, per
/// <c>spec/code-gen.convention.md</c>: <c>/api/{tablePlural}</c>, PUT with the key in the body, and
/// an <c>{id:int}</c> segment because pkid is a numeric (tinyint) key.
/// </summary>
public class PublishStatusesRoutingConventionTests
{
    private static MethodInfo Action(string name)
        => typeof(PublishStatusesController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{name} not found on PublishStatusesController.");

    [Fact]
    public void Controller_IsRoutedAtApiPublishStatuses()
    {
        var route = typeof(PublishStatusesController).GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/publish-statuses", route!.Template);
    }

    [Fact]
    public void FilteredSearch_IsPostedToTheQuerySubRoute()
    {
        var post = Action(nameof(PublishStatusesController.Query)).GetCustomAttribute<HttpPostAttribute>();

        Assert.Equal("query", post!.Template);
    }

    [Theory]
    [InlineData(nameof(PublishStatusesController.GetById))]
    [InlineData(nameof(PublishStatusesController.Delete))]
    public void SingleRecordRoutes_ConstrainTheIdSegmentToAnInteger(string actionName)
    {
        var template = Action(actionName).GetCustomAttributes<HttpMethodAttribute>().Single().Template;

        Assert.Equal("{id:int}", template);
    }

    [Fact]
    public void Update_TakesNoRouteParameter_SoTheKeyComesFromTheBody()
    {
        var put = Action(nameof(PublishStatusesController.Update)).GetCustomAttribute<HttpPutAttribute>();

        Assert.Null(put!.Template);
    }

    [Fact]
    public void SingleRecordRoutes_BindTheKeyAsAByte_MatchingTheTinyintColumn()
    {
        var parameter = Action(nameof(PublishStatusesController.GetById))
            .GetParameters()
            .Single(p => p.Name == "id");

        Assert.Equal(typeof(byte), parameter.ParameterType);
    }
}
