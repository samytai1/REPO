using System.Reflection;
using CMS.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CMS.API.Tests;

/// <summary>
/// Pins the routing shape the frontend data service is written against, per
/// <c>spec/code-gen.convention.md</c>: <c>/api/{tablePlural}</c>, PUT with the key in the body, and
/// an <c>{id:int}</c> segment because pkid is a numeric (smallint) key.
/// </summary>
public class PartnersRoutingConventionTests
{
    private static MethodInfo Action(string name)
        => typeof(PartnersController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{name} not found on PartnersController.");

    [Fact]
    public void Controller_IsRoutedAtApiPartners()
    {
        var route = typeof(PartnersController).GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/partners", route!.Template);
    }

    [Fact]
    public void FilteredSearch_IsPostedToTheQuerySubRoute()
    {
        var post = Action(nameof(PartnersController.Query)).GetCustomAttribute<HttpPostAttribute>();

        Assert.Equal("query", post!.Template);
    }

    [Theory]
    [InlineData(nameof(PartnersController.GetById))]
    [InlineData(nameof(PartnersController.Delete))]
    public void SingleRecordRoutes_ConstrainTheIdSegmentToAnInteger(string actionName)
    {
        var template = Action(actionName).GetCustomAttributes<HttpMethodAttribute>().Single().Template;

        Assert.Equal("{id:int}", template);
    }

    [Fact]
    public void Update_TakesNoRouteParameter_SoTheKeyComesFromTheBody()
    {
        var put = Action(nameof(PartnersController.Update)).GetCustomAttribute<HttpPutAttribute>();

        Assert.Null(put!.Template);
    }

    [Fact]
    public void SingleRecordRoutes_BindTheKeyAsAShort_MatchingTheSmallintColumn()
    {
        var parameter = Action(nameof(PartnersController.GetById))
            .GetParameters()
            .Single(p => p.Name == "id");

        Assert.Equal(typeof(short), parameter.ParameterType);
    }

    [Fact]
    public void Update_TakesTheKeyBearingUpdateRequest_NotTheCreateRequest()
    {
        var parameter = Action(nameof(PartnersController.Update))
            .GetParameters()
            .Single(p => p.Name == "request");

        Assert.Equal(typeof(Models.PartnerUpdateRequest), parameter.ParameterType);
    }

    [Fact]
    public void Create_TakesTheKeylessCreateRequest_BecausePkidIsAnIdentityColumn()
    {
        var parameter = Action(nameof(PartnersController.Create))
            .GetParameters()
            .Single(p => p.Name == "request");

        Assert.Equal(typeof(Models.PartnerRequest), parameter.ParameterType);
        Assert.Null(typeof(Models.PartnerRequest).GetProperty("Pkid"));
    }
}
