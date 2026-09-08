using System.Data;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <inheritdoc />
public class FeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private const string OrderBySql = "\nORDER BY f.ScheduleOn ASC, f.TrainingCenter_pkid ASC, f.Slot ASC";

    /// <summary>splitOn markers — each is the first column of its nav block (see docs/backend.md).</summary>
    private const string NavSplitOn = "PromoCode,Name";

    /// <summary>
    /// A slot value outside 1–3 used only inside the swap transaction, so the UNIQUE key never
    /// collides mid-swap. Never visible outside <see cref="MoveSlotAsync"/>.
    /// </summary>
    private const byte ParkingSlot = 0;

    /// <summary>Table this repository owns, as it is written to dbo.RowAudit.TableName.</summary>
    private const string AuditTableName = "FeaturedPromoItem";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IRowAuditWriter _rowAudit;

    /// <summary>Shared projection. The two FK nav blocks come last, in splitOn order.</summary>
    private const string SelectSql = @"
SELECT  f.pkid                 AS Pkid,
        f.ScheduleOn           AS ScheduleOn,
        f.TrainingCenter_pkid  AS TrainingCenterPkid,
        f.Slot                 AS Slot,
        f.Promotion_pkid       AS PromotionPkid,
        f.Topic                AS Topic,
        f.Description          AS Description,
        p.PromoCode            AS PromoCode,
        p.pkid                 AS Pkid,
        tc.Name                AS Name,
        tc.pkid                AS Pkid
FROM    dbo.FeaturedPromoItem f
        INNER JOIN dbo.Promotion2     p  ON p.pkid  = f.Promotion_pkid
        INNER JOIN dbo.TrainingCenter tc ON tc.pkid = f.TrainingCenter_pkid";

    public FeaturedPromoItemRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter rowAudit)
    {
        _connectionFactory = connectionFactory;
        _rowAudit = rowAudit;
    }

    public async Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await QueryWithNavAsync(connection, null, SelectSql + OrderBySql, null, cancellationToken);
    }

    public async Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (query.TrainingCenterPkid.HasValue)
        {
            where.Add("f.TrainingCenter_pkid = @TrainingCenterPkid");
            parameters.Add("TrainingCenterPkid", query.TrainingCenterPkid.Value);
        }

        if (query.WeekOf.HasValue)
        {
            var (monday, sunday) = WeekOf(query.WeekOf.Value);
            where.Add("f.ScheduleOn >= @WeekStart AND f.ScheduleOn <= @WeekEnd");
            parameters.Add("WeekStart", monday);
            parameters.Add("WeekEnd", sunday);
        }

        var sql = SelectSql
            + (where.Count > 0 ? "\nWHERE " + string.Join(" AND ", where) : string.Empty)
            + OrderBySql;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await QueryWithNavAsync(connection, null, sql, parameters, cancellationToken);
    }

    public async Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await GetByIdAsync(connection, null, pkid, cancellationToken);
    }

    public async Task<bool> IsSlotTakenAsync(
        DateOnly scheduleOn,
        short trainingCenterPkid,
        byte slot,
        int? excludePkid,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT  COUNT(1)
FROM    dbo.FeaturedPromoItem f
WHERE   f.ScheduleOn = @ScheduleOn
        AND f.TrainingCenter_pkid = @TrainingCenterPkid
        AND f.Slot = @Slot
        AND (@ExcludePkid IS NULL OR f.pkid <> @ExcludePkid)",
            new { ScheduleOn = scheduleOn, TrainingCenterPkid = trainingCenterPkid, Slot = slot, ExcludePkid = excludePkid },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<FeaturedPromoItem> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // pkid is an IDENTITY column, so it is left out of the INSERT and read back afterwards.
        var pkid = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.FeaturedPromoItem
        (ScheduleOn, TrainingCenter_pkid, Slot, Promotion_pkid, Topic, Description)
VALUES  (@ScheduleOn, @TrainingCenterPkid, @Slot, @PromotionPkid, @Topic, @Description);
SELECT CAST(SCOPE_IDENTITY() AS int);",
            ToParameters(request),
            transaction,
            cancellationToken: cancellationToken));

        var created = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

        // Inside the transaction: a failed INSERT leaves no row and no audit row.
        await _rowAudit.LogInsertAsync(connection, transaction, AuditTableName, created!, cancellationToken);

        transaction.Commit();

        return created!;
    }

    public async Task<FeaturedPromoItem?> UpdateAsync(FeaturedPromoItemUpdateRequest request, CancellationToken cancellationToken = default)
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
UPDATE  dbo.FeaturedPromoItem
SET     ScheduleOn          = @ScheduleOn,
        TrainingCenter_pkid = @TrainingCenterPkid,
        Slot                = @Slot,
        Promotion_pkid      = @PromotionPkid,
        Topic               = @Topic,
        Description         = @Description
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

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // A transaction the delete did not need before the audit trail existed: the row and the
        // record of its removal have to land — or not land — together.
        using var transaction = connection.BeginTransaction();

        // Read before deleting; once the row is gone its Topic is the only human-readable trace the
        // trail can keep.
        var deleting = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

        if (deleting is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.FeaturedPromoItem WHERE pkid = @Pkid",
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));

        await _rowAudit.LogDeleteAsync(connection, transaction, AuditTableName, deleting, cancellationToken);

        transaction.Commit();

        return true;
    }

    public async Task<FeaturedPromoItem?> MoveSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var item = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

        if (item is null)
        {
            transaction.Rollback();
            return null;
        }

        if (item.Slot == targetSlot)
        {
            transaction.Commit();
            return item;
        }

        var occupantPkid = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
SELECT  f.pkid
FROM    dbo.FeaturedPromoItem f
WHERE   f.ScheduleOn = @ScheduleOn
        AND f.TrainingCenter_pkid = @TrainingCenterPkid
        AND f.Slot = @Slot",
            new { item.ScheduleOn, item.TrainingCenterPkid, Slot = targetSlot },
            transaction,
            cancellationToken: cancellationToken));

        // The occupant's before-picture, read while it still has its old slot: a swap moves two
        // rows, and the trail owes a row for each of them.
        var occupant = occupantPkid.HasValue
            ? await GetByIdAsync(connection, transaction, occupantPkid.Value, cancellationToken)
            : null;

        // (ScheduleOn, TrainingCenter_pkid, Slot) is UNIQUE, so a swap has to go through a parking
        // slot: occupant → 0, item → target, occupant → item's old slot.
        if (occupantPkid.HasValue)
        {
            await SetSlotAsync(connection, transaction, occupantPkid.Value, ParkingSlot, cancellationToken);
        }

        await SetSlotAsync(connection, transaction, pkid, targetSlot, cancellationToken);

        if (occupantPkid.HasValue)
        {
            await SetSlotAsync(connection, transaction, occupantPkid.Value, item.Slot, cancellationToken);
        }

        var moved = await GetByIdAsync(connection, transaction, pkid, cancellationToken);

        // A move is an update like any other — it just happens to be spelled as three statements.
        // The parking slot never reaches the trail: only the before and after states do.
        await _rowAudit.LogUpdateAsync(connection, transaction, AuditTableName, item, moved!, cancellationToken);

        if (occupant is not null)
        {
            var occupantAfter = await GetByIdAsync(connection, transaction, occupant.Pkid, cancellationToken);

            await _rowAudit.LogUpdateAsync(connection, transaction, AuditTableName, occupant, occupantAfter!, cancellationToken);
        }

        transaction.Commit();

        return moved;
    }

    /// <summary>Monday and Sunday of the week containing <paramref name="date"/>.</summary>
    public static (DateOnly Monday, DateOnly Sunday) WeekOf(DateOnly date)
    {
        // DayOfWeek: Sunday = 0 … Saturday = 6. Shift so Monday = 0 … Sunday = 6.
        var offset = ((int)date.DayOfWeek + 6) % 7;
        var monday = date.AddDays(-offset);
        return (monday, monday.AddDays(6));
    }

    private static async Task SetSlotAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int pkid,
        byte slot,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.FeaturedPromoItem SET Slot = @Slot WHERE pkid = @Pkid",
            new { Pkid = pkid, Slot = slot },
            transaction,
            cancellationToken: cancellationToken));
    }

    /// <summary>Runs the shared projection through the multi-map that fills the FK nav objects.</summary>
    private static async Task<IEnumerable<FeaturedPromoItem>> QueryWithNavAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        string sql,
        object? parameters,
        CancellationToken cancellationToken)
    {
        return await connection.QueryAsync<FeaturedPromoItem, FeaturedPromoItemPromotionRef, FeaturedPromoItemTrainingCenterRef, FeaturedPromoItem>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken),
            (item, promotion, trainingCenter) =>
            {
                item.Promotion = promotion;
                item.TrainingCenter = trainingCenter;
                return item;
            },
            splitOn: NavSplitOn);
    }

    /// <summary>Reads a single item on an existing connection, optionally inside a transaction.</summary>
    private static async Task<FeaturedPromoItem?> GetByIdAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        int pkid,
        CancellationToken cancellationToken)
    {
        var rows = await QueryWithNavAsync(
            connection,
            transaction,
            SelectSql + "\nWHERE f.pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken);

        return rows.FirstOrDefault();
    }

    /// <summary>Trims the writable text columns. Nothing here is nullable.</summary>
    private static DynamicParameters ToParameters(FeaturedPromoItemRequest request)
    {
        var parameters = new DynamicParameters();

        parameters.Add("ScheduleOn", request.ScheduleOn);
        parameters.Add("TrainingCenterPkid", request.TrainingCenterPkid);
        parameters.Add("Slot", request.Slot);
        parameters.Add("PromotionPkid", request.PromotionPkid);
        parameters.Add("Topic", request.Topic.Trim());
        parameters.Add("Description", request.Description.Trim());

        return parameters;
    }
}
