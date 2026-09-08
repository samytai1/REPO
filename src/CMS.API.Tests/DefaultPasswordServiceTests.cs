using CMS.API.Infrastructure;
using CMS.API.Tests.Fakes;

namespace CMS.API.Tests;

/// <summary>
/// Covers 預設密碼 <see cref="DefaultPasswordService"/> on its own, over an in-memory dbo.SysConfig.
///
/// It needs its own suite rather than a corner of <see cref="AuthControllerTests"/> because most of
/// what matters here is unreachable through <c>Login</c>: an appConfig row that is missing or
/// unreadable makes <c>JwtTokenService.GetSigningKeyAsync</c> throw first, so the fail-open branches
/// can only be observed by calling the service directly.
/// </summary>
public class DefaultPasswordServiceTests
{
    private const string SigningKey = "cloud4fun#123456cloud4fun#123456";

    /// <summary>The row as production holds it: a signing key beside the shared default password.</summary>
    private static readonly string AppConfigJson = $$"""
    {
      "defaultPassword": "CMS4fun#",
      "symmetricSecurityKey": "{{SigningKey}}",
      "enforcePasswordPolicy": true
    }
    """;

    private static DefaultPasswordService ServiceOver(string? configValue)
    {
        var sysConfig = new InMemorySysConfigRepository();

        if (configValue is not null) sysConfig.Seed(JwtTokenService.AppConfigKey, configValue);

        return new DefaultPasswordService(sysConfig);
    }

    // ---------- The match itself ----------

    [Fact]
    public async Task IsDefault_WhenTheStoredHashIsTheConfiguredDefault_IsTrue()
    {
        var service = ServiceOver(AppConfigJson);

        Assert.True(await service.IsDefaultAsync(PasswordHasher.Hash("CMS4fun#")));
    }

    [Fact]
    public async Task IsDefault_WhenTheStoredHashIsAnythingElse_IsFalse()
    {
        var service = ServiceOver(AppConfigJson);

        Assert.False(await service.IsDefaultAsync(PasswordHasher.Hash("N3wP@ssw0rd")));
        Assert.False(await service.IsDefaultAsync(PasswordHasher.Hash("cms4fun#")));
    }

    [Fact]
    public async Task IsDefault_IgnoresTheHexCasingOfTheStoredHash()
    {
        // dbo.AppUser holds lowercase hex, but PasswordHasher.Matches compares decoded bytes, so a
        // row written in upper case by hand still reads as the default.
        var service = ServiceOver(AppConfigJson);

        Assert.True(await service.IsDefaultAsync(
            "7589E6405E39963484614EE26F6EBBBBD7B9EF9E6F9400DEA0E53B08B5F9787E"));
    }

    [Theory]
    [InlineData("not-a-sha256-hash")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task IsDefault_WithAStoredHashThatIsNotAHash_IsFalse(string? storedHash)
    {
        var service = ServiceOver(AppConfigJson);

        Assert.False(await service.IsDefaultAsync(storedHash));
    }

    // ---------- Fail open ----------

    /// <summary>
    /// Every unreadable shape means "nobody is forced". Failing closed here would flag every
    /// account at once and answer 403 to the whole API — the inverse of
    /// <see cref="PasswordPolicyService"/>, which reads the same row and fails closed.
    /// </summary>
    [Theory]
    [InlineData(null)]                                              // no appConfig row at all
    [InlineData("")]                                                // present but empty
    [InlineData("   ")]
    [InlineData("{ not json")]                                      // unparseable
    [InlineData("[1, 2, 3]")]                                       // root is not an object
    [InlineData("""{ "symmetricSecurityKey": "abc" }""")]           // property absent
    [InlineData("""{ "defaultPassword": "" }""")]                   // blank
    [InlineData("""{ "defaultPassword": "   " }""")]                // whitespace
    [InlineData("""{ "defaultPassword": 123 }""")]                  // not a string
    [InlineData("""{ "defaultPassword": null }""")]
    [InlineData("""{ "defaultPassword": ["CMS4fun#"] }""")]
    public async Task IsDefault_FailsOpen_WhenTheDefaultCannotBeRead(string? configValue)
    {
        var service = ServiceOver(configValue);

        Assert.False(await service.IsDefaultAsync(PasswordHasher.Hash("CMS4fun#")));
    }

    [Fact]
    public async Task IsDefault_WithABlankDefaultPassword_DoesNotMatchTheHashOfTheEmptyString()
    {
        // Hash("") is a real, computable value. If the blank check ran *after* the comparison it
        // would become a live target — every account whose hash is e3b0c442… would be flagged.
        var service = ServiceOver("""{ "defaultPassword": "" }""");

        Assert.False(await service.IsDefaultAsync(PasswordHasher.Hash(string.Empty)));
    }

    [Fact]
    public async Task IsDefault_AndThePasswordPolicy_FailInOppositeDirections_OverTheSameBadRow()
    {
        // The one place the two services that read appConfig disagree on purpose, asserted in code
        // rather than left to the comments.
        var sysConfig = new InMemorySysConfigRepository().Seed(JwtTokenService.AppConfigKey, "{ not json");

        Assert.False(await new DefaultPasswordService(sysConfig).IsDefaultAsync(PasswordHasher.Hash("CMS4fun#")));
        Assert.Equal(
            PasswordPolicyService.PolicyMessage,
            await new PasswordPolicyService(sysConfig).ValidateAsync("weak"));
    }

    // ---------- Read per call ----------

    [Fact]
    public async Task IsDefault_ReadsTheConfigOnEveryCall_SoARotationTakesEffectImmediately()
    {
        var sysConfig = new InMemorySysConfigRepository()
            .Seed(JwtTokenService.AppConfigKey, """{ "defaultPassword": "CMS4fun#" }""");
        var service = new DefaultPasswordService(sysConfig);

        Assert.True(await service.IsDefaultAsync(PasswordHasher.Hash("CMS4fun#")));

        sysConfig.Seed(JwtTokenService.AppConfigKey, """{ "defaultPassword": "Welcome#2026" }""");

        Assert.False(await service.IsDefaultAsync(PasswordHasher.Hash("CMS4fun#")));
        Assert.True(await service.IsDefaultAsync(PasswordHasher.Hash("Welcome#2026")));
    }
}
