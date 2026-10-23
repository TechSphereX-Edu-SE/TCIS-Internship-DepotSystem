using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

/*
    Feedback 4: the repair page asks to send the container to an outside facility: where (Facility),
    why (Reason) and on which instruction / approval (Reference). The gate-out page then confirms it left.
*/
public sealed record RequestExternalRepairCommand(Guid CaseId, string Facility, string Reason, string Reference)
    : ICommand<Result<Guid>>;
