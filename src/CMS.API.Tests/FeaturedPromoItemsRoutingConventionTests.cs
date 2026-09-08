using System.Reflection;
using CMS.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CMS.API.Tests;

/// <summary>
/// Pins the routing shape the frontend data service is written against: <c>/api/featured-promo-items</c>,
/// PUT with the key in the body, <c>{id:int}</c> segments, and the <c>{id:int}/move-slot</c> action
/// the grid's +/− buttons call.
/// </summary>
public class FeaturedPromoItemsRoutingConventionTests
{
    private static MethodInfo Action(string name)
        => typeof(FeaturedPromoItemsController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{name} not found on FeaturedPromoItemsController.");

    [Fact]
    public void Controller_IsRoutedAtApiFeaturedPromoItems()
    {
        var route = typeof(FeaturedPromoItemsController).GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/featured-promo-items", route!.Template);
    }

    [Fact]
    public void FilteredSearch_IsPostedToTheQuerySubRoute()
    {
        var post = Action(nameof(FeaturedPromoItemsController.Query)).GetCustomAttribute<HttpPostAttribute>();

        Assert.Equal("query", post!.Template);
    }

    [Theory]
    [InlineData(nameof(FeaturedPromoItemsController.GetById))]
    [InlineData(nameof(FeaturedPromoItemsController.Delete))]
    public void SingleRecordRoutes_ConstrainTheIdSegmentToAnInteger(string actionName)
    {
        var template = Action(actionName).GetCustomAttributes<HttpMethodAttribute>().Single().Template;

        Assert.Equal("{id:int}", template);
    }

    [Fact]
    public void MoveSlot_IsPostedToTheMoveSlotSubRoute()
    {
        var post = Action(nameof(FeaturedPromoItemsController.MoveSlot)).GetCustomAttribute<HttpPostAttribute>();

        Assert.Equal("{id:int}/move-slot", post!.Template);
    }

    [Fact]
    public void Update_TakesNoRouteParameter_SoTheKeyComesFromTheBody()
    {
        var put = Action(nameof(FeaturedPromoItemsController.Update)).GetCustomAttribute<HttpPutAttribute>();

        Assert.Null(put!.Template);
    }

    [Fact]
    public void SingleRecordRoutes_BindTheKeyAsAnInt_MatchingTheIntColumn()
    {
        var parameter = Action(nameof(FeaturedPromoItemsController.GetById))
            .GetParameters()
            .Single(p => p.Name == "id");

        Assert.Equal(typeof(int), parameter.ParameterType);
    }

    [Fact]
    public void Update_TakesTheKeyBearingUpdateRequest_NotTheCreateRequest()
    {
        var parameter = Action(nameof(FeaturedPromoItemsController.Update))
            .GetParameters()
            .Single(p => p.Name == "request");

        Assert.Equal(typeof(Models.FeaturedPromoItemUpdateRequest), parameter.ParameterType);
    }

    [Fact]
    public void Create_TakesTheKeylessCreateRequest_BecausePkidIsAnIdentityColumn()
    {
        var parameter = Action(nameof(FeaturedPromoItemsController.Create))
            .GetParameters()
            .Single(p => p.Name == "request");

        Assert.Equal(typeof(Models.FeaturedPromoItemRequest), parameter.ParameterType);
        Assert.Null(typeof(Models.FeaturedPromoItemRequest).GetProperty("Pkid"));
    }
}
