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

        var container = new Container(
            number,
            command.IsoCode,
            command.Size,
            command.Category,
            command.MaxGrossWeightKg,
            command.TareWeightKg,
            command.ManufactureDate,
            command.OwnerName);

        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateContainerResponse(container.Id, container.ContainerNumber, container.IsoCode));
    }
}
