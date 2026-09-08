using System.Data;
using System.Text.RegularExpressions;
using CMS.API.Infrastructure;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Tests;

/// <summary>
/// A throwaway SQL Server database, created for one test class and dropped after it.
///
/// It exists because 異動紀錄 is the one thing in this codebase that an in-memory fake cannot show.
/// The promise the audit trail makes — "a change that is rolled back leaves no audit row" — is a
/// promise about a transaction, and a fake has no transactions; a repository test that swapped the
/// database out would assert nothing about the property that matters.
///
/// The schema is built by running <c>database\admin.sql</c> itself (copied beside the test binary),
/// so the tables under test are the real ones, column types, collation and all. Nothing is
/// hand-written here and nothing can drift.
/// </summary>
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    /// <summary>Where the throwaway database is created. Override with CMS_TEST_SQLSERVER.</summary>
    public static string ServerConnectionString =>
        Environment.GetEnvironmentVariable("CMS_TEST_SQLSERVER")
        ?? @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

    /// <summary>
    /// Whether a SQL Server is actually there. Probed once per test run — a developer without the
    /// local instance gets these tests skipped, not failed, exactly as the rest of the suite runs
    /// with no database at all.
    /// </summary>
    public static bool IsAvailable => AvailabilityProbe.Value;

    private static readonly Lazy<bool> AvailabilityProbe = new(() =>
    {
        try
        {
            using var connection = new SqlConnection(ServerConnectionString);
            connection.Open();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    });

    /// <summary>Unique per fixture, so two test classes never share a database.</summary>
    public string DatabaseName { get; } = "CMS_RowAuditTests_" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Hands repositories connections to the throwaway database.</summary>
    public IDbConnectionFactory ConnectionFactory { get; private set; } = null!;

    /// <summary>
    /// The throwaway database's connection string, for a test that has to configure the real host
    /// rather than build a repository by hand.
    /// </summary>
    public string ConnectionStringForTests => DatabaseConnectionString;

    private string DatabaseConnectionString =>
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = DatabaseName }.ConnectionString;

    public async Task InitializeAsync()
    {
        if (!IsAvailable) return;

        // The same handlers Program.cs installs before the host is built.
        DapperConfig.Register();

        await ExecuteOnServerAsync($"CREATE DATABASE [{DatabaseName}];");

        var ddl = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "database", "admin.sql"));

        using var connection = new SqlConnection(DatabaseConnectionString);
        await connection.OpenAsync();

        foreach (var batch in Batches(ddl))
        {
            await connection.ExecuteAsync(batch);
        }

        ConnectionFactory = new FixtureConnectionFactory(DatabaseConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable) return;

        // Pooled connections would otherwise keep the database in use and block the drop.
        SqlConnection.ClearAllPools();

        await ExecuteOnServerAsync(
            $"IF DB_ID('{DatabaseName}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{DatabaseName}]; END;");
    }

    /// <summary>Empties every table this suite writes to, so each test starts from nothing.</summary>
    public async Task ResetAsync()
    {
        using var connection = new SqlConnection(DatabaseConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
DELETE FROM dbo.AppUserRole;
DELETE FROM dbo.AppRole;
DELETE FROM dbo.AppUser;
DELETE FROM dbo.PublishStatus;
DELETE FROM dbo.RowAudit;");
    }

    /// <summary>Every dbo.RowAudit row, oldest first — the audit trail as a test can read it.</summary>
    public async Task<IReadOnlyList<RowAuditRow>> AuditRowsAsync()
    {
        using var connection = new SqlConnection(DatabaseConnectionString);
        await connection.OpenAsync();

        var rows = await connection.QueryAsync<RowAuditRow>(@"
SELECT  pkid             AS Pkid,
        TableName        AS TableName,
        UserName         AS UserName,
        PrimaryKeyValues AS PrimaryKeyValues,
        ActionType       AS ActionType,
        ActionDesc       AS ActionDesc,
        [DateTime]       AS [DateTime]
FROM    dbo.RowAudit
ORDER BY pkid ASC");

        return rows.ToList();
    }

    /// <summary>Opens a connection to the throwaway database for a test to use directly.</summary>
    public async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(DatabaseConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteOnServerAsync(string sql)
    {
        using var connection = new SqlConnection(ServerConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql);
    }

    /// <summary>
    /// Splits an SSMS-exported script on its <c>GO</c> separators — they are a client instruction,
    /// not T-SQL, so ADO.NET cannot run the file whole. The <c>USE [CMS]</c> line goes with them:
    /// the connection is already pointed at the throwaway database.
    /// </summary>
    private static IEnumerable<string> Batches(string script)
        => Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(batch => Regex.Replace(batch, @"^\s*USE\s*\[[^\]]+\]\s*$", string.Empty, RegexOptions.Multiline))
            .Where(batch => !string.IsNullOrWhiteSpace(batch));

    private sealed class FixtureConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public FixtureConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}

/// <summary>One dbo.RowAudit row as a test reads it back.</summary>
public sealed class RowAuditRow
{
    public int Pkid { get; set; }

    public string TableName { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string PrimaryKeyValues { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public string? ActionDesc { get; set; }

    public DateTime DateTime { get; set; }
}

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself when no SQL Server is reachable, so the suite
/// stays green on a machine that has never had the CMS database — the other 400 tests need none.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerDatabaseFixture.IsAvailable)
        {
            Skip = $"No SQL Server reachable at '{SqlServerDatabaseFixture.ServerConnectionString}'.";
        }
    }
}
