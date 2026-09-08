using System.Security.Claims;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests;

/// <summary>
/// 異動紀錄 over the awkward shape: a string business key, an n-n junction, and a model whose
/// members list and derived count are read back fresh on both sides of an edit.
///
/// <see cref="PublishStatusRepositoryAuditTests"/> covers the three write paths and the rollback
/// promise on the simplest entity. This suite exists for the two things only AppRole can show —
/// that the changed-column list stays honest when the model carries a list of other models, and
/// that rewriting dbo.AppUserRole (the write that can grant Admin) reaches the trail at all.
/// </summary>
public class AppRoleRepositoryAuditTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _database;

    public AppRoleRepositoryAuditTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        if (!SqlServerDatabaseFixture.IsAvailable) return;

        await _database.ResetAsync();

        using var connection = await _database.OpenAsync();

        await connection.ExecuteAsync(@"
INSERT INTO dbo.AppUser (UserId, UserName, IsActive, PasswordHash)
VALUES ('ann@example.com', N'安', 1, 'x'), ('bob@example.com', N'鮑', 1, 'x');");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppRoleRepository Repository()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(JwtTokenService.UserNameClaimType, "admin@example.com") },
                    authenticationType: "TestBearer"))
            }
        };

        return new AppRoleRepository(_database.ConnectionFactory, new RowAuditWriter(_database.ConnectionFactory, accessor));
    }

    private static AppRoleRequest Request(string roleId, string roleName, params string[] userIds)
        => new()
        {
            RoleId = roleId,
            RoleName = roleName,
            PermissionLevel = 100,
            Description = null,
            UserIds = userIds.ToList()
        };

    [SqlServerFact]
    public async Task Create_WritesAnInsertRowKeyedByPkidAndDescribedByTheRoleId()
    {
        await Repository().CreateAsync(Request("Admin", "管理員", "ann@example.com"));

        var audit = Assert.Single(await _database.AuditRowsAsync());

        Assert.Equal("AppRole", audit.TableName);
        Assert.Equal(RowAuditWriter.InsertAction, audit.ActionType);

        // dbo.AppRole has both: pkid is the IDENTITY surrogate the trail keys on, RoleId the first
        // string column and so the name a reader sees.
        Assert.Equal("Admin", audit.ActionDesc);
        Assert.NotEqual(string.Empty, audit.PrimaryKeyValues);
    }

    [SqlServerFact]
    public async Task Update_ThatOnlyRewritesTheMembers_NamesTheMembershipAndNothingElse()
    {
        var repository = Repository();

        await repository.CreateAsync(Request("Admin", "管理員", "ann@example.com"));

        await repository.UpdateAsync(Request("Admin", "管理員", "ann@example.com", "bob@example.com"));

        var audit = (await _database.AuditRowsAsync()).Last();

        // Granting a role is the most consequential edit this API allows, and here it is in the
        // trail. RoleName and PermissionLevel were resubmitted unchanged and are correctly absent.
        Assert.Equal(RowAuditWriter.UpdateAction, audit.ActionType);
        Assert.Equal("UserCount, Users", audit.ActionDesc);
    }

    [SqlServerFact]
    public async Task Update_ThatResubmitsEverythingUnchanged_NamesNothing()
    {
        var repository = Repository();

        await repository.CreateAsync(Request("Admin", "管理員", "ann@example.com"));
        await repository.UpdateAsync(Request("Admin", "管理員", "ann@example.com"));

        var audit = (await _database.AuditRowsAsync()).Last();

        // The members list and the nav models inside it are different object instances on each
        // read. Compared by reference they would report a change here on every single save.
        Assert.Equal(string.Empty, audit.ActionDesc);
    }

    [SqlServerFact]
    public async Task Delete_WritesADeleteRowAndClearsTheJunctionWithIt()
    {
        var repository = Repository();

        await repository.CreateAsync(Request("Admin", "管理員", "ann@example.com"));

        Assert.True(await repository.DeleteAsync("Admin"));

        var audit = (await _database.AuditRowsAsync()).Last();

        Assert.Equal(RowAuditWriter.DeleteAction, audit.ActionType);
        Assert.Equal("Admin", audit.ActionDesc);

        using var connection = await _database.OpenAsync();
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.AppUserRole"));
    }

    [SqlServerFact]
    public async Task Delete_OfARoleThatIsNotThere_WritesNoAuditRow()
    {
        Assert.False(await Repository().DeleteAsync("Missing"));

        Assert.Empty(await _database.AuditRowsAsync());
    }
}
