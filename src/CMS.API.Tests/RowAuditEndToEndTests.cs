using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace CMS.API.Tests;

/// <summary>
/// 異動紀錄 through the whole stack: a real HTTP request, the real bearer pipeline, the real
/// dependency graph, the real repositories, real SQL — with only the connection string pointed at a
/// throwaway database.
///
/// Nothing is substituted, and that is the point. Two things are true only here:
/// <list type="bullet">
/// <item>every repository can still be *constructed* by the container now that each one takes an
/// <see cref="IRowAuditWriter"/> — a missing registration is a 500 at runtime that no unit test
/// sees, because the unit tests build repositories by hand;</item>
/// <item>the UserName in the trail is the one carried by a token a real login issued, rather than
/// one a test handed to a <c>ClaimsPrincipal</c> it built itself.</item>
/// </list>
/// </summary>
public class RowAuditEndToEndTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string SigningKey = "cloud4fun#123456cloud4fun#123456";
    private const string AdminUserId = "admin@example.com";
    private const string AdminUserName = "Admin User";
    private const string AdminPassword = "Str0ng!Pass";

    /// <summary>Must differ from <see cref="AdminPassword"/>, or admin logs in flagged and is 403'd everywhere.</summary>
    private const string DefaultPassword = "CMS-default#1";

    private readonly SqlServerDatabaseFixture _database;

    public RowAuditEndToEndTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        if (!SqlServerDatabaseFixture.IsAvailable) return;

        await _database.ResetAsync();

        using var connection = await _database.OpenAsync();

        await connection.ExecuteAsync(
            @"DELETE FROM dbo.SysConfig;
              INSERT INTO dbo.SysConfig (configKey, configValue) VALUES (@Key, @Value);
              INSERT INTO dbo.AppUser (UserId, UserName, IsActive, PasswordHash)
              VALUES (@UserId, @UserName, 1, @PasswordHash);",
            new
            {
                Key = JwtTokenService.AppConfigKey,
                Value = $$"""
                {
                  "defaultPassword": "{{DefaultPassword}}",
                  "symmetricSecurityKey": "{{SigningKey}}"
                }
                """,
                UserId = AdminUserId,
                UserName = AdminUserName,
                PasswordHash = PasswordHasher.Hash(AdminPassword)
            });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// The real host, with one setting changed. No <c>RemoveAll</c>, no fakes — every repository
    /// resolved here is the SQL one the API runs in production.
    /// </summary>
    private sealed class RealStackFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public RealStackFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.UseSetting("ConnectionStrings:CMS", _connectionString);
        }
    }

    private async Task<HttpClient> SignedInClientAsync(RealStackFactory factory)
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = AdminUserId,
            Password = AdminPassword
        });

        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private RealStackFactory Factory() => new(_database.ConnectionStringForTests);

    private static PublishStatusRequest Request(byte pkid, string description, bool isDraft = true)
        => new()
        {
            Pkid = pkid,
            Description = description,
            IsDraft = isDraft,
            IsPublished = false,
            IsDiscontinued = false
        };

    [SqlServerFact]
    public async Task ACompleteCrudRoundTrip_LeavesOneAuditRowPerChange_SignedByTheTokensUser()
    {
        using var factory = Factory();
        using var client = await SignedInClientAsync(factory);

        var created = await client.PostAsJsonAsync("/api/publish-statuses", Request(10, "草稿"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var updated = await client.PutAsJsonAsync("/api/publish-statuses", Request(10, "已發布", isDraft: false));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deleted = await client.DeleteAsync("/api/publish-statuses/10");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var trail = await _database.AuditRowsAsync();

        Assert.Equal(3, trail.Count);
        Assert.All(trail, row => Assert.Equal("PublishStatus", row.TableName));
        Assert.All(trail, row => Assert.Equal("10", row.PrimaryKeyValues));

        // The name is the one dbo.AppUser holds, carried in the token's userName claim — not the
        // userId the login was made with, which is what User.Identity.Name would have given.
        Assert.All(trail, row => Assert.Equal(AdminUserName, row.UserName));
        Assert.DoesNotContain(trail, row => row.UserName == AdminUserId);

        Assert.Equal(
            new[] { RowAuditWriter.InsertAction, RowAuditWriter.UpdateAction, RowAuditWriter.DeleteAction },
            trail.Select(row => row.ActionType));

        Assert.Equal("草稿", trail[0].ActionDesc);
        Assert.Equal("Description, IsDraft", trail[1].ActionDesc);
        Assert.Equal("已發布", trail[2].ActionDesc);
    }

    [SqlServerFact]
    public async Task ARequestThatFailsValidation_ChangesNothingAndAuditsNothing()
    {
        using var factory = Factory();
        using var client = await SignedInClientAsync(factory);

        // Description is [Required]: the request never reaches the repository, so there is nothing
        // to audit — the trail must not record an attempt.
        var response = await client.PostAsJsonAsync("/api/publish-statuses", Request(10, string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _database.AuditRowsAsync());
    }

    [SqlServerFact]
    public async Task AnAnonymousRequest_ChangesNothingAndAuditsNothing()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/publish-statuses", Request(10, "草稿"));

        // Rejected by the fallback policy before any controller — and so before any repository.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await _database.AuditRowsAsync());
    }
}
