using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>Covers the lookup lists the AppRole form and filter drawer depend on.</summary>
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

        return new LookupsController(users, roles);
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
}
