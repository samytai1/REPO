using System.Reflection;
using CMS.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CMS.API.Tests;

/// <summary>
/// Pins the routing shape the frontend data service is written against, per
/// <c>spec/code-gen.convention.md</c>: <c>/api/{tablePlural}</c>, PUT with the key in the body, and
/// an <c>{id:int}</c> segment because pkid is a numeric (int) key.
/// </summary>
public class CoursesRoutingConventionTests
{
    private static MethodInfo Action(string name)
        => typeof(CoursesController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{name} not found on CoursesController.");

    [Fact]
    public void Controller_IsRoutedAtApiCourses()
    {
        var route = typeof(CoursesController).GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/courses", route!.Template);
    }

    [Fact]
    public void FilteredSearch_IsPostedToTheQuerySubRoute()
    {
        var post = Action(nameof(CoursesController.Query)).GetCustomAttribute<HttpPostAttribute>();

        Assert.Equal("query", post!.Template);
    }

    [Theory]
    [InlineData(nameof(CoursesController.GetById))]
    [InlineData(nameof(CoursesController.Delete))]
    public void SingleRecordRoutes_ConstrainTheIdSegmentToAnInteger(string actionName)
    {
        var template = Action(actionName).GetCustomAttributes<HttpMethodAttribute>().Single().Template;

        Assert.Equal("{id:int}", template);
    }

    [Fact]
    public void Update_TakesNoRouteParameter_SoTheKeyComesFromTheBody()
    {
        var put = Action(nameof(CoursesController.Update)).GetCustomAttribute<HttpPutAttribute>();

        Assert.Null(put!.Template);
    }

    [Fact]
    public void SingleRecordRoutes_BindTheKeyAsAnInt_MatchingTheIntColumn()
    {
        var parameter = Action(nameof(CoursesController.GetById))
            .GetParameters()
            .Single(p => p.Name == "id");

        Assert.Equal(typeof(int), parameter.ParameterType);
    }

    [Fact]
    public void Update_TakesTheKeyBearingUpdateRequest_NotTheCreateRequest()
    {
        var parameter = Action(nameof(CoursesController.Update))
            .GetParameters()
            .Single(p => p.Name == "request");

        Assert.Equal(typeof(Models.CourseUpdateRequest), parameter.ParameterType);
    }

    [Fact]
    public void Create_TakesTheKeylessCreateRequest_BecausePkidIsAnIdentityColumn()
    {
        var parameter = Action(nameof(CoursesController.Create))
            .GetParameters()
            .Single(p => p.Name == "request");

        Assert.Equal(typeof(Models.CourseRequest), parameter.ParameterType);
        Assert.Null(typeof(Models.CourseRequest).GetProperty("Pkid"));
    }
}
