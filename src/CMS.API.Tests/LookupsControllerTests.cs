using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>Covers the lookup lists the feature forms and filter drawers depend on.</summary>
public class LookupsControllerTests
{
    private static LookupsController Controller()
    {
        var users = new InMemoryAppUserRepository(
            new AppUserLookup { UserId = "miles@uuu.com.tw", UserName = "Miles Sun", IsActive = true },
            new AppUserLookup { UserId = "helen", UserName = "helen", IsActive = false });

        var roles = new InMemoryAppRoleRepository()
            .Seed("User", "User", 100, "一般使用者")
            .Seed("Admin", "Administrator", 1, "系統管理員");

        var publishStatuses = new InMemoryPublishStatusRepository()
            .Seed(2, "已發布", isDraft: false, isPublished: true, isDiscontinued: false)
            .Seed(1, "草稿", isDraft: true, isPublished: false, isDiscontinued: false);

        var partners = new InMemoryPartnerRepository()
            .Seed(1, "微軟", "MS", "Microsoft 微軟課程", "微軟", 20, "ms-logo.png")
            .Seed(2, "思科", "CISCO", "Cisco 思科課程", "思科", 10, "cisco.png");

        var courseGroups = new InMemoryCourseGroupRepository()
            .Seed(10, "雲端")
            .Seed(20, "資安");

        var jobCategories = new InMemoryJobCategoryRepository()
            .Seed(6, "網路管理")
            .Seed(5, "系統管理");

        var certifications = new InMemoryCertificationRepository()
            .Seed(200, "CCNA", 2)
            .Seed(100, "AZ-104", 1);

        return new LookupsController(
            users, roles, publishStatuses, partners, courseGroups, jobCategories, certifications,
            TrainingCenters(), Promotions());
    }

    private static InMemoryTrainingCenterRepository TrainingCenters()
        => new InMemoryTrainingCenterRepository()
            .Seed(54, "線上研討會", 5)
            .Seed(1, "台北", 1)
            .Seed(5, "高雄", 4)
            .Seed(2, "新竹", 2)
            .Seed(3, "台中", 3);

    private static InMemoryPromotion2Repository Promotions()
        => new InMemoryPromotion2Repository()
            .Seed(10, "20251204_SkillTrainAI", "成為能AI協作的程式設計師", "轉職就業養成班")
            .Seed(11, "251211_GoogleAI", "Google AI工具一次掌握", "不需技術基礎")
            .Seed(12, "20251215_n8n", "n8n自動化三部曲", "從自動化新手到企業級AI架構師")
            .Seed(13, "20260313_OpenShift", "認證總複習假日優惠班", "OpenShift");

    [Fact]
    public async Task GetAppUsers_ReturnsUsersSortedByUserName()
    {
        var result = await Controller().GetAppUsers(CancellationToken.None);

        var users = Assert.IsAssignableFrom<IEnumerable<AppUserLookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "Miles Sun", "helen" }, users.Select(u => u.UserName));
    }

    [Fact]
    public async Task GetAppUsers_CarriesTheIsActiveFlag()
    {
        var result = await Controller().GetAppUsers(CancellationToken.None);

        var users = Assert.IsAssignableFrom<IEnumerable<AppUserLookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.False(users.Single(u => u.UserId == "helen").IsActive);
    }

    [Fact]
    public async Task GetAppRoles_ReturnsRolesSortedByRoleId()
    {
        var result = await Controller().GetAppRoles(CancellationToken.None);

        var roles = Assert.IsAssignableFrom<IEnumerable<AppRole>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "Admin", "User" }, roles.Select(r => r.RoleId));
    }

    [Fact]
    public async Task GetPublishStatuses_ReturnsStatusesSortedByPkid()
    {
        var result = await Controller().GetPublishStatuses(CancellationToken.None);

        var statuses = Assert.IsAssignableFrom<IEnumerable<PublishStatus>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new byte[] { 1, 2 }, statuses.Select(s => s.Pkid));
        Assert.Equal(new[] { "草稿", "已發布" }, statuses.Select(s => s.Description));
    }

    [Fact]
    public async Task GetPartners_ReturnsPartnersSortedByDisplayOrder()
    {
        var result = await Controller().GetPartners(CancellationToken.None);

        var partners = Assert.IsAssignableFrom<IEnumerable<Partner>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new short[] { 2, 1 }, partners.Select(p => p.Pkid));
        Assert.Equal(new[] { "思科", "微軟" }, partners.Select(p => p.Name));
    }

    [Fact]
    public async Task GetCourseGroups_ReturnsGroupsSortedByDescription()
    {
        var result = await Controller().GetCourseGroups(CancellationToken.None);

        var groups = Assert.IsAssignableFrom<IEnumerable<CourseGroupLookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "資安", "雲端" }, groups.Select(g => g.Description));
        Assert.Equal(new short[] { 20, 10 }, groups.Select(g => g.Pkid));
    }

    [Fact]
    public async Task GetJobCategories_ReturnsCategoriesSortedByDescription()
    {
        var result = await Controller().GetJobCategories(CancellationToken.None);

        var categories = Assert.IsAssignableFrom<IEnumerable<JobCategoryLookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "系統管理", "網路管理" }, categories.Select(c => c.Description));
    }

    [Fact]
    public async Task GetCertifications_ReturnsCertificationsSortedByTitle_WithThePartnerKey()
    {
        var result = await Controller().GetCertifications(CancellationToken.None);

        var certifications = Assert.IsAssignableFrom<IEnumerable<CertificationLookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "AZ-104", "CCNA" }, certifications.Select(c => c.Title));
        Assert.Equal(new short[] { 1, 2 }, certifications.Select(c => c.PartnerPkid));
    }

    [Fact]
    public async Task GetTrainingCenters_ReturnsCentersSortedByDisplayOrder_ForTheTabStrip()
    {
        var result = await Controller().GetTrainingCenters(CancellationToken.None);

        var centers = Assert.IsAssignableFrom<IEnumerable<TrainingCenterLookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "台北", "新竹", "台中", "高雄", "線上研討會" }, centers.Select(c => c.Name));
        Assert.Equal(new short[] { 1, 2, 3, 5, 54 }, centers.Select(c => c.Pkid));
    }

    [Fact]
    public async Task GetPromotions_WithoutAKeyword_ReturnsEveryCode_NewestFirst()
    {
        var result = await Controller().GetPromotions(null, CancellationToken.None);

        var promotions = Assert.IsAssignableFrom<IEnumerable<Promotion2Lookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(
            new[] { "251211_GoogleAI", "20260313_OpenShift", "20251215_n8n", "20251204_SkillTrainAI" },
            promotions.Select(p => p.PromoCode));
    }

    [Fact]
    public async Task GetPromotions_MatchesThePromoCodePrefix_CaseInsensitively()
    {
        var result = await Controller().GetPromotions("2025", CancellationToken.None);

        var promotions = Assert.IsAssignableFrom<IEnumerable<Promotion2Lookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(new[] { "20251215_n8n", "20251204_SkillTrainAI" }, promotions.Select(p => p.PromoCode));

        var lower = await Controller().GetPromotions("20251204_skilltrainai", CancellationToken.None);
        var match = Assert.IsAssignableFrom<IEnumerable<Promotion2Lookup>>(
            Assert.IsType<OkObjectResult>(lower.Result).Value).Single();

        Assert.Equal(10, match.Pkid);
    }

    [Fact]
    public async Task GetPromotions_CarriesTopicAndDescription_ForTheFormToPreFill()
    {
        var result = await Controller().GetPromotions("20251215", CancellationToken.None);

        var promotion = Assert.IsAssignableFrom<IEnumerable<Promotion2Lookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).Single();

        Assert.Equal(12, promotion.Pkid);
        Assert.Equal("n8n自動化三部曲", promotion.Topic);
        Assert.Equal("從自動化新手到企業級AI架構師", promotion.Description);
    }

    [Fact]
    public async Task GetPromotions_ReturnsNothing_WhenNoCodeMatches()
    {
        var result = await Controller().GetPromotions("zzz", CancellationToken.None);

        var promotions = Assert.IsAssignableFrom<IEnumerable<Promotion2Lookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Empty(promotions);
    }

    [Fact]
    public async Task GetPromotions_CapsTheAutocompleteAtTwentyRows()
    {
        var promotions = new InMemoryPromotion2Repository();
        for (var i = 1; i <= 25; i++)
        {
            promotions.Seed(100 + i, $"2026{i:00}_Promo");
        }

        var controller = new LookupsController(
            new InMemoryAppUserRepository(),
            new InMemoryAppRoleRepository(),
            new InMemoryPublishStatusRepository(),
            new InMemoryPartnerRepository(),
            new InMemoryCourseGroupRepository(),
            new InMemoryJobCategoryRepository(),
            new InMemoryCertificationRepository(),
            new InMemoryTrainingCenterRepository(),
            promotions);

        var result = await controller.GetPromotions("2026", CancellationToken.None);

        var rows = Assert.IsAssignableFrom<IEnumerable<Promotion2Lookup>>(
            Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

        Assert.Equal(20, rows.Count);
        Assert.Equal("202625_Promo", rows[0].PromoCode);
    }
}
