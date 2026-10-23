using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.ThroughputEvents;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
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

        group.MapGet("/throughput/events", GetThroughputEvents)
            .WithName("GetThroughputEvents")
            .WithSummary("Gate-in or gate-out rows behind the throughput totals, same filters, newest first");
    }

    // Same filters as the detail rows (GET /api/container-visits/yard-search), so totals and rows agree
    private static async Task<IResult> GetYardInventory(
        int? longStayDays,
        Guid? operatorId,
        ContainerCategory? category,
        ContainerSize? size,
        int? minDwellDays,
        int? maxDwellDays,
        Guid? blockId,
        YardRepairState? repair,
        DateOnly? gateInFrom,
        DateOnly? gateInTo,
        IValidator<GetYardInventoryQuery> validator,
        IQueryHandler<GetYardInventoryQuery, Result<YardInventoryResponse>> handler,
        CancellationToken cancellationToken)
    {
        // Query string values are validated as a query object (the validation filter only reads the body)
        var query = new GetYardInventoryQuery(longStayDays ?? GetYardInventoryQuery.DefaultLongStayDays,
            operatorId, category, size, minDwellDays, maxDwellDays, blockId, repair, gateInFrom, gateInTo);
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // from/to are required query string dates (?from=2026-10-01&to=2026-10-31); the other filters are optional
    private static async Task<IResult> GetThroughput(
        DateOnly from,
        DateOnly to,
        Guid? lineOperatorId,
        ContainerCategory? category,
        ContainerSize? size,
        GateOutPurpose? purpose,
        IValidator<GetThroughputQuery> validator,
        IQueryHandler<GetThroughputQuery, Result<ThroughputResponse>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetThroughputQuery(from, to, lineOperatorId, category, size, purpose);
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // ?kind=GateIn|GateOut plus the same filters as /throughput, one page at a time
    private static async Task<IResult> GetThroughputEvents(
        ThroughputEventKind kind,
        DateOnly from,
        DateOnly to,
        Guid? lineOperatorId,
        ContainerCategory? category,
        ContainerSize? size,
        GateOutPurpose? purpose,
        int? page,
        int? pageSize,
        IValidator<GetThroughputEventsQuery> validator,
        IQueryHandler<GetThroughputEventsQuery, Result<PagedResult<VisitHistoryRow>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetThroughputEventsQuery(kind, from, to, lineOperatorId, category, size, purpose,
            page ?? 1, pageSize ?? 20);
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }
}
