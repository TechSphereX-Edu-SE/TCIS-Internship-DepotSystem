using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Create;

public sealed class CreateLineOperatorCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<CreateLineOperatorCommand, Result<CreateLineOperatorResponse>>
{
    public async Task<Result<CreateLineOperatorResponse>> HandleAsync(
        CreateLineOperatorCommand command, CancellationToken cancellationToken = default)
    {
        var code = command.Code.Trim().ToUpperInvariant();

        var exists = await dbContext.LineOperators.AnyAsync(l => l.Code == code, cancellationToken);
        if (exists)
            return Result.Failure<CreateLineOperatorResponse>(
                Error.Conflict("LineOperator.Duplicate", $"Line operator '{code}' already exists."));

        var lineOperator = new LineOperator(code, command.Name, command.TaxCode, command.IsOwner, command.IsOperator);

        dbContext.LineOperators.Add(lineOperator);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateLineOperatorResponse(lineOperator.Id, lineOperator.Code, lineOperator.Name));
    }
}
