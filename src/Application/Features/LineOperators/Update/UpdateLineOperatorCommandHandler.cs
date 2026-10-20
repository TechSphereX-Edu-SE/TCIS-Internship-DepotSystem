using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;

public sealed class UpdateLineOperatorCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<UpdateLineOperatorCommand>
{
    public async Task<Result> HandleAsync(
        UpdateLineOperatorCommand command, CancellationToken cancellationToken = default)
    {
        var lineOperator = await dbContext.LineOperators.FindAsync([command.Id], cancellationToken);
        if (lineOperator is null)
            return Result.Failure(Error.NotFound("LineOperator.NotFound", $"Line operator with ID '{command.Id}' was not found."));

        lineOperator.Rename(command.Name);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
