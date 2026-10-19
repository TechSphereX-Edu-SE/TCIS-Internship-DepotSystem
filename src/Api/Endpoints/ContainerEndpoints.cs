using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Create;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Delete;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateStatus;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

public static class ContainerEndpoints
{
    public static void MapContainerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/containers")
            .WithTags("Containers")
            .RequireAuthorization();

        group.MapGet("/", GetAll)
            .WithName("GetAllContainers")
            .WithSummary("Get all containers with pagination");

        group.MapGet("/{id:guid}", GetById)
            .WithName("GetContainerById")
            .WithSummary("Get a container by ID");

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<CreateContainerCommand>>()
            .WithName("CreateContainer")
            .WithSummary("Register a new container into the yard");

        group.MapPatch("/{id:guid}/status", UpdateStatus)
            .WithName("UpdateContainerStatus")
            .WithSummary("Change container status (InYard, Loaded, Departed, Damaged)");

        group.MapDelete("/{id:guid}", Delete)
            .WithName("DeleteContainer")
            .WithSummary("Delete a container");
    }

    private static async Task<IResult> GetAll(
        int? page,
        int? pageSize,
        IQueryHandler<GetAllContainersQuery, Result<PagedResult<ContainerResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetAllContainersQuery(page ?? 1, pageSize ?? 10);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueryHandler<GetContainerQuery, Result<ContainerResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetContainerQuery(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Create(
        CreateContainerCommand command,
        ICommandHandler<CreateContainerCommand, Result<CreateContainerResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, "GetContainerById", new { id = result.Value!.Id })
            : result.ToProblemDetails();
    }

    private static async Task<IResult> UpdateStatus(
        Guid id,
        UpdateContainerStatusRequest request,
        ICommandHandler<UpdateContainerStatusCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateContainerStatusCommand(id, request.Status);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> Delete(
        Guid id,
        ICommandHandler<DeleteContainerCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteContainerCommand(id), cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }
}

public sealed record UpdateContainerStatusRequest(ContainerStatus Status);
