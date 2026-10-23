using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;

namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence.Demo;

/*
    Feedback 10 (database demo): what the demo dataset contains. Pure data and pure functions, so the
    plan is the same on every run and on every machine:
      5 line operators x 7 container types x (2 x 20ft + 2 x 40ft) = 140 containers.
    Container numbers: operator prefix + 8000TS (T = type index 0..6, S = slot 1..4) + ISO 6346 check digit,
    e.g. MSCU800001?, so they never clash with the sample containers of AppDbSeeder.
    Each container follows one scenario (16 kinds, assigned round-robin), so the 140 containers
    are spread over every state the feedback lists: never in, in the yard for 0 / 9 / 10 / 11 / 75 days,
    released, back several times, each repair step, outside repair and back.
*/
public static class DemoCatalog
{
    // Changing the plan in a way that breaks old demo rows means a new key (and a demo reset)
    public const string DatasetKey = "depot-demo-v1";

    public const string CustomerTaxCode = "0312345678";
    public const string CustomerName = "Công ty CP Demo Logistics";

    // Blocks the demo uses; the same codes as the sample blocks of AppDbSeeder, created when missing
    public const string DryBlockCode = "A";
    public const string ReeferBlockCode = "B";
    public const string RepairBlockCode = "WAIT";

    public const string OutsideFacility = "Xưởng sửa chữa Cát Lái";

    // The five operators of the demo (feedback: "danh sách 5 hãng cấu hình trong bộ demo")
    public static readonly IReadOnlyList<DemoOperator> Operators =
    [
        new("MSC", "Mediterranean Shipping Company", "MSCU"),
        new("CMA", "CMA CGM", "CMAU"),
        new("HMM", "Hyundai Merchant Marine", "HMMU"),
        new("ONE", "Ocean Network Express", "ONEU"),
        new("EMC", "Evergreen Marine", "EMCU")
    ];

    // Every type of the project, with an ISO code valid for each size (IsoCodeRules) and usual weights
    public static readonly IReadOnlyList<DemoType> Types =
    [
        new(ContainerCategory.Dry, "22G1", "42G1", 2200, 3750, 30480),
        new(ContainerCategory.Reefer, "22R1", "45R1", 3000, 4800, 34000),
        new(ContainerCategory.OpenTop, "22U1", "42U1", 2300, 3900, 30480),
        new(ContainerCategory.FlatRack, "22P1", "42P1", 2700, 5000, 34000),
        new(ContainerCategory.Bunker, "22T0", "42T0", 3600, 4500, 36000),
        new(ContainerCategory.Ventilated, "22V0", "42V0", 2400, 3900, 30480),
        new(ContainerCategory.Specialized, "22S1", "42S1", 2500, 4000, 30480)
    ];

    // Days of history before the anchor: the confirmed assignments start then (oldest event: 120 days)
    public const int HistoryDays = 400;

    // Slots 1-2 are 20ft, slots 3-4 are 40ft
    public const int ContainersPerType = 4;

    public static IReadOnlyList<DemoContainerPlan> Containers { get; } = BuildContainers();

    // Damage texts written at the gate (first finding of the repair case)
    private static readonly string[] DamageNotes =
        ["Móp vách trái", "Thủng sàn góc phải", "Cửa sau kẹt bản lề", "Rỉ sét nóc", "Gãy thanh ngang đáy"];

    public static string DamageNote(DemoContainerPlan plan) => DamageNotes[plan.Index % DamageNotes.Length];

    // e.g. 51C-10123: unique per container, short enough for VehicleNo (20 characters)
    public static string VehicleNo(DemoContainerPlan plan, int trip) => $"51C-{10000 + plan.Index * 10 + trip}";

    /*
        What happens to a container, oldest event first. DaysAgo counts Vietnam calendar days back from the
        anchor day; ExtraMinutes orders two events of the same container on the same day.
    */
    public static IReadOnlyList<DemoStepPlan> StepsOf(DemoScenario scenario) => scenario switch
    {
        DemoScenario.NeverIn => [],
        DemoScenario.InYardToday => [Step(0, DemoAction.GateIn)],
        DemoScenario.InYard9Days => [Step(9, DemoAction.GateIn)],
        DemoScenario.InYard10Days => [Step(10, DemoAction.GateIn)],
        DemoScenario.InYard11Days => [Step(11, DemoAction.GateIn)],
        DemoScenario.LongStay => [Step(75, DemoAction.GateIn), Step(40, DemoAction.Move)],
        DemoScenario.Released => [Step(50, DemoAction.GateIn), Step(30, DemoAction.Release)],
        DemoScenario.ReturnedManyTimes =>
        [
            Step(120, DemoAction.GateIn), Step(100, DemoAction.Release),
            Step(60, DemoAction.GateIn), Step(45, DemoAction.Release),
            Step(5, DemoAction.GateIn)
        ],
        DemoScenario.DamagedAwaitingInspection => [Step(2, DemoAction.GateInDamaged)],
        DemoScenario.InspectedNoRepair => [Step(6, DemoAction.GateInDamaged), Step(5, DemoAction.InspectPassed)],
        DemoScenario.AwaitingEstimateApproval =>
        [
            Step(15, DemoAction.GateInDamaged), Step(14, DemoAction.InspectNeedsRepair), Step(13, DemoAction.SubmitEstimate),
            Step(11, DemoAction.RejectEstimate), Step(10, DemoAction.SubmitNextEstimate)
        ],
        DemoScenario.InRepair =>
        [
            Step(20, DemoAction.GateInDamaged), Step(19, DemoAction.InspectNeedsRepair), Step(18, DemoAction.SubmitEstimate),
            Step(17, DemoAction.ApproveEstimate), Step(16, DemoAction.StartRepair), Step(15, DemoAction.DisputePayer)
        ],
        DemoScenario.AwaitingAcceptance =>
        [
            Step(30, DemoAction.GateInDamaged), Step(29, DemoAction.InspectNeedsRepair), Step(28, DemoAction.SubmitEstimate),
            Step(27, DemoAction.ApproveEstimate), Step(26, DemoAction.StartRepair), Step(24, DemoAction.CompleteRepair),
            Step(23, DemoAction.AcceptRejected), Step(3, DemoAction.CompleteRepair)
        ],
        DemoScenario.RepairedAndReleased =>
        [
            Step(90, DemoAction.GateInDamaged), Step(89, DemoAction.InspectNeedsRepair), Step(88, DemoAction.SubmitEstimate),
            Step(87, DemoAction.ApproveEstimate), Step(86, DemoAction.StartRepair), Step(84, DemoAction.CompleteRepair),
            Step(83, DemoAction.AcceptPassed), Step(70, DemoAction.Release)
        ],
        DemoScenario.AtOutsideRepair =>
        [
            Step(12, DemoAction.GateInDamaged), Step(11, DemoAction.InspectNeedsRepair), Step(10, DemoAction.SubmitEstimate),
            Step(9, DemoAction.ApproveEstimate), Step(8, DemoAction.RequestExternal), Step(7, DemoAction.GateOutForRepair)
        ],
        DemoScenario.BackFromOutsideRepair =>
        [
            Step(45, DemoAction.GateInDamaged), Step(44, DemoAction.InspectNeedsRepair), Step(43, DemoAction.SubmitEstimate),
            Step(42, DemoAction.ApproveEstimate), Step(41, DemoAction.RequestExternal), Step(40, DemoAction.GateOutForRepair),
            Step(25, DemoAction.ReturnFromRepair), Step(24, DemoAction.StartRepair), Step(24, DemoAction.CompleteRepair, 60),
            Step(22, DemoAction.AcceptPassed)
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
    };

    /*
        Times of the steps of one container. A past day: 07:00 + a per-container minute (spread over the
        working day so events of different containers do not share a time) + ExtraMinutes.
        The anchor day (DaysAgo = 0): a few seconds before the anchor, so nothing is in the future.
    */
    public static IEnumerable<DemoStep> Timeline(DemoContainerPlan plan, DateTimeOffset anchorAt)
    {
        var anchorDay = DepotTime.DateOf(anchorAt);
        foreach (var step in StepsOf(plan.Scenario))
        {
            var at = step.DaysAgo == 0
                ? anchorAt.AddSeconds(-(plan.Index + 1))
                : DepotTime.StartOf(anchorDay.AddDays(-step.DaysAgo))
                    .AddHours(7)
                    .AddMinutes(plan.Index * 37 % 480 + step.ExtraMinutes);
            yield return new DemoStep(at, plan, step.Action);
        }
    }

    public static string NumberOf(DemoOperator op, int typeIndex, int slot)
    {
        var firstTen = $"{op.Prefix}{800000 + typeIndex * 10 + slot:D6}";
        return firstTen + ContainerNumberRules.CalculateCheckDigit(firstTen);
    }

    private static DemoStepPlan Step(int daysAgo, DemoAction action, int extraMinutes = 0) => new(daysAgo, extraMinutes, action);

    private static List<DemoContainerPlan> BuildContainers()
    {
        var scenarios = Enum.GetValues<DemoScenario>();
        var plans = new List<DemoContainerPlan>();
        for (var o = 0; o < Operators.Count; o++)
        for (var t = 0; t < Types.Count; t++)
        for (var s = 1; s <= ContainersPerType; s++)
        {
            var index = plans.Count;
            var size = s <= 2 ? ContainerSize.Ft20 : ContainerSize.Ft40;
            // Round-robin shifted by one every 16 containers: a plain index % 16 would give each scenario
            // always the same slot (16 is a multiple of 4), i.e. only 20ft or only 40ft containers
            var scenario = scenarios[(index + index / scenarios.Length) % scenarios.Length];
            plans.Add(new DemoContainerPlan(index, Operators[o], Types[t], size, NumberOf(Operators[o], t, s), scenario));
        }
        return plans;
    }
}

// Scenario of one demo container (see DemoCatalog.StepsOf for its events)
public enum DemoScenario
{
    NeverIn,
    InYardToday,
    InYard9Days,
    InYard10Days,
    InYard11Days,
    LongStay,
    Released,
    ReturnedManyTimes,
    DamagedAwaitingInspection,
    InspectedNoRepair,
    AwaitingEstimateApproval,
    InRepair,
    AwaitingAcceptance,
    RepairedAndReleased,
    AtOutsideRepair,
    BackFromOutsideRepair
}

// One business operation, replayed through the real command handlers (same rules as the screens)
public enum DemoAction
{
    GateIn,
    GateInDamaged,
    Move,
    Release,
    InspectNeedsRepair,
    InspectPassed,
    SubmitEstimate,
    RejectEstimate,
    SubmitNextEstimate,
    ApproveEstimate,
    DisputePayer,
    StartRepair,
    CompleteRepair,
    AcceptRejected,
    AcceptPassed,
    RequestExternal,
    GateOutForRepair,
    ReturnFromRepair
}

public sealed record DemoOperator(string Code, string Name, string Prefix);

public sealed record DemoType(ContainerCategory Category, string Iso20, string Iso40, int Tare20, int Tare40, int MaxGrossKg)
{
    public string IsoFor(ContainerSize size) => size == ContainerSize.Ft20 ? Iso20 : Iso40;
    public int TareFor(ContainerSize size) => size == ContainerSize.Ft20 ? Tare20 : Tare40;
}

public sealed record DemoContainerPlan(
    int Index, DemoOperator Operator, DemoType Type, ContainerSize Size, string Number, DemoScenario Scenario);

public sealed record DemoStepPlan(int DaysAgo, int ExtraMinutes, DemoAction Action);

public sealed record DemoStep(DateTimeOffset At, DemoContainerPlan Container, DemoAction Action);
