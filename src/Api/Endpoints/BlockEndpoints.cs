using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Create;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Resize;
using TechSpherex.CleanArchitecture.Domain.Common;
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
            .WithSummary("Get all yard blocks with the number of containers in each");

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
    }

    private static async Task<IResult> GetAll(
        int? page,
        int? pageSize,
        IQueryHandler<GetAllBlocksQuery, Result<PagedResult<BlockResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetAllBlocksQuery(page ?? 1, pageSize ?? 20);
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
}

public sealed record ResizeBlockRequest(int MaxBays, int MaxRows, int MaxTiers);
