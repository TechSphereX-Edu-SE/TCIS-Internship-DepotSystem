using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Create;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

public static class LineOperatorEndpoints
{
    public static void MapLineOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/line-operators")
            .WithTags("Line operators")
            .RequireAuthorization();

        group.MapGet("/", GetAll)
            .WithName("GetAllLineOperators")
            .WithSummary("Get all line operators");

        group.MapGet("/{id:guid}", GetById)
            .WithName("GetLineOperatorById")
            .WithSummary("Get a line operator by ID");

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<CreateLineOperatorCommand>>()
            .WithName("CreateLineOperator")
            .WithSummary("Create a line operator");

        group.MapPut("/{id:guid}", Update)
            .WithName("UpdateLineOperator")
            .WithSummary("Rename a line operator");
    }

    private static async Task<IResult> GetAll(
        int? page,
        int? pageSize,
        IQueryHandler<GetAllLineOperatorsQuery, Result<PagedResult<LineOperatorResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetAllLineOperatorsQuery(page ?? 1, pageSize ?? 20);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueryHandler<GetLineOperatorQuery, Result<LineOperatorResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetLineOperatorQuery(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Create(
        CreateLineOperatorCommand command,
        ICommandHandler<CreateLineOperatorCommand, Result<CreateLineOperatorResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, "GetLineOperatorById", new { id = result.Value!.Id })
            : result.ToProblemDetails();
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateLineOperatorRequest request,
        IValidator<UpdateLineOperatorCommand> validator,
        ICommandHandler<UpdateLineOperatorCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        // Validate the command (id from the route + body), not the raw request
        var command = new UpdateLineOperatorCommand(id, request.Name);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }
}

public sealed record UpdateLineOperatorRequest(string Name);
