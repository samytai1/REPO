using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 合作夥伴 Partner — CRUD endpoints over dbo.Partner.
/// pkid is a smallint IDENTITY, so the database assigns it on create and the update body carries it.
/// </summary>
[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class PartnersController : ControllerBase
{
    private readonly IPartnerRepository _repository;

    public PartnersController(IPartnerRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All partners, ordered by DisplayOrder then pkid.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> GetAll(CancellationToken cancellationToken)
    {
        var partners = await _repository.GetAllAsync(cancellationToken);
        return Ok(partners);
    }

    /// <summary>Filtered search over partners.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> Query(
        [FromBody] PartnerQuery query,
        CancellationToken cancellationToken)
    {
        var partners = await _repository.QueryAsync(query ?? new PartnerQuery(), cancellationToken);
        return Ok(partners);
    }

    /// <summary>A single partner.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> GetById(short id, CancellationToken cancellationToken)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);

        if (partner is null) return NotFound();

        return Ok(partner);
    }

    /// <summary>Creates a partner. pkid is assigned by the database.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Partner>> Create(
        [FromBody] PartnerRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var created = await _repository.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = (int)created.Pkid }, created);
    }

    /// <summary>Updates a partner. The key (pkid) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> Update(
        [FromBody] PartnerUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _repository.UpdateAsync(request, cancellationToken);

        if (updated is null) return NotFound();

        return Ok(updated);
    }

    /// <summary>Deletes a partner. Conflicts when a certification, course or course group still references it.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(id, cancellationToken);

        return result switch
        {
            PartnerDeleteResult.Deleted => NoContent(),
            PartnerDeleteResult.NotFound => NotFound(),
            _ => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Partner in use",
                Detail = $"主代碼 '{id}' 已被認證、課程或課程群組使用，無法刪除。"
            })
        };
    }
}
