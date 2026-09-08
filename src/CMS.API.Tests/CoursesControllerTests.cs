using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests;

/// <summary>
/// Covers the 課程 Course endpoints: list, filtered query, view, add, edit and delete — including
/// the FK nav objects, both n-n junctions and the still-referenced conflict path. There is no
/// duplicate-key path here: pkid is an IDENTITY column, so the user never supplies it.
/// </summary>
public class CoursesControllerTests
{
    /// <summary>Three seeded courses, deliberately out of DisplayOrder sequence by pkid.</summary>
    private static InMemoryCourseRepository SeededRepository()
        => new InMemoryCourseRepository()
            .SeedPartner(1, "微軟")
            .SeedPartner(2, "思科")
            .SeedCourseGroup(10, "雲端")
            .SeedCourseGroup(20, "資安")
            .SeedPublishStatus(1, "草稿")
            .SeedPublishStatus(2, "已發布")
            .SeedJobCategory(5, "系統管理")
            .SeedJobCategory(6, "網路管理")
            .SeedCertification(100, "AZ-104 Azure Administrator")
            .SeedCertification(200, "CCNA", 2)
            .Seed(
                NewCourse(1, "Azure 基礎架構", "AZ-104", "MS-AZ104", "azure-admin", 20,
                    partnerPkid: 1, courseGroupPkid: 10, publishStatusPkid: 2,
                    officialTitle: "Microsoft Azure Administrator",
                    scheduleOn: new DateOnly(2026, 1, 1), scheduleOff: new DateOnly(2036, 1, 1),
                    hour: 30, listPrice: 24000m, learningCredit: 30.0m, canRepeat: true),
                jobCategoryPkids: new short[] { 5 },
                certificationPkids: new[] { 100 })
            .Seed(
                NewCourse(2, "CCNA 網路實務", "CCNA-200", "CI-CCNA", "ccna", 10,
                    partnerPkid: 2, courseGroupPkid: null, publishStatusPkid: 1,
                    scheduleOn: new DateOnly(2026, 3, 1), scheduleOff: new DateOnly(2030, 3, 1),
                    hour: 40, listPrice: 32000m, learningCredit: 40.5m, canRepeat: false))
            .Seed(
                NewCourse(3, "資安入門", "SEC-101", "UU-SEC101", "security-101", 30,
                    partnerPkid: 1, courseGroupPkid: 20, publishStatusPkid: 2,
                    scheduleOn: new DateOnly(2026, 6, 1), scheduleOff: new DateOnly(2028, 6, 1),
                    hour: 21, listPrice: 15000m, learningCredit: 21.0m, canRepeat: true),
                jobCategoryPkids: new short[] { 5, 6 },
                certificationPkids: new[] { 100, 200 });

    private static Course NewCourse(
        int pkid,
        string title,
        string courseId,
        string prodCourseId,
        string friendlyUrl,
        int displayOrder,
        short partnerPkid,
        short? courseGroupPkid,
        byte publishStatusPkid,
        DateOnly scheduleOn,
        DateOnly scheduleOff,
        short hour,
        decimal listPrice,
        decimal learningCredit,
        bool canRepeat,
        string? officialTitle = null)
        => new()
        {
            Pkid = pkid,
            Title = title,
            OfficialTitle = officialTitle,
            CourseId = courseId,
            ProdCourseId = prodCourseId,
            FriendlyUrl = friendlyUrl,
            DisplayOrder = displayOrder,
            PartnerPkid = partnerPkid,
            CourseGroupPkid = courseGroupPkid,
            PublishStatusPkid = publishStatusPkid,
            ScheduleOn = scheduleOn,
            ScheduleOff = scheduleOff,
            Hour = hour,
            ListPrice = listPrice,
            LearningCredit = learningCredit,
            CanRepeat = canRepeat
        };

    private static CoursesController ControllerFor(InMemoryCourseRepository repository)
        => new(repository);

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private static CourseRequest Request(
        string title = "Linux 系統管理",
        string? officialTitle = null,
        string courseId = "LX-101",
        string prodCourseId = "UU-LX101",
        string friendlyUrl = "linux-admin",
        int displayOrder = 40,
        short partnerPkid = 1,
        short? courseGroupPkid = 10,
        byte publishStatusPkid = 2,
        string? material = null,
        string? outline = null,
        bool canRepeat = false,
        IEnumerable<short>? jobCategoryPkids = null,
        IEnumerable<int>? certificationPkids = null)
        => new()
        {
            Title = title,
            OfficialTitle = officialTitle,
            CourseId = courseId,
            ProdCourseId = prodCourseId,
            FriendlyUrl = friendlyUrl,
            DisplayOrder = displayOrder,
            PartnerPkid = partnerPkid,
            CourseGroupPkid = courseGroupPkid,
            PublishStatusPkid = publishStatusPkid,
            ScheduleOn = new DateOnly(2026, 9, 1),
            ScheduleOff = new DateOnly(2036, 9, 1),
            Hour = 24,
            ListPrice = 18000m,
            LearningCredit = 24.0m,
            Material = material,
            Outline = outline,
            CanRepeat = canRepeat,
            JobCategoryPkids = (jobCategoryPkids ?? Enumerable.Empty<short>()).ToList(),
            CertificationPkids = (certificationPkids ?? Enumerable.Empty<int>()).ToList()
        };

    private static CourseUpdateRequest UpdateRequest(
        int pkid,
        string title = "Linux 系統管理",
        short? courseGroupPkid = 10,
        IEnumerable<short>? jobCategoryPkids = null,
        IEnumerable<int>? certificationPkids = null)
        => new()
        {
            Pkid = pkid,
            Title = title,
            CourseId = "LX-101",
            ProdCourseId = "UU-LX101",
            FriendlyUrl = "linux-admin",
            DisplayOrder = 40,
            PartnerPkid = 1,
            CourseGroupPkid = courseGroupPkid,
            PublishStatusPkid = 2,
            ScheduleOn = new DateOnly(2026, 9, 1),
            ScheduleOff = new DateOnly(2036, 9, 1),
            Hour = 24,
            ListPrice = 18000m,
            LearningCredit = 24.0m,
            CanRepeat = false,
            JobCategoryPkids = (jobCategoryPkids ?? Enumerable.Empty<short>()).ToList(),
            CertificationPkids = (certificationPkids ?? Enumerable.Empty<int>()).ToList()
        };

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsEveryCourse_SortedByDisplayOrderThenPkid()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(new[] { 2, 1, 3 }, courses.Select(c => c.Pkid));
    }

    [Fact]
    public async Task GetAll_ProjectsEveryColumn()
    {
        var controller = ControllerFor(SeededRepository());

        var azure = AssertOk(await controller.GetAll(CancellationToken.None)).Single(c => c.Pkid == 1);

        Assert.Equal("Azure 基礎架構", azure.Title);
        Assert.Equal("Microsoft Azure Administrator", azure.OfficialTitle);
        Assert.Equal("AZ-104", azure.CourseId);
        Assert.Equal("MS-AZ104", azure.ProdCourseId);
        Assert.Equal("azure-admin", azure.FriendlyUrl);
        Assert.Equal(20, azure.DisplayOrder);
        Assert.Equal(new DateOnly(2026, 1, 1), azure.ScheduleOn);
        Assert.Equal(new DateOnly(2036, 1, 1), azure.ScheduleOff);
        Assert.Equal<short>(30, azure.Hour);
        Assert.Equal(24000m, azure.ListPrice);
        Assert.Equal(30.0m, azure.LearningCredit);
        Assert.True(azure.CanRepeat);
    }

    [Fact]
    public async Task GetAll_ResolvesTheThreeForeignKeyNavObjects()
    {
        var controller = ControllerFor(SeededRepository());

        var azure = AssertOk(await controller.GetAll(CancellationToken.None)).Single(c => c.Pkid == 1);

        Assert.Equal("微軟", azure.Partner!.Name);
        Assert.Equal("雲端", azure.CourseGroup!.Description);
        Assert.Equal("已發布", azure.PublishStatus!.Description);
    }

    [Fact]
    public async Task GetAll_LeavesTheCourseGroupNavObjectNull_WhenTheNullableForeignKeyIsNull()
    {
        var controller = ControllerFor(SeededRepository());

        var ccna = AssertOk(await controller.GetAll(CancellationToken.None)).Single(c => c.Pkid == 2);

        Assert.Null(ccna.CourseGroupPkid);
        Assert.Null(ccna.CourseGroup);
        Assert.NotNull(ccna.Partner);
        Assert.NotNull(ccna.PublishStatus);
    }

    [Fact]
    public async Task GetAll_CarriesTheNnCounts_ButNotTheMembers()
    {
        var controller = ControllerFor(SeededRepository());

        var security = AssertOk(await controller.GetAll(CancellationToken.None)).Single(c => c.Pkid == 3);

        Assert.Equal(2, security.JobCategoryCount);
        Assert.Equal(2, security.CertificationCount);
        Assert.Empty(security.JobCategories);
        Assert.Empty(security.Certifications);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllCourses()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery(), CancellationToken.None)).ToList();

        Assert.Equal(3, courses.Count);
    }

    [Fact]
    public async Task Query_KeywordMatchesTitle()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { Keyword = "資安" }, CancellationToken.None)).ToList();

        Assert.Equal(3, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_KeywordMatchesOfficialTitle()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { Keyword = "Administrator" }, CancellationToken.None)).ToList();

        Assert.Equal(1, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_KeywordMatchesCourseId()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { Keyword = "SEC-101" }, CancellationToken.None)).ToList();

        Assert.Equal(3, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_KeywordMatchesProdCourseId()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { Keyword = "CI-CCNA" }, CancellationToken.None)).ToList();

        Assert.Equal(2, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_KeywordMatchesFriendlyUrl()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { Keyword = "azure-admin" }, CancellationToken.None)).ToList();

        Assert.Equal(1, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_PartnerPkid_FiltersExactly()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { PartnerPkid = 1 }, CancellationToken.None)).ToList();

        Assert.Equal(new[] { 1, 3 }, courses.Select(c => c.Pkid));
    }

    [Fact]
    public async Task Query_CourseGroupPkid_FiltersExactly()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { CourseGroupPkid = 20 }, CancellationToken.None)).ToList();

        Assert.Equal(3, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_PublishStatusPkid_FiltersExactly()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { PublishStatusPkid = 1 }, CancellationToken.None)).ToList();

        Assert.Equal(2, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_CanRepeatTrue_ReturnsOnlyRepeatableCourses()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { CanRepeat = true }, CancellationToken.None)).ToList();

        Assert.Equal(new[] { 1, 3 }, courses.Select(c => c.Pkid));
    }

    [Fact]
    public async Task Query_CanRepeatFalse_IsAFilter_NotAnAbsentOne()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(new CourseQuery { CanRepeat = false }, CancellationToken.None)).ToList();

        Assert.Equal(2, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_ScheduleOnRange_IsInclusiveOnBothEnds()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(
            new CourseQuery
            {
                ScheduleOnFrom = new DateOnly(2026, 1, 1),
                ScheduleOnTo = new DateOnly(2026, 3, 1)
            },
            CancellationToken.None)).ToList();

        Assert.Equal(new[] { 2, 1 }, courses.Select(c => c.Pkid));
    }

    [Fact]
    public async Task Query_ScheduleOffRange_IsInclusiveOnBothEnds()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(
            new CourseQuery
            {
                ScheduleOffFrom = new DateOnly(2028, 6, 1),
                ScheduleOffTo = new DateOnly(2030, 3, 1)
            },
            CancellationToken.None)).ToList();

        Assert.Equal(new[] { 2, 3 }, courses.Select(c => c.Pkid));
    }

    [Fact]
    public async Task Query_CombinesFiltersWithAnd()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(
            new CourseQuery { PartnerPkid = 1, PublishStatusPkid = 2, CanRepeat = true, Keyword = "資安" },
            CancellationToken.None)).ToList();

        Assert.Equal(3, Assert.Single(courses).Pkid);
    }

    [Fact]
    public async Task Query_WithNullBody_IsTreatedAsAnEmptyFilter()
    {
        var controller = ControllerFor(SeededRepository());

        var courses = AssertOk(await controller.Query(null!, CancellationToken.None)).ToList();

        Assert.Equal(3, courses.Count);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsTheCourse()
    {
        var controller = ControllerFor(SeededRepository());

        var course = AssertOk(await controller.GetById(2, CancellationToken.None));

        Assert.Equal("CCNA 網路實務", course.Title);
        Assert.Null(course.OfficialTitle);
        Assert.Equal("思科", course.Partner!.Name);
    }

    [Fact]
    public async Task GetById_PopulatesBothNnCollections()
    {
        var controller = ControllerFor(SeededRepository());

        var course = AssertOk(await controller.GetById(3, CancellationToken.None));

        Assert.Equal(new[] { "系統管理", "網路管理" }, course.JobCategories.Select(jc => jc.Description));
        Assert.Equal(new[] { "AZ-104 Azure Administrator", "CCNA" }, course.Certifications.Select(ct => ct.Title));
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenTheCourseIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_PersistsTheCourse_AndReturnsCreatedAtActionWithTheAssignedPkid()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Create(Request(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var course = Assert.IsType<Course>(created.Value);

        Assert.Equal(nameof(CoursesController.GetById), created.ActionName);
        Assert.Equal(4, created.RouteValues!["id"]);
        Assert.Equal(4, course.Pkid);
        Assert.Equal("Linux 系統管理", course.Title);
        Assert.NotNull(await repository.GetByIdAsync(4, CancellationToken.None));
    }

    [Fact]
    public async Task Create_TrimsEveryStringColumn()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Create(
            Request(title: "  Linux 系統管理  ", courseId: "  LX-101  ", prodCourseId: "  UU-LX101  ",
                friendlyUrl: "  linux-admin  ", material: "  講義  "),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(4, CancellationToken.None);
        Assert.Equal("Linux 系統管理", stored!.Title);
        Assert.Equal("LX-101", stored.CourseId);
        Assert.Equal("UU-LX101", stored.ProdCourseId);
        Assert.Equal("linux-admin", stored.FriendlyUrl);
        Assert.Equal("講義", stored.Material);
    }

    [Fact]
    public async Task Create_StoresBlankNullableColumnsAsNull()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Create(Request(officialTitle: "   ", material: "", outline: "  "), CancellationToken.None);

        var stored = await repository.GetByIdAsync(4, CancellationToken.None);
        Assert.Null(stored!.OfficialTitle);
        Assert.Null(stored.Material);
        Assert.Null(stored.Outline);
    }

    [Fact]
    public async Task Create_WritesBothJunctions_DeDuplicated()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Create(
            Request(jobCategoryPkids: new short[] { 5, 6, 5 }, certificationPkids: new[] { 100, 100 }),
            CancellationToken.None);

        Assert.Equal(new short[] { 5, 6 }, repository.JobCategoryLinks(4));
        Assert.Equal(new[] { 100 }, repository.CertificationLinks(4));
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(CourseRequest.Title), "課程名稱為必填。");

        var result = await controller.Create(Request(title: string.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_PersistsTheChanges()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var course = AssertOk(await controller.Update(UpdateRequest(2, title: "CCNA 網路實務（改版）"), CancellationToken.None));

        Assert.Equal("CCNA 網路實務（改版）", course.Title);
        Assert.Equal("CCNA 網路實務（改版）", (await repository.GetByIdAsync(2, CancellationToken.None))!.Title);
    }

    [Fact]
    public async Task Update_ReplacesBothJunctions()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        await controller.Update(
            UpdateRequest(3, jobCategoryPkids: new short[] { 6 }, certificationPkids: new[] { 200 }),
            CancellationToken.None);

        Assert.Equal(new short[] { 6 }, repository.JobCategoryLinks(3));
        Assert.Equal(new[] { 200 }, repository.CertificationLinks(3));
    }

    [Fact]
    public async Task Update_ClearsTheNullableForeignKey()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var course = AssertOk(await controller.Update(UpdateRequest(1, courseGroupPkid: null), CancellationToken.None));

        Assert.Null(course.CourseGroupPkid);
        Assert.Null(course.CourseGroup);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenTheCourseIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Update(UpdateRequest(99), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        var controller = ControllerFor(SeededRepository());
        controller.ModelState.AddModelError(nameof(CourseRequest.FriendlyUrl), "網址代稱為必填。");

        var result = await controller.Update(UpdateRequest(1), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesTheCourse_AndReturnsNoContent()
    {
        var repository = SeededRepository();
        var controller = ControllerFor(repository);

        var result = await controller.Delete(2, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync(2, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenTheCourseIsMissing()
    {
        var controller = ControllerFor(SeededRepository());

        var result = await controller.Delete(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenAChildTableStillReferencesTheCourse()
    {
        var repository = SeededRepository().MarkInUse(1);
        var controller = ControllerFor(repository);

        var result = await controller.Delete(1, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);

        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Contains("無法刪除", problem.Detail);
        Assert.NotNull(await repository.GetByIdAsync(1, CancellationToken.None));
    }
}
