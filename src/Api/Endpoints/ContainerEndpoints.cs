using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Create;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Delete;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Update;
using TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateCondition;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
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
            .WithSummary("Get containers with pagination, optionally searching by container number");

        group.MapGet("/{id:guid}", GetById)
            .WithName("GetContainerById")
            .WithSummary("Get a container by ID");

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<CreateContainerCommand>>()
            .WithName("CreateContainer")
            .WithSummary("Register container master data (number, ISO code, size, type, weights, owner)");

        group.MapPut("/{id:guid}", Update)
            .WithName("UpdateContainer")
            .WithSummary("Update container master data (the container number cannot change)");

        group.MapPut("/{id:guid}/condition", UpdateCondition)
            .WithName("UpdateContainerCondition")
            .WithSummary("Record the container condition (e.g. back to Normal after repair)");

        group.MapDelete("/{id:guid}", Delete)
            .WithName("DeleteContainer")
            .WithSummary("Delete a container that has no gate-in/out history");
    }

    private static async Task<IResult> GetAll(
        int? page,
        int? pageSize,
        string? search,
        Guid? operatorId,
        IQueryHandler<GetAllContainersQuery, Result<PagedResult<ContainerResponse>>> handler,
        CancellationToken cancellationToken)
    {
        // ?search= filters by part of the container number (gate-in lookup, container list search box)
        // ?operatorId= keeps containers whose confirmed operating assignment today names that operator
        var query = new GetAllContainersQuery(page ?? 1, pageSize ?? 10, search, operatorId);
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

    private static async Task<IResult> Update(
        Guid id,
        UpdateContainerRequest request,
        IValidator<UpdateContainerCommand> validator,
        ICommandHandler<UpdateContainerCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        // Validate the command (id from the route + body), not the raw request
        var command = new UpdateContainerCommand(id, request.IsoCode, request.Size, request.Category,
            request.MaxGrossWeightKg, request.TareWeightKg, request.ManufactureDate, request.OwnerId);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> UpdateCondition(
    Guid id,
    UpdateContainerConditionRequest request,
    IValidator<UpdateContainerConditionCommand> validator,
    ICommandHandler<UpdateContainerConditionCommand, Result> handler,
    CancellationToken cancellationToken)
    {
        // Validate the command (id from the route + body), not the raw request
        var command = new UpdateContainerConditionCommand(id, request.Condition, request.ConditionNote);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

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

public sealed record UpdateContainerRequest(
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    int MaxGrossWeightKg,
    int TareWeightKg,
    DateOnly? ManufactureDate,
    Guid OwnerId);


public sealed record UpdateContainerConditionRequest(
    ContainerCondition Condition,
    string? ConditionNote);
