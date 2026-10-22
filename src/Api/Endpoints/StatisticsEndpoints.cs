using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

// Depot statistics (plan phase 4); all dates are Vietnam dates
public static class StatisticsEndpoints
{
    public static void MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/statistics")
            .WithTags("Statistics")
            .RequireAuthorization();

        group.MapGet("/yard-inventory", GetYardInventory)
            .WithName("GetYardInventory")
            .WithSummary("Containers in the yard per line operator, split by dwell time (default 10 days)");

        group.MapGet("/throughput", GetThroughput)
            .WithName("GetThroughput")
            .WithSummary("Gate-in and gate-out volume per line operator per day");
    }

    private static async Task<IResult> GetYardInventory(
        int? longStayDays,
        IValidator<GetYardInventoryQuery> validator,
        IQueryHandler<GetYardInventoryQuery, Result<YardInventoryResponse>> handler,
        CancellationToken cancellationToken)
    {
        // Query string values are validated as a query object (the validation filter only reads the body)
        var query = new GetYardInventoryQuery(longStayDays ?? GetYardInventoryQuery.DefaultLongStayDays);
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // from/to are required query string dates (?from=2026-10-01&to=2026-10-31), lineOperatorId is optional
    private static async Task<IResult> GetThroughput(
        DateOnly from,
        DateOnly to,
        Guid? lineOperatorId,
        IValidator<GetThroughputQuery> validator,
        IQueryHandler<GetThroughputQuery, Result<ThroughputResponse>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetThroughputQuery(from, to, lineOperatorId);
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }
}
