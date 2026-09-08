using System.Data;

namespace CMS.API.Infrastructure;

/// <summary>
/// 異動紀錄 — writes one dbo.RowAudit row describing a change to any business table.
///
/// Cross-cutting and entity-agnostic: a repository calls it right after its own INSERT / UPDATE /
/// DELETE succeeds, passing the table name and the entity, and reflection works out the rest
/// (the pkid, an identifying string, the changed column names). Nothing here knows about Course,
/// Partner or AppRole.
///
/// The caller supplies the table name rather than having it derived from the type, because the
/// model name and the table name are free to diverge and the audit trail must name the *table*.
///
/// **Every overload comes in pairs.** The one taking an <see cref="IDbConnection"/> and an
/// <see cref="IDbTransaction"/> enlists the audit INSERT in the caller's own transaction, so a
/// change that is rolled back takes its audit row down with it — that is the overload a repository
/// uses, and the reason the audit trail can be trusted. The short overload opens its own
/// connection and commits independently; it is for a caller that has no transaction to join.
/// </summary>
public interface IRowAuditWriter
{
    /// <summary>
    /// Logs an insert. <c>ActionDesc</c> becomes the entity's first string property in declaration
    /// order — the Name / Title / Code that identifies the new row to a human reader.
    /// </summary>
    Task LogInsertAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default)
        where T : class;

    /// <inheritdoc cref="LogInsertAsync{T}(string, T, CancellationToken)"/>
    /// <remarks>Writes on <paramref name="connection"/>, inside <paramref name="transaction"/>.</remarks>
    Task LogInsertAsync<T>(
        IDbConnection connection,
        IDbTransaction? transaction,
        string tableName,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Logs an update. <c>ActionDesc</c> becomes a comma-separated list of the property names whose
    /// value differs between <paramref name="before"/> and <paramref name="after"/> — empty when
    /// nothing changed, which still writes a row: "someone saved this and changed nothing" is
    /// itself a fact the trail should carry.
    /// </summary>
    Task LogUpdateAsync<T>(string tableName, T before, T after, CancellationToken cancellationToken = default)
        where T : class;

    /// <inheritdoc cref="LogUpdateAsync{T}(string, T, T, CancellationToken)"/>
    /// <remarks>Writes on <paramref name="connection"/>, inside <paramref name="transaction"/>.</remarks>
    Task LogUpdateAsync<T>(
        IDbConnection connection,
        IDbTransaction? transaction,
        string tableName,
        T before,
        T after,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Logs a delete. <c>ActionDesc</c> follows the insert rule — the first string property of the
    /// row as it was, which is the only trace of it left once the row is gone.
    /// </summary>
    Task LogDeleteAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default)
        where T : class;

    /// <inheritdoc cref="LogDeleteAsync{T}(string, T, CancellationToken)"/>
    /// <remarks>Writes on <paramref name="connection"/>, inside <paramref name="transaction"/>.</remarks>
    Task LogDeleteAsync<T>(
        IDbConnection connection,
        IDbTransaction? transaction,
        string tableName,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class;
}
