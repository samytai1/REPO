using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CMS.API.Infrastructure;

/// <summary>
/// Refuses a caller whose token says their password is still the configured default.
///
/// The requirement is satisfied by the **absence** of the claim, so a token minted before this
/// feature existed — or by any user whose password is not the default — passes untouched.
/// </summary>
public class MustChangePasswordRequirement : IAuthorizationRequirement
{
    /// <summary>Shown verbatim in the 403 body, and asserted by the tests. See <see cref="PasswordChangeRequiredResultHandler"/>.</summary>
    public const string Message = "請先變更預設密碼。";
}

/// <summary>
/// Succeeds unless the principal carries <see cref="JwtTokenService.MustChangePasswordClaimType"/>
/// with the value <c>"true"</c>.
///
/// It deliberately never calls <c>context.Fail()</c>. An explicit fail leaves
/// <c>AuthorizationFailure.FailedRequirements</c> empty, and
/// <see cref="PasswordChangeRequiredResultHandler"/> would then be unable to tell this rejection
/// from any other. Simply not succeeding names the requirement in the failure.
/// </summary>
public class MustChangePasswordHandler : AuthorizationHandler<MustChangePasswordRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MustChangePasswordRequirement requirement)
    {
        if (!IsFlagged(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool IsFlagged(ClaimsPrincipal user)
        => user.HasClaim(claim =>
            claim.Type == JwtTokenService.MustChangePasswordClaimType
            && string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase));
}
