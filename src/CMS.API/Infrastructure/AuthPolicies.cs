namespace CMS.API.Infrastructure;

/// <summary>
/// The named authorization policies, so both are greppable rather than spelled out at each
/// <c>[Authorize]</c>.
///
/// <see cref="PasswordNotDefault"/> is what everything gets — it is the fallback policy *and* the
/// default policy, so an endpoint with no attribute and an endpoint with a bare <c>[Authorize]</c>
/// land on the same rules. <see cref="PasswordChangeExempt"/> is the single deliberate escape
/// hatch, and <c>AuthController.ChangePassword</c> is the only action allowed to name it — pinned by
/// <c>AuthorizationConventionTests</c>.
/// </summary>
public static class AuthPolicies
{
    /// <summary>Authenticated **and** not sitting on the default password.</summary>
    public const string PasswordNotDefault = "password-not-default";

    /// <summary>Authenticated only — for 變更密碼, the one thing a flagged user must still reach.</summary>
    public const string PasswordChangeExempt = "password-change-exempt";
}
