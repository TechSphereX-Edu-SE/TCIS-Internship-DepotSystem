using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Create;

/*
    Records which line operator operates a container from ValidFrom to ValidTo (Vietnam dates, both included).
    Confirm = true records it already verified against the contract / instruction (Reference);
    otherwise it stays Pending and gate-in cannot use it yet.
*/
public sealed record CreateOperatingAssignmentCommand(
    Guid ContainerId,
    Guid OperatorId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Reference,
    string? Note,
    bool Confirm)
    : ICommand<Result<CreateOperatingAssignmentResponse>>;

public sealed record CreateOperatingAssignmentResponse(Guid Id);
