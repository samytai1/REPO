using System.Security.Claims;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Dapper;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests;

/// <summary>
/// 異動紀錄, end to end: a real repository, real SQL, a real transaction, and the dbo.RowAudit rows
/// it leaves behind — against a throwaway database built from <c>database\admin.sql</c>.
///
/// <see cref="RowAuditWriterTests"/> proves what the writer *says* about an entity. This suite
/// proves the retrofit itself: that each write path calls it at all, with the right table and the
/// right before-picture, and — the part no unit test can reach — that a change which does not
/// commit leaves no trace in the trail either.
///
/// PublishStatus is the entity under test because its key is user-supplied, which makes a failing
/// write trivial to provoke: insert the same pkid twice.
/// </summary>
public class PublishStatusRepositoryAuditTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string SignedInUserName = "admin@example.com";

    private readonly SqlServerDatabaseFixture _database;

    public PublishStatusRepositoryAuditTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    public Task InitializeAsync()
        => SqlServerDatabaseFixture.IsAvailable ? _database.ResetAsync() : Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    private PublishStatusRepository Repository()
        => new(_database.ConnectionFactory, Writer());

    private RowAuditWriter Writer()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(JwtTokenService.UserIdClaimType, "7"),
                        new Claim(JwtTokenService.UserNameClaimType, SignedInUserName)
                    },
                    authenticationType: "TestBearer"))
            }
        };

        return new RowAuditWriter(_database.ConnectionFactory, accessor);
    }

    private static PublishStatusRequest Request(byte pkid, string description, bool isDraft = true)
        => new()
        {
            Pkid = pkid,
            Description = description,
            IsDraft = isDraft,
            IsPublished = false,
            IsDiscontinued = false
        };

    // ---------- The three write paths ----------

    [SqlServerFact]
    public async Task Create_WritesOneInsertRowNamingTheTableAndTheFirstStringColumn()
    {
        await Repository().CreateAsync(Request(10, "草稿"));

        var audit = Assert.Single(await _database.AuditRowsAsync());

        Assert.Equal("PublishStatus", audit.TableName);
        Assert.Equal(RowAuditWriter.InsertAction, audit.ActionType);
        Assert.Equal("10", audit.PrimaryKeyValues);
        Assert.Equal("草稿", audit.ActionDesc);
        Assert.Equal(SignedInUserName, audit.UserName);
        Assert.InRange(audit.DateTime, DateTime.Now.AddMinutes(-5), DateTime.Now.AddMinutes(5));
    }

    [SqlServerFact]
    public async Task Update_WritesOneUpdateRowNamingExactlyTheColumnsThatChanged()
    {
        var repository = Repository();

        await repository.CreateAsync(Request(10, "草稿"));

        var updated = await repository.UpdateAsync(Request(10, "已發布", isDraft: false));

        Assert.NotNull(updated);

        var audit = (await _database.AuditRowsAsync()).Last();

        Assert.Equal(RowAuditWriter.UpdateAction, audit.ActionType);
        Assert.Equal("10", audit.PrimaryKeyValues);

        // Description and IsDraft moved; IsPublished, IsDiscontinued and the key did not. Getting
        // this list right is why UpdateAsync reads the row before it writes it.
        Assert.Equal("Description, IsDraft", audit.ActionDesc);
    }

    [SqlServerFact]
    public async Task Update_ThatChangesNothing_WritesARowWithAnEmptyChangeList()
    {
        var repository = Repository();

        await repository.CreateAsync(Request(10, "草稿"));
        await repository.UpdateAsync(Request(10, "草稿"));

        var audit = (await _database.AuditRowsAsync()).Last();

        Assert.Equal(RowAuditWriter.UpdateAction, audit.ActionType);
        Assert.Equal(string.Empty, audit.ActionDesc);
    }

    [SqlServerFact]
    public async Task Delete_WritesOneDeleteRowCarryingTheDescriptionOfTheRowThatIsGone()
    {
        var repository = Repository();

        await repository.CreateAsync(Request(10, "草稿"));

        Assert.Equal(PublishStatusDeleteResult.Deleted, await repository.DeleteAsync(10));

        var audit = (await _database.AuditRowsAsync()).Last();

        Assert.Equal(RowAuditWriter.DeleteAction, audit.ActionType);
        Assert.Equal("10", audit.PrimaryKeyValues);

        // The row is gone from dbo.PublishStatus; this string is the only trace of what it was,
        // which is why DeleteAsync reads it before deleting rather than after.
        Assert.Equal("草稿", audit.ActionDesc);
        Assert.Null(await repository.GetByIdAsync(10));
    }

    // ---------- A change that does not happen leaves no trace ----------

    [SqlServerFact]
    public async Task Create_WithAKeyThatIsAlreadyTaken_LeavesNeitherARowNorAnAuditRow()
    {
        var repository = Repository();

        await repository.CreateAsync(Request(10, "草稿"));

        await Assert.ThrowsAnyAsync<Exception>(() => repository.CreateAsync(Request(10, "重複")));

        // Exactly the one row from the successful create: the failed INSERT rolled its whole
        // transaction back, and the row it would have described was never written either.
        var audit = Assert.Single(await _database.AuditRowsAsync());
        Assert.Equal(RowAuditWriter.InsertAction, audit.ActionType);
        Assert.Equal("草稿", audit.ActionDesc);

        var stored = await repository.GetByIdAsync(10);
        Assert.Equal("草稿", stored!.Description);
    }

    [SqlServerFact]
    public async Task Update_OfARowThatIsNotThere_WritesNoAuditRow()
    {
        Assert.Null(await Repository().UpdateAsync(Request(99, "沒有這一筆")));

        Assert.Empty(await _database.AuditRowsAsync());
    }

    [SqlServerFact]
    public async Task Delete_OfARowThatIsNotThere_WritesNoAuditRow()
    {
        Assert.Equal(PublishStatusDeleteResult.NotFound, await Repository().DeleteAsync(99));

        Assert.Empty(await _database.AuditRowsAsync());
    }

    [SqlServerFact]
    public async Task AnAuditRowWrittenInsideATransactionDiesWithIt()
    {
        // The guarantee stated directly, at the seam that provides it: the writer enlists in the
        // caller's transaction, so rolling that transaction back takes the audit row with it. Every
        // repository above depends on this being true, and no in-memory fake can show it.
        using var connection = await _database.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await Writer().LogInsertAsync(
            connection,
            transaction,
            "PublishStatus",
            new PublishStatus { Pkid = 10, Description = "捨棄" });

        // Visible inside the transaction that wrote it …
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.RowAudit", transaction: transaction));

        transaction.Rollback();

        // … and gone once that transaction is not committed.
        Assert.Empty(await _database.AuditRowsAsync());
    }
}
