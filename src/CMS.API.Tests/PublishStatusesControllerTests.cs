using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>
/// Covers the 發布狀態 PublishStatus endpoints: list, filtered query, view, add, edit and delete —
/// including the two conflict paths (duplicate pkid on create, still-referenced row on delete).
/// </summary>
public class PublishStatusesControllerTests
{
    /// <summary>Three seeded statuses: 1 草稿, 2 已發布, 3 已下架.</summary>
    private static InMemoryPublishStatusRepository SeededRepository()
        => new InMemoryPublishStatusRepository()
            .Seed(1, "草稿", isDraft: true, isPublished: false, isDiscontinued: false)
            .Seed(2, "已發布", isDraft: false, isPublished: true, isDiscontinued: false)
            .Seed(3, "已下架", isDraft: false, isPublished: false, isDiscontinued: true);

    private static PublishStatusesController ControllerFor(InMemoryPublishStatusRepository repository)
        => new(repository);

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private static PublishStatusRequest Request(byte pkid, string description, bool draft = false, bool published = false, bool discontinued = false)
        => new()
        {
            Pkid = pkid,
            Description = description,
            IsDraft = draft,
            IsPublished = published,
            IsDiscontinued = discontinued
        };

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsEveryStatus_SortedByPkid()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(new byte[] { 1, 2, 3 }, statuses.Select(s => s.Pkid));
    }

    [Fact]
    public async Task GetAll_ProjectsEveryFlag()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        var published = statuses.Single(s => s.Pkid == 2);
        Assert.Equal("已發布", published.Description);
        Assert.False(published.IsDraft);
        Assert.True(published.IsPublished);
        Assert.False(published.IsDiscontinued);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllStatuses()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.Query(new PublishStatusQuery(), CancellationToken.None)).ToList();

        Assert.Equal(3, statuses.Count);
    }

    [Fact]
    public async Task Query_KeywordMatchesDescription()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.Query(new PublishStatusQuery { Keyword = "已發" }, CancellationToken.None)).ToList();

        Assert.Equal<byte>(2, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_PkidRange_IsInclusiveOnBothEnds()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { PkidFrom = 2, PkidTo = 3 },
            CancellationToken.None)).ToList();

        Assert.Equal(new byte[] { 2, 3 }, statuses.Select(s => s.Pkid));
    }

    [Fact]
    public async Task Query_IsDraftTrue_ReturnsOnlyDrafts()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.Query(new PublishStatusQuery { IsDraft = true }, CancellationToken.None)).ToList();

        Assert.Equal<byte>(1, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_IsPublishedFalse_ExcludesPublishedRows()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.Query(new PublishStatusQuery { IsPublished = false }, CancellationToken.None)).ToList();

        Assert.Equal(new byte[] { 1, 3 }, statuses.Select(s => s.Pkid));
    }

    [Fact]
    public async Task Query_IsDiscontinuedTrue_ReturnsOnlyDiscontinued()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.Query(new PublishStatusQuery { IsDiscontinued = true }, CancellationToken.None)).ToList();

        Assert.Equal<byte>(3, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_CombinesFiltersWithAnd()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.Query(
            new PublishStatusQuery { Keyword = "已", IsPublished = true },
            CancellationToken.None)).ToList();

        Assert.Equal<byte>(2, Assert.Single(statuses).Pkid);
    }

    [Fact]
    public async Task Query_WithNullBody_IsTreatedAsAnEmptyFilter()
    {
        var controller = ControllerFor(SeededRepository());

        var statuses = AssertOk(await controller.Query(null!, CancellationToken.None)).ToList();

        Assert.Equal(3, statuses.Count);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsTheStatus()
    {
        var controller = ControllerFor(SeededRepository());

        var status = AssertOk(await controller.GetById(3, CancellationToken.None));

        Assert.Equal("已下架", status.Description);
        Assert.True(status.IsDiscontinued);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenTheStatusIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_PersistsTheStatus_AndReturnsCreatedAtAction()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Create(Request(4, "審核中", draft: true), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var status = Assert.IsType<PublishStatus>(created.Value);

        Assert.Equal(nameof(PublishStatusesController.GetById), created.ActionName);
        Assert.Equal(4, created.RouteValues!["id"]);
        Assert.Equal("審核中", status.Description);
        Assert.True(status.IsDraft);
        Assert.NotNull(await repository.GetByIdAsync(4, CancellationToken.None));
    }

    [Fact]
    public async Task Create_TrimsTheDescription()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Create(Request(4, "  審核中  "), CancellationToken.None);

        var stored = await repository.GetByIdAsync(4, CancellationToken.None);
        Assert.Equal("審核中", stored!.Description);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenThePkidAlreadyExists()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Create(Request(2, "重複"), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(PublishStatusRequest.Description), "狀態說明為必填。");

        var result = await controller.Create(Request(4, string.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_RewritesEveryEditableField()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var status = AssertOk(await controller.Update(
            Request(1, "草稿（新）", draft: false, published: true),
            CancellationToken.None));

        Assert.Equal("草稿（新）", status.Description);
        Assert.False(status.IsDraft);
        Assert.True(status.IsPublished);

        var stored = await repository.GetByIdAsync(1, CancellationToken.None);
        Assert.Equal("草稿（新）", stored!.Description);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheStatusIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Update(Request(99, "不存在"), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(PublishStatusRequest.Description), "狀態說明為必填。");

        var result = await controller.Update(Request(1, string.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesTheStatus()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Delete(3, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync(3, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenTheStatusIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenAChildTableStillReferencesTheStatus()
    {
        var repository = SeededRepository().MarkInUse(2);
        var controller = ControllerFor(repository);

        var result = await controller.Delete(2, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.NotNull(await repository.GetByIdAsync(2, CancellationToken.None));
    }
}
