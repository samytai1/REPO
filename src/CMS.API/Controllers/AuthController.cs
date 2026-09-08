using System.Security.Claims;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 登入 Auth — credential check against dbo.AppUser, JWT issue, and the signed-in user's own
/// 個人資料. Not a CRUD table, so the route is <c>/api/auth</c> rather than a kebab-case plural.
///
/// Authorization is global (a fallback policy in <c>Program.cs</c> requires an authenticated user
/// everywhere), so signing in has to opt back out of it. <c>[AllowAnonymous]</c> therefore sits on
/// <see cref="Login"/> alone and **not** on the type: a class-level attribute would silently open
/// up every action added here later, <see cref="UpdateProfile"/> included.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _repository;
    private readonly IJwtTokenService _tokenService;

    public AuthController(IAuthRepository repository, IJwtTokenService tokenService)
    {
        _repository = repository;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Signs a user in. The account must exist, be active, and its PasswordHash must equal the
    /// SHA-256 of the supplied password. Every failure returns the same generic 401 so the response
    /// never reveals which check failed.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var credential = await _repository.GetCredentialAsync(request.UserId, cancellationToken);

        if (credential is null
            || !credential.IsActive
            || !PasswordHasher.Matches(request.Password, credential.PasswordHash))
        {
            return InvalidCredentials();
        }

        var roleIds = await _repository.GetRoleIdsAsync(credential.UserId, cancellationToken);

        var accessToken = await _tokenService.CreateAccessTokenAsync(
            credential.UserId, credential.UserName, roleIds, cancellationToken);

        return Ok(new LoginResponse
        {
            UserId = credential.UserId,
            UserName = credential.UserName,
            AccessToken = accessToken
        });
    }

    /// <summary>
    /// 個人資料 — renames the **signed-in** account and nothing else.
    ///
    /// The row that is written is the one the bearer token names: the UserId comes from the
    /// <c>userId</c> claim, never from the request, and <see cref="UpdateProfileRequest"/> has no
    /// property for a UserId or a role list, so anything of the sort posted alongside is dropped
    /// during deserialization. UserName is required and is trimmed before it reaches SQL.
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // [Required] rejects null and "", but not "   " — and a whitespace name is not a name.
        var userName = request.UserName.Trim();

        if (userName.Length == 0)
        {
            ModelState.AddModelError(nameof(request.UserName), "使用者名稱為必填。");
            return BadRequest(ModelState);
        }

        var userId = CurrentUserId();

        if (string.IsNullOrWhiteSpace(userId)) return InvalidCredentials();

        var updated = await _repository.UpdateUserNameAsync(userId, userName, cancellationToken);

        // The token validated, but its account has since gone — the browser treats it as signed out.
        if (updated is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = "查無使用者資料。"
            });
        }

        return Ok(updated);
    }

    /// <summary>
    /// dbo.AppUser.UserId of the caller, taken from the validated token's <c>userId</c> claim.
    /// <c>sub</c> is the fallback for a token minted before that claim existed.
    /// </summary>
    private string? CurrentUserId()
        => User.FindFirstValue(JwtTokenService.UserIdClaimType)
           ?? User.FindFirstValue("sub");

    /// <summary>
    /// The single 401 body used for an unknown user, a disabled user and a wrong password alike.
    /// </summary>
    private UnauthorizedObjectResult InvalidCredentials()
        => Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Invalid credentials",
            Detail = "帳號或密碼錯誤。"
        });
}
