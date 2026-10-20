using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Create;

// Physical block: MaxBays/MaxRows/MaxTiers are required. Virtual block: all three are omitted.
public sealed record CreateBlockCommand(
    string Code,
    string? Description,
    bool IsVirtual,
    int? MaxBays,
    int? MaxRows,
    int? MaxTiers)
    : ICommand<Result<CreateBlockResponse>>;

public sealed record CreateBlockResponse(Guid Id, string Code, bool IsVirtual);
