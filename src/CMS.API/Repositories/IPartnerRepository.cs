using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for dbo.Partner (合作夥伴).</summary>
public interface IPartnerRepository
{
    /// <summary>All partners, default sort DisplayOrder ASC, pkid ASC.</summary>
    Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Filtered search.</summary>
    Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default);

    /// <summary>Single partner by pkid. Null when not found.</summary>
    Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);

    /// <summary>Inserts the partner; returns the created record with its assigned pkid.</summary>
    Task<Partner> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates the partner. Null when the row is missing.</summary>
    Task<Partner?> UpdateAsync(PartnerUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes the partner. Reports NotFound, or InUse when a child table still references it.</summary>
    Task<PartnerDeleteResult> DeleteAsync(short pkid, CancellationToken cancellationToken = default);
}
