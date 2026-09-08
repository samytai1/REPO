using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for dbo.Course (課程).</summary>
public interface ICourseRepository
{
    /// <summary>All courses, default sort DisplayOrder ASC, pkid ASC.</summary>
    Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Filtered search.</summary>
    Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default);

    /// <summary>Single course by pkid, with both n-n collections populated. Null when not found.</summary>
    Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>Inserts the course and its junction rows; returns the record with its assigned pkid.</summary>
    Task<Course> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates the course and replaces its junction rows. Null when the row is missing.</summary>
    Task<Course?> UpdateAsync(CourseUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes the course. Reports NotFound, or InUse when a child table still references it.</summary>
    Task<CourseDeleteResult> DeleteAsync(int pkid, CancellationToken cancellationToken = default);
}
