using System.Security.Claims;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 登入 Auth — credential check against dbo.AppUser, JWT issue, and the signed-in user's own
/// 個人資料 and 變更密碼. Not a CRUD table, so the route is <c>/api/auth</c> rather than a
/// kebab-case plural.
///
/// Authorization is global (a fallback policy in <c>Program.cs</c> requires an authenticated user
/// everywhere), so signing in has to opt back out of it. <c>[AllowAnonymous]</c> therefore sits on
/// <see cref="Login"/> alone and **not** on the type: a class-level attribute would silently open
/// up every action added here later, <see cref="UpdateProfile"/> and
/// <see cref="ChangePassword"/> included.
///
/// This controller carries the API's **two** deliberate exceptions, and both are scoped to a single
/// action. The second is <see cref="ChangePassword"/>: it names
/// <see cref="AuthPolicies.PasswordChangeExempt"/>, so a user still on the default password can
/// reach it while every other endpoint answers them 403.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _repository;
    private readonly IJwtTokenService _tokenService;
    private readonly IPasswordPolicyService _passwordPolicy;
    private readonly IDefaultPasswordService _defaultPassword;

    public AuthController(
        IAuthRepository repository,
        IJwtTokenService tokenService,
        IPasswordPolicyService passwordPolicy,
        IDefaultPasswordService defaultPassword)
    {
        _repository = repository;
        _tokenService = tokenService;
        _passwordPolicy = passwordPolicy;
        _defaultPassword = defaultPassword;
    }

    /// <summary>
    /// Signs a user in. The account must exist, be active, and its PasswordHash must equal the
    /// SHA-256 of the supplied password. Every failure returns the same generic 401 so the response
    /// never reveals which check failed.
    ///
    /// An account still on the configured <c>defaultPassword</c> gets a token carrying
    /// <see cref="JwtTokenService.MustChangePasswordClaimType"/>, which makes every endpoint but
    /// <see cref="ChangePassword"/> answer it 403. <see cref="LoginResponse"/> is unchanged: the
    /// flag rides in the token exactly as the roles do, so it survives a page reload and there is
    /// one source of truth.
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

        // After the credential guard, so a rejected login never pays for the extra SysConfig read.
        var mustChangePassword = await _defaultPassword.IsDefaultAsync(
            credential.PasswordHash, cancellationToken);

        var accessToken = await _tokenService.CreateAccessTokenAsync(
            credential.UserId, credential.UserName, roleIds, mustChangePassword, cancellationToken);

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
    [Authorize(Policy = AuthPolicies.PasswordNotDefault)]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    // Representative of every endpoint in the API: a caller still on the default password is
    // refused until they have changed it.
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
        return updated is null ? UserNotFound() : Ok(updated);
    }

    /// <summary>
    /// 變更密碼 — replaces the **signed-in** user's password, once they have proved they know the
    /// current one. The account is the one the bearer token names; the body cannot choose it.
    ///
    /// Every rejection here is a **400, never a 401**. To the browser's error interceptor a 401
    /// from anything but the login call means "your session expired": it clears the session and
    /// bounces to /login. Answering a mistyped current password with 401 would therefore sign the
    /// user out mid-change. The single 401 left is a token carrying no userId at all, where signing
    /// out is exactly the right answer.
    ///
    /// The session survives a successful change. Tokens are signed with one global secret and there
    /// is no revocation, so forcing a re-login here would imply an invalidation that does not
    /// actually happen on any other device.
    ///
    /// This is the API's one <see cref="AuthPolicies.PasswordChangeExempt"/> action — the deliberate
    /// escape hatch from <see cref="AuthPolicies.PasswordNotDefault"/>, without which a user on the
    /// default password would be 403'd out of the very endpoint that clears the flag. The token is
    /// **not** re-issued, so the caller stays flagged until they sign in again; that is why the
    /// browser signs them out here rather than leaving them on a token every other route refuses.
    /// </summary>
    [Authorize(Policy = AuthPolicies.PasswordChangeExempt)]
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = CurrentUserId();

        if (string.IsNullOrWhiteSpace(userId)) return InvalidCredentials();

        var credential = await _repository.GetCredentialAsync(userId, cancellationToken);

        // A disabled account is treated exactly like a deleted one: the token still validates, but
        // there is nothing here left to change.
        if (credential is null || !credential.IsActive) return UserNotFound();

        if (!PasswordHasher.Matches(request.CurrentPassword, credential.PasswordHash))
        {
            return PasswordRejected("目前密碼不正確。");
        }

        if (PasswordHasher.Matches(request.NewPassword, credential.PasswordHash))
        {
            return PasswordRejected("新密碼不可與目前密碼相同。");
        }

        var policyError = await _passwordPolicy.ValidateAsync(request.NewPassword, cancellationToken);

        if (policyError is not null) return PasswordRejected(policyError);

        var changed = await _repository.UpdatePasswordAsync(
            credential.UserId, PasswordHasher.Hash(request.NewPassword), cancellationToken);

        // Nothing to return: a password, hashed or not, never travels back.
        return changed ? NoContent() : UserNotFound();
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

    /// <summary>The token validated, but the account it names is gone or disabled.</summary>
    private NotFoundObjectResult UserNotFound()
        => NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "User not found",
            Detail = "查無使用者資料。"
        });

    /// <summary>
    /// A password change the server refused. Deliberately a 400: see <see cref="ChangePassword"/>
    /// for why a 401 here would sign the user out. <paramref name="detail"/> is shown verbatim.
    /// </summary>
    private BadRequestObjectResult PasswordRejected(string detail)
        => BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Password rejected",
            Detail = detail
        });
}
