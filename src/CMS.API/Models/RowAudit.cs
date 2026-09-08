namespace CMS.API.Models;

/// <summary>
/// One row of dbo.RowAudit (異動紀錄) — the cross-cutting audit trail. Every business write
/// contributes exactly one of these, whatever table it touched.
///
/// The row is written by <see cref="Infrastructure.RowAuditWriter"/> and never read back by the
/// API, so there is no repository, no query DTO and no controller: this is a write model.
/// <c>pkid</c> is IDENTITY and therefore absent, exactly as a create DTO omits it elsewhere.
/// </summary>
public class RowAudit
{
    /// <summary>異動資料表 — dbo.RowAudit.TableName, the business table that changed (e.g. "Course").</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>異動人員 — dbo.RowAudit.UserName, the signed-in caller's UserName, or "system".</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>主鍵值 — dbo.RowAudit.PrimaryKeyValues, the changed row's pkid as text.</summary>
    public string PrimaryKeyValues { get; set; } = string.Empty;

    /// <summary>異動類型 — dbo.RowAudit.ActionType: Insert / Update / Delete.</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// 異動說明 — dbo.RowAudit.ActionDesc (nullable). An identifying string for an insert or a
    /// delete; the names of the columns that changed for an update.
    /// </summary>
    public string? ActionDesc { get; set; }

    /// <summary>異動時間 — dbo.RowAudit.[DateTime], local time.</summary>
    public DateTime DateTime { get; set; }
}
