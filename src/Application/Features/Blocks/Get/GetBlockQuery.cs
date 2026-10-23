using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;

public sealed record GetBlockQuery(Guid Id) : IQuery<Result<BlockResponse>>;

public sealed record BlockResponse(
    Guid Id,
    string Code,
    string? Description,
    bool IsVirtual,
    int? MaxBays,
    int? MaxRows,
    int? MaxTiers,
    IReadOnlyList<ContainerCategory> AllowedCategories,
    int ContainersInYard,
    DateTimeOffset CreatedAt);
