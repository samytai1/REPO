using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IFeaturedPromoItemRepository"/> that mirrors the SQL semantics
/// of <c>FeaturedPromoItemRepository</c>: pkid is assigned like an IDENTITY column, results sort by
/// ScheduleOn, TrainingCenter_pkid, Slot, the week filter snaps any date to its Monday–Sunday range
/// (through the same <see cref="FeaturedPromoItemRepository.WeekOf"/> helper), the FK nav objects
/// resolve from seeded lookup rows, and a slot move swaps with the occupant.
/// </summary>
public class InMemoryFeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private readonly List<FeaturedPromoItem> _items = new();
    private readonly Dictionary<short, FeaturedPromoItemTrainingCenterRef> _trainingCenters = new();
    private readonly Dictionary<int, FeaturedPromoItemPromotionRef> _promotions = new();

    private int _nextPkid = 1;

    /// <summary>Seeds a training center the JOIN can resolve.</summary>
    public InMemoryFeaturedPromoItemRepository SeedTrainingCenter(short pkid, string name)
    {
        _trainingCenters[pkid] = new FeaturedPromoItemTrainingCenterRef { Pkid = pkid, Name = name };
        return this;
    }

    /// <summary>Seeds a promotion the JOIN can resolve.</summary>
    public InMemoryFeaturedPromoItemRepository SeedPromotion(int pkid, string promoCode)
    {
        _promotions[pkid] = new FeaturedPromoItemPromotionRef { Pkid = pkid, PromoCode = promoCode };
        return this;
    }

    /// <summary>Seeds an item directly, bypassing the write path.</summary>
    public InMemoryFeaturedPromoItemRepository Seed(
        int pkid,
        DateOnly scheduleOn,
        short trainingCenterPkid,
        byte slot,
        int promotionPkid,
        string topic = "主題",
        string description = "說明")
    {
        _items.Add(new FeaturedPromoItem
        {
            Pkid = pkid,
            ScheduleOn = scheduleOn,
            TrainingCenterPkid = trainingCenterPkid,
            Slot = slot,
            PromotionPkid = promotionPkid,
            Topic = topic,
            Description = description
        });

        if (pkid >= _nextPkid) _nextPkid = pkid + 1;

        return this;
    }

    public Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<FeaturedPromoItem>>(Sorted(_items).ToList());

    public Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<FeaturedPromoItem> results = _items;

        if (query.TrainingCenterPkid.HasValue)
        {
            results = results.Where(i => i.TrainingCenterPkid == query.TrainingCenterPkid.Value);
        }

        if (query.WeekOf.HasValue)
        {
            var (monday, sunday) = FeaturedPromoItemRepository.WeekOf(query.WeekOf.Value);
            results = results.Where(i => i.ScheduleOn >= monday && i.ScheduleOn <= sunday);
        }

        return Task.FromResult<IEnumerable<FeaturedPromoItem>>(Sorted(results).ToList());
    }

    public Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var item = Find(pkid);
        return Task.FromResult(item is null ? null : Project(item));
    }

    public Task<bool> IsSlotTakenAsync(
        DateOnly scheduleOn,
        short trainingCenterPkid,
        byte slot,
        int? excludePkid,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Occupant(scheduleOn, trainingCenterPkid, slot, excludePkid) is not null);

    public Task<FeaturedPromoItem> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = new FeaturedPromoItem { Pkid = _nextPkid++ };

        Apply(item, request);
        _items.Add(item);

        return Task.FromResult(Project(item));
    }

    public Task<FeaturedPromoItem?> UpdateAsync(FeaturedPromoItemUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var item = Find(request.Pkid);

        if (item is null) return Task.FromResult<FeaturedPromoItem?>(null);

        Apply(item, request);

        return Task.FromResult<FeaturedPromoItem?>(Project(item));
    }

    public Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var item = Find(pkid);

        if (item is null) return Task.FromResult(false);

        _items.Remove(item);

        return Task.FromResult(true);
    }

    public Task<FeaturedPromoItem?> MoveSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default)
    {
        var item = Find(pkid);

        if (item is null) return Task.FromResult<FeaturedPromoItem?>(null);

        if (item.Slot != targetSlot)
        {
            var occupant = Occupant(item.ScheduleOn, item.TrainingCenterPkid, targetSlot, pkid);

            if (occupant is not null) occupant.Slot = item.Slot;

            item.Slot = targetSlot;
        }

        return Task.FromResult<FeaturedPromoItem?>(Project(item));
    }

    private FeaturedPromoItem? Find(int pkid) => _items.FirstOrDefault(i => i.Pkid == pkid);

    private FeaturedPromoItem? Occupant(DateOnly scheduleOn, short trainingCenterPkid, byte slot, int? excludePkid)
        => _items.FirstOrDefault(i =>
            i.ScheduleOn == scheduleOn
            && i.TrainingCenterPkid == trainingCenterPkid
            && i.Slot == slot
            && (!excludePkid.HasValue || i.Pkid != excludePkid.Value));

    private IEnumerable<FeaturedPromoItem> Sorted(IEnumerable<FeaturedPromoItem> items) => items
        .OrderBy(i => i.ScheduleOn)
        .ThenBy(i => i.TrainingCenterPkid)
        .ThenBy(i => i.Slot)
        .Select(Project)
        .ToList();

    private static void Apply(FeaturedPromoItem item, FeaturedPromoItemRequest request)
    {
        item.ScheduleOn = request.ScheduleOn;
        item.TrainingCenterPkid = request.TrainingCenterPkid;
        item.Slot = request.Slot;
        item.PromotionPkid = request.PromotionPkid;
        item.Topic = request.Topic.Trim();
        item.Description = request.Description.Trim();
    }

    /// <summary>Returns a detached copy with the FK nav objects, as the SQL projection does.</summary>
    private FeaturedPromoItem Project(FeaturedPromoItem item) => new()
    {
        Pkid = item.Pkid,
        ScheduleOn = item.ScheduleOn,
        TrainingCenterPkid = item.TrainingCenterPkid,
        Slot = item.Slot,
        PromotionPkid = item.PromotionPkid,
        Topic = item.Topic,
        Description = item.Description,
        Promotion = _promotions.TryGetValue(item.PromotionPkid, out var promotion) ? promotion : null,
        TrainingCenter = _trainingCenters.TryGetValue(item.TrainingCenterPkid, out var trainingCenter) ? trainingCenter : null
    };
}
