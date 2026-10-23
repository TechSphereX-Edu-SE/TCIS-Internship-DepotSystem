using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Report;

/*
    Reports a damage on a container (feedback 4: from the yard / move screen, the container catalog
    or during inspection). Goes into the open case of the container, or opens a new one.
    Gate-in reports through the gate-in itself (condition Damaged), not through this command.
*/
public sealed record ReportDamageCommand(Guid ContainerId, string Description, RepairFindingSource Source)
    : ICommand<Result<RecordedDamage>>;
