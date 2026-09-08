using System.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class PartnerRepository : IPartnerRepository
{
    /// <summary>SQL Server error number for a foreign key constraint violation.</summary>
    private const int ForeignKeyViolation = 547;

    private const string OrderBySql = "\nORDER BY p.DisplayOrder ASC, p.pkid ASC";

    /// <summary>Table this repository owns, as it is written to dbo.RowAudit.TableName.</summary>
    private const string AuditTableName = "Partner";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _rowAudit;

    /// <summary>Shared projection. No FKs and no junction tables, so this is a plain single-table read.</summary>
    private const string SelectSql = @"
SELECT  p.pkid                    AS Pkid,
        p.Name                    AS Name,
        p.AppKey                  AS AppKey,
        p.NameOnPartnerMenu       AS NameOnPartnerMenu,
        p.NameOnCourseDetailPage  AS NameOnCourseDetailPage,
        p.DisplayOrder            AS DisplayOrder,
        p.ImageFilename           AS ImageFilename
FROM    dbo.Partner p";

    public PartnerRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter rowAudit)
    {
        _connectionFactory = connectionFactory;
        _rowAudit = rowAudit;
    }

    public async Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<Partner>(new CommandDefinition(
            SelectSql + OrderBySql,
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add(@"(p.Name LIKE @Keyword
              OR p.AppKey LIKE @Keyword
              OR p.NameOnPartnerMenu LIKE @Keyword
              OR p.NameOnCourseDetailPage LIKE @Keyword)");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        if (query.DisplayOrderFrom.HasValue)
        {
            where.Add("p.DisplayOrder >= @DisplayOrderFrom");
            parameters.Add("DisplayOrderFrom", query.DisplayOrderFrom.Value);
        }

        if (query.DisplayOrderTo.HasValue)
        {
            where.Add("p.DisplayOrder <= @DisplayOrderTo");
            parameters.Add("DisplayOrderTo", query.DisplayOrderTo.Value);
        }

        if (query.HasImage.HasValue)
        {
            // ImageFilename is nullable and can also hold blanks, so "has an image" is a derived
            // predicate rather than a bit column.
            where.Add(query.HasImage.Value
                ? "(p.ImageFilename IS NOT NULL AND LTRIM(RTRIM(p.ImageFilename)) <> '')"
                : "(p.ImageFilename IS NULL OR LTRIM(RTRIM(p.ImageFilename)) = '')");
        }

        var sql = SelectSql
            + (where.Count > 0 ? "\nWHERE " + string.Join(" AND ", where) : string.Empty)
            + OrderBySql;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QueryAsync<Partner>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, null, pkid, cancellationToken);
    }

    public async Task<Partner> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // pkid is an IDENTITY column, so it is left out of the INSERT and read back afterwards.
        var pkid = await connection.ExecuteScalarAsync<short>(new CommandDefinition(@"
INSERT INTO dbo.Partner
        (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES  (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);",
            ToParameters(request),
            transaction,
            cancellationToken: cancellationToken));

        var created = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

        // Inside the transaction: a failed INSERT leaves no row and no audit row.
        await _rowAudit.LogInsertAsync(connection, transaction, AuditTableName, created!, cancellationToken);

        transaction.Commit();

        return created!;
    }

    public async Task<Partner?> UpdateAsync(PartnerUpdateRequest request, CancellationToken cancellationToken = default)
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

        var parameters = ToParameters(request);
        parameters.Add("Pkid", request.Pkid);

        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE  dbo.Partner
SET     Name                   = @Name,
        AppKey                 = @AppKey,
        NameOnPartnerMenu      = @NameOnPartnerMenu,
        NameOnCourseDetailPage = @NameOnCourseDetailPage,
        DisplayOrder           = @DisplayOrder,
        ImageFilename          = @ImageFilename
WHERE   pkid = @Pkid",
            parameters,
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

    public async Task<PartnerDeleteResult> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // A transaction the delete did not need before the audit trail existed: the row and the
        // record of its removal have to land — or not land — together.
        using var transaction = connection.BeginTransaction();

        try
        {
            // Read before deleting; once the row is gone its Name is unrecoverable, and that name is
            // the only human-readable trace the trail can keep.
            var deleting = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

            if (deleting is null)
            {
                transaction.Rollback();
                return PartnerDeleteResult.NotFound;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.Partner WHERE pkid = @Pkid",
                new { Pkid = pkid },
                transaction,
                cancellationToken: cancellationToken));

            await _rowAudit.LogDeleteAsync(connection, transaction, AuditTableName, deleting, cancellationToken);

            transaction.Commit();

            return PartnerDeleteResult.Deleted;
        }
        catch (SqlException ex) when (ex.Number == ForeignKeyViolation)
        {
            // Certification / Course / PartnerCourseGroup still point at this row.
            transaction.Rollback();
            return PartnerDeleteResult.InUse;
        }
    }

    /// <summary>Trims the writable columns and normalises a blank ImageFilename to null.</summary>
    private static DynamicParameters ToParameters(PartnerRequest request)
    {
        var parameters = new DynamicParameters();

        parameters.Add("Name", request.Name.Trim());
        parameters.Add("AppKey", request.AppKey.Trim());
        parameters.Add("NameOnPartnerMenu", request.NameOnPartnerMenu.Trim());
        parameters.Add("NameOnCourseDetailPage", request.NameOnCourseDetailPage.Trim());
        parameters.Add("DisplayOrder", request.DisplayOrder);
        parameters.Add("ImageFilename", string.IsNullOrWhiteSpace(request.ImageFilename)
            ? null
            : request.ImageFilename.Trim());

        return parameters;
    }

    /// <summary>Reads a single row on an existing connection, optionally inside a transaction.</summary>
    private static async Task<Partner?> GetByIdAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        short pkid,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            SelectSql + "\nWHERE p.pkid = @Pkid",
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));
    }
}
