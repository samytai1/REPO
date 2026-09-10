using CMS.API.Infrastructure;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.OpenApi.Models;

// Dapper type handlers must be registered before any query runs.
DapperConfig.Register();

var builder = WebApplication.CreateBuilder(args);

const string LocalhostCorsPolicy = "LocalhostCors";

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CMS API",
        Version = "v1",
        Description = "CMS backend API (Dapper over MS SQL Server). Every endpoint but "
                      + "POST /api/auth/login needs a bearer token, and every endpoint but "
                      + "PUT /api/auth/password answers 403 while the caller's password is still "
                      + "the configured default."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Every endpoint but /api/auth/login needs a bearer token, so Swagger has to be able to send one.
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "貼上 POST /api/auth/login 回傳的 accessToken（不必自行加上 Bearer 前綴）。"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = JwtBearerDefaults.AuthenticationScheme
            }
        }] = Array.Empty<string>()
    });
});

// CORS: allow any localhost origin (Angular dev server runs on 4200).
builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalhostCorsPolicy, policy => policy
        .SetIsOriginAllowed(origin =>
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            return uri.IsLoopback;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Bearer authentication. The validation key is read per request from dbo.SysConfig — see
// JwtBearerSetup, which needs IHttpContextAccessor to bridge the synchronous key resolver.
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IHttpContextAccessor>(JwtBearerSetup.Configure);

// Authorization is global: every endpoint requires an authenticated user — and a user who is not
// still on the configured default password — unless it carries [AllowAnonymous], which only
// AuthController.Login does. A new controller is protected by default.
builder.Services.AddAuthorization(options =>
{
    var passwordNotDefault = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new MustChangePasswordRequirement())
        .Build();

    options.AddPolicy(AuthPolicies.PasswordNotDefault, passwordNotDefault);

    // 變更密碼 alone: authenticated, but the default-password requirement lifted. Without it a
    // flagged user would be locked out of the one endpoint that clears the flag.
    options.AddPolicy(
        AuthPolicies.PasswordChangeExempt,
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    // Both, deliberately. FallbackPolicy covers an endpoint with no [Authorize] at all;
    // DefaultPolicy covers one carrying a *bare* [Authorize], which bypasses the fallback entirely.
    // Setting only the first would leave any future bare [Authorize] — or an [Authorize(Roles=…)],
    // which also opts out — silently exempt from the password check.
    options.DefaultPolicy = passwordNotDefault;
    options.FallbackPolicy = passwordNotDefault;
});

// The requirement's handler, and the result handler that gives its 403 a Chinese body.
builder.Services.AddSingleton<IAuthorizationHandler, MustChangePasswordHandler>();
builder.Services
    .AddSingleton<IAuthorizationMiddlewareResultHandler, PasswordChangeRequiredResultHandler>();

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
builder.Services.AddScoped<IJobCategoryRepository, JobCategoryRepository>();
builder.Services.AddScoped<ICertificationRepository, CertificationRepository>();
builder.Services.AddScoped<IFeaturedPromoItemRepository, FeaturedPromoItemRepository>();
builder.Services.AddScoped<ITrainingCenterRepository, TrainingCenterRepository>();
builder.Services.AddScoped<IPromotion2Repository, Promotion2Repository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<ISysConfigRepository, SysConfigRepository>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
builder.Services.AddScoped<IDefaultPasswordService, DefaultPasswordService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors(LocalhostCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Exposed so the test project can host the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
