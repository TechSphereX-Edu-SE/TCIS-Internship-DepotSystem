using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Create;

// OwnerId: a partner with the Owner role (feedback 9: stored by id, not as typed text)
public sealed record CreateContainerCommand(
    string ContainerNumber,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    int MaxGrossWeightKg,
    int TareWeightKg,
    DateOnly? ManufactureDate,
    Guid OwnerId)
    : ICommand<Result<CreateContainerResponse>>;

public sealed record CreateContainerResponse(Guid Id, string ContainerNumber, string IsoCode);
