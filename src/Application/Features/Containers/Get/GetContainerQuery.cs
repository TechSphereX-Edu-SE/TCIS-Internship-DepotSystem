using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Get;

public sealed record GetContainerQuery(Guid Id) : IQuery<Result<ContainerResponse>>;

public sealed record ContainerResponse(
    Guid Id,
    string ContainerNumber,
    string IsoCode,
    string Size,
    string Category,
    int MaxGrossWeightKg,
    int TareWeightKg,
    DateOnly? ManufactureDate,
    string OwnerName,
    string Condition,
    string? ConditionNote,
    DateTimeOffset CreatedAt)
{
    public static ContainerResponse From(Container c) => new(
        c.Id,
        c.ContainerNumber,
        c.IsoCode,
        c.Size.ToString(),
        c.Category.ToString(),
        c.MaxGrossWeightKg,
        c.TareWeightKg,
        c.ManufactureDate,
        c.OwnerName,
        c.Condition.ToString(),
        c.ConditionNote,
        c.CreatedAt);
}
