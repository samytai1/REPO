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

    public LookupsController(IAppUserRepository appUserRepository, IAppRoleRepository appRoleRepository)
    {
        _appUserRepository = appUserRepository;
        _appRoleRepository = appRoleRepository;
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
}
