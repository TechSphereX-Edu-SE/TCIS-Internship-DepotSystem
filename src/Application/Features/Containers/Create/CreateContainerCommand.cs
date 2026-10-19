using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Create;

public sealed record CreateContainerCommand(
    string ContainerNumber,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    int MaxGrossWeightKg,
    int TareWeightKg,
    DateOnly? ManufactureDate,
    string OwnerName)
    : ICommand<Result<CreateContainerResponse>>;

public sealed record CreateContainerResponse(Guid Id, string ContainerNumber, string IsoCode);
