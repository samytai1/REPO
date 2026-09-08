using System.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class CourseRepository : ICourseRepository
{
    /// <summary>SQL Server error number for a foreign key constraint violation.</summary>
    private const int ForeignKeyViolation = 547;

    private const string OrderBySql = "\nORDER BY c.DisplayOrder ASC, c.pkid ASC";

    /// <summary>
    /// splitOn markers for the multi-map read. Each name is the first column of its nav block, and
    /// Dapper scans forward from the previous split — so the repeated Description resolves to the
    /// CourseGroup block first and the PublishStatus block second.
    /// </summary>
    private const string NavSplitOn = "Name,Description,Description";

    /// <summary>Table this repository owns, as it is written to dbo.RowAudit.TableName.</summary>
    private const string AuditTableName = "Course";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _rowAudit;

    /// <summary>
    /// Shared projection. The three FK nav blocks come last, in splitOn order; the two n-n counts
    /// are subqueries so a list read stays a single round trip.
    /// </summary>
    private const string SelectSql = @"
SELECT  c.pkid                AS Pkid,
        c.Title               AS Title,
        c.OfficialTitle       AS OfficialTitle,
        c.CourseId            AS CourseId,
        c.ProdCourseId        AS ProdCourseId,
        c.FriendlyUrl         AS FriendlyUrl,
        c.DisplayOrder        AS DisplayOrder,
        c.Partner_pkid        AS PartnerPkid,
        c.CourseGroup_pkid    AS CourseGroupPkid,
        c.PublishStatus_pkid  AS PublishStatusPkid,
        c.ScheduleOn          AS ScheduleOn,
        c.ScheduleOff         AS ScheduleOff,
        c.[Hour]              AS [Hour],
        c.ListPrice           AS ListPrice,
        c.LearningCredit      AS LearningCredit,
        c.Material            AS Material,
        c.Objective           AS Objective,
        c.Target              AS Target,
        c.Prerequisites       AS Prerequisites,
        c.Outline             AS Outline,
        c.TowardCertOrExam    AS TowardCertOrExam,
        c.Note                AS Note,
        c.OtherInfo           AS OtherInfo,
        c.CanRepeat           AS CanRepeat,
        (SELECT COUNT(1) FROM dbo.CourseJobCategories  jx WHERE jx.Course_pkid = c.pkid) AS JobCategoryCount,
        (SELECT COUNT(1) FROM dbo.CourseInCertification cx WHERE cx.Course_pkid = c.pkid) AS CertificationCount,
        p.Name                AS Name,
        p.pkid                AS Pkid,
        cg.Description        AS Description,
        cg.pkid               AS Pkid,
        ps.Description        AS Description,
        ps.pkid               AS Pkid
FROM    dbo.Course c
        INNER JOIN dbo.Partner       p  ON p.pkid  = c.Partner_pkid
        LEFT  JOIN dbo.CourseGroup   cg ON cg.pkid = c.CourseGroup_pkid
        INNER JOIN dbo.PublishStatus ps ON ps.pkid = c.PublishStatus_pkid";

    /// <summary>Writable column list, shared by INSERT and UPDATE through the parameter bag.</summary>
    private const string InsertSql = @"
INSERT INTO dbo.Course
        (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
         Partner_pkid, CourseGroup_pkid, PublishStatus_pkid,
         ScheduleOn, ScheduleOff, [Hour], ListPrice, LearningCredit,
         Material, Objective, Target, Prerequisites, Outline, TowardCertOrExam,
         Note, OtherInfo, CanRepeat)
VALUES  (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
         @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid,
         @ScheduleOn, @ScheduleOff, @Hour, @ListPrice, @LearningCredit,
         @Material, @Objective, @Target, @Prerequisites, @Outline, @TowardCertOrExam,
         @Note, @OtherInfo, @CanRepeat);
SELECT CAST(SCOPE_IDENTITY() AS int);";

    private const string UpdateSql = @"
UPDATE  dbo.Course
SET     Title              = @Title,
        OfficialTitle      = @OfficialTitle,
        CourseId           = @CourseId,
        ProdCourseId       = @ProdCourseId,
        FriendlyUrl        = @FriendlyUrl,
        DisplayOrder       = @DisplayOrder,
        Partner_pkid       = @PartnerPkid,
        CourseGroup_pkid   = @CourseGroupPkid,
        PublishStatus_pkid = @PublishStatusPkid,
        ScheduleOn         = @ScheduleOn,
        ScheduleOff        = @ScheduleOff,
        [Hour]             = @Hour,
        ListPrice          = @ListPrice,
        LearningCredit     = @LearningCredit,
        Material           = @Material,
        Objective          = @Objective,
        Target             = @Target,
        Prerequisites      = @Prerequisites,
        Outline            = @Outline,
        TowardCertOrExam   = @TowardCertOrExam,
        Note               = @Note,
        OtherInfo          = @OtherInfo,
        CanRepeat          = @CanRepeat
WHERE   pkid = @Pkid";

    private const string JobCategoriesSql = @"
SELECT  jc.pkid         AS Pkid,
        jc.Description  AS Description
FROM    dbo.CourseJobCategories jx
        INNER JOIN dbo.JobCategory jc ON jc.pkid = jx.JobCategory_pkid
WHERE   jx.Course_pkid = @Pkid
ORDER BY jc.Description ASC";

    private const string CertificationsSql = @"
SELECT  ct.pkid          AS Pkid,
        RTRIM(ct.Title)  AS Title,
        ct.Partner_pkid  AS PartnerPkid
FROM    dbo.CourseInCertification cx
        INNER JOIN dbo.Certification ct ON ct.pkid = cx.Certification_pkid
WHERE   cx.Course_pkid = @Pkid
ORDER BY RTRIM(ct.Title) ASC";

    public CourseRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter rowAudit)
    {
        _connectionFactory = connectionFactory;
        _rowAudit = rowAudit;
    }

    public async Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await QueryWithNavAsync(connection, null, SelectSql + OrderBySql, null, cancellationToken);
    }

    public async Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add(@"(c.Title LIKE @Keyword
              OR c.OfficialTitle LIKE @Keyword
              OR c.CourseId LIKE @Keyword
              OR c.ProdCourseId LIKE @Keyword
              OR c.FriendlyUrl LIKE @Keyword)");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        if (query.PartnerPkid.HasValue)
        {
            where.Add("c.Partner_pkid = @PartnerPkid");
            parameters.Add("PartnerPkid", query.PartnerPkid.Value);
        }

        if (query.CourseGroupPkid.HasValue)
        {
            where.Add("c.CourseGroup_pkid = @CourseGroupPkid");
            parameters.Add("CourseGroupPkid", query.CourseGroupPkid.Value);
        }

        if (query.PublishStatusPkid.HasValue)
        {
            where.Add("c.PublishStatus_pkid = @PublishStatusPkid");
            parameters.Add("PublishStatusPkid", query.PublishStatusPkid.Value);
        }

        if (query.CanRepeat.HasValue)
        {
            where.Add("c.CanRepeat = @CanRepeat");
            parameters.Add("CanRepeat", query.CanRepeat.Value);
        }

        if (query.ScheduleOnFrom.HasValue)
        {
            where.Add("c.ScheduleOn >= @ScheduleOnFrom");
            parameters.Add("ScheduleOnFrom", query.ScheduleOnFrom.Value);
        }

        if (query.ScheduleOnTo.HasValue)
        {
            where.Add("c.ScheduleOn <= @ScheduleOnTo");
            parameters.Add("ScheduleOnTo", query.ScheduleOnTo.Value);
        }

        if (query.ScheduleOffFrom.HasValue)
        {
            where.Add("c.ScheduleOff >= @ScheduleOffFrom");
            parameters.Add("ScheduleOffFrom", query.ScheduleOffFrom.Value);
        }

        if (query.ScheduleOffTo.HasValue)
        {
            where.Add("c.ScheduleOff <= @ScheduleOffTo");
            parameters.Add("ScheduleOffTo", query.ScheduleOffTo.Value);
        }

        var sql = SelectSql
            + (where.Count > 0 ? "\nWHERE " + string.Join(" AND ", where) : string.Empty)
            + OrderBySql;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await QueryWithNavAsync(connection, null, sql, parameters, cancellationToken);
    }

    public async Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, null, pkid, cancellationToken);
    }

    public async Task<Course> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // pkid is an IDENTITY column, so it is left out of the INSERT and read back afterwards.
        var pkid = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            InsertSql,
            ToParameters(request),
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceJunctionsAsync(connection, transaction, pkid, request, cancellationToken);

        var created = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

        // Inside the transaction: a failed INSERT leaves no row and no audit row.
        await _rowAudit.LogInsertAsync(connection, transaction, AuditTableName, created!, cancellationToken);

        transaction.Commit();

        return created!;
    }

    public async Task<Course?> UpdateAsync(CourseUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Read first — nav objects, junction counts and all: the changed-column list is the
        // difference between this row and the one the UPDATE leaves behind.
        var before = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);

        if (before is null)
        {
            transaction.Rollback();
            return null;
        }

        var parameters = ToParameters(request);
        parameters.Add("Pkid", request.Pkid);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return null;
        }

        await ReplaceJunctionsAsync(connection, transaction, request.Pkid, request, cancellationToken);

        var updated = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);

        await _rowAudit.LogUpdateAsync(connection, transaction, AuditTableName, before, updated!, cancellationToken);

        transaction.Commit();

        return updated;
    }

    public async Task<CourseDeleteResult> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // Read before deleting; once the row is gone its CourseId is the only human-readable
            // trace the trail can keep.
            var deleting = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

            if (deleting is null)
            {
                transaction.Rollback();
                return CourseDeleteResult.NotFound;
            }

            // Both junctions declare ON DELETE CASCADE; clearing them here keeps the intent explicit.
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.CourseJobCategories WHERE Course_pkid = @Pkid",
                new { Pkid = pkid },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.CourseInCertification WHERE Course_pkid = @Pkid",
                new { Pkid = pkid },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.Course WHERE pkid = @Pkid",
                new { Pkid = pkid },
                transaction,
                cancellationToken: cancellationToken));

            await _rowAudit.LogDeleteAsync(connection, transaction, AuditTableName, deleting, cancellationToken);

            transaction.Commit();

            return CourseDeleteResult.Deleted;
        }
        catch (SqlException ex) when (ex.Number == ForeignKeyViolation)
        {
            // CourseFAQ / CourseRelatedLink / HotCourse still point at this row (none of them cascade).
            transaction.Rollback();
            return CourseDeleteResult.InUse;
        }
    }

    /// <summary>
    /// Runs the shared projection through the four-way multi-map that fills the FK nav objects.
    /// </summary>
    private static async Task<IEnumerable<Course>> QueryWithNavAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        string sql,
        object? parameters,
        CancellationToken cancellationToken)
    {
        return await connection.QueryAsync<Course, CoursePartnerRef, CourseGroupRef, CoursePublishStatusRef, Course>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken),
            (course, partner, courseGroup, publishStatus) =>
            {
                course.Partner = partner;
                // A LEFT JOIN miss still materialises an all-null ref, so key off the FK itself.
                course.CourseGroup = course.CourseGroupPkid.HasValue ? courseGroup : null;
                course.PublishStatus = publishStatus;
                return course;
            },
            splitOn: NavSplitOn);
    }

    /// <summary>Reads a single course plus both n-n collections on an existing connection.</summary>
    private static async Task<Course?> GetByIdAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        int pkid,
        CancellationToken cancellationToken)
    {
        var rows = await QueryWithNavAsync(
            connection,
            transaction,
            SelectSql + "\nWHERE c.pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken);

        var course = rows.FirstOrDefault();

        if (course is null) return null;

        var jobCategories = await connection.QueryAsync<JobCategoryLookup>(new CommandDefinition(
            JobCategoriesSql,
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));

        var certifications = await connection.QueryAsync<CertificationLookup>(new CommandDefinition(
            CertificationsSql,
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));

        course.JobCategories = jobCategories.ToList();
        course.Certifications = certifications.ToList();

        return course;
    }

    /// <summary>n-n: delete-then-reinsert the junction rows for both relationships.</summary>
    private static async Task ReplaceJunctionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int pkid,
        CourseRequest request,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.CourseJobCategories WHERE Course_pkid = @Pkid",
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));

        // The junction PK is composite, so a repeated id would violate it.
        var jobCategoryPkids = request.JobCategoryPkids.Distinct().ToList();

        if (jobCategoryPkids.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.CourseJobCategories (Course_pkid, JobCategory_pkid) VALUES (@Pkid, @JobCategoryPkid)",
                jobCategoryPkids.Select(id => new { Pkid = pkid, JobCategoryPkid = id }),
                transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.CourseInCertification WHERE Course_pkid = @Pkid",
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));

        var certificationPkids = request.CertificationPkids.Distinct().ToList();

        if (certificationPkids.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.CourseInCertification (Course_pkid, Certification_pkid) VALUES (@Pkid, @CertificationPkid)",
                certificationPkids.Select(id => new { Pkid = pkid, CertificationPkid = id }),
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    /// <summary>Trims the writable columns and normalises every blank nullable column to null.</summary>
    private static DynamicParameters ToParameters(CourseRequest request)
    {
        var parameters = new DynamicParameters();

        parameters.Add("Title", request.Title.Trim());
        parameters.Add("OfficialTitle", NullIfBlank(request.OfficialTitle));
        parameters.Add("CourseId", request.CourseId.Trim());
        parameters.Add("ProdCourseId", request.ProdCourseId.Trim());
        parameters.Add("FriendlyUrl", request.FriendlyUrl.Trim());
        parameters.Add("DisplayOrder", request.DisplayOrder);
        parameters.Add("PartnerPkid", request.PartnerPkid);
        parameters.Add("CourseGroupPkid", request.CourseGroupPkid);
        parameters.Add("PublishStatusPkid", request.PublishStatusPkid);
        parameters.Add("ScheduleOn", request.ScheduleOn);
        parameters.Add("ScheduleOff", request.ScheduleOff);
        parameters.Add("Hour", request.Hour);
        parameters.Add("ListPrice", request.ListPrice);
        parameters.Add("LearningCredit", request.LearningCredit);
        parameters.Add("Material", NullIfBlank(request.Material));
        parameters.Add("Objective", NullIfBlank(request.Objective));
        parameters.Add("Target", NullIfBlank(request.Target));
        parameters.Add("Prerequisites", NullIfBlank(request.Prerequisites));
        parameters.Add("Outline", NullIfBlank(request.Outline));
        parameters.Add("TowardCertOrExam", NullIfBlank(request.TowardCertOrExam));
        parameters.Add("Note", NullIfBlank(request.Note));
        parameters.Add("OtherInfo", NullIfBlank(request.OtherInfo));
        parameters.Add("CanRepeat", request.CanRepeat);

        return parameters;
    }

    /// <summary>A nullable column stores null, never an empty or whitespace-only string.</summary>
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
