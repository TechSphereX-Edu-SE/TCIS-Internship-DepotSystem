using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Create;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Delete;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Resize;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

public static class BlockEndpoints
{
    public static void MapBlockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/blocks")
            .WithTags("Blocks")
            .RequireAuthorization();

        group.MapGet("/", GetAll)
            .WithName("GetAllBlocks")
            .WithSummary("Get yard blocks with the number of containers in each (archived ones only with includeArchived=true)");

        group.MapGet("/{id:guid}", GetById)
            .WithName("GetBlockById")
            .WithSummary("Get a yard block by ID");

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<CreateBlockCommand>>()
            .WithName("CreateBlock")
            .WithSummary("Create a physical (bay/row/tier) or virtual block");

        group.MapPut("/{id:guid}/size", Resize)
            .WithName("ResizeBlock")
            .WithSummary("Expand or shrink a physical block");

        group.MapPut("/{id:guid}", Update)
            .WithName("UpdateBlock")
            .WithSummary("Change the code, description and accepted container types of a block");

        group.MapDelete("/{id:guid}", Delete)
            .WithName("DeleteBlock")
            .WithSummary("Delete an empty block; a block with history is archived instead");
    }

    private static async Task<IResult> GetAll(
        int? page,
        int? pageSize,
        bool? includeArchived,
        IQueryHandler<GetAllBlocksQuery, Result<PagedResult<BlockResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetAllBlocksQuery(page ?? 1, pageSize ?? 20, includeArchived ?? false);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueryHandler<GetBlockQuery, Result<BlockResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetBlockQuery(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Create(
        CreateBlockCommand command,
        ICommandHandler<CreateBlockCommand, Result<CreateBlockResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, "GetBlockById", new { id = result.Value!.Id })
            : result.ToProblemDetails();
    }

    private static async Task<IResult> Resize(
        Guid id,
        ResizeBlockRequest request,
        IValidator<ResizeBlockCommand> validator,
        ICommandHandler<ResizeBlockCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        // Validate the command (id from the route + body), not the raw request
        var command = new ResizeBlockCommand(id, request.MaxBays, request.MaxRows, request.MaxTiers);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateBlockRequest request,
        IValidator<UpdateBlockCommand> validator,
        ICommandHandler<UpdateBlockCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        // Same pattern as Resize: the id comes from the route, the fields from the body
        var command = new UpdateBlockCommand(id, request.Code, request.Description, request.AllowedCategories ?? []);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    // 200 with { archived }: the client tells "deleted" apart from "archived because it has history"
    private static async Task<IResult> Delete(
        Guid id,
        ICommandHandler<DeleteBlockCommand, Result<DeleteBlockResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteBlockCommand(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }
}

public sealed record ResizeBlockRequest(int MaxBays, int MaxRows, int MaxTiers);

public sealed record UpdateBlockRequest(string Code, string? Description, IReadOnlyList<ContainerCategory>? AllowedCategories);
