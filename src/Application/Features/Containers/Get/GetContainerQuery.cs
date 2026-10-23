using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Get;

public sealed record GetContainerQuery(Guid Id) : IQuery<Result<ContainerResponse>>;

/*
    Container master data.
    OwnerId/OwnerCode/OwnerPartnerName: the owner partner (null until the container is reconciled).
    OwnerName: owner text as first recorded (the only owner field before 23/10/2026), kept for reconciliation.
    CurrentOperatorId/Code: line operator of the confirmed operating assignment that covers today (Vietnam date),
    null when there is none - gate-in is then refused until the relation is recorded and confirmed.
*/
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
    DateTimeOffset CreatedAt,
    Guid? OwnerId,
    string? OwnerCode,
    string? OwnerPartnerName,
    Guid? CurrentOperatorId,
    string? CurrentOperatorCode)
{
    // owner = the loaded Owner navigation; currentOperator = operator of today's confirmed assignment
    public static ContainerResponse From(Container c, LineOperator? currentOperator) => new(
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
        c.CreatedAt,
        c.OwnerId,
        c.Owner?.Code,
        c.Owner?.Name,
        currentOperator?.Id,
        currentOperator?.Code);
}
