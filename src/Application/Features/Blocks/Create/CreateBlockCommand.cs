using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Create;

/*
    Physical block: MaxBays/MaxRows/MaxTiers are required. Virtual block: all three are omitted.
    AllowedCategories: the container types the block takes (at least one), for physical and virtual blocks.
*/
public sealed record CreateBlockCommand(
    string Code,
    string? Description,
    bool IsVirtual,
    int? MaxBays,
    int? MaxRows,
    int? MaxTiers,
    IReadOnlyList<ContainerCategory> AllowedCategories)
    : ICommand<Result<CreateBlockResponse>>;

public sealed record CreateBlockResponse(Guid Id, string Code, bool IsVirtual);
