using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Slim lookup lists used to populate FK / n-n option controls on forms and filter drawers.
/// </summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IPublishStatusRepository _publishStatusRepository;

    public LookupsController(
        IAppUserRepository appUserRepository,
        IAppRoleRepository appRoleRepository,
        IPublishStatusRepository publishStatusRepository)
    {
        _appUserRepository = appUserRepository;
        _appRoleRepository = appRoleRepository;
        _publishStatusRepository = publishStatusRepository;
    }

    /// <summary>使用者 AppUser lookup list.</summary>
    [HttpGet("app-users")]
    [ProducesResponseType(typeof(IEnumerable<AppUserLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUserLookup>>> GetAppUsers(CancellationToken cancellationToken)
    {
        var users = await _appUserRepository.GetLookupAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>角色 AppRole lookup list.</summary>
    [HttpGet("app-roles")]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> GetAppRoles(CancellationToken cancellationToken)
    {
        var roles = await _appRoleRepository.GetAllAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>發布狀態 PublishStatus lookup list — option label is Description, ordered by pkid.</summary>
    [HttpGet("publish-statuses")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> GetPublishStatuses(CancellationToken cancellationToken)
    {
        var statuses = await _publishStatusRepository.GetAllAsync(cancellationToken);
        return Ok(statuses);
    }
}
