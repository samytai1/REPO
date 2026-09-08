using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 上稿作業 FeaturedPromoItem — CRUD endpoints over dbo.FeaturedPromoItem, plus the slot move the
/// weekly grid's +/− buttons use. pkid is an int IDENTITY; the natural key
/// (ScheduleOn, TrainingCenter_pkid, Slot) is UNIQUE, so create and update can conflict.
/// </summary>
[ApiController]
[Route("api/featured-promo-items")]
[Produces("application/json")]
public class FeaturedPromoItemsController : ControllerBase
{
    private readonly IFeaturedPromoItemRepository _repository;

    public FeaturedPromoItemsController(IFeaturedPromoItemRepository repository)
    {
        _repository = repository;
    }

    /// <summary>All items, ordered by ScheduleOn, training center, slot. Large — prefer <see cref="Query"/>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>One training center's items for the Monday-to-Sunday week containing WeekOf.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> Query(
        [FromBody] FeaturedPromoItemQuery query,
        CancellationToken cancellationToken)
    {
        var items = await _repository.QueryAsync(query ?? new FeaturedPromoItemQuery(), cancellationToken);
        return Ok(items);
    }

    /// <summary>A single item.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);

        if (item is null) return NotFound();

        return Ok(item);
    }

    /// <summary>Creates an item. Conflicts when the slot on that day and center is already taken.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Create(
        [FromBody] FeaturedPromoItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _repository.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, null, cancellationToken))
        {
            return SlotTaken(request);
        }

        var created = await _repository.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Pkid }, created);
    }

    /// <summary>Updates an item. The key (pkid) comes from the body, not the route.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Update(
        [FromBody] FeaturedPromoItemUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _repository.IsSlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, request.Pkid, cancellationToken))
        {
            return SlotTaken(request);
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);

        if (updated is null) return NotFound();

        return Ok(updated);
    }

    /// <summary>Deletes an item.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Moves an item to another slot on the same day and center (the grid's + / − buttons).
    /// If that slot is occupied, the two items swap.
    /// </summary>
    [HttpPost("{id:int}/move-slot")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> MoveSlot(
        int id,
        [FromBody] FeaturedPromoItemMoveRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var moved = await _repository.MoveSlotAsync(id, request.TargetSlot, cancellationToken);

        if (moved is null) return NotFound();

        return Ok(moved);
    }

    private ConflictObjectResult SlotTaken(FeaturedPromoItemRequest request) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Slot already taken",
        Detail = $"{request.ScheduleOn:yyyy-MM-dd} 教育中心 {request.TrainingCenterPkid} 的版位 {request.Slot} 已有上稿資料。"
    });
}
