using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Infrastructure;

/// <summary>
/// Configures bearer-token validation against the same secret <see cref="JwtTokenService"/> signs
/// with — the <c>symmetricSecurityKey</c> inside dbo.SysConfig['appConfig'].
///
/// That secret lives behind an async repository call, but
/// <see cref="TokenValidationParameters.IssuerSigningKeyResolver"/> is synchronous. So the key is
/// read in <see cref="JwtBearerEvents.OnMessageReceived"/> — which may await — and stashed on the
/// request for the resolver to hand back. Nothing is cached and nothing blocks a thread: a rotated
/// secret takes effect on the next request, exactly as it does for issuing.
/// </summary>
public static class JwtBearerSetup
{
    /// <summary><c>HttpContext.Items</c> slot carrying the signing key read for this request.</summary>
    public const string SigningKeyItemsKey = "CMS.Jwt.SigningKey";

    /// <summary>
    /// Applies the validation parameters and the key-reading event. Registered through
    /// <c>AddOptions&lt;JwtBearerOptions&gt;(…).Configure&lt;IHttpContextAccessor&gt;(…)</c> in
    /// <c>Program.cs</c>, which is how the accessor reaches the synchronous resolver.
    /// </summary>
    public static void Configure(JwtBearerOptions options, IHttpContextAccessor httpContextAccessor)
    {
        // Claims stay exactly as issued — no mapping of `role` / `sub` onto the long WS-* URIs — so
        // RoleClaimType below really is the short name JwtTokenService writes.
        options.MapInboundClaims = false;

        // The API issues and consumes its own tokens, so there is no issuer or audience to check;
        // the signature and the expiry are what matter.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtTokenService.UserIdClaimType,
            RoleClaimType = JwtTokenService.RoleClaimType,
            IssuerSigningKeyResolver = (_, _, _, _) =>
                httpContextAccessor.HttpContext?.Items[SigningKeyItemsKey] is SecurityKey key
                    ? [key]
                    : Array.Empty<SecurityKey>()
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = async context =>
            {
                // An anonymous request must not pay for a SysConfig read.
                if (!CarriesAToken(context)) return;

                var tokenService = context.HttpContext.RequestServices.GetRequiredService<IJwtTokenService>();

                context.HttpContext.Items[SigningKeyItemsKey] =
                    await tokenService.GetSigningKeyAsync(context.HttpContext.RequestAborted);
            }
        };
    }

    /// <summary>
    /// True when the caller presented a token — either one an earlier handler already extracted, or
    /// an <c>Authorization: Bearer …</c> header this handler is about to read.
    /// </summary>
    private static bool CarriesAToken(MessageReceivedContext context)
    {
        if (!string.IsNullOrEmpty(context.Token)) return true;

        return context.Request.Headers.Authorization
            .Any(value => value is not null
                          && value.StartsWith(JwtBearerDefaults.AuthenticationScheme + ' ', StringComparison.OrdinalIgnoreCase));
    }
}
