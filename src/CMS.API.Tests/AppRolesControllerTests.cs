using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>
/// Covers the 角色 AppRole endpoints: list, filtered query, view, add, edit and delete.
/// </summary>
public class AppRolesControllerTests
{
    private static readonly AppUserLookup Helen = new() { UserId = "helen", UserName = "helen", IsActive = true };
    private static readonly AppUserLookup Jenny = new() { UserId = "Jenny_Tsao", UserName = "Jenny_Tsao", IsActive = true };
    private static readonly AppUserLookup Miles = new() { UserId = "miles@uuu.com.tw", UserName = "Miles Sun", IsActive = true };

    /// <summary>Two seeded roles: Admin (level 1, 3 users) and User (level 100, 1 user).</summary>
    private static InMemoryAppRoleRepository SeededRepository()
        => new InMemoryAppRoleRepository(new[] { Helen, Jenny, Miles })
            .Seed("Admin", "Administrator", 1, "系統管理員", "helen", "Jenny_Tsao", "miles@uuu.com.tw")
            .Seed("User", "User", 100, "一般使用者", "helen");

    private static AppRolesController ControllerFor(InMemoryAppRoleRepository repository)
        => new(repository);

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsEveryRole_SortedByRoleId()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(new[] { "Admin", "User" }, roles.Select(r => r.RoleId));
    }

    [Fact]
    public async Task GetAll_ProjectsUserCountFromTheJunctionTable()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(3, roles.Single(r => r.RoleId == "Admin").UserCount);
        Assert.Equal(1, roles.Single(r => r.RoleId == "User").UserCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllRoles()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.Query(new AppRoleQuery(), CancellationToken.None)).ToList();

        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public async Task Query_KeywordMatchesRoleId_CaseInsensitively()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.Query(new AppRoleQuery { Keyword = "adm" }, CancellationToken.None)).ToList();

        Assert.Equal("Admin", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_KeywordMatchesRoleName()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.Query(new AppRoleQuery { Keyword = "Administrator" }, CancellationToken.None)).ToList();

        Assert.Equal("Admin", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_KeywordMatchesDescription()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.Query(new AppRoleQuery { Keyword = "一般" }, CancellationToken.None)).ToList();

        Assert.Equal("User", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_PermissionLevelRange_FiltersInclusively()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.Query(
            new AppRoleQuery { PermissionLevelFrom = 1, PermissionLevelTo = 50 },
            CancellationToken.None)).ToList();

        Assert.Equal("Admin", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_CombinesKeywordAndPermissionLevel()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.Query(
            new AppRoleQuery { Keyword = "user", PermissionLevelFrom = 100 },
            CancellationToken.None)).ToList();

        Assert.Equal("User", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var controller = ControllerFor(SeededRepository());

        var roles = AssertOk(await controller.Query(new AppRoleQuery { Keyword = "nothing-matches" }, CancellationToken.None));

        Assert.Empty(roles);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsTheRoleWithItsUsers()
    {
        var controller = ControllerFor(SeededRepository());

        var role = AssertOk(await controller.GetById("Admin", CancellationToken.None));

        Assert.Equal("Administrator", role.RoleName);
        Assert.Equal(1, role.PermissionLevel);
        Assert.Equal("系統管理員", role.Description);
        Assert.Equal(3, role.UserCount);
        Assert.Contains(role.Users, u => u.UserId == "miles@uuu.com.tw");
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_ForAnUnknownRole()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.GetById("Ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetById_AcceptsAStringKeyContainingUrlUnsafeCharacters()
    {
        var repository = SeededRepository().Seed("Ops/Support", "Operations", 50, null);
        var controller = ControllerFor(repository);

        var role = AssertOk(await controller.GetById("Ops/Support", CancellationToken.None));

        Assert.Equal("Operations", role.RoleName);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_PersistsTheRoleAndReturns201WithLocation()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var request = new AppRoleRequest
        {
            RoleId = "Editor",
            RoleName = "Content Editor",
            PermissionLevel = 50,
            Description = "內容編輯",
            UserIds = { "helen" }
        };

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AppRolesController.GetById), created.ActionName);
        Assert.Equal("Editor", created.RouteValues!["id"]);

        var role = Assert.IsType<AppRole>(created.Value);
        Assert.Equal("Content Editor", role.RoleName);
        Assert.Equal(50, role.PermissionLevel);
        Assert.Equal(1, role.UserCount);

        Assert.NotNull(await repository.GetByIdAsync("Editor"));
    }

    [Fact]
    public async Task Create_AssignsANewPkid()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Create(
            new AppRoleRequest { RoleId = "Editor", RoleName = "Content Editor", PermissionLevel = 50 },
            CancellationToken.None);

        var role = Assert.IsType<AppRole>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);

        Assert.True(role.Pkid > 0);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenRoleIdAlreadyExists()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Create(
            new AppRoleRequest { RoleId = "Admin", RoleName = "Duplicate", PermissionLevel = 1 },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsValidationProblem_WhenRoleNameIsMissing()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);
        controller.ModelState.AddModelError(nameof(AppRoleRequest.RoleName), "Required");

        var result = await controller.Create(
            new AppRoleRequest { RoleId = "Editor", RoleName = string.Empty },
            CancellationToken.None);

        var problem = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Null(await repository.GetByIdAsync("Editor"));
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_TakesTheKeyFromTheBodyAndSavesTheChanges()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Update(new AppRoleRequest
        {
            RoleId = "User",
            RoleName = "General User",
            PermissionLevel = 200,
            Description = "更新後的描述",
            UserIds = { "helen", "Jenny_Tsao" }
        }, CancellationToken.None);

        var role = AssertOk(result);
        Assert.Equal("General User", role.RoleName);
        Assert.Equal(200, role.PermissionLevel);
        Assert.Equal("更新後的描述", role.Description);

        var reread = await repository.GetByIdAsync("User");
        Assert.Equal("General User", reread!.RoleName);
    }

    [Fact]
    public async Task Update_ReplacesTheUserAssignments()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Update(new AppRoleRequest
        {
            RoleId = "Admin",
            RoleName = "Administrator",
            PermissionLevel = 1,
            Description = "系統管理員",
            UserIds = { "helen" }
        }, CancellationToken.None);

        Assert.Equal(new[] { "helen" }, repository.AssignedUserIds("Admin"));
        Assert.Equal(1, (await repository.GetByIdAsync("Admin"))!.UserCount);
    }

    [Fact]
    public async Task Update_ClearsTheUserAssignments_WhenNoneAreSubmitted()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Update(new AppRoleRequest
        {
            RoleId = "Admin",
            RoleName = "Administrator",
            PermissionLevel = 1,
            UserIds = { }
        }, CancellationToken.None);

        Assert.Empty(repository.AssignedUserIds("Admin"));
    }

    [Fact]
    public async Task Update_DeduplicatesSubmittedUserIds()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Update(new AppRoleRequest
        {
            RoleId = "User",
            RoleName = "User",
            PermissionLevel = 100,
            UserIds = { "helen", "helen", " helen " }
        }, CancellationToken.None);

        Assert.Equal(new[] { "helen" }, repository.AssignedUserIds("User"));
    }

    [Fact]
    public async Task Update_ReturnsNotFound_ForAnUnknownRole()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Update(
            new AppRoleRequest { RoleId = "Ghost", RoleName = "Ghost", PermissionLevel = 1 },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsValidationProblem_WhenTheModelIsInvalid()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);
        controller.ModelState.AddModelError(nameof(AppRoleRequest.RoleName), "Required");

        var result = await controller.Update(
            new AppRoleRequest { RoleId = "User", RoleName = string.Empty },
            CancellationToken.None);

        var problem = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("User", (await repository.GetByIdAsync("User"))!.RoleName);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesTheRole()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Delete("User", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync("User"));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_ForAnUnknownRole()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Delete("Ghost", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
