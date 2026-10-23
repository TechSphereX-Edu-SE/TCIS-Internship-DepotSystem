namespace TechSpherex.CleanArchitecture.Domain.Enums;

/*
    Role of a partner company (feedback 2). One company can have both roles,
    e.g. HMM owns containers and also operates them; a leasing company is only an owner.
*/
public enum PartnerRole
{
    Owner = 0,
    Operator = 1
}

/*
    Operating assignment (giai đoạn khai thác) life cycle:
    Pending (recorded, not verified yet) -> Confirmed (can be used at gate-in) -> ended by a ValidTo date,
    or Cancelled (recorded by mistake). A cancelled assignment is kept for the history.
*/
public enum OperatingAssignmentStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2
}
