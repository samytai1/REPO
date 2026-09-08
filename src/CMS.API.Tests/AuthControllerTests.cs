using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Tests;

/// <summary>
/// Covers 登入 <c>POST /api/auth/login</c>: the credential check (active user, wrong password,
/// unknown user, disabled user), the claims and lifetime of the issued JWT, and the guarantee that
/// PasswordHash never reaches the wire.
///
/// The real <see cref="JwtTokenService"/> is used over an in-memory dbo.SysConfig so the tests
/// assert against a genuinely signed token rather than a stub string.
/// </summary>
public class AuthControllerTests
{
    private const string SigningKey = "cloud4fun#123456cloud4fun#123456";

    private static readonly string AppConfigJson = $$"""
    {
      "defaultPassword": "CMS4fun#",
      "symmetricSecurityKey": "{{SigningKey}}",
      "enforcePasswordPolicy": true
    }
    """;

    /// <summary>Same config with the strength rules switched off.</summary>
    private static readonly string PolicyOffConfigJson = $$"""
    {
      "symmetricSecurityKey": "{{SigningKey}}",
      "enforcePasswordPolicy": false
    }
    """;

    /// <summary>admin (active, two roles), helen (active, no roles) and retired (disabled).</summary>
    private static InMemoryAuthRepository SeededRepository()
        => new InMemoryAuthRepository()
            .Seed("admin@example.com", "Admin User", "CMS4fun#", true, "Admin", "User")
            .Seed("helen", "Helen Wu", "helen-pw", true)
            .Seed("retired@example.com", "Retired User", "CMS4fun#", false, "Admin");

    private static AuthController ControllerFor(
        InMemoryAuthRepository? repository = null,
        InMemorySysConfigRepository? sysConfig = null)
    {
        var config = sysConfig ?? new InMemorySysConfigRepository().Seed("appConfig", AppConfigJson);
        return new AuthController(
            repository ?? SeededRepository(),
            new JwtTokenService(config),
            new PasswordPolicyService(config));
    }

    private static LoginRequest Login(string userId, string password)
        => new() { UserId = userId, Password = password };

    private static LoginResponse AssertOk(ActionResult<LoginResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<LoginResponse>(ok.Value);
    }

    /// <summary>Asserts the generic 401 and hands back its body for message comparisons.</summary>
    private static ProblemDetails AssertUnauthorized(ActionResult<LoginResponse> result)
    {
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(unauthorized.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
        return problem;
    }

    /// <summary>Reads a token without validating it — for claim and lifetime assertions.</summary>
    private static JsonWebToken Read(string accessToken) => new JsonWebTokenHandler().ReadJsonWebToken(accessToken);

    private static IEnumerable<string> ClaimValues(JsonWebToken token, string claimType)
        => token.Claims.Where(c => c.Type == claimType).Select(c => c.Value);

    // ---------- Success ----------

    [Fact]
    public async Task Login_WithActiveUserAndCorrectPassword_ReturnsTheProfileAndAToken()
    {
        var controller = ControllerFor();

        var response = AssertOk(await controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));

        Assert.Equal("admin@example.com", response.UserId);
        Assert.Equal("Admin User", response.UserName);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal(3, response.AccessToken.Split('.').Length);
    }

    [Fact]
    public async Task Login_ReturnsTheStoredUserId_NotTheSuppliedCasing()
    {
        var controller = ControllerFor();

        var response = AssertOk(await controller.Login(Login("  ADMIN@EXAMPLE.COM  ", "CMS4fun#"), CancellationToken.None));

        Assert.Equal("admin@example.com", response.UserId);
    }

    [Fact]
    public async Task Login_UserWithNoRoles_StillSucceeds()
    {
        var controller = ControllerFor();

        var response = AssertOk(await controller.Login(Login("helen", "helen-pw"), CancellationToken.None));

        Assert.Equal("Helen Wu", response.UserName);
        Assert.Empty(ClaimValues(Read(response.AccessToken), JwtTokenService.RoleClaimType));
    }

    // ---------- 401 paths ----------

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var controller = ControllerFor();

        var result = await controller.Login(Login("admin@example.com", "wrong-password"), CancellationToken.None);

        AssertUnauthorized(result);
    }

    [Fact]
    public async Task Login_WithUnknownUserId_Returns401()
    {
        var controller = ControllerFor();

        var result = await controller.Login(Login("nobody@example.com", "CMS4fun#"), CancellationToken.None);

        AssertUnauthorized(result);
    }

    [Fact]
    public async Task Login_WithInactiveUser_Returns401_EvenWithTheCorrectPassword()
    {
        var controller = ControllerFor();

        var result = await controller.Login(Login("retired@example.com", "CMS4fun#"), CancellationToken.None);

        AssertUnauthorized(result);
    }

    [Fact]
    public async Task Login_EveryFailure_ReturnsTheSameGenericMessage()
    {
        var controller = ControllerFor();

        var wrongPassword = AssertUnauthorized(await controller.Login(Login("admin@example.com", "nope"), CancellationToken.None));
        var unknownUser = AssertUnauthorized(await controller.Login(Login("nobody@example.com", "CMS4fun#"), CancellationToken.None));
        var inactiveUser = AssertUnauthorized(await controller.Login(Login("retired@example.com", "CMS4fun#"), CancellationToken.None));

        // The body must not hint at which check failed.
        Assert.Equal(wrongPassword.Detail, unknownUser.Detail);
        Assert.Equal(wrongPassword.Detail, inactiveUser.Detail);
        Assert.Equal(wrongPassword.Title, unknownUser.Title);
        Assert.Equal(wrongPassword.Title, inactiveUser.Title);

        foreach (var text in new[] { wrongPassword.Detail, wrongPassword.Title })
        {
            Assert.DoesNotContain("password", text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("active", text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not found", text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Login_WhenTheStoredHashIsNotValidHex_Returns401()
    {
        var controller = ControllerFor(new InMemoryAuthRepository()
            .SeedHashed("legacy", "Legacy User", "not-a-sha256-hash"));

        var result = await controller.Login(Login("legacy", "CMS4fun#"), CancellationToken.None);

        AssertUnauthorized(result);
    }

    [Fact]
    public async Task Login_FailedAttempt_IssuesNoToken()
    {
        var controller = ControllerFor();

        var result = await controller.Login(Login("admin@example.com", "nope"), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.IsNotType<LoginResponse>(unauthorized.Value);
    }

    // ---------- Model validation ----------

    [Fact]
    public async Task Login_WithInvalidModelState_Returns400()
    {
        var controller = ControllerFor();
        controller.ModelState.AddModelError(nameof(LoginRequest.Password), "必填");

        var result = await controller.Login(Login("admin@example.com", string.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Token claims ----------

    [Fact]
    public async Task Login_IssuedToken_CarriesOneRoleClaimPerAssignedRole()
    {
        var controller = ControllerFor();

        var response = AssertOk(await controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));

        var roles = ClaimValues(Read(response.AccessToken), JwtTokenService.RoleClaimType).OrderBy(r => r, StringComparer.Ordinal);
        Assert.Equal(new[] { "Admin", "User" }, roles);
    }

    [Fact]
    public async Task Login_IssuedToken_CarriesTheUserIdAndUserName()
    {
        var controller = ControllerFor();

        var response = AssertOk(await controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));

        var token = Read(response.AccessToken);
        Assert.Equal("admin@example.com", Assert.Single(ClaimValues(token, JwtTokenService.UserIdClaimType)));
        Assert.Equal("Admin User", Assert.Single(ClaimValues(token, JwtTokenService.UserNameClaimType)));
        Assert.Equal("admin@example.com", Assert.Single(ClaimValues(token, JwtRegisteredClaimNames.Sub)));
    }

    [Fact]
    public async Task Login_IssuedToken_ExpiresIn24Hours()
    {
        var controller = ControllerFor();
        var before = DateTime.UtcNow;

        var response = AssertOk(await controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));

        var token = Read(response.AccessToken);
        var expected = before.Add(JwtTokenService.TokenLifetime);

        Assert.Equal(TimeSpan.FromHours(24), JwtTokenService.TokenLifetime);
        // JWT times have one-second resolution, so allow a small window around the call.
        Assert.True(
            (token.ValidTo - expected).Duration() < TimeSpan.FromMinutes(1),
            $"expiry {token.ValidTo:O} is not ~24h after {before:O}");
        Assert.True(token.ValidTo > DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task Login_IssuedToken_IsSignedWithTheKeyFromSysConfig()
    {
        var controller = ControllerFor();

        var response = AssertOk(await controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));

        var valid = await new JsonWebTokenHandler().ValidateTokenAsync(response.AccessToken, ValidationParameters(SigningKey));
        Assert.True(valid.IsValid, valid.Exception?.Message);

        var wrongKey = await new JsonWebTokenHandler().ValidateTokenAsync(
            response.AccessToken, ValidationParameters("some-other-secret-that-is-long-enough!"));
        Assert.False(wrongKey.IsValid);
    }

    [Fact]
    public async Task Login_WhenTheSigningKeyIsMissingFromSysConfig_Throws()
    {
        var controller = ControllerFor(sysConfig: new InMemorySysConfigRepository()
            .Seed("appConfig", """{ "defaultPassword": "CMS4fun#" }"""));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));
    }

    [Fact]
    public async Task Login_WhenTheAppConfigRowIsAbsent_Throws()
    {
        var controller = ControllerFor(sysConfig: new InMemorySysConfigRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));
    }

    // ---------- PasswordHash never leaves the server ----------

    [Fact]
    public void LoginResponse_HasNoPasswordProperty()
    {
        var properties = typeof(LoginResponse).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(properties, p => p.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new[] { "UserId", "UserName", "AccessToken" }.OrderBy(p => p), properties.OrderBy(p => p));
    }

    [Fact]
    public async Task Login_SerializedResponse_ContainsNeitherPasswordHashNorItsValue()
    {
        var controller = ControllerFor();
        var storedHash = PasswordHasher.Hash("CMS4fun#");

        var response = AssertOk(await controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));

        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(storedHash, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CMS4fun#", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_IssuedToken_CarriesNoPasswordMaterial()
    {
        var controller = ControllerFor();
        var storedHash = PasswordHasher.Hash("CMS4fun#");

        var response = AssertOk(await controller.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));

        // The payload is only base64url-encoded, so inspect the decoded claims themselves.
        var claims = Read(response.AccessToken).Claims.Select(c => $"{c.Type}={c.Value}").ToList();

        Assert.DoesNotContain(claims, c => c.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(claims, c => c.Contains(storedHash, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(claims, c => c.Contains("CMS4fun#", StringComparison.Ordinal));
    }

    // ---------- Hashing ----------

    [Fact]
    public void PasswordHasher_ProducesTheLowercaseHexSha256StoredInAppUser()
    {
        // The hash seeded in dbo.AppUser for the default password.
        Assert.Equal("7589e6405e39963484614ee26f6ebbbbd7b9ef9e6f9400dea0e53b08b5f9787e", PasswordHasher.Hash("CMS4fun#"));
        Assert.True(PasswordHasher.Matches("CMS4fun#", "7589E6405E39963484614EE26F6EBBBBD7B9EF9E6F9400DEA0E53B08B5F9787E"));
        Assert.False(PasswordHasher.Matches("cms4fun#", "7589e6405e39963484614ee26f6ebbbbd7b9ef9e6f9400dea0e53b08b5f9787e"));
    }

    // ---------- 個人資料 PUT /api/auth/profile ----------

    /// <summary>
    /// The controller under a signed-in identity. The claims are the ones
    /// <see cref="JwtTokenService"/> issues, so what the action reads here is what a real bearer
    /// token would give it.
    /// </summary>
    private static AuthController SignedInAs(
        InMemoryAuthRepository repository,
        string? userId,
        params string[] roleIds)
        => SignedInAsWith(repository, null, userId, roleIds);

    /// <summary>
    /// <see cref="SignedInAs"/> over a specific dbo.SysConfig — for the 變更密碼 tests, which turn
    /// <c>enforcePasswordPolicy</c> off or corrupt the row outright. A separate name rather than an
    /// optional parameter, because <c>params</c> has to stay last.
    /// </summary>
    private static AuthController SignedInAsWith(
        InMemoryAuthRepository repository,
        InMemorySysConfigRepository? sysConfig,
        string? userId,
        params string[] roleIds)
    {
        var controller = ControllerFor(repository, sysConfig);

        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId));
            claims.Add(new Claim(JwtTokenService.UserIdClaimType, userId));
        }

        claims.AddRange(roleIds.Select(roleId => new Claim(JwtTokenService.RoleClaimType, roleId)));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestBearer"))
            }
        };

        return controller;
    }

    private static UserProfileResponse AssertProfileOk(ActionResult<UserProfileResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<UserProfileResponse>(ok.Value);
    }

    [Fact]
    public async Task UpdateProfile_RenamesTheAccountTheTokenNames()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com", "Admin", "User");

        var response = AssertProfileOk(await controller.UpdateProfile(
            new UpdateProfileRequest { UserName = "王小明" }, CancellationToken.None));

        Assert.Equal("admin@example.com", response.UserId);
        Assert.Equal("王小明", response.UserName);
        Assert.Equal("王小明", repository.Stored("admin@example.com")!.UserName);
    }

    [Fact]
    public async Task UpdateProfile_WritesOnlyTheTokenUser_LeavingEveryOtherAccountAlone()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "helen");

        AssertProfileOk(await controller.UpdateProfile(
            new UpdateProfileRequest { UserName = "Helen Renamed" }, CancellationToken.None));

        Assert.Equal("Helen Renamed", repository.Stored("helen")!.UserName);
        Assert.Equal("Admin User", repository.Stored("admin@example.com")!.UserName);
    }

    [Fact]
    public void UpdateProfileRequest_HasNoUserIdOrRoleProperty_SoTheBodyCannotNameAnAccount()
    {
        // This is what makes "ignores a userId in the body" true: there is nothing to bind it to,
        // so System.Text.Json drops the property during deserialization.
        var properties = typeof(UpdateProfileRequest).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal(new[] { nameof(UpdateProfileRequest.UserName) }, properties);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNeitherTheKeyNorTheRolesNorTheCredential()
    {
        var repository = SeededRepository();
        var before = repository.Stored("admin@example.com")!;
        var storedHash = before.PasswordHash;
        var controller = SignedInAs(repository, "admin@example.com", "Admin", "User");

        await controller.UpdateProfile(new UpdateProfileRequest { UserName = "Renamed" }, CancellationToken.None);

        var after = repository.Stored("admin@example.com")!;
        Assert.Equal("admin@example.com", after.UserId);
        Assert.Equal(storedHash, after.PasswordHash);
        Assert.True(after.IsActive);
        Assert.Equal(new[] { "Admin", "User" }, await repository.GetRoleIdsAsync("admin@example.com"));
    }

    [Fact]
    public async Task UpdateProfile_TrimsTheUserName()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com");

        var response = AssertProfileOk(await controller.UpdateProfile(
            new UpdateProfileRequest { UserName = "  王小明  " }, CancellationToken.None));

        Assert.Equal("王小明", response.UserName);
        Assert.Equal("王小明", repository.Stored("admin@example.com")!.UserName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public async Task UpdateProfile_WithAnEmptyOrWhitespaceUserName_Returns400_AndKeepsTheStoredName(string userName)
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com");

        // [Required] rejects "" before the action runs — MVC would have added that error itself — while the
        // action's own trim check is what catches the whitespace-only cases.
        if (userName.Length == 0)
        {
            controller.ModelState.AddModelError(nameof(UpdateProfileRequest.UserName), "使用者名稱為必填。");
        }

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest { UserName = userName }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.IsType<SerializableError>(badRequest.Value);
        Assert.Equal("Admin User", repository.Stored("admin@example.com")!.UserName);
    }

    [Fact]
    public async Task UpdateProfile_WithNoUserIdClaim_Returns401()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, userId: null);

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest { UserName = "Nobody" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateProfile_WhenTheTokenUserNoLongerExists_Returns404()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "deleted@example.com");

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest { UserName = "Ghost" }, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public void UserProfileResponse_CarriesOnlyTheIdAndTheName()
    {
        var properties = typeof(UserProfileResponse).GetProperties().Select(p => p.Name).OrderBy(p => p);

        // No PasswordHash, and no roles — those ride in the token's `role` claims.
        Assert.Equal(new[] { "UserId", "UserName" }, properties);
    }

    // ---------- 變更密碼 PUT /api/auth/password ----------

    private static ChangePasswordRequest Change(string current, string next)
        => new() { CurrentPassword = current, NewPassword = next };

    /// <summary>The policy message, so no test re-states the rule text by hand.</summary>
    private static string PolicyMessage => PasswordPolicyService.PolicyMessage;

    private static ProblemDetails AssertRejected(IActionResult result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        return problem;
    }

    [Fact]
    public async Task ChangePassword_WithTheCorrectCurrentPassword_RewritesTheHashForTheTokenUser()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);

        var stored = repository.Stored("admin@example.com")!;
        Assert.Equal(PasswordHasher.Hash("N3wP@ssw0rd"), stored.PasswordHash);
        Assert.True(PasswordHasher.Matches("N3wP@ssw0rd", stored.PasswordHash));
        Assert.False(PasswordHasher.Matches("CMS4fun#", stored.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_StampsPasswordUpdatedTime()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com");

        Assert.Null(repository.PasswordUpdatedTime("admin@example.com"));

        await controller.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        Assert.NotNull(repository.PasswordUpdatedTime("admin@example.com"));
    }

    [Fact]
    public async Task ChangePassword_WritesOnlyTheTokenUser_LeavingEveryOtherAccountAlone()
    {
        var repository = SeededRepository();
        var helenHashBefore = repository.Stored("helen")!.PasswordHash;
        var controller = SignedInAs(repository, "admin@example.com");

        await controller.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        Assert.Equal(helenHashBefore, repository.Stored("helen")!.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_ChangesNeitherTheNameNorTheRolesNorIsActive()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com", "Admin", "User");

        await controller.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        var after = repository.Stored("admin@example.com")!;
        Assert.Equal("Admin User", after.UserName);
        Assert.True(after.IsActive);
        Assert.Equal(new[] { "Admin", "User" }, await repository.GetRoleIdsAsync("admin@example.com"));
    }

    /// <summary>
    /// The load-bearing status code. A 401 would trip the browser error interceptor, which clears
    /// the session and redirects to /login — so a typo in 目前密碼 would sign the user out.
    /// </summary>
    [Fact]
    public async Task ChangePassword_WithTheWrongCurrentPassword_Returns400_NOT401()
    {
        var repository = SeededRepository();
        var hashBefore = repository.Stored("admin@example.com")!.PasswordHash;
        var controller = SignedInAs(repository, "admin@example.com");

        var result = await controller.ChangePassword(Change("not-my-password", "N3wP@ssw0rd"), CancellationToken.None);

        Assert.IsNotType<UnauthorizedObjectResult>(result);
        Assert.Equal("目前密碼不正確。", AssertRejected(result).Detail);
        Assert.Equal(hashBefore, repository.Stored("admin@example.com")!.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_RefusesANewPasswordEqualToTheCurrentOne()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", "CMS4fun#"), CancellationToken.None);

        Assert.Equal("新密碼不可與目前密碼相同。", AssertRejected(result).Detail);
        Assert.Null(repository.PasswordUpdatedTime("admin@example.com"));
    }

    [Theory]
    [InlineData("Sh0rt!")]          // under 8 characters
    [InlineData("nouppercase1!")]   // no upper-case letter
    [InlineData("NOLOWERCASE1!")]   // no lower-case letter
    [InlineData("NoDigitsHere!")]   // no digit
    [InlineData("NoSymbolHere1")]   // no symbol
    public async Task ChangePassword_EnforcesTheStrengthRules_WhenThePolicyIsOn(string newPassword)
    {
        var repository = SeededRepository();
        var hashBefore = repository.Stored("admin@example.com")!.PasswordHash;
        var controller = SignedInAs(repository, "admin@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", newPassword), CancellationToken.None);

        Assert.Equal(PolicyMessage, AssertRejected(result).Detail);
        Assert.Equal(hashBefore, repository.Stored("admin@example.com")!.PasswordHash);
    }

    [Theory]
    [InlineData("N3wP@ssw0rd")]
    [InlineData("Aa1!aaaa")]        // exactly the 8-character minimum
    [InlineData("  Aa1! pad  ")]    // spaces count, and are never trimmed away
    public async Task ChangePassword_AcceptsAPasswordThatMeetsEveryRule(string newPassword)
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", newPassword), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        // Stored exactly as typed — trimming would make a legitimate password un-typeable.
        Assert.True(PasswordHasher.Matches(newPassword, repository.Stored("admin@example.com")!.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_WithThePolicyTurnedOff_AcceptsAWeakPassword()
    {
        var repository = SeededRepository();
        var sysConfig = new InMemorySysConfigRepository().Seed("appConfig", PolicyOffConfigJson);
        var controller = SignedInAsWith(repository, sysConfig, "admin@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", "x"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(PasswordHasher.Matches("x", repository.Stored("admin@example.com")!.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_WithThePolicyTurnedOff_StillRefusesABlankPassword()
    {
        var repository = SeededRepository();
        var sysConfig = new InMemorySysConfigRepository().Seed("appConfig", PolicyOffConfigJson);
        var controller = SignedInAsWith(repository, sysConfig, "admin@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", "   "), CancellationToken.None);

        Assert.Equal(PasswordPolicyService.RequiredMessage, AssertRejected(result).Detail);
    }

    [Fact]
    public async Task ChangePassword_WithAnUnreadableConfig_KeepsThePolicyOn()
    {
        // Fail closed: a configuration mistake must not quietly switch the password rules off.
        var repository = SeededRepository();
        var sysConfig = new InMemorySysConfigRepository().Seed("appConfig", "{ not json");
        var controller = SignedInAsWith(repository, sysConfig, "admin@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", "weak"), CancellationToken.None);

        Assert.Equal(PolicyMessage, AssertRejected(result).Detail);
    }

    [Fact]
    public async Task ChangePassword_WithInvalidModelState_Returns400()
    {
        var controller = SignedInAs(SeededRepository(), "admin@example.com");
        controller.ModelState.AddModelError(nameof(ChangePasswordRequest.NewPassword), "必填");

        var result = await controller.ChangePassword(Change("CMS4fun#", string.Empty), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.IsType<SerializableError>(badRequest.Value);
    }

    [Fact]
    public async Task ChangePassword_WithNoUserIdClaim_Returns401()
    {
        var controller = SignedInAs(SeededRepository(), userId: null);

        var result = await controller.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task ChangePassword_WhenTheTokenUserNoLongerExists_Returns404()
    {
        var controller = SignedInAs(SeededRepository(), "deleted@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ChangePassword_ForADisabledAccount_Returns404_AndWritesNothing()
    {
        var repository = SeededRepository();
        var hashBefore = repository.Stored("retired@example.com")!.PasswordHash;
        var controller = SignedInAs(repository, "retired@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(hashBefore, repository.Stored("retired@example.com")!.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_TheNewPasswordWorksAtLogin_AndTheOldOneDoesNot()
    {
        // The round trip that matters: the hash written here is the one Login checks against.
        var repository = SeededRepository();
        var changer = SignedInAs(repository, "admin@example.com");
        await changer.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        var loginController = ControllerFor(repository);

        AssertOk(await loginController.Login(Login("admin@example.com", "N3wP@ssw0rd"), CancellationToken.None));
        AssertUnauthorized(await loginController.Login(Login("admin@example.com", "CMS4fun#"), CancellationToken.None));
    }

    [Fact]
    public async Task ChangePassword_SendsNoPasswordMaterialBack()
    {
        var repository = SeededRepository();
        var controller = SignedInAs(repository, "admin@example.com");

        var result = await controller.ChangePassword(Change("CMS4fun#", "N3wP@ssw0rd"), CancellationToken.None);

        // 204 has no body at all, which is the strongest form of "the hash never travels back".
        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);
    }

    [Fact]
    public void ChangePasswordRequest_CarriesOnlyTheTwoPasswords()
    {
        // No UserId: the account is the token's. No confirm field either — re-typing is a UI check,
        // and sending it would only put the secret on the wire twice.
        var properties = typeof(ChangePasswordRequest).GetProperties().Select(p => p.Name).OrderBy(p => p);

        Assert.Equal(new[] { "CurrentPassword", "NewPassword" }, properties);
    }

    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
}
