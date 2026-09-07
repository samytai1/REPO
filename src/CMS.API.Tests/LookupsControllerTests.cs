using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>Covers the lookup lists the feature forms and filter drawers depend on.</summary>
public class LookupsControllerTests
{
    private static LookupsController Controller()
    {
        var users = new InMemoryAppUserRepository(
            new AppUserLookup { UserId = "miles@uuu.com.tw", UserName = "Miles Sun", IsActive = true },
            new AppUserLookup { UserId = "helen", UserName = "helen", IsActive = false });

        var roles = new InMemoryAppRoleRepository()
            .Seed("User", "User", 100, "一般使用者")
            .Seed("Admin", "Administrator", 1, "系統管理員");

        var publishStatuses = new InMemoryPublishStatusRepository()
            .Seed(2, "已發布", isDraft: false, isPublished: true, isDiscontinued: false)
            .Seed(1, "草稿", isDraft: true, isPublished: false, isDiscontinued: false);

        return new LookupsController(users, roles, publishStatuses);
    }

    [Fact]
    public async Task GetAppUsers_ReturnsUsersSortedByUserName()
    {
        var result = await Controller().GetAppUsers(CancellationToken.None);

        var users = Assert.IsAssignableFrom<IEnumerable<AppUserLookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "Miles Sun", "helen" }, users.Select(u => u.UserName));
    }

    [Fact]
    public async Task GetAppUsers_CarriesTheIsActiveFlag()
    {
        var result = await Controller().GetAppUsers(CancellationToken.None);

        var users = Assert.IsAssignableFrom<IEnumerable<AppUserLookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.False(users.Single(u => u.UserId == "helen").IsActive);
    }

    [Fact]
    public async Task GetAppRoles_ReturnsRolesSortedByRoleId()
    {
        var result = await Controller().GetAppRoles(CancellationToken.None);

        var roles = Assert.IsAssignableFrom<IEnumerable<AppRole>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "Admin", "User" }, roles.Select(r => r.RoleId));
    }

    [Fact]
    public async Task GetPublishStatuses_ReturnsStatusesSortedByPkid()
    {
        var result = await Controller().GetPublishStatuses(CancellationToken.None);

        var statuses = Assert.IsAssignableFrom<IEnumerable<PublishStatus>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new byte[] { 1, 2 }, statuses.Select(s => s.Pkid));
        Assert.Equal(new[] { "草稿", "已發布" }, statuses.Select(s => s.Description));
    }
}
