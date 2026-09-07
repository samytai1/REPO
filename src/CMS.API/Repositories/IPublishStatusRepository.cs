using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for dbo.PublishStatus (發布狀態).</summary>
public interface IPublishStatusRepository
{
    /// <summary>All statuses, default sort pkid ASC.</summary>
    Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Filtered search.</summary>
    Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default);

    /// <summary>Single status by pkid. Null when not found.</summary>
    Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default);

    /// <summary>True when a status with the given pkid already exists.</summary>
    Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken = default);

    /// <summary>Inserts the status; returns the created record.</summary>
    Task<PublishStatus> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates the status. Null when the row is missing.</summary>
    Task<PublishStatus?> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes the status. Reports NotFound, or InUse when a child table still references it.</summary>
    Task<PublishStatusDeleteResult> DeleteAsync(byte pkid, CancellationToken cancellationToken = default);
}
