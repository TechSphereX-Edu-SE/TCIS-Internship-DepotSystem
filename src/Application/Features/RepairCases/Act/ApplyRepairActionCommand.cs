using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;

// One step of the repair flow (Q6); Note is required for AcceptRejected (sửa lại)
public sealed record ApplyRepairActionCommand(Guid Id, RepairAction Action, string? Note) : ICommand;
