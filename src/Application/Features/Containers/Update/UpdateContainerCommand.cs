using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Update;

// Updates container master data. The container number is the identity and cannot change.
// OwnerId: a partner with the Owner role; picking it for an older container reconciles its owner text.
public sealed record UpdateContainerCommand(
    Guid Id,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    int MaxGrossWeightKg,
    int TareWeightKg,
    DateOnly? ManufactureDate,
    Guid OwnerId) : ICommand;
