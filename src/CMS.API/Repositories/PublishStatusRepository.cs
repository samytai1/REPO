using System.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class PublishStatusRepository : IPublishStatusRepository
{
    /// <summary>SQL Server error number for a foreign key constraint violation.</summary>
    private const int ForeignKeyViolation = 547;

    /// <summary>Table this repository owns, as it is written to dbo.RowAudit.TableName.</summary>
    private const string AuditTableName = "PublishStatus";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _rowAudit;

    /// <summary>Shared projection. No FKs and no junction tables, so this is a plain single-table read.</summary>
    private const string SelectSql = @"
SELECT  s.pkid            AS Pkid,
        s.Description     AS Description,
        s.IsDraft         AS IsDraft,
        s.IsPublished     AS IsPublished,
        s.IsDiscontinued  AS IsDiscontinued
FROM    dbo.PublishStatus s";

    public PublishStatusRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter rowAudit)
    {
        _connectionFactory = connectionFactory;
        _rowAudit = rowAudit;
    }

    public async Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<PublishStatus>(new CommandDefinition(
            SelectSql + "\nORDER BY s.pkid ASC",
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("s.Description LIKE @Keyword");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        if (query.PkidFrom.HasValue)
        {
            where.Add("s.pkid >= @PkidFrom");
            parameters.Add("PkidFrom", query.PkidFrom.Value);
        }

        if (query.PkidTo.HasValue)
        {
            where.Add("s.pkid <= @PkidTo");
            parameters.Add("PkidTo", query.PkidTo.Value);
        }

        if (query.IsDraft.HasValue)
        {
            where.Add("s.IsDraft = @IsDraft");
            parameters.Add("IsDraft", query.IsDraft.Value);
        }

        if (query.IsPublished.HasValue)
        {
            where.Add("s.IsPublished = @IsPublished");
            parameters.Add("IsPublished", query.IsPublished.Value);
        }

        if (query.IsDiscontinued.HasValue)
        {
            where.Add("s.IsDiscontinued = @IsDiscontinued");
            parameters.Add("IsDiscontinued", query.IsDiscontinued.Value);
        }

        var sql = SelectSql
            + (where.Count > 0 ? "\nWHERE " + string.Join(" AND ", where) : string.Empty)
            + "\nORDER BY s.pkid ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<PublishStatus>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, null, pkid, cancellationToken);
    }

    public async Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.PublishStatus WHERE pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<PublishStatus> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // pkid is not an IDENTITY column, so it is written like any other value.
        await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued)",
            new
            {
                request.Pkid,
                Description = request.Description.Trim(),
                request.IsDraft,
                request.IsPublished,
                request.IsDiscontinued
            },
            transaction,
            cancellationToken: cancellationToken));

        var created = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);

        // Inside the transaction: a failed INSERT leaves no row and no audit row.
        await _rowAudit.LogInsertAsync(connection, transaction, AuditTableName, created!, cancellationToken);

        transaction.Commit();

        return created!;
    }

    public async Task<PublishStatus?> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Read first: the audit trail's changed-column list is the difference between this row and
        // the one the UPDATE leaves behind, so it cannot be worked out afterwards.
        var before = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);

        if (before is null)
        {
            transaction.Rollback();
            return null;
        }

        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE  dbo.PublishStatus
SET     Description    = @Description,
        IsDraft        = @IsDraft,
        IsPublished    = @IsPublished,
        IsDiscontinued = @IsDiscontinued
WHERE   pkid = @Pkid",
            new
            {
                request.Pkid,
                Description = request.Description.Trim(),
                request.IsDraft,
                request.IsPublished,
                request.IsDiscontinued
            },
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return null;
        }

        var updated = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);

        await _rowAudit.LogUpdateAsync(connection, transaction, AuditTableName, before, updated!, cancellationToken);

        transaction.Commit();

        return updated;
    }

    public async Task<PublishStatusDeleteResult> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // A transaction the delete did not need before the audit trail existed: the row and the
        // record of its removal have to land — or not land — together.
        using var transaction = connection.BeginTransaction();

        try
        {
            // Read before deleting; once the row is gone its Description is unrecoverable, and that
            // description is the only human-readable trace the trail can keep.
            var deleting = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

            if (deleting is null)
            {
                transaction.Rollback();
                return PublishStatusDeleteResult.NotFound;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.PublishStatus WHERE pkid = @Pkid",
                new { Pkid = pkid },
                transaction,
                cancellationToken: cancellationToken));

            await _rowAudit.LogDeleteAsync(connection, transaction, AuditTableName, deleting, cancellationToken);

            transaction.Commit();

            return PublishStatusDeleteResult.Deleted;
        }
        catch (SqlException ex) when (ex.Number == ForeignKeyViolation)
        {
            // Course.PublishStatus_pkid / Promotion2.PublishStatus_pkid still point at this row.
            transaction.Rollback();
            return PublishStatusDeleteResult.InUse;
        }
    }

    /// <summary>Reads a single row on an existing connection, optionally inside a transaction.</summary>
    private static async Task<PublishStatus?> GetByIdAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        byte pkid,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<PublishStatus>(new CommandDefinition(
            SelectSql + "\nWHERE s.pkid = @Pkid",
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));
    }
}
