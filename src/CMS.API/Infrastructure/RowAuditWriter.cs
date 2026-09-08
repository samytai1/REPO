using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Security.Claims;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Infrastructure;

/// <inheritdoc />
public class RowAuditWriter : IRowAuditWriter
{
    /// <summary>異動類型 written for a create.</summary>
    public const string InsertAction = "Insert";

    /// <summary>異動類型 written for an edit.</summary>
    public const string UpdateAction = "Update";

    /// <summary>異動類型 written for a delete.</summary>
    public const string DeleteAction = "Delete";

    /// <summary>UserName written when the change has no signed-in caller behind it.</summary>
    public const string SystemUserName = "system";

    /// <summary>Property that carries the audited row's key. Matched case-insensitively.</summary>
    public const string PrimaryKeyPropertyName = "pkid";

    /// <summary>Separator between the changed column names in an update's ActionDesc.</summary>
    public const string ChangedPropertySeparator = ", ";

    /// <summary>
    /// How deep the structural comparison follows nested objects. A model here nests one level
    /// (a nav object, or a list of lookups); the cap only exists so an unexpected graph cannot
    /// recurse without end.
    /// </summary>
    private const int MaxComparisonDepth = 8;

    /// <summary>
    /// Widest ActionDesc dbo.RowAudit accepts — and the units matter. The column is
    /// <c>varchar(1000)</c>, so that is 1000 **bytes**, and the database collates as
    /// <c>Chinese_Taiwan_Stroke_CI_AS</c> (CP950), where a Chinese character costs two of them.
    /// A 1000-character 課程名稱 is 2000 bytes and SQL Server rejects the whole INSERT. So this is a
    /// byte budget, which — since no character costs less than one byte — also caps the text at
    /// 1000 characters.
    /// </summary>
    public const int ActionDescMaxBytes = 1000;

    // Remaining column widths from database\admin.sql. Everything is truncated to fit rather than
    // letting SQL Server reject the INSERT: an over-long title must not fail the business write
    // that the audit row is merely describing. These three need no byte arithmetic — the two
    // nvarchar columns are counted in characters, and TableName / ActionType are ASCII by
    // construction.
    private const int TableNameMaxLength = 50;
    private const int UserNameMaxLength = 100;
    private const int PrimaryKeyValuesMaxLength = 100;
    private const int ActionTypeMaxLength = 20;

    private const string InsertSql = @"
INSERT INTO dbo.RowAudit
        (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
VALUES  (@TableName, @UserName, @PrimaryKeyValues, @ActionType, @ActionDesc, @DateTime);";

    /// <summary>
    /// Reflection is per type, not per row: a list edit of a hundred rows would otherwise walk the
    /// same metadata a hundred times.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RowAuditWriter(IDbConnectionFactory connectionFactory, IHttpContextAccessor httpContextAccessor)
    {
        _connectionFactory = connectionFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task LogInsertAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default)
        where T : class
        => AuditInsertAsync(null, null, tableName, entity, cancellationToken);

    public Task LogInsertAsync<T>(
        IDbConnection connection,
        IDbTransaction? transaction,
        string tableName,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class
        => AuditInsertAsync(connection, transaction, tableName, entity, cancellationToken);

    public Task LogDeleteAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default)
        where T : class
        => AuditDeleteAsync(null, null, tableName, entity, cancellationToken);

    public Task LogDeleteAsync<T>(
        IDbConnection connection,
        IDbTransaction? transaction,
        string tableName,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class
        => AuditDeleteAsync(connection, transaction, tableName, entity, cancellationToken);

    public Task LogUpdateAsync<T>(string tableName, T before, T after, CancellationToken cancellationToken = default)
        where T : class
        => AuditUpdateAsync(null, null, tableName, before, after, cancellationToken);

    public Task LogUpdateAsync<T>(
        IDbConnection connection,
        IDbTransaction? transaction,
        string tableName,
        T before,
        T after,
        CancellationToken cancellationToken = default)
        where T : class
        => AuditUpdateAsync(connection, transaction, tableName, before, after, cancellationToken);

    private Task AuditInsertAsync<T>(
        IDbConnection? connection,
        IDbTransaction? transaction,
        string tableName,
        T entity,
        CancellationToken cancellationToken)
        where T : class
        => WriteAsync(
            BuildEntry(tableName, InsertAction, entity, FirstStringValue(entity)),
            connection,
            transaction,
            cancellationToken);

    private Task AuditDeleteAsync<T>(
        IDbConnection? connection,
        IDbTransaction? transaction,
        string tableName,
        T entity,
        CancellationToken cancellationToken)
        where T : class
        => WriteAsync(
            BuildEntry(tableName, DeleteAction, entity, FirstStringValue(entity)),
            connection,
            transaction,
            cancellationToken);

    private Task AuditUpdateAsync<T>(
        IDbConnection? connection,
        IDbTransaction? transaction,
        string tableName,
        T before,
        T after,
        CancellationToken cancellationToken)
        where T : class
        // The key comes off `after`: an edit cannot move a row, so either side would do, but the
        // post-write entity is the one the repository is holding anyway.
        => WriteAsync(
            BuildEntry(
                tableName,
                UpdateAction,
                after,
                string.Join(ChangedPropertySeparator, ChangedProperties(before, after))),
            connection,
            transaction,
            cancellationToken);

    /// <summary>
    /// Assembles the row: the caller's UserName, the entity's pkid, and every string trimmed to the
    /// width of its column.
    /// </summary>
    private RowAudit BuildEntry(string tableName, string actionType, object entity, string? actionDesc)
        => new()
        {
            TableName = Truncate(tableName ?? string.Empty, TableNameMaxLength)!,
            UserName = Truncate(ResolveUserName(), UserNameMaxLength)!,
            PrimaryKeyValues = Truncate(PrimaryKeyValue(entity), PrimaryKeyValuesMaxLength)!,
            ActionType = Truncate(actionType, ActionTypeMaxLength)!,
            ActionDesc = TruncateToBytes(actionDesc, ActionDescMaxBytes),
            DateTime = DateTime.Now
        };

    /// <summary>
    /// The single INSERT. When <paramref name="connection"/> is given the row is written on it —
    /// inside <paramref name="transaction"/> when there is one, which is what ties the audit row's
    /// fate to the change it describes; otherwise the writer opens and owns a connection of its own.
    ///
    /// <c>virtual</c> so the reflection tests can observe the assembled row without a database —
    /// this is the only seam in the class, and the only reason it exists.
    /// </summary>
    protected virtual async Task WriteAsync(
        RowAudit entry,
        IDbConnection? connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (connection is not null)
        {
            await ExecuteAsync(connection, transaction, entry, cancellationToken);
            return;
        }

        using var owned = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        await ExecuteAsync(owned, null, entry, cancellationToken);
    }

    private static Task ExecuteAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        RowAudit entry,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("TableName", entry.TableName);
        parameters.Add("UserName", entry.UserName);
        parameters.Add("PrimaryKeyValues", entry.PrimaryKeyValues);
        parameters.Add("ActionType", entry.ActionType);
        parameters.Add("ActionDesc", entry.ActionDesc);
        parameters.Add("DateTime", entry.DateTime);

        // pkid is IDENTITY, so it is absent from the column list and never read back — nothing
        // downstream needs the audit row's own key.
        return connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// The signed-in caller's dbo.AppUser.UserName, taken from the validated token's
    /// <c>userName</c> claim, or <see cref="SystemUserName"/> when nothing is signed in — a
    /// migration, a background job, or a seeding script.
    ///
    /// Deliberately not <c>User.Identity.Name</c>: <c>JwtBearerSetup</c> sets <c>NameClaimType</c>
    /// to <c>userId</c>, so that property hands back the id and the trail would name every change
    /// after a number.
    /// </summary>
    private string ResolveUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true) return SystemUserName;

        var userName = user.FindFirst(JwtTokenService.UserNameClaimType)?.Value
                       ?? user.FindFirst(ClaimTypes.Name)?.Value;

        return string.IsNullOrWhiteSpace(userName) ? SystemUserName : userName.Trim();
    }

    /// <summary>
    /// The entity's <c>pkid</c> as text. Every business model carries one — including the
    /// string-keyed tables, where <c>pkid</c> is the surrogate beside the business key
    /// (<c>AppRole.RoleId</c>). An entity without one audits as an empty key rather than throwing:
    /// losing precision in the trail is bad, failing the business write over it is worse.
    /// </summary>
    private static string PrimaryKeyValue(object entity)
    {
        var property = ReadableProperties(entity.GetType())
            .FirstOrDefault(p => string.Equals(p.Name, PrimaryKeyPropertyName, StringComparison.OrdinalIgnoreCase));

        return property?.GetValue(entity)?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// The value of the entity's first string property in declaration order — the Name / Title /
    /// Code that identifies a row to whoever reads the trail. <c>null</c> when the type has no
    /// string property, or when that property is null; ActionDesc is nullable and says so.
    /// </summary>
    private static string? FirstStringValue(object entity)
    {
        var property = ReadableProperties(entity.GetType())
            .FirstOrDefault(p => p.PropertyType == typeof(string));

        return property?.GetValue(entity) as string;
    }

    /// <summary>
    /// The names of the properties whose value differs between the two snapshots, in declaration
    /// order. Empty when the edit changed nothing.
    /// </summary>
    private static IEnumerable<string> ChangedProperties(object before, object after)
    {
        var beforeType = before.GetType();
        var afterType = after.GetType();

        foreach (var property in ReadableProperties(beforeType))
        {
            // The two snapshots are normally the same type; when a caller mixes a request DTO with
            // a response model, match by name and ignore what only one side has.
            var afterProperty = afterType == beforeType
                ? property
                : ReadableProperties(afterType).FirstOrDefault(p => p.Name == property.Name);

            if (afterProperty is null) continue;

            if (!ValuesEqual(property.GetValue(before), afterProperty.GetValue(after)))
            {
                yield return property.Name;
            }
        }
    }

    /// <summary>
    /// Value equality — **structural**, and that is the whole point. `before` and `after` are two
    /// separate reads of the same row, so every reference-typed property is a different instance
    /// and plain <see cref="object.Equals(object?, object?)"/> would call all of them changed: an
    /// n-n key list (<c>Course.JobCategoryPkids</c>), a members list (<c>AppRole.Users</c>) and
    /// every FK nav object (<c>Course.Partner</c>) would appear in ActionDesc on every single edit
    /// and drown the columns that really moved.
    ///
    /// So a collection compares element by element, and any other reference type compares property
    /// by property, recursively. Structs and strings are left to <c>Equals</c>, which is already
    /// value equality for them.
    /// </summary>
    private static bool ValuesEqual(object? before, object? after, int depth = 0)
    {
        if (Equals(before, after)) return true;
        if (before is null || after is null) return false;
        if (before is string || after is string) return false;

        if (before is IEnumerable beforeItems && after is IEnumerable afterItems)
        {
            return SequenceEqual(beforeItems, afterItems, depth);
        }

        // A nav object or a nested model. Same type only: two different types are a change by
        // definition, and the depth cap keeps a surprising object graph from recursing forever.
        var type = before.GetType();

        if (depth >= MaxComparisonDepth || type.IsValueType || type != after.GetType()) return false;

        return ReadableProperties(type)
            .All(p => ValuesEqual(p.GetValue(before), p.GetValue(after), depth + 1));
    }

    /// <summary>Element-wise comparison that recurses through <see cref="ValuesEqual"/>, so a list of models compares by value too.</summary>
    private static bool SequenceEqual(IEnumerable before, IEnumerable after, int depth)
    {
        var beforeItems = before.Cast<object?>().ToList();
        var afterItems = after.Cast<object?>().ToList();

        if (beforeItems.Count != afterItems.Count) return false;

        return !beforeItems.Where((item, i) => !ValuesEqual(item, afterItems[i], depth + 1)).Any();
    }

    /// <summary>
    /// Public instance properties that can be read, in declaration order. Ordering by metadata
    /// token is what makes "the first string property" mean the first one in the source file —
    /// <c>Type.GetProperties()</c> promises no order of its own.
    /// </summary>
    private static PropertyInfo[] ReadableProperties(Type type)
        => PropertyCache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.MetadataToken)
            .ToArray());

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>
    /// Cuts <paramref name="value"/> to fit a byte-counted column, charging two bytes for anything
    /// outside ASCII — which is what every double-byte Windows collation does, CP950 included.
    /// Counting characters instead would let a Chinese title overflow <c>varchar(1000)</c> and take
    /// the INSERT — and with it the business write — down with it.
    /// </summary>
    private static string? TruncateToBytes(string? value, int maxBytes)
    {
        if (value is null) return null;

        var bytes = 0;

        for (var i = 0; i < value.Length; i++)
        {
            bytes += value[i] < 0x80 ? 1 : 2;

            if (bytes <= maxBytes) continue;

            // Never end on half a surrogate pair: the trailing char would be meaningless on its own.
            var end = i > 0 && char.IsHighSurrogate(value[i - 1]) ? i - 1 : i;

            return value[..end];
        }

        return value;
    }
}
