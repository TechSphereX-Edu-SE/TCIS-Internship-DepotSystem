using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOut;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOutForRepair;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Create;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Demo;

// Result of a demo seed or reset, returned to the caller and written to the log
public sealed record DemoSeedReport(
    DateTimeOffset AnchorAt,
    bool AnchorCreated,
    int ContainersCreated,
    int ContainersSkipped,
    int StepsRun,
    IReadOnlyList<string> Failures);

/*
    Feedback 10: creates or completes the demo dataset (DemoCatalog) in the real database.
    - Idempotent: a demo container that already exists is skipped with all its events, so a second run adds
      only what is missing and never touches what users did with the demo data (no overwrite, no reset).
    - Fixed dates: events are placed relative to the anchor saved on the first run (DemoDataset), so they
      keep the same dates on every run; nothing is moved to today.
    - Real rules: every event goes through the same command handlers as the screens, with a clock set to the
      event time. History rows, repair cases, orders and positions are therefore what the system would have
      recorded; a step the rules refuse (e.g. a slot a user took meanwhile) is reported and the rest of that
      container's scenario is skipped. No business endpoint accepts a date from outside.
    - Reset (ResetAsync) is separate and only called on purpose; it deletes the depot data, never users.
*/
public sealed class DemoDataSeeder(AppDbContext dbContext, TimeProvider timeProvider, ILogger<DemoDataSeeder> logger)
{
    private readonly DemoClock _clock = new();
    private readonly DemoUser _user = new();

    public async Task<DemoSeedReport> SeedAsync(CancellationToken cancellationToken = default)
    {
        // Audit times of the rows written below follow the demo clock (see AppDbContext.AuditClock)
        dbContext.AuditClock = _clock;
        try
        {
            return await SeedCoreAsync(cancellationToken);
        }
        finally
        {
            dbContext.AuditClock = null;
        }
    }

    private async Task<DemoSeedReport> SeedCoreAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var dataset = await dbContext.DemoDatasets.FirstOrDefaultAsync(d => d.Key == DemoCatalog.DatasetKey, cancellationToken);
        var anchorCreated = dataset is null;
        if (dataset is null)
        {
            // Whole minutes: the anchor is shown in the guide and the README
            dataset = DemoDataset.Start(DemoCatalog.DatasetKey,
                new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset));
            dbContext.DemoDatasets.Add(dataset);
        }
        var anchorAt = dataset.AnchorAt;
        // Partners, blocks and containers created now are dated from the start of their assignments
        _clock.Now = DepotTime.StartOf(DepotTime.DateOf(anchorAt).AddDays(-DemoCatalog.HistoryDays));

        var master = await EnsureMasterDataAsync(anchorAt, cancellationToken);

        var numbers = DemoCatalog.Containers.Select(c => c.Number).ToList();
        var existing = (await dbContext.Containers
                .Where(c => numbers.Contains(c.ContainerNumber))
                .Select(c => c.ContainerNumber)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var missing = DemoCatalog.Containers.Where(p => !existing.Contains(p.Number)).ToList();

        var states = await CreateContainersAsync(missing, master, anchorAt, cancellationToken);

        // Every event of every new container, in time order, so the yard is always in a real state
        var steps = missing
            .SelectMany(p => DemoCatalog.Timeline(p, anchorAt))
            .OrderBy(s => s.At).ThenBy(s => s.Container.Index)
            .ToList();
        var failures = new List<string>();
        var stepsRun = 0;
        foreach (var step in steps)
        {
            var state = states[step.Container.Index];
            if (state.Failed)
                continue;

            _clock.Now = step.At;
            Result result;
            try
            {
                result = await RunAsync(step, state, master, cancellationToken);
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
            {
                result = Result.Failure(Error.Conflict("Demo.StepFailed", ex.InnerException?.Message ?? ex.Message));
            }
            // Each step starts with an empty tracker: what a refused step changed in memory (handlers save
            // nothing on failure) must not be saved by the next one, and 600 steps stay fast
            dbContext.ChangeTracker.Clear();

            stepsRun++;
            if (result.IsFailure)
            {
                state.Failed = true;
                failures.Add($"{step.Container.Number} {step.Action} {step.At:yyyy-MM-dd HH:mm}: {result.Error!.Code} - {result.Error.Message}");
            }
        }

        _clock.Now = now;
        var saved = await dbContext.DemoDatasets.FirstAsync(d => d.Key == DemoCatalog.DatasetKey, cancellationToken);
        saved.MarkSeeded(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        var report = new DemoSeedReport(anchorAt, anchorCreated, missing.Count, existing.Count, stepsRun, failures);
        logger.LogInformation(
            "Demo seed: anchor {Anchor} (new: {New}), {Created} containers created, {Skipped} already there, {Steps} steps, {Failed} failures",
            anchorAt, anchorCreated, report.ContainersCreated, report.ContainersSkipped, stepsRun, failures.Count);
        foreach (var failure in failures)
            logger.LogWarning("Demo step refused: {Failure}", failure);
        return report;
    }

    /*
        Deletes every depot row (containers, visits, orders, repair cases, partners, customers, blocks and the
        demo anchor), then seeds the demo again with a new anchor. Users, roles and todos stay.
        Photo files on disk are not deleted (only their rows). Children first, one save per level, so the
        restricted foreign keys never see a parent go before its children.
    */
    public async Task<DemoSeedReport> ResetAsync(CancellationToken cancellationToken = default)
    {
        dbContext.ChangeTracker.Clear();
        await RemoveAllAsync(dbContext.RepairEstimateLines, cancellationToken);
        await RemoveAllAsync(dbContext.RepairEstimates, cancellationToken);
        await RemoveAllAsync(dbContext.RepairPhotos, cancellationToken);
        await RemoveAllAsync(dbContext.RepairCaseEvents, cancellationToken);
        await RemoveAllAsync(dbContext.ExternalRepairTransfers, cancellationToken);
        await RemoveAllAsync(dbContext.RepairFindings, cancellationToken);
        await RemoveAllAsync(dbContext.RepairCases, cancellationToken);
        await RemoveAllAsync(dbContext.ContainerMoves, cancellationToken);
        await RemoveAllAsync(dbContext.ContainerVisits, cancellationToken);
        // Lines are loaded with their order so they go too, also where the database does not cascade (tests)
        dbContext.DeliveryOrders.RemoveRange(await dbContext.DeliveryOrders.Include(o => o.Lines).ToListAsync(cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await RemoveAllAsync(dbContext.OperatingAssignments, cancellationToken);
        await RemoveAllAsync(dbContext.Containers, cancellationToken);
        await RemoveAllAsync(dbContext.Blocks, cancellationToken);
        await RemoveAllAsync(dbContext.Customers, cancellationToken);
        await RemoveAllAsync(dbContext.LineOperators, cancellationToken);
        await RemoveAllAsync(dbContext.DemoDatasets, cancellationToken);
        logger.LogWarning("Demo reset: depot data deleted");

        return await SeedAsync(cancellationToken);
    }

    // ---------- master data ----------

    private sealed record DemoMaster(
        IReadOnlyDictionary<string, Guid> OperatorIds, Guid CustomerId, Guid DryBlockId, Guid ReeferBlockId, Guid RepairBlockId);

    // Operators, the order customer and the three blocks; existing rows (same code) are reused as they are
    private async Task<DemoMaster> EnsureMasterDataAsync(DateTimeOffset anchorAt, CancellationToken cancellationToken)
    {
        var codes = DemoCatalog.Operators.Select(o => o.Code).ToList();
        var operators = await dbContext.LineOperators.Where(o => codes.Contains(o.Code)).ToListAsync(cancellationToken);
        foreach (var op in DemoCatalog.Operators.Where(o => operators.All(e => e.Code != o.Code)))
        {
            var created = new LineOperator(op.Code, op.Name, isOwner: true, isOperator: true);
            dbContext.LineOperators.Add(created);
            operators.Add(created);
        }
        // The demo containers are owned and operated by their line: both roles are needed
        foreach (var op in operators.Where(o => !o.IsOwner || !o.IsOperator))
            op.UpdateProfile(op.Name, op.TaxCode, isOwner: true, isOperator: true);

        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.TaxCode == DemoCatalog.CustomerTaxCode, cancellationToken);
        if (customer is null)
        {
            customer = new Customer(DemoCatalog.CustomerTaxCode, DemoCatalog.CustomerName);
            dbContext.Customers.Add(customer);
        }

        var notReefer = Block.AllCategories.Where(c => c != ContainerCategory.Reefer);
        var dry = await FindOrAddBlockAsync(DemoCatalog.DryBlockCode,
            () => Block.CreatePhysical(DemoCatalog.DryBlockCode, maxBays: 20, maxRows: 6, maxTiers: 5, "Khu container khô", notReefer),
            cancellationToken);
        var reefer = await FindOrAddBlockAsync(DemoCatalog.ReeferBlockCode,
            () => Block.CreatePhysical(DemoCatalog.ReeferBlockCode, maxBays: 10, maxRows: 4, maxTiers: 4, "Khu container lạnh",
                [ContainerCategory.Reefer]),
            cancellationToken);
        var repair = await FindOrAddBlockAsync(DemoCatalog.RepairBlockCode,
            () => Block.CreateVirtual(DemoCatalog.RepairBlockCode, "Khu chờ sửa chữa / giám định", Block.AllCategories),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return new DemoMaster(operators.ToDictionary(o => o.Code, o => o.Id), customer.Id, dry.Id, reefer.Id, repair.Id);
    }

    private async Task<Block> FindOrAddBlockAsync(string code, Func<Block> create, CancellationToken cancellationToken)
    {
        var block = await dbContext.Blocks.FirstOrDefaultAsync(b => b.Code == code, cancellationToken);
        if (block is not null)
            return block;
        block = create();
        dbContext.Blocks.Add(block);
        return block;
    }

    // Catalog rows of the missing containers, each with a confirmed open-ended assignment to its line
    private async Task<Dictionary<int, DemoContainerState>> CreateContainersAsync(
        IReadOnlyList<DemoContainerPlan> missing, DemoMaster master, DateTimeOffset anchorAt, CancellationToken cancellationToken)
    {
        var operatorIds = master.OperatorIds.Values.ToList();
        var operators = await dbContext.LineOperators
            .Where(o => operatorIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Code, cancellationToken);
        var validFrom = DepotTime.DateOf(anchorAt).AddDays(-DemoCatalog.HistoryDays);
        var states = new Dictionary<int, DemoContainerState>();

        foreach (var plan in missing)
        {
            var lineOperator = operators[plan.Operator.Code];
            var container = new Container(plan.Number, plan.Type.IsoFor(plan.Size), plan.Size, plan.Type.Category,
                plan.Type.MaxGrossKg, plan.Type.TareFor(plan.Size), new DateOnly(2018 + plan.Index % 6, 1, 1), lineOperator.Code);
            container.SetOwner(lineOperator);
            dbContext.Containers.Add(container);

            var assignment = OperatingAssignment.Create(container, lineOperator, validFrom, null, $"DEMO-{plan.Operator.Code}",
                "Dữ liệu demo").Value!;
            assignment.Confirm(DepotTime.StartOf(validFrom));
            dbContext.OperatingAssignments.Add(assignment);

            states[plan.Index] = new DemoContainerState(container.Id, lineOperator.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return states;
    }

    // ---------- events ----------

    private async Task<Result> RunAsync(DemoStep step, DemoContainerState state, DemoMaster master, CancellationToken ct)
    {
        var plan = step.Container;
        switch (step.Action)
        {
            case DemoAction.GateIn:
            {
                var blockId = plan.Type.Category == ContainerCategory.Reefer ? master.ReeferBlockId : master.DryBlockId;
                return await GateInAsync(state, plan, blockId, ContainerCondition.Normal, null, ct);
            }
            case DemoAction.GateInDamaged:
                return await GateInAsync(state, plan, master.RepairBlockId, ContainerCondition.Damaged, DemoCatalog.DamageNote(plan), ct);
            case DemoAction.ReturnFromRepair:
                return await GateInAsync(state, plan, master.RepairBlockId, ContainerCondition.Normal, null, ct);
            case DemoAction.Move:
            {
                var visit = await dbContext.ContainerVisits.AsNoTracking()
                    .FirstAsync(v => v.ContainerId == state.ContainerId && v.GateOutAt == null, ct);
                var slot = await FreeSlotAsync(visit.BlockId, plan.Size, ct);
                if (slot is null)
                    return NoFreeSlot(visit.BlockId);
                var moved = await new MoveContainerCommandHandler(dbContext, _clock, _user).HandleAsync(
                    new MoveContainerCommand(state.ContainerId, visit.BlockId, slot.Value.Bay, slot.Value.Row, slot.Value.Tier), ct);
                return moved;
            }
            case DemoAction.Release:
                return await ReleaseAsync(state, plan, master, ct);
            case DemoAction.InspectNeedsRepair:
                return await ActAsync(state, RepairAction.InspectNeedsRepair, "Cần sửa theo kết quả giám định", ct);
            case DemoAction.InspectPassed:
                return await ActAsync(state, RepairAction.InspectPassed, "Hư nhẹ, không ảnh hưởng khai thác", ct);
            case DemoAction.StartRepair:
                return await ActAsync(state, RepairAction.StartRepair, null, ct);
            case DemoAction.CompleteRepair:
                return await ActAsync(state, RepairAction.CompleteRepair, null, ct);
            case DemoAction.AcceptRejected:
                return await ActAsync(state, RepairAction.AcceptRejected, "Sơn chưa đều, làm lại mặt vách", ct);
            case DemoAction.AcceptPassed:
                return await ActAsync(state, RepairAction.AcceptPassed, null, ct);
            case DemoAction.SubmitEstimate:
                return await SubmitEstimateAsync(state, plan, copy: false, ct);
            case DemoAction.SubmitNextEstimate:
                return await SubmitEstimateAsync(state, plan, copy: true, ct);
            case DemoAction.RejectEstimate:
                return await new DecideRepairEstimateCommandHandler(dbContext, _clock, _user).HandleAsync(
                    new DecideRepairEstimateCommand(state.EstimateId!.Value, RepairEstimateDecision.Reject, plan.Operator.Code,
                        null, null, "Đơn giá sơn cao hơn biểu giá của hãng"), ct);
            case DemoAction.ApproveEstimate:
                return await new DecideRepairEstimateCommandHandler(dbContext, _clock, _user).HandleAsync(
                    new DecideRepairEstimateCommand(state.EstimateId!.Value, RepairEstimateDecision.Approve, plan.Operator.Code,
                        $"Phòng M&R {plan.Operator.Code}", $"{plan.Operator.Code}-MR-{plan.Index:000}", null), ct);
            case DemoAction.DisputePayer:
            {
                var line = await dbContext.RepairEstimateLines.AsNoTracking()
                    .FirstAsync(l => l.EstimateId == state.EstimateId && l.Position == 2, ct);
                return await new SetEstimateLinePayerCommandHandler(dbContext, _clock, _user).HandleAsync(
                    new SetEstimateLinePayerCommand(state.EstimateId!.Value, line.Id, CostPayer.Customer, null,
                        CostPayerStatus.Disputed, "Khách hàng cho rằng gioăng đã rách trước khi nhận container"), ct);
            }
            case DemoAction.RequestExternal:
            {
                var requested = await new RequestExternalRepairCommandHandler(dbContext, _clock, _user).HandleAsync(
                    new RequestExternalRepairCommand(state.CaseId!.Value, DemoCatalog.OutsideFacility,
                        "Cần hàn vách, bãi không có thiết bị", $"{plan.Operator.Code}-EXT-{plan.Index:000}"), ct);
                if (requested.IsSuccess)
                    state.TransferId = requested.Value;
                return requested;
            }
            case DemoAction.GateOutForRepair:
                return await new GateOutForRepairCommandHandler(dbContext, _clock, _user).HandleAsync(
                    new GateOutForRepairCommand(state.TransferId!.Value, DemoCatalog.VehicleNo(plan, state.NextTrip())), ct);
            default:
                throw new ArgumentOutOfRangeException(nameof(step), step.Action, null);
        }
    }

    // Normal containers go to the first free slot of their block; damaged ones to the repair area (virtual)
    private async Task<Result> GateInAsync(
        DemoContainerState state, DemoContainerPlan plan, Guid blockId, ContainerCondition condition, string? note, CancellationToken ct)
    {
        var slot = await FreeSlotAsync(blockId, plan.Size, ct);
        if (slot is null)
            return NoFreeSlot(blockId);

        var gateIn = await new GateInCommandHandler(dbContext, _clock, _user).HandleAsync(
            new GateInCommand(state.ContainerId, state.OperatorId, blockId, slot.Value.Bay, slot.Value.Row, slot.Value.Tier,
                condition, note, DemoCatalog.VehicleNo(plan, state.NextTrip())), ct);
        if (gateIn.IsSuccess && gateIn.Value!.RepairCaseId is { } caseId)
            state.CaseId = caseId;
        return gateIn;
    }

    // One delivery order per release (2 hours before, valid 7 days), then the gate-out under it
    private async Task<Result> ReleaseAsync(DemoContainerState state, DemoContainerPlan plan, DemoMaster master, CancellationToken ct)
    {
        var releaseAt = _clock.Now;
        _clock.Now = releaseAt.AddHours(-2);
        var day = DepotTime.DateOf(releaseAt);
        var order = await new CreateDeliveryOrderCommandHandler(dbContext, _clock).HandleAsync(
            new CreateDeliveryOrderCommand($"DEMO-{plan.Operator.Code}-{day:yyMMdd}-{plan.Index:000}", state.OperatorId,
                master.CustomerId, day.AddDays(7), "DEMO VOYAGE 001N",
                [new DeliveryOrderLineRequest(plan.Type.Category, 1, plan.Size)]), ct);
        if (order.IsFailure)
            return order;

        _clock.Now = releaseAt;
        return await new GateOutCommandHandler(dbContext, _clock).HandleAsync(
            new GateOutCommand(state.ContainerId, order.Value!.Id, DemoCatalog.VehicleNo(plan, state.NextTrip())), ct);
    }

    private Task<Result> ActAsync(DemoContainerState state, RepairAction action, string? note, CancellationToken ct) =>
        new ApplyRepairActionCommandHandler(dbContext, _clock, _user)
            .HandleAsync(new ApplyRepairActionCommand(state.CaseId!.Value, action, note), ct);

    /*
        Two items: the damage seen at the gate (linked to the first finding, paid by the line) and a door gasket
        (paid by the customer). The next version starts as a copy and lowers the first amount, as asked.
    */
    private async Task<Result> SubmitEstimateAsync(DemoContainerState state, DemoContainerPlan plan, bool copy, CancellationToken ct)
    {
        var caseId = state.CaseId!.Value;
        var created = await new CreateRepairEstimateCommandHandler(dbContext, _clock, _user).HandleAsync(
            new CreateRepairEstimateCommand(caseId, "VND", null, $"EIR-{plan.Number}"), ct);
        if (created.IsFailure)
            return created;
        state.EstimateId = created.Value;

        var findingId = await dbContext.RepairFindings.AsNoTracking()
            .Where(f => f.RepairCaseId == caseId)
            .OrderBy(f => f.ReportedAt)
            .Select(f => (Guid?)f.Id)
            .FirstOrDefaultAsync(ct);
        var bodyAmount = plan.Size == ContainerSize.Ft20 ? 1_500_000m : 2_200_000m;
        if (copy)
            bodyAmount *= 0.8m;
        RepairEstimateLineInput[] lines =
        [
            new(findingId, DemoCatalog.DamageNote(plan), "Nắn phẳng, sơn lại", bodyAmount, CostPayer.LineOperator, null),
            new(null, "Gioăng cửa rách", "Thay gioăng cửa", 800_000m, CostPayer.Customer, null)
        ];
        var updated = await new UpdateRepairEstimateCommandHandler(dbContext).HandleAsync(
            new UpdateRepairEstimateCommand(created.Value, "VND", copy ? "Giảm đơn giá sơn theo yêu cầu" : null,
                $"EIR-{plan.Number}", lines), ct);
        if (updated.IsFailure)
            return updated;

        return await new SubmitRepairEstimateCommandHandler(dbContext, _clock, _user)
            .HandleAsync(new SubmitRepairEstimateCommand(created.Value), ct);
    }

    /*
        First free position of the block for the size: lowest tier, then row, then bay, with the same overlap and
        support rules as gate-in (YardSlotRules). Virtual block: no position. Null when the block is full.
    */
    private async Task<(int? Bay, int? Row, int? Tier)?> FreeSlotAsync(Guid blockId, ContainerSize size, CancellationToken ct)
    {
        var block = await dbContext.Blocks.AsNoTracking().FirstAsync(b => b.Id == blockId, ct);
        if (block.IsVirtual)
            return (null, null, null);

        var placed = await dbContext.ContainerVisits.AsNoTracking()
            .Where(v => v.BlockId == blockId && v.GateOutAt == null && v.Row != null)
            .Select(v => new { Row = v.Row!.Value, Bay = v.Bay!.Value, Tier = v.Tier!.Value, v.Container.Size })
            .ToListAsync(ct);

        var bays = Enumerable.Range(1, block.MaxBays!.Value)
            .Where(b => size == ContainerSize.Ft20 ? b % 2 == 1 : b % 2 == 0 && b + 1 <= block.MaxBays)
            .ToList();
        for (var tier = 1; tier <= block.MaxTiers; tier++)
        for (var row = 1; row <= block.MaxRows; row++)
        {
            var sameRow = placed.Where(p => p.Row == row)
                .Select(p => new YardSlotRules.PlacedContainer(p.Bay, p.Tier, p.Size))
                .ToList();
            foreach (var bay in bays)
            {
                if (!YardSlotRules.Overlaps(bay, tier, size, sameRow) && YardSlotRules.IsSupported(bay, tier, size, sameRow))
                    return (bay, row, tier);
            }
        }
        return null;
    }

    private static Result NoFreeSlot(Guid blockId) =>
        Result.Failure(Error.Conflict("Demo.BlockFull", $"Block {blockId} has no free position for this container."));

    private async Task RemoveAllAsync<T>(DbSet<T> set, CancellationToken ct) where T : class
    {
        set.RemoveRange(await set.ToListAsync(ct));
        await dbContext.SaveChangesAsync(ct);
    }

    // What the steps of one container need from the earlier ones (ids only: the tracker can be cleared)
    private sealed class DemoContainerState(Guid containerId, Guid operatorId)
    {
        public Guid ContainerId { get; } = containerId;
        public Guid OperatorId { get; } = operatorId;
        public Guid? CaseId { get; set; }
        public Guid? EstimateId { get; set; }
        public Guid? TransferId { get; set; }
        public bool Failed { get; set; }
        private int _trips;

        public int NextTrip() => ++_trips;
    }

    // The clock the handlers read: moved to the time of each event
    private sealed class DemoClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }

    // History rows of the demo say who wrote them
    private sealed class DemoUser : ICurrentUser
    {
        public string? UserId => null;
        public string? Email => "demo-seed@depot.local";
        public bool IsAuthenticated => true;
    }
}
