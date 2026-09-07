using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>
/// Covers the 合作夥伴 Partner endpoints: list, filtered query, view, add, edit and delete —
/// including the still-referenced conflict path. There is no duplicate-key path here: pkid is an
/// IDENTITY column, so the user never supplies it.
/// </summary>
public class PartnersControllerTests
{
    /// <summary>Three seeded partners, deliberately out of DisplayOrder sequence by pkid.</summary>
    private static InMemoryPartnerRepository SeededRepository()
        => new InMemoryPartnerRepository()
            .Seed(1, "微軟", "MS", "Microsoft 微軟課程", "微軟", 20, "ms-logo.png")
            .Seed(2, "思科", "CISCO", "Cisco 思科課程", "思科", 10, "cisco.png")
            .Seed(3, "自辦課程", "OWN", "恆逸自辦課程", "恆逸", 30);

    private static PartnersController ControllerFor(InMemoryPartnerRepository repository)
        => new(repository);

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private static PartnerRequest Request(
        string name = "紅帽",
        string appKey = "RH",
        string menu = "Red Hat 紅帽課程",
        string detail = "紅帽",
        int displayOrder = 40,
        string? image = null)
        => new()
        {
            Name = name,
            AppKey = appKey,
            NameOnPartnerMenu = menu,
            NameOnCourseDetailPage = detail,
            DisplayOrder = displayOrder,
            ImageFilename = image
        };

    private static PartnerUpdateRequest UpdateRequest(
        short pkid,
        string name = "紅帽",
        string appKey = "RH",
        string menu = "Red Hat 紅帽課程",
        string detail = "紅帽",
        int displayOrder = 40,
        string? image = null)
        => new()
        {
            Pkid = pkid,
            Name = name,
            AppKey = appKey,
            NameOnPartnerMenu = menu,
            NameOnCourseDetailPage = detail,
            DisplayOrder = displayOrder,
            ImageFilename = image
        };

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsEveryPartner_SortedByDisplayOrderThenPkid()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(new short[] { 2, 1, 3 }, partners.Select(p => p.Pkid));
    }

    [Fact]
    public async Task GetAll_ProjectsEveryColumn()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        var microsoft = partners.Single(p => p.Pkid == 1);
        Assert.Equal("微軟", microsoft.Name);
        Assert.Equal("MS", microsoft.AppKey);
        Assert.Equal("Microsoft 微軟課程", microsoft.NameOnPartnerMenu);
        Assert.Equal("微軟", microsoft.NameOnCourseDetailPage);
        Assert.Equal(20, microsoft.DisplayOrder);
        Assert.Equal("ms-logo.png", microsoft.ImageFilename);
    }

    [Fact]
    public async Task GetAll_LeavesAMissingImageFilenameNull()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Null(partners.Single(p => p.Pkid == 3).ImageFilename);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllPartners()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(new PartnerQuery(), CancellationToken.None)).ToList();

        Assert.Equal(3, partners.Count);
    }

    [Fact]
    public async Task Query_KeywordMatchesName()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(new PartnerQuery { Keyword = "思科" }, CancellationToken.None)).ToList();

        Assert.Equal<short>(2, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_KeywordMatchesAppKey()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(new PartnerQuery { Keyword = "CISCO" }, CancellationToken.None)).ToList();

        Assert.Equal<short>(2, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_KeywordMatchesNameOnPartnerMenu()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(new PartnerQuery { Keyword = "恆逸自辦" }, CancellationToken.None)).ToList();

        Assert.Equal<short>(3, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_KeywordMatchesNameOnCourseDetailPage()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(new PartnerQuery { Keyword = "恆逸" }, CancellationToken.None)).ToList();

        Assert.Equal<short>(3, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_DisplayOrderRange_IsInclusiveOnBothEnds()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(
            new PartnerQuery { DisplayOrderFrom = 10, DisplayOrderTo = 20 },
            CancellationToken.None)).ToList();

        Assert.Equal(new short[] { 2, 1 }, partners.Select(p => p.Pkid));
    }

    [Fact]
    public async Task Query_HasImageTrue_ReturnsOnlyPartnersWithAnImage()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(new PartnerQuery { HasImage = true }, CancellationToken.None)).ToList();

        Assert.Equal(new short[] { 2, 1 }, partners.Select(p => p.Pkid));
    }

    [Fact]
    public async Task Query_HasImageFalse_ReturnsOnlyPartnersWithout()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(new PartnerQuery { HasImage = false }, CancellationToken.None)).ToList();

        Assert.Equal<short>(3, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_HasImageFalse_TreatsAWhitespaceOnlyFilenameAsNoImage()
    {
        var repository = SeededRepository().Seed(4, "空白圖", "BLANK", "空白圖課程", "空白圖", 50, "   ");
        var controller = ControllerFor(repository);

        var partners = AssertOk(await controller.Query(new PartnerQuery { HasImage = false }, CancellationToken.None)).ToList();

        Assert.Equal(new short[] { 3, 4 }, partners.Select(p => p.Pkid));
    }

    [Fact]
    public async Task Query_CombinesFiltersWithAnd()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(
            new PartnerQuery { Keyword = "課程", HasImage = false },
            CancellationToken.None)).ToList();

        Assert.Equal<short>(3, Assert.Single(partners).Pkid);
    }

    [Fact]
    public async Task Query_WithNullBody_IsTreatedAsAnEmptyFilter()
    {
        var controller = ControllerFor(SeededRepository());

        var partners = AssertOk(await controller.Query(null!, CancellationToken.None)).ToList();

        Assert.Equal(3, partners.Count);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsThePartner()
    {
        var controller = ControllerFor(SeededRepository());

        var partner = AssertOk(await controller.GetById(2, CancellationToken.None));

        Assert.Equal("思科", partner.Name);
        Assert.Equal("cisco.png", partner.ImageFilename);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenThePartnerIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_PersistsThePartner_AndReturnsCreatedAtActionWithTheAssignedPkid()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Create(Request(image: "redhat.png"), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var partner = Assert.IsType<Partner>(created.Value);

        Assert.Equal(nameof(PartnersController.GetById), created.ActionName);
        Assert.Equal(4, created.RouteValues!["id"]);
        Assert.Equal<short>(4, partner.Pkid);
        Assert.Equal("紅帽", partner.Name);
        Assert.NotNull(await repository.GetByIdAsync(4, CancellationToken.None));
    }

    [Fact]
    public async Task Create_TrimsEveryStringColumn()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Create(
            Request(name: "  紅帽  ", appKey: "  RH  ", menu: "  Red Hat  ", detail: "  紅帽  ", image: "  rh.png  "),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(4, CancellationToken.None);
        Assert.Equal("紅帽", stored!.Name);
        Assert.Equal("RH", stored.AppKey);
        Assert.Equal("Red Hat", stored.NameOnPartnerMenu);
        Assert.Equal("紅帽", stored.NameOnCourseDetailPage);
        Assert.Equal("rh.png", stored.ImageFilename);
    }

    [Fact]
    public async Task Create_StoresABlankImageFilenameAsNull()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Create(Request(image: "   "), CancellationToken.None);

        var stored = await repository.GetByIdAsync(4, CancellationToken.None);
        Assert.Null(stored!.ImageFilename);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(PartnerRequest.Name), "名稱為必填。");

        var result = await controller.Create(Request(name: string.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_RewritesEveryEditableField()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var partner = AssertOk(await controller.Update(
            UpdateRequest(1, name: "微軟（新）", appKey: "MSFT", menu: "Microsoft", detail: "MS", displayOrder: 5, image: "new.png"),
            CancellationToken.None));

        Assert.Equal("微軟（新）", partner.Name);
        Assert.Equal("MSFT", partner.AppKey);
        Assert.Equal(5, partner.DisplayOrder);
        Assert.Equal("new.png", partner.ImageFilename);

        var stored = await repository.GetByIdAsync(1, CancellationToken.None);
        Assert.Equal("微軟（新）", stored!.Name);
    }

    [Fact]
    public async Task Update_ClearsTheImageFilename_WhenTheFieldIsBlanked()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Update(UpdateRequest(1, name: "微軟", image: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(1, CancellationToken.None);
        Assert.Null(stored!.ImageFilename);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenThePartnerIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Update(UpdateRequest(99), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(PartnerUpdateRequest.Name), "名稱為必填。");

        var result = await controller.Update(UpdateRequest(1, name: string.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesThePartner()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Delete(3, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync(3, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenThePartnerIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenAChildTableStillReferencesThePartner()
    {
        var repository = SeededRepository().MarkInUse(1);
        var controller = ControllerFor(repository);

        var result = await controller.Delete(1, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.NotNull(await repository.GetByIdAsync(1, CancellationToken.None));
    }
}
