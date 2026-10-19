using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Create;

public sealed class CreateContainerCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<CreateContainerCommand, Result<CreateContainerResponse>>
{
    public async Task<Result<CreateContainerResponse>> HandleAsync(
        CreateContainerCommand command, CancellationToken cancellationToken = default)
    {
        var number = command.ContainerNumber.Trim().ToUpperInvariant();

        var exists = await dbContext.Containers
            .AnyAsync(c => c.ContainerNumber == number, cancellationToken);
        if (exists)
            return Result.Failure<CreateContainerResponse>(
                Error.Conflict("Container.Duplicate", $"Container '{number}' already exists."));

        var container = new Container(number, command.ContainerType);

        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateContainerResponse(
            container.Id, container.ContainerNumber, container.ContainerType));
    }
}
