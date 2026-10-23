using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Update;

/*
    Feedback 9: change the code, the description and the accepted container types of a block,
    physical or virtual. The size has its own command (Resize); a block never switches between
    physical and virtual, because its visits are stored in that positioning mode.
*/
public sealed record UpdateBlockCommand(
    Guid Id,
    string Code,
    string? Description,
    IReadOnlyList<ContainerCategory> AllowedCategories)
    : ICommand;
