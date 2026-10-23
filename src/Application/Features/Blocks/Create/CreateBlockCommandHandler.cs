using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Create;

public sealed class CreateBlockCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<CreateBlockCommand, Result<CreateBlockResponse>>
{
    public async Task<Result<CreateBlockResponse>> HandleAsync(
        CreateBlockCommand command, CancellationToken cancellationToken = default)
    {
        var code = command.Code.Trim().ToUpperInvariant();

        var exists = await dbContext.Blocks.AnyAsync(b => b.Code == code, cancellationToken);
        if (exists)
            return Result.Failure<CreateBlockResponse>(
                Error.Conflict("Block.Duplicate", $"Block '{code}' already exists."));

        var block = command.IsVirtual
            ? Block.CreateVirtual(code, command.Description, command.AllowedCategories)
            : Block.CreatePhysical(code, command.MaxBays!.Value, command.MaxRows!.Value, command.MaxTiers!.Value,
                command.Description, command.AllowedCategories);

        dbContext.Blocks.Add(block);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateBlockResponse(block.Id, block.Code, block.IsVirtual));
    }
}
