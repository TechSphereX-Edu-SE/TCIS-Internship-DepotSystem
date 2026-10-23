using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Delete;

// Feedback 9: delete a block that holds no container; a block with history is archived instead
public sealed record DeleteBlockCommand(Guid Id) : ICommand<Result<DeleteBlockResponse>>;

// Archived = true: the block had history and was kept (soft delete); false: it was removed
public sealed record DeleteBlockResponse(Guid Id, string Code, bool Archived);
