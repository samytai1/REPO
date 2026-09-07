using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 發布狀態 PublishStatus — CRUD endpoints over dbo.PublishStatus.
/// pkid is a tinyint that the user supplies (the column is not an IDENTITY), so it is required on
/// create and immutable on update.
/// </summary>
[ApiController]
[Route("api/publish-statuses")]
[Produces("application/json")]
public class PublishStatusesController : ControllerBase
{
    private readonly IPublishStatusRepository _repository;

    public PublishStatusesController(IPublishStatusRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All statuses, ordered by pkid.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> GetAll(CancellationToken cancellationToken)
    {
        var statuses = await _repository.GetAllAsync(cancellationToken);
        return Ok(statuses);
    }

    /// <summary>Filtered search over statuses.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> Query(
        [FromBody] PublishStatusQuery query,
        CancellationToken cancellationToken)
    {
        var statuses = await _repository.QueryAsync(query ?? new PublishStatusQuery(), cancellationToken);
        return Ok(statuses);
    }

    /// <summary>A single status.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> GetById(byte id, CancellationToken cancellationToken)
    {
        var status = await _repository.GetByIdAsync(id, cancellationToken);

        if (status is null) return NotFound();

        return Ok(status);
    }

    /// <summary>Creates a status. pkid must be unique.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishStatus>> Create(
        [FromBody] PublishStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _repository.ExistsAsync(request.Pkid, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate pkid",
                Detail = $"主代碼 '{request.Pkid}' 已存在。"
            });
        }

        var created = await _repository.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = (int)created.Pkid }, created);
    }

    /// <summary>Updates a status. The key (pkid) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> Update(
        [FromBody] PublishStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _repository.UpdateAsync(request, cancellationToken);

        if (updated is null) return NotFound();

        return Ok(updated);
    }

    /// <summary>Deletes a status. Conflicts when a course or promotion still references it.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(byte id, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(id, cancellationToken);

        return result switch
        {
            PublishStatusDeleteResult.Deleted => NoContent(),
            PublishStatusDeleteResult.NotFound => NotFound(),
            _ => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "PublishStatus in use",
                Detail = $"主代碼 '{id}' 已被課程或促銷使用，無法刪除。"
            })
        };
    }
}
