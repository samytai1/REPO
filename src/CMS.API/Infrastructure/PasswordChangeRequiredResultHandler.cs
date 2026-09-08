using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Infrastructure;

/// <summary>
/// Gives the 403 a body. A failed requirement produces a bare, bodyless 403 from the JWT bearer
/// handler, which would be the one rejection in this API that does not say why in Chinese — and the
/// only way a Swagger user or any non-browser caller could tell "your password is the default" from
/// an ordinary refusal.
///
/// Every other outcome is delegated to the framework's own handler untouched.
/// </summary>
public class PasswordChangeRequiredResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _inner = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!IsPasswordChangeRequired(authorizeResult))
        {
            await _inner.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        // The contentType argument is what sets the header — assigning Response.ContentType first
        // would be overwritten by the serializer's own default of application/json.
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Password change required",
                Detail = MustChangePasswordRequirement.Message
            },
            options: null,
            contentType: "application/problem+json",
            context.RequestAborted);
    }

    private static bool IsPasswordChangeRequired(PolicyAuthorizationResult authorizeResult)
        => authorizeResult is { Succeeded: false, Forbidden: true }
           && authorizeResult.AuthorizationFailure?.FailedRequirements
               .OfType<MustChangePasswordRequirement>()
               .Any() == true;
}
