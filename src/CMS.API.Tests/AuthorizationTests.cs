using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CMS.API.Infrastructure;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// End-to-end cover for the request pipeline set up in <c>Program.cs</c>: a global fallback policy
/// requires an authenticated user on every endpoint, and <c>AuthController.Login</c> is the one
/// <c>[AllowAnonymous]</c> exception.
///
/// These run against a real host (<see cref="TestApiFactory"/>) rather than a controller instance,
/// because that is the only way the middleware — not the action — is what gets tested.
/// </summary>
public class AuthorizationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthorizationTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- No token ----------

    [Fact]
    public async Task ProtectedEndpoint_WithoutABearerToken_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(TestApiFactory.ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutABearerToken_ChallengesWithTheBearerScheme()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(TestApiFactory.ProtectedRoute);

        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    /// <summary>
    /// Authorization is global, not per-controller: nothing has to be remembered when a feature is
    /// added. Every verb and every route family is closed by the same fallback policy — including
    /// PUT /api/auth/profile, which sits on the same controller as the one anonymous action.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/app-roles")]
    [InlineData("GET", "/api/publish-statuses")]
    [InlineData("GET", "/api/partners")]
    [InlineData("GET", "/api/courses")]
    [InlineData("GET", "/api/featured-promo-items")]
    [InlineData("GET", "/api/lookups/partners")]
    [InlineData("GET", "/api/publish-statuses/1")]
    [InlineData("POST", "/api/publish-statuses/query")]
    [InlineData("POST", "/api/publish-statuses")]
    [InlineData("PUT", "/api/publish-statuses")]
    [InlineData("DELETE", "/api/publish-statuses/1")]
    [InlineData("PUT", "/api/auth/profile")]
    [InlineData("PUT", "/api/auth/password")]
    public async Task EveryEndpointButLogin_Returns401_WithoutABearerToken(string method, string route)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);

        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- A valid token ----------

    [Fact]
    public async Task ProtectedEndpoint_WithAValidBearerToken_Returns200AndTheData()
    {
        var token = await _factory.LoginAsync();
        using var client = _factory.CreateClientWithToken(token);

        var response = await client.GetAsync(TestApiFactory.ProtectedRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var statuses = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();
        Assert.Equal(new byte[] { 1, 2 }, statuses!.Select(s => s.Pkid));
    }

    [Fact]
    public async Task ProtectedEndpoint_AcceptsATokenIssuedByTheLoginEndpoint()
    {
        // The full round trip: sign in, then spend the token that came back.
        var token = await _factory.LoginAsync("helen", "helen-pw");
        using var client = _factory.CreateClientWithToken(token);

        var response = await client.GetAsync($"{TestApiFactory.ProtectedRoute}/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- Tokens that must not open the door ----------

    [Fact]
    public async Task ProtectedEndpoint_WithAMalformedToken_Returns401()
    {
        using var client = _factory.CreateClientWithToken("not-even-a-jwt");

        var response = await client.GetAsync(TestApiFactory.ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithATokenSignedByAnotherKey_Returns401()
    {
        var foreign = TestApiFactory.SignToken(TestApiFactory.ForeignSigningKey);
        using var client = _factory.CreateClientWithToken(foreign);

        var response = await client.GetAsync(TestApiFactory.ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAnExpiredToken_Returns401()
    {
        // Well past the one-minute clock skew the validation parameters allow.
        var expired = TestApiFactory.SignToken(TestApiFactory.SigningKey, TimeSpan.FromMinutes(-30));
        using var client = _factory.CreateClientWithToken(expired);

        var response = await client.GetAsync(TestApiFactory.ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTheTokenButNotTheBearerScheme_Returns401()
    {
        var token = await _factory.LoginAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

        var response = await client.GetAsync(TestApiFactory.ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithATamperedPayload_Returns401()
    {
        var token = await _factory.LoginAsync();
        var parts = token.Split('.');

        // Same header and payload, one character flipped in the signature.
        var signature = parts[2];
        parts[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];

        using var client = _factory.CreateClientWithToken(string.Join('.', parts));

        var response = await client.GetAsync(TestApiFactory.ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- The signing key really comes from dbo.SysConfig ----------

    [Fact]
    public async Task RotatingTheSecretInSysConfig_InvalidatesAlreadyIssuedTokens_WithoutARestart()
    {
        // Its own host: the rotation must not leak into the tests sharing the class fixture.
        await using var factory = new TestApiFactory();

        var token = await factory.LoginAsync();

        using (var before = factory.CreateClientWithToken(token))
        {
            Assert.Equal(HttpStatusCode.OK, (await before.GetAsync(TestApiFactory.ProtectedRoute)).StatusCode);
        }

        factory.RotateSigningKey(TestApiFactory.ForeignSigningKey);

        using (var after = factory.CreateClientWithToken(token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await after.GetAsync(TestApiFactory.ProtectedRoute)).StatusCode);
        }

        // …and a token issued under the new secret is accepted straight away.
        var reissued = await factory.LoginAsync();
        using var reissuedClient = factory.CreateClientWithToken(reissued);
        Assert.Equal(HttpStatusCode.OK, (await reissuedClient.GetAsync(TestApiFactory.ProtectedRoute)).StatusCode);
    }

    // ---------- Auth stays anonymous ----------

    [Fact]
    public async Task Login_NeedsNoBearerToken()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = TestApiFactory.AdminUserId,
            Password = TestApiFactory.AdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal(TestApiFactory.AdminUserName, body!.UserName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task Login_WithBadCredentials_Returns401FromTheCredentialCheck_NotAChallenge()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = TestApiFactory.AdminUserId,
            Password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // A rejected login answers with the controller's own body; an unauthenticated request to a
        // protected endpoint answers with an empty challenge. Only the first names the failure.
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>();
        Assert.Equal("帳號或密碼錯誤。", problem!.Detail);
    }

    [Fact]
    public async Task Login_WithAnInvalidToken_StillSucceeds_BecauseItIsAnonymous()
    {
        // A stale token sitting in the browser must not lock the user out of signing in again.
        using var client = _factory.CreateClientWithToken(
            TestApiFactory.SignToken(TestApiFactory.ForeignSigningKey));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = TestApiFactory.AdminUserId,
            Password = TestApiFactory.AdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- 個人資料 over the real pipeline ----------

    /// <summary>
    /// The whole point of the endpoint, asserted where it matters: the row that changes is the one
    /// the *token* names, and the request body has no say in it.
    /// </summary>
    [Fact]
    public async Task UpdateProfile_WithAToken_RenamesTheTokenUser()
    {
        // Its own factory: this test mutates the seeded user, and the class fixture is shared.
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(await factory.LoginAsync());

        var response = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            UserName = "王小明"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal(TestApiFactory.AdminUserId, body!.UserId);
        Assert.Equal("王小明", body.UserName);
    }

    [Fact]
    public async Task UpdateProfile_IgnoresAUserIdInTheRequestBody_AndRenamesTheTokenUserInstead()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(await factory.LoginAsync("helen", "helen-pw"));

        // Raw JSON, because the typed DTO cannot even express this: helen asks to rename admin.
        using var content = new StringContent(
            """{ "userId": "admin@example.com", "userName": "Hijacked", "roles": ["Admin"] }""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PutAsync("/api/auth/profile", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("helen", body!.UserId);
        Assert.Equal("Hijacked", body.UserName);

        // The account named in the body is untouched, and helen gained no role.
        Assert.Equal(TestApiFactory.AdminUserName, factory.Users.Stored(TestApiFactory.AdminUserId)!.UserName);
        Assert.Empty(await factory.Users.GetRoleIdsAsync("helen"));
    }

    [Theory]
    [InlineData("""{ "userName": "" }""")]
    [InlineData("""{ "userName": "   " }""")]
    [InlineData("""{ }""")]
    public async Task UpdateProfile_WithAnEmptyOrWhitespaceUserName_Returns400_AndChangesNothing(string json)
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(await factory.LoginAsync());
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/api/auth/profile", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(TestApiFactory.AdminUserName, factory.Users.Stored(TestApiFactory.AdminUserId)!.UserName);
    }

    [Fact]
    public async Task UpdateProfile_WithAForeignSignedToken_Returns401_AndChangesNothing()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(
            TestApiFactory.SignToken(TestApiFactory.ForeignSigningKey));

        var response = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest
        {
            UserName = "Forged"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TestApiFactory.AdminUserName, factory.Users.Stored(TestApiFactory.AdminUserId)!.UserName);
    }

    // ---------- 變更密碼 over the real pipeline ----------

    [Fact]
    public async Task ChangePassword_WithAToken_RewritesTheHash_AndTheNewPasswordThenLogsIn()
    {
        // Its own factory: this mutates the seeded user, and the class fixture is shared.
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(await factory.LoginAsync());

        var response = await client.PutAsJsonAsync("/api/auth/password", new ChangePasswordRequest
        {
            CurrentPassword = TestApiFactory.AdminPassword,
            NewPassword = "N3wP@ssw0rd"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);

        // The endpoint that matters: the new password is the one login now accepts.
        Assert.False(string.IsNullOrWhiteSpace(
            await factory.LoginAsync(TestApiFactory.AdminUserId, "N3wP@ssw0rd")));

        using var stale = factory.CreateClient();
        var oldPassword = await stale.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserId = TestApiFactory.AdminUserId,
            Password = TestApiFactory.AdminPassword
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
    }

    /// <summary>
    /// The status code the whole feature hangs on. The Angular error interceptor turns any 401
    /// outside the login call into "your session expired" — it clears the session and redirects to
    /// /login. So a mistyped 目前密碼 must come back 400, or changing a password with a typo would
    /// sign the user out instead of showing them the message.
    /// </summary>
    [Fact]
    public async Task ChangePassword_WithTheWrongCurrentPassword_Returns400_NotA401ThatWouldSignTheUserOut()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(await factory.LoginAsync());

        var response = await client.PutAsJsonAsync("/api/auth/password", new ChangePasswordRequest
        {
            CurrentPassword = "not-my-password",
            NewPassword = "N3wP@ssw0rd"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>();
        Assert.Equal("目前密碼不正確。", problem!.Detail);

        // And the old password still works, so nothing was written.
        Assert.False(string.IsNullOrWhiteSpace(await factory.LoginAsync()));
    }

    [Fact]
    public async Task ChangePassword_WithAWeakNewPassword_Returns400_AndNamesTheRule()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(await factory.LoginAsync());

        var response = await client.PutAsJsonAsync("/api/auth/password", new ChangePasswordRequest
        {
            CurrentPassword = TestApiFactory.AdminPassword,
            NewPassword = "weak"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>();
        Assert.Equal(PasswordPolicyService.PolicyMessage, problem!.Detail);
    }

    [Fact]
    public async Task ChangePassword_CannotNameAnotherAccount_WhateverTheBodyCarries()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(await factory.LoginAsync("helen", "helen-pw"));

        // Raw JSON, because the typed DTO cannot express this: helen tries to reset admin.
        using var content = new StringContent(
            """{ "userId": "admin@example.com", "currentPassword": "helen-pw", "newPassword": "N3wP@ssw0rd" }""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PutAsync("/api/auth/password", content);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // helen's own password changed; admin's did not.
        Assert.False(string.IsNullOrWhiteSpace(await factory.LoginAsync("helen", "N3wP@ssw0rd")));
        Assert.False(string.IsNullOrWhiteSpace(await factory.LoginAsync()));
    }

    [Fact]
    public async Task ChangePassword_DoesNotInvalidateTheCallersOwnToken()
    {
        // The session deliberately survives: there is no revocation, so signing the user out here
        // would only suggest one. The same token keeps working on the very next request.
        using var factory = new TestApiFactory();
        var token = await factory.LoginAsync();
        using var client = factory.CreateClientWithToken(token);

        var change = await client.PutAsJsonAsync("/api/auth/password", new ChangePasswordRequest
        {
            CurrentPassword = TestApiFactory.AdminPassword,
            NewPassword = "N3wP@ssw0rd"
        });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var afterwards = await client.GetAsync(TestApiFactory.ProtectedRoute);
        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithAForeignSignedToken_Returns401_AndChangesNothing()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClientWithToken(
            TestApiFactory.SignToken(TestApiFactory.ForeignSigningKey));

        var response = await client.PutAsJsonAsync("/api/auth/password", new ChangePasswordRequest
        {
            CurrentPassword = TestApiFactory.AdminPassword,
            NewPassword = "N3wP@ssw0rd"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await factory.LoginAsync()));
    }

    // ---------- Swagger stays reachable ----------

    [Fact]
    public async Task SwaggerDocument_IsServedWithoutAToken()
    {
        // The Swagger middleware sits ahead of authorization, so the docs stay usable — which is
        // how a developer gets a token in the first place.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(JwtBearerSchemeName, await response.Content.ReadAsStringAsync());
    }

    private const string JwtBearerSchemeName = "Bearer";

    /// <summary>Just enough of <c>ProblemDetails</c> to read the message back off the wire.</summary>
    private sealed class ProblemDetailsBody
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
    }
}
