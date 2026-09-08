using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>
/// Covers the 上稿作業 FeaturedPromoItem endpoints: the weekly grid query (one training center,
/// one Monday-to-Sunday week), view, add, edit, delete, the slot-move swap, and the UNIQUE
/// (ScheduleOn, TrainingCenter_pkid, Slot) conflict on create and update.
/// </summary>
public class FeaturedPromoItemsControllerTests
{
    // 2026-03-16 is a Monday; the seeded week is 03-16 (Mon) … 03-22 (Sun).
    private static readonly DateOnly Mon = new(2026, 3, 16);
    private static readonly DateOnly Tue = new(2026, 3, 17);
    private static readonly DateOnly Wed = new(2026, 3, 18);
    private static readonly DateOnly Sun = new(2026, 3, 22);
    private static readonly DateOnly PrevSun = new(2026, 3, 15);
    private static readonly DateOnly NextMon = new(2026, 3, 23);

    private static InMemoryFeaturedPromoItemRepository SeededRepository()
        => new InMemoryFeaturedPromoItemRepository()
            .SeedTrainingCenter(1, "台北")
            .SeedTrainingCenter(2, "新竹")
            .SeedPromotion(10, "20251204_SkillTrainAI")
            .SeedPromotion(11, "251211_GoogleAI")
            .SeedPromotion(12, "20251215_n8n")
            .Seed(1, Mon, 1, 1, 10, "成為能AI協作的程式設計師", "轉職就業養成班")
            .Seed(2, Mon, 1, 2, 11, "Google AI工具一次掌握", "不需技術基礎")
            .Seed(3, Tue, 1, 1, 12, "n8n自動化三部曲", "從自動化新手到企業級AI架構師")
            .Seed(4, Sun, 1, 1, 10)
            .Seed(5, NextMon, 1, 1, 11)
            .Seed(6, PrevSun, 1, 1, 12)
            .Seed(7, Wed, 2, 1, 10);

    private static FeaturedPromoItemsController ControllerFor(InMemoryFeaturedPromoItemRepository repository)
        => new(repository);

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private static FeaturedPromoItemRequest Request(
        DateOnly? scheduleOn = null,
        short trainingCenterPkid = 1,
        byte slot = 3,
        int promotionPkid = 12,
        string topic = "n8n自動化三部曲",
        string description = "從自動化新手到企業級AI架構師")
        => new()
        {
            ScheduleOn = scheduleOn ?? Mon,
            TrainingCenterPkid = trainingCenterPkid,
            Slot = slot,
            PromotionPkid = promotionPkid,
            Topic = topic,
            Description = description
        };

    private static FeaturedPromoItemUpdateRequest UpdateRequest(
        int pkid,
        DateOnly? scheduleOn = null,
        short trainingCenterPkid = 1,
        byte slot = 1,
        int promotionPkid = 10,
        string topic = "主題（改）",
        string description = "說明（改）")
        => new()
        {
            Pkid = pkid,
            ScheduleOn = scheduleOn ?? Mon,
            TrainingCenterPkid = trainingCenterPkid,
            Slot = slot,
            PromotionPkid = promotionPkid,
            Topic = topic,
            Description = description
        };

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsEveryItem_SortedByDateThenCenterThenSlot()
    {
        var controller = ControllerFor(SeededRepository());

        var items = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(new[] { 6, 1, 2, 3, 7, 4, 5 }, items.Select(i => i.Pkid));
    }

    [Fact]
    public async Task GetAll_ProjectsEveryColumn_AndResolvesTheNavObjects()
    {
        var controller = ControllerFor(SeededRepository());

        var first = AssertOk(await controller.GetAll(CancellationToken.None)).Single(i => i.Pkid == 1);

        Assert.Equal(Mon, first.ScheduleOn);
        Assert.Equal<short>(1, first.TrainingCenterPkid);
        Assert.Equal<byte>(1, first.Slot);
        Assert.Equal(10, first.PromotionPkid);
        Assert.Equal("成為能AI協作的程式設計師", first.Topic);
        Assert.Equal("轉職就業養成班", first.Description);
        Assert.Equal("20251204_SkillTrainAI", first.Promotion!.PromoCode);
        Assert.Equal("台北", first.TrainingCenter!.Name);
    }

    // ---------- Query: training center and one-week filters ----------

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllItems()
    {
        var controller = ControllerFor(SeededRepository());

        var items = AssertOk(await controller.Query(new FeaturedPromoItemQuery(), CancellationToken.None)).ToList();

        Assert.Equal(7, items.Count);
    }

    [Fact]
    public async Task Query_TrainingCenterPkid_FiltersExactly()
    {
        var controller = ControllerFor(SeededRepository());

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { TrainingCenterPkid = 2 },
            CancellationToken.None)).ToList();

        Assert.Equal(7, Assert.Single(items).Pkid);
    }

    [Fact]
    public async Task Query_WeekOf_ReturnsMondayThroughSunday_AndNothingOutside()
    {
        var controller = ControllerFor(SeededRepository());

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { WeekOf = Wed },
            CancellationToken.None)).ToList();

        // 03-16 (Mon) … 03-22 (Sun): excludes 03-15 (pkid 6) and 03-23 (pkid 5).
        Assert.Equal(new[] { 1, 2, 3, 7, 4 }, items.Select(i => i.Pkid));
    }

    [Theory]
    [InlineData(2026, 3, 16)] // Monday
    [InlineData(2026, 3, 19)] // Thursday
    [InlineData(2026, 3, 22)] // Sunday
    public async Task Query_WeekOf_SnapsAnyDayOfTheWeekToTheSameMondayToSundayRange(int y, int m, int d)
    {
        var controller = ControllerFor(SeededRepository());

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { WeekOf = new DateOnly(y, m, d) },
            CancellationToken.None)).ToList();

        Assert.Equal(new[] { 1, 2, 3, 7, 4 }, items.Select(i => i.Pkid));
    }

    [Fact]
    public async Task Query_WeekOf_IsInclusiveOnBothEnds()
    {
        var controller = ControllerFor(SeededRepository());

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { WeekOf = Mon },
            CancellationToken.None)).ToList();

        Assert.Contains(items, i => i.ScheduleOn == Mon);
        Assert.Contains(items, i => i.ScheduleOn == Sun);
        Assert.DoesNotContain(items, i => i.ScheduleOn == PrevSun);
        Assert.DoesNotContain(items, i => i.ScheduleOn == NextMon);
    }

    [Fact]
    public async Task Query_CombinesTheTrainingCenterAndWeekFilters()
    {
        var controller = ControllerFor(SeededRepository());

        var items = AssertOk(await controller.Query(
            new FeaturedPromoItemQuery { TrainingCenterPkid = 1, WeekOf = Wed },
            CancellationToken.None)).ToList();

        Assert.Equal(new[] { 1, 2, 3, 4 }, items.Select(i => i.Pkid));
    }

    [Fact]
    public async Task Query_WithNullBody_IsTreatedAsAnEmptyFilter()
    {
        var controller = ControllerFor(SeededRepository());

        var items = AssertOk(await controller.Query(null!, CancellationToken.None)).ToList();

        Assert.Equal(7, items.Count);
    }

    [Theory]
    [InlineData(2026, 3, 16, 2026, 3, 16)] // Monday stays
    [InlineData(2026, 3, 18, 2026, 3, 16)] // Wednesday → Monday
    [InlineData(2026, 3, 21, 2026, 3, 16)] // Saturday → Monday
    [InlineData(2026, 3, 22, 2026, 3, 16)] // Sunday → the Monday *before* it, not after
    [InlineData(2026, 3, 23, 2026, 3, 23)] // next Monday
    public void WeekOf_SnapsToTheMondayOnOrBeforeTheDate(int y, int m, int d, int ey, int em, int ed)
    {
        var (monday, sunday) = FeaturedPromoItemRepository.WeekOf(new DateOnly(y, m, d));

        Assert.Equal(new DateOnly(ey, em, ed), monday);
        Assert.Equal(monday.AddDays(6), sunday);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsTheItem()
    {
        var controller = ControllerFor(SeededRepository());

        var item = AssertOk(await controller.GetById(3, CancellationToken.None));

        Assert.Equal("n8n自動化三部曲", item.Topic);
        Assert.Equal("20251215_n8n", item.Promotion!.PromoCode);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenTheItemIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_PersistsTheItem_AndReturnsCreatedAtActionWithTheAssignedPkid()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Create(Request(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var item = Assert.IsType<FeaturedPromoItem>(created.Value);

        Assert.Equal(nameof(FeaturedPromoItemsController.GetById), created.ActionName);
        Assert.Equal(8, created.RouteValues!["id"]);
        Assert.Equal(8, item.Pkid);
        Assert.Equal<byte>(3, item.Slot);
        Assert.Equal("20251215_n8n", item.Promotion!.PromoCode);
        Assert.NotNull(await repository.GetByIdAsync(8, CancellationToken.None));
    }

    [Fact]
    public async Task Create_TrimsTopicAndDescription()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Create(Request(topic: "  主題  ", description: "  說明  "), CancellationToken.None);

        var stored = await repository.GetByIdAsync(8, CancellationToken.None);
        Assert.Equal("主題", stored!.Topic);
        Assert.Equal("說明", stored.Description);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenTheSlotOnThatDayAndCenterIsTaken()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        // Mon / 台北 / slot 1 is pkid 1.
        var result = await controller.Create(Request(slot: 1), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Contains("已有上稿資料", problem.Detail);
        Assert.Equal(7, (await repository.GetAllAsync(CancellationToken.None)).Count());
    }

    [Fact]
    public async Task Create_AllowsTheSameSlotOnAnotherCenter()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Create(Request(slot: 1, trainingCenterPkid: 2), CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(FeaturedPromoItemRequest.Slot), "版位須為 1–3。");

        var result = await controller.Create(Request(slot: 9), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_PersistsTheChanges()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var item = AssertOk(await controller.Update(UpdateRequest(1, promotionPkid: 12), CancellationToken.None));

        Assert.Equal("主題（改）", item.Topic);
        Assert.Equal("20251215_n8n", item.Promotion!.PromoCode);
        Assert.Equal(12, (await repository.GetByIdAsync(1, CancellationToken.None))!.PromotionPkid);
    }

    [Fact]
    public async Task Update_KeepingItsOwnSlot_DoesNotConflictWithItself()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Update(UpdateRequest(1, slot: 1), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenMovedOntoAnotherItemsSlot()
    {
        var controller = ControllerFor(SeededRepository());

        // Mon / 台北 / slot 2 is pkid 2.
        var result = await controller.Update(UpdateRequest(1, slot: 2), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheItemIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Update(UpdateRequest(99, slot: 3), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(FeaturedPromoItemRequest.Topic), "主題為必填。");

        var result = await controller.Update(UpdateRequest(1), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesTheItem_AndReturnsNoContent()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Delete(2, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync(2, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenTheItemIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- Move slot (+ / −) ----------

    [Fact]
    public async Task MoveSlot_IntoAnEmptySlot_JustMovesTheItem()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var moved = AssertOk(await controller.MoveSlot(1, new FeaturedPromoItemMoveRequest { TargetSlot = 3 }, CancellationToken.None));

        Assert.Equal<byte>(3, moved.Slot);
        Assert.Equal<byte>(2, (await repository.GetByIdAsync(2, CancellationToken.None))!.Slot);
    }

    [Fact]
    public async Task MoveSlot_IntoAnOccupiedSlot_SwapsTheTwoItems()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        // "+" on slot 1: pkid 1 goes to slot 2, pkid 2 comes up to slot 1.
        var moved = AssertOk(await controller.MoveSlot(1, new FeaturedPromoItemMoveRequest { TargetSlot = 2 }, CancellationToken.None));

        Assert.Equal<byte>(2, moved.Slot);
        Assert.Equal<byte>(1, (await repository.GetByIdAsync(2, CancellationToken.None))!.Slot);

        var monday = await repository.QueryAsync(new FeaturedPromoItemQuery { TrainingCenterPkid = 1, WeekOf = Mon }, CancellationToken.None);
        Assert.Equal(new[] { 2, 1 }, monday.Where(i => i.ScheduleOn == Mon).Select(i => i.Pkid));
    }

    [Fact]
    public async Task MoveSlot_IntoItsOwnSlot_IsANoOp()
    {
        var controller = ControllerFor(SeededRepository());

        var moved = AssertOk(await controller.MoveSlot(1, new FeaturedPromoItemMoveRequest { TargetSlot = 1 }, CancellationToken.None));

        Assert.Equal<byte>(1, moved.Slot);
    }

    [Fact]
    public async Task MoveSlot_ReturnsNotFound_WhenTheItemIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.MoveSlot(99, new FeaturedPromoItemMoveRequest { TargetSlot = 2 }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task MoveSlot_ReturnsBadRequest_WhenTheTargetSlotIsOutOfRange()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(FeaturedPromoItemMoveRequest.TargetSlot), "版位須為 1–3。");

        var result = await controller.MoveSlot(1, new FeaturedPromoItemMoveRequest { TargetSlot = 4 }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
