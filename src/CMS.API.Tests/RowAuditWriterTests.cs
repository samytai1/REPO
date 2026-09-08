using System.Data;
using System.Security.Claims;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests;

/// <summary>
/// Covers 異動紀錄 <see cref="RowAuditWriter"/> — the reflection that turns an arbitrary entity into
/// one dbo.RowAudit row, and the claim that names who changed it.
///
/// The writer is cross-cutting rather than owned by a controller, so like
/// <see cref="DefaultPasswordServiceTests"/> it needs its own suite: no controller test could reach
/// "the first string property in declaration order" or the "system" fallback. The INSERT itself is
/// the one thing not asserted here — it is overridden away, and
/// <see cref="ThrowingConnectionFactory"/> fails the test if anything tries to open a connection,
/// so a suite that looks like a unit test cannot quietly become an integration test.
/// </summary>
public class RowAuditWriterTests
{
    // ---------- Fixtures ----------

    /// <summary>
    /// Stands in for a business model: an IDENTITY key first, then the identifying string the
    /// audit trail wants, then the properties that only ever show up as changed column names.
    /// </summary>
    private sealed class Course
    {
        public int Pkid { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Note { get; set; }

        public int DisplayOrder { get; set; }

        public bool CanRepeat { get; set; }

        public decimal? ListPrice { get; set; }

        public List<int> JobCategoryPkids { get; set; } = new();

        /// <summary>An FK nav object, as every list read of a real model carries.</summary>
        public PartnerRef? Partner { get; set; }

        /// <summary>An n-n members list of models, as <c>AppRole.Users</c> is.</summary>
        public List<PartnerRef> Certifications { get; set; } = new();
    }

    private sealed class PartnerRef
    {
        public short Pkid { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>The key written in the database's own casing, to pin that the match ignores it.</summary>
    private sealed class LowerCaseKeyEntity
    {
        public short pkid { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>No key and no string — both fallbacks in one type.</summary>
    private sealed class KeylessEntity
    {
        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }
    }

    private static Course ACourse() => new()
    {
        Pkid = 42,
        Title = "資訊安全概論",
        Note = "備註",
        DisplayOrder = 3,
        CanRepeat = true,
        ListPrice = 24000m,
        JobCategoryPkids = new List<int> { 1, 2 },
        Partner = new PartnerRef { Pkid = 3, Name = "資展國際" },
        Certifications = new List<PartnerRef> { new() { Pkid = 8, Name = "PMP" } }
    };

    // ---------- Seams ----------

    /// <summary>Fails loudly: nothing in this suite may reach a database.</summary>
    private sealed class ThrowingConnectionFactory : IDbConnectionFactory
    {
        public Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("RowAuditWriterTests must never open a connection.");
    }

    /// <summary>Captures the assembled row instead of inserting it.</summary>
    private sealed class RecordingRowAuditWriter : RowAuditWriter
    {
        public RecordingRowAuditWriter(IHttpContextAccessor httpContextAccessor)
            : base(new ThrowingConnectionFactory(), httpContextAccessor)
        {
        }

        public List<RowAudit> Entries { get; } = new();

        /// <summary>The one row this writer was asked to write.</summary>
        public RowAudit Written => Assert.Single(Entries);

        protected override Task WriteAsync(
            RowAudit entry,
            IDbConnection? connection,
            IDbTransaction? transaction,
            CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private static RecordingRowAuditWriter WriterFor(ClaimsPrincipal? user)
    {
        var accessor = new HttpContextAccessor();

        if (user is not null)
        {
            accessor.HttpContext = new DefaultHttpContext { User = user };
        }

        return new RecordingRowAuditWriter(accessor);
    }

    /// <summary>A signed-in caller, with the claims <c>JwtTokenService</c> actually issues.</summary>
    private static RecordingRowAuditWriter WriterSignedInAs(string userName)
        => WriterFor(new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(JwtTokenService.UserIdClaimType, "7"),
                new Claim(JwtTokenService.UserNameClaimType, userName)
            },
            authenticationType: "TestBearer")));

    /// <summary>The default: signed in, so a test about something else is not also a UserName test.</summary>
    private static RecordingRowAuditWriter Writer() => WriterSignedInAs("admin@example.com");

    // ---------- ActionDesc: the first string property ----------

    [Fact]
    public async Task LogInsert_ActionDesc_IsTheFirstStringPropertyInDeclarationOrder()
    {
        var writer = Writer();

        await writer.LogInsertAsync("Course", ACourse());

        // Title, not Note: Type.GetProperties() promises no order, so the writer sorts by metadata
        // token. Reversing the two declarations above should be what breaks this test.
        Assert.Equal("資訊安全概論", writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogDelete_ActionDesc_IsTheFirstStringPropertyOfTheDeletedRow()
    {
        var writer = Writer();

        await writer.LogDeleteAsync("Course", ACourse());

        Assert.Equal("資訊安全概論", writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogInsert_WhenTheFirstStringPropertyIsNull_ActionDescIsNull()
    {
        var writer = Writer();

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, Title = null! });

        Assert.Null(writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogInsert_ForATypeWithNoStringProperty_ActionDescIsNull()
    {
        var writer = Writer();

        await writer.LogInsertAsync("Keyless", new KeylessEntity { DisplayOrder = 9 });

        Assert.Null(writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogInsert_ActionDesc_IsTruncatedToTheColumnWidth()
    {
        var writer = Writer();
        var title = new string('x', 1500);

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, Title = title });

        var written = writer.Written.ActionDesc;

        Assert.Equal(RowAuditWriter.ActionDescMaxBytes, written!.Length);
        Assert.Equal(title[..RowAuditWriter.ActionDescMaxBytes], written);
    }

    [Fact]
    public async Task LogInsert_ActionDescAtExactlyTheColumnWidth_IsNotTruncated()
    {
        var writer = Writer();
        var title = new string('x', RowAuditWriter.ActionDescMaxBytes);

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, Title = title });

        Assert.Equal(title, writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogInsert_ActionDesc_ChargesTwoBytesForAChineseCharacter()
    {
        // ActionDesc is varchar(1000) under a CP950 collation, so 1000 Chinese characters are 2000
        // bytes and SQL Server would reject the INSERT outright. Cutting at 1000 *characters* is
        // exactly the bug this asserts against; the real budget is bytes.
        var writer = Writer();
        var title = new string('課', 1500);

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, Title = title });

        var written = writer.Written.ActionDesc;

        Assert.Equal(RowAuditWriter.ActionDescMaxBytes / 2, written!.Length);
        Assert.Equal(title[..(RowAuditWriter.ActionDescMaxBytes / 2)], written);
    }

    [Fact]
    public async Task LogInsert_ActionDesc_DoesNotEndOnHalfASurrogatePair()
    {
        var writer = Writer();

        // 499 Chinese characters (998 bytes) then an astral character: the pair does not fit, and
        // half of it must not be written.
        var title = new string('課', 499) + "\U0002A6B2";

        await writer.LogInsertAsync("Course", new Course { Pkid = 1, Title = title });

        var written = writer.Written.ActionDesc;

        Assert.Equal(499, written!.Length);
        Assert.DoesNotContain(written, (char c) => char.IsSurrogate(c));
    }

    // ---------- ActionDesc: the changed property names ----------

    [Fact]
    public async Task LogUpdate_ActionDesc_NamesExactlyThePropertiesThatChanged()
    {
        var writer = Writer();
        var before = ACourse();
        var after = ACourse();

        after.Title = "資訊安全實務";
        after.DisplayOrder = 5;

        await writer.LogUpdateAsync("Course", before, after);

        // Comma-separated, in declaration order — and nothing else: Note, CanRepeat, ListPrice and
        // the key all held their value.
        Assert.Equal("Title, DisplayOrder", writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_WhenNothingChanged_ActionDescIsEmpty()
    {
        var writer = Writer();

        await writer.LogUpdateAsync("Course", ACourse(), ACourse());

        // A row is still written: "someone saved this and changed nothing" is a fact about the
        // session, and a missing row would be indistinguishable from a missing audit call.
        Assert.Equal(string.Empty, writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_CountsAPropertyThatBecameNull_AndOneThatStoppedBeingNull()
    {
        var writer = Writer();
        var before = ACourse();
        var after = ACourse();

        before.Note = null;
        after.ListPrice = null;

        await writer.LogUpdateAsync("Course", before, after);

        Assert.Equal("Note, ListPrice", writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_ComparesACollectionPropertyByItsElements()
    {
        var writer = Writer();
        var before = ACourse();
        var after = ACourse();

        // Two equal lists, two different List<int> instances. Reference equality would report
        // JobCategoryPkids as changed on every single edit.
        Assert.NotSame(before.JobCategoryPkids, after.JobCategoryPkids);

        await writer.LogUpdateAsync("Course", before, after);

        Assert.Equal(string.Empty, writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_ComparesAnFkNavObjectByItsContent()
    {
        var writer = Writer();
        var before = ACourse();
        var after = ACourse();

        // `before` and `after` are two separate reads of the same row, so every nav object is a
        // different instance. Reference equality would put Partner in ActionDesc on every edit.
        Assert.NotSame(before.Partner, after.Partner);

        await writer.LogUpdateAsync("Course", before, after);

        Assert.Equal(string.Empty, writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_ReportsAnFkNavObjectWhoseContentDiffers()
    {
        var writer = Writer();
        var before = ACourse();
        var after = ACourse();

        after.Partner = new PartnerRef { Pkid = 4, Name = "資策會" };

        await writer.LogUpdateAsync("Course", before, after);

        Assert.Equal("Partner", writer.Written.ActionDesc);
    }

    [Fact]
    public async Task LogUpdate_ComparesAListOfModelsElementByElement()
    {
        var writer = Writer();
        var before = ACourse();
        var after = ACourse();

        after.Certifications = new List<PartnerRef> { new() { Pkid = 8, Name = "PMP" } };
        Assert.Equal(string.Empty, (await Logged(writer, before, after)).ActionDesc);

        var changed = Writer();
        after.Certifications = new List<PartnerRef> { new() { Pkid = 9, Name = "CISSP" } };

        Assert.Equal("Certifications", (await Logged(changed, before, after)).ActionDesc);
    }

    private static async Task<RowAudit> Logged(RecordingRowAuditWriter writer, Course before, Course after)
    {
        await writer.LogUpdateAsync("Course", before, after);
        return writer.Written;
    }

    [Fact]
    public async Task LogUpdate_ReportsACollectionPropertyWhoseElementsDiffer()
    {
        var writer = Writer();
        var before = ACourse();
        var after = ACourse();

        after.JobCategoryPkids = new List<int> { 1, 2, 3 };

        await writer.LogUpdateAsync("Course", before, after);

        Assert.Equal("JobCategoryPkids", writer.Written.ActionDesc);
    }

    // ---------- PrimaryKeyValues ----------

    [Fact]
    public async Task PrimaryKeyValues_IsThePkidAsText()
    {
        var writer = Writer();

        await writer.LogInsertAsync("Course", ACourse());

        Assert.Equal("42", writer.Written.PrimaryKeyValues);
    }

    [Fact]
    public async Task PrimaryKeyValues_MatchesThePkidPropertyWhateverItsCasing()
    {
        var writer = Writer();

        await writer.LogDeleteAsync("Partner", new LowerCaseKeyEntity { pkid = 7, Name = "合作夥伴" });

        Assert.Equal("7", writer.Written.PrimaryKeyValues);
    }

    [Fact]
    public async Task PrimaryKeyValues_ForATypeWithNoPkid_IsEmptyRatherThanAThrow()
    {
        var writer = Writer();

        // Losing the key costs the trail precision; throwing would cost the caller its write.
        await writer.LogInsertAsync("Keyless", new KeylessEntity());

        Assert.Equal(string.Empty, writer.Written.PrimaryKeyValues);
    }

    [Fact]
    public async Task LogUpdate_PrimaryKeyValues_ComesFromTheEntity()
    {
        var writer = Writer();

        await writer.LogUpdateAsync("Course", ACourse(), ACourse());

        Assert.Equal("42", writer.Written.PrimaryKeyValues);
    }

    // ---------- UserName ----------

    [Fact]
    public async Task UserName_IsTheUserNameClaimOfTheSignedInCaller()
    {
        var writer = WriterSignedInAs("admin@example.com");

        await writer.LogInsertAsync("Course", ACourse());

        Assert.Equal("admin@example.com", writer.Written.UserName);
    }

    [Fact]
    public async Task UserName_WithNoHttpContextAtAll_IsSystem()
    {
        var writer = WriterFor(null);

        await writer.LogInsertAsync("Course", ACourse());

        Assert.Equal(RowAuditWriter.SystemUserName, writer.Written.UserName);
    }

    [Fact]
    public async Task UserName_WithAnAnonymousCaller_IsSystem()
    {
        // No authentication type, so IsAuthenticated is false — what an unauthenticated request's
        // principal actually looks like.
        var writer = WriterFor(new ClaimsPrincipal(new ClaimsIdentity()));

        await writer.LogInsertAsync("Course", ACourse());

        Assert.Equal(RowAuditWriter.SystemUserName, writer.Written.UserName);
    }

    [Fact]
    public async Task UserName_WhenTheTokenCarriesNoUserName_IsSystemRatherThanTheUserId()
    {
        // JwtBearerSetup maps NameClaimType to userId, so User.Identity.Name would hand back "7"
        // and every audit row would be signed by a number.
        var writer = WriterFor(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(JwtTokenService.UserIdClaimType, "7") },
            authenticationType: "TestBearer")));

        await writer.LogInsertAsync("Course", ACourse());

        Assert.Equal(RowAuditWriter.SystemUserName, writer.Written.UserName);
    }

    [Fact]
    public async Task UserName_WhenTheUserNameClaimIsBlank_IsSystem()
    {
        var writer = WriterSignedInAs("   ");

        await writer.LogInsertAsync("Course", ACourse());

        Assert.Equal(RowAuditWriter.SystemUserName, writer.Written.UserName);
    }

    // ---------- The rest of the row ----------

    [Theory]
    [InlineData(RowAuditWriter.InsertAction)]
    [InlineData(RowAuditWriter.UpdateAction)]
    [InlineData(RowAuditWriter.DeleteAction)]
    public async Task ActionType_NamesTheOperation(string expected)
    {
        var writer = Writer();
        var course = ACourse();

        switch (expected)
        {
            case RowAuditWriter.InsertAction:
                await writer.LogInsertAsync("Course", course);
                break;
            case RowAuditWriter.UpdateAction:
                await writer.LogUpdateAsync("Course", course, ACourse());
                break;
            default:
                await writer.LogDeleteAsync("Course", course);
                break;
        }

        Assert.Equal(expected, writer.Written.ActionType);
    }

    [Fact]
    public async Task TableName_IsWrittenAsGiven()
    {
        var writer = Writer();

        await writer.LogInsertAsync("Course", ACourse());

        Assert.Equal("Course", writer.Written.TableName);
    }

    [Fact]
    public async Task DateTime_IsTheLocalTimeOfTheChange()
    {
        var writer = Writer();
        var before = DateTime.Now;

        await writer.LogInsertAsync("Course", ACourse());

        var written = writer.Written.DateTime;

        Assert.InRange(written, before.AddSeconds(-1), DateTime.Now.AddSeconds(1));
        Assert.Equal(DateTimeKind.Local, written.Kind);
    }

    [Fact]
    public async Task EachCall_WritesExactlyOneRow()
    {
        var writer = Writer();

        await writer.LogInsertAsync("Course", ACourse());
        await writer.LogUpdateAsync("Course", ACourse(), ACourse());
        await writer.LogDeleteAsync("Course", ACourse());

        Assert.Equal(3, writer.Entries.Count);
    }
}
