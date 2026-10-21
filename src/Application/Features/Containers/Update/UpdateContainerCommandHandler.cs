using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Update;

public sealed class UpdateContainerCommandHandler(IAppDbContext dbContext) : ICommandHandler<UpdateContainerCommand>
{
    public async Task<Result> HandleAsync(UpdateContainerCommand command, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers.FindAsync([command.Id], cancellationToken);
        if (container is null)
            return Result.Failure(Error.NotFound("Container.NotFound", $"Container with ID '{command.Id}' was not found."));

        // The yard position (odd/even bay) depends on size, so size cannot change while in the yard
        if (command.Size != container.Size)
        {
            var inYard = await dbContext.ContainerVisits
                .AnyAsync(v => v.ContainerId == container.Id && v.GateOutAt == null, cancellationToken);
            if (inYard)
                return Result.Failure(Error.Conflict("Container.SizeChangeInYard",
                    $"Container '{container.ContainerNumber}' is in the yard; its size cannot be changed."));
        }

        container.UpdateDetails(
            command.IsoCode,
            command.Size,
            command.Category,
            command.MaxGrossWeightKg,
            command.TareWeightKg,
            command.ManufactureDate,
            command.OwnerName);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
