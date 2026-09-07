using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 角色 AppRole — CRUD endpoints over dbo.AppRole.
/// RoleId (nvarchar) is the record key, so routes use an unconstrained <c>{id}</c> segment.
/// </summary>
[ApiController]
[Route("api/app-roles")]
[Produces("application/json")]
public class AppRolesController : ControllerBase
{
    private readonly IAppRoleRepository _repository;

    public AppRolesController(IAppRoleRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All roles, ordered by RoleId.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _repository.GetAllAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>Filtered search over roles.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> Query(
        [FromBody] AppRoleQuery query,
        CancellationToken cancellationToken)
    {
        var roles = await _repository.QueryAsync(query ?? new AppRoleQuery(), cancellationToken);
        return Ok(roles);
    }

    /// <summary>A single role, including its assigned users.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> GetById(string id, CancellationToken cancellationToken)
    {
        var role = await _repository.GetByIdAsync(id, cancellationToken);

        if (role is null) return NotFound();

        return Ok(role);
    }

    /// <summary>Creates a role. RoleId must be unique.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppRole>> Create(
        [FromBody] AppRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _repository.ExistsAsync(request.RoleId, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate RoleId",
                Detail = $"角色代碼 '{request.RoleId}' 已存在。"
            });
        }

        var created = await _repository.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.RoleId }, created);
    }

    /// <summary>Updates a role. The key (RoleId) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> Update(
        [FromBody] AppRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _repository.UpdateAsync(request, cancellationToken);

        if (updated is null) return NotFound();

        return Ok(updated);
    }

    /// <summary>Deletes a role and its user assignments.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        if (!deleted) return NotFound();

        return NoContent();
    }
}
