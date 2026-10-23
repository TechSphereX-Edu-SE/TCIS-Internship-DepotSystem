namespace TechSpherex.CleanArchitecture.Domain.Enums;

/*
    Repair case flow (feedback 4, Q6 23/10/2026):
    AwaitingInspection -> Passed (no repair needed)
                       -> AwaitingEstimateApproval -> RepairApproved -> InRepair -> AwaitingAcceptance -> Passed
                                                                         ^                |
                                                                         +--- rejected ---+  (sửa lại)
    Every status except Passed keeps the container from being released (cấp rỗng).
*/
public enum RepairCaseStatus
{
    AwaitingInspection = 0,
    AwaitingEstimateApproval = 1,
    RepairApproved = 2,
    InRepair = 3,
    AwaitingAcceptance = 4,
    Passed = 5
}

// One step of the flow above; RepairCase.Transitions says from which status each one is allowed
public enum RepairAction
{
    InspectPassed = 0,
    InspectNeedsRepair = 1,
    ApproveRepair = 2,
    StartRepair = 3,
    CompleteRepair = 4,
    AcceptPassed = 5,
    AcceptRejected = 6
}

// Where a damage was reported (feedback 4: gate-in, yard / move screen, container catalog, or during inspection)
public enum RepairFindingSource
{
    GateIn = 0,
    Yard = 1,
    Catalog = 2,
    Inspection = 3
}

// Kind of a history row: the case was opened, a finding was added, or one of the actions
public enum RepairEventKind
{
    Opened = 0,
    FindingAdded = 1,
    InspectPassed = 2,
    InspectNeedsRepair = 3,
    ApproveRepair = 4,
    StartRepair = 5,
    CompleteRepair = 6,
    AcceptPassed = 7,
    AcceptRejected = 8,
    // External repair (feedback 4: sửa ngoài); the case status does not change with these
    ExternalRequested = 9,
    ExternalCancelled = 10,
    SentForExternalRepair = 11,
    ReturnedFromExternalRepair = 12,
    // Repair costs (feedback 4): estimate versions, decisions and payers; ApproveRepair stays the status step
    EstimateCreated = 13,
    EstimateSubmitted = 14,
    EstimateApproved = 15,
    EstimateRejected = 16,
    EstimateChangesRequested = 17,
    PayerChanged = 18
}

/*
    Transfer of a container to an outside repair facility (feedback 4, sửa ngoài):
    Requested (on the repair page) -> GatedOut (confirmed at the gate-out page) -> Returned (new gate-in),
    or Cancelled before it leaves. The repair case stays open the whole time.
*/
public enum ExternalRepairStatus
{
    Requested = 0,
    Cancelled = 1,
    GatedOut = 2,
    Returned = 3
}

/*
    Repair estimate version (feedback 4: "lưu phiên bản dự toán"):
    Draft (lines can change) -> Submitted (sent to the approving party, locked)
      -> Approved | Rejected | ChangesRequested (decision recorded with who approved and the document).
    Rejected / ChangesRequested keep the case waiting; a new version is created to revise it.
*/
public enum RepairEstimateStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    ChangesRequested = 4
}

// Decision on a submitted estimate
public enum RepairEstimateDecision
{
    Approve = 0,
    Reject = 1,
    RequestChanges = 2
}

/*
    Who bears the cost of one item (feedback 4). Never derived automatically (no "before handover the
    line pays, after it the customer pays" rule): staff set it after checking existing damage, wear and tear,
    who caused it and the agreements.
*/
public enum CostPayer
{
    Undetermined = 0,
    LineOperator = 1,
    Owner = 2,
    Customer = 3,
    Depot = 4,
    Other = 5
}

// Expected = proposed, Agreed = allocation settled, Disputed = a party contests it (the note says why)
public enum CostPayerStatus
{
    Expected = 0,
    Agreed = 1,
    Disputed = 2
}
