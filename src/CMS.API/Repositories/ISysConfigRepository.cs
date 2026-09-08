namespace CMS.API.Repositories;

/// <summary>Read-only data access over dbo.SysConfig (系統設定).</summary>
public interface ISysConfigRepository
{
    /// <summary>The configValue stored under <paramref name="configKey"/>, or <c>null</c> when absent.</summary>
    Task<string?> GetValueAsync(string configKey, CancellationToken cancellationToken = default);
}
