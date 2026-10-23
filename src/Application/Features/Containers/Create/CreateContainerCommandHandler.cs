using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Create;

public sealed class CreateContainerCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<CreateContainerCommand, Result<CreateContainerResponse>>
{
    public async Task<Result<CreateContainerResponse>> HandleAsync(
        CreateContainerCommand command, CancellationToken cancellationToken = default)
    {
        var number = ContainerNumberRules.Normalize(command.ContainerNumber);

        var exists = await dbContext.Containers
            .AnyAsync(c => c.ContainerNumber == number, cancellationToken);
        if (exists)
            return Result.Failure<CreateContainerResponse>(
                Error.Conflict("Container.Duplicate", $"Container '{number}' already exists."));

        var owner = await dbContext.LineOperators.FindAsync([command.OwnerId], cancellationToken);
        if (owner is null)
            return Result.Failure<CreateContainerResponse>(Error.NotFound("LineOperator.NotFound",
                $"Partner with ID '{command.OwnerId}' was not found."));

        // The owner text recorded with the container is the owner's code (a reference for later reconciliation)
        var container = new Container(
            number,
            command.IsoCode,
            command.Size,
            command.Category,
            command.MaxGrossWeightKg,
            command.TareWeightKg,
            command.ManufactureDate,
            owner.Code);

        var setOwner = container.SetOwner(owner);
        if (setOwner.IsFailure)
            return Result.Failure<CreateContainerResponse>(setOwner.Error!);

        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateContainerResponse(container.Id, container.ContainerNumber, container.IsoCode));
    }
}
