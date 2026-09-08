using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Slim lookup lists used to populate FK / n-n option controls on forms and filter drawers.
/// </summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IPublishStatusRepository _publishStatusRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly ICourseGroupRepository _courseGroupRepository;
    private readonly IJobCategoryRepository _jobCategoryRepository;
    private readonly ICertificationRepository _certificationRepository;
    private readonly ITrainingCenterRepository _trainingCenterRepository;
    private readonly IPromotion2Repository _promotion2Repository;

    /// <summary>Upper bound on the PromoCode autocomplete — the client narrows by prefix.</summary>
    private const int PromotionSearchTake = 20;

    public LookupsController(
        IAppUserRepository appUserRepository,
        IAppRoleRepository appRoleRepository,
        IPublishStatusRepository publishStatusRepository,
        IPartnerRepository partnerRepository,
        ICourseGroupRepository courseGroupRepository,
        IJobCategoryRepository jobCategoryRepository,
        ICertificationRepository certificationRepository,
        ITrainingCenterRepository trainingCenterRepository,
        IPromotion2Repository promotion2Repository)
    {
        _appUserRepository = appUserRepository;
        _appRoleRepository = appRoleRepository;
        _publishStatusRepository = publishStatusRepository;
        _partnerRepository = partnerRepository;
        _courseGroupRepository = courseGroupRepository;
        _jobCategoryRepository = jobCategoryRepository;
        _certificationRepository = certificationRepository;
        _trainingCenterRepository = trainingCenterRepository;
        _promotion2Repository = promotion2Repository;
    }

    /// <summary>使用者 AppUser lookup list.</summary>
    [HttpGet("app-users")]
    [ProducesResponseType(typeof(IEnumerable<AppUserLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUserLookup>>> GetAppUsers(CancellationToken cancellationToken)
    {
        var users = await _appUserRepository.GetLookupAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>角色 AppRole lookup list.</summary>
    [HttpGet("app-roles")]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> GetAppRoles(CancellationToken cancellationToken)
    {
        var roles = await _appRoleRepository.GetAllAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>發布狀態 PublishStatus lookup list — option label is Description, ordered by pkid.</summary>
    [HttpGet("publish-statuses")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> GetPublishStatuses(CancellationToken cancellationToken)
    {
        var statuses = await _publishStatusRepository.GetAllAsync(cancellationToken);
        return Ok(statuses);
    }

    /// <summary>合作夥伴 Partner lookup list — option label is Name, ordered by DisplayOrder then pkid.</summary>
    [HttpGet("partners")]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> GetPartners(CancellationToken cancellationToken)
    {
        var partners = await _partnerRepository.GetAllAsync(cancellationToken);
        return Ok(partners);
    }

    /// <summary>課程群組 CourseGroup lookup list — option label is Description, ordered by Description.</summary>
    [HttpGet("course-groups")]
    [ProducesResponseType(typeof(IEnumerable<CourseGroupLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroupLookup>>> GetCourseGroups(CancellationToken cancellationToken)
    {
        var courseGroups = await _courseGroupRepository.GetLookupAsync(cancellationToken);
        return Ok(courseGroups);
    }

    /// <summary>職務類別 JobCategory lookup list — option label is Description, ordered by Description.</summary>
    [HttpGet("job-categories")]
    [ProducesResponseType(typeof(IEnumerable<JobCategoryLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<JobCategoryLookup>>> GetJobCategories(CancellationToken cancellationToken)
    {
        var jobCategories = await _jobCategoryRepository.GetLookupAsync(cancellationToken);
        return Ok(jobCategories);
    }

    /// <summary>認證 Certification lookup list — option label is Title (RTRIMmed), ordered by Title.</summary>
    [HttpGet("certifications")]
    [ProducesResponseType(typeof(IEnumerable<CertificationLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationLookup>>> GetCertifications(CancellationToken cancellationToken)
    {
        var certifications = await _certificationRepository.GetLookupAsync(cancellationToken);
        return Ok(certifications);
    }

    /// <summary>教育中心 TrainingCenter lookup list — option label is Name, ordered by DisplayOrder then pkid.</summary>
    [HttpGet("training-centers")]
    [ProducesResponseType(typeof(IEnumerable<TrainingCenterLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrainingCenterLookup>>> GetTrainingCenters(CancellationToken cancellationToken)
    {
        var trainingCenters = await _trainingCenterRepository.GetLookupAsync(cancellationToken);
        return Ok(trainingCenters);
    }

    /// <summary>
    /// 促銷 Promotion2 lookup — PromoCode prefix search for the 上稿作業 autocomplete, newest code
    /// first, at most 20 rows. Carries Topic and Description so the form can pre-fill them.
    /// </summary>
    [HttpGet("promotions")]
    [ProducesResponseType(typeof(IEnumerable<Promotion2Lookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Promotion2Lookup>>> GetPromotions(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var promotions = await _promotion2Repository.SearchLookupAsync(keyword, PromotionSearchTake, cancellationToken);
        return Ok(promotions);
    }
}
