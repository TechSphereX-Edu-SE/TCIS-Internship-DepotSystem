using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOut;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOutForRepair;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetGateOutCandidates;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetPosition;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetDaily;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Api.Endpoints;

// Container lifecycle in the yard (spec section 3): gate-in, move, current position, gate-out
public static class ContainerVisitEndpoints
{
    public static void MapContainerVisitEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/container-visits")
            .WithTags("ContainerVisits")
            .RequireAuthorization();

        group.MapPost("/gate-in", GateIn)
            .AddEndpointFilter<ValidationFilter<GateInCommand>>()
            .WithName("GateInContainer")
            .WithSummary("Bring a container into the yard at a free position");

        group.MapPost("/move", Move)
            .AddEndpointFilter<ValidationFilter<MoveContainerCommand>>()
            .WithName("MoveContainer")
            .WithSummary("Move a container in the yard to another free position or block");

        group.MapPost("/gate-out", GateOut)
            .AddEndpointFilter<ValidationFilter<GateOutCommand>>()
            .WithName("GateOutContainer")
            .WithSummary("Release a container under a delivery order of its line operator");

        group.MapPost("/gate-out-repair", GateOutForRepair)
            .AddEndpointFilter<ValidationFilter<GateOutForRepairCommand>>()
            .WithName("GateOutContainerForRepair")
            .WithSummary("Confirm a container requested for outside repair has left (no delivery order used)");

        group.MapGet("/gate-out-candidates", GetGateOutCandidates)
            .WithName("GetGateOutCandidates")
            .WithSummary("Containers in the yard matching a delivery order, with the reason when one cannot leave yet");

        group.MapGet("/position", GetPosition)
            .WithName("GetContainerPosition")
            .WithSummary("Get the current yard position of a container by its container number");

        group.MapGet("/in-yard", GetInYard)
            .WithName("GetContainersInYard")
            .WithSummary("List containers in the yard, optionally by block and row (yard map)");

        group.MapGet("/yard-search", SearchYard)
            .WithName("SearchYardContainers")
            .WithSummary("Search containers in the yard with filters, one page at a time (Vị trí & đảo chuyển)");

        group.MapGet("/history", SearchHistory)
            .WithName("SearchVisitHistory")
            .WithSummary("Every visit, open or closed, one row per visit (Lịch sử container)");

        group.MapGet("/history/{visitId:guid}", GetVisitHistory)
            .WithName("GetVisitHistory")
            .WithSummary("One visit with its gate-in/out, moves, repair cases and stored documents");

        group.MapGet("/history/containers/{containerId:guid}", GetContainerVisitSummary)
            .WithName("GetContainerVisitSummary")
            .WithSummary("Visit counts, presence and dwell days of one container");

        group.MapGet("/daily", GetDaily)
            .WithName("GetDailyVisits")
            .WithSummary("Gate-ins and gate-outs of one Vietnam calendar day (default: today)");
    }

    private static async Task<IResult> GateIn(
        GateInCommand command,
        ICommandHandler<GateInCommand, Result<GateInResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        // 200 instead of 201: a visit has no GET-by-id; its position is read by container number
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Move(
        MoveContainerCommand command,
        ICommandHandler<MoveContainerCommand, Result<ContainerPositionResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GateOut(
    GateOutCommand command,
    ICommandHandler<GateOutCommand, Result<GateOutResponse>> handler,
    CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GateOutForRepair(
        GateOutForRepairCommand command,
        ICommandHandler<GateOutForRepairCommand, Result<GateOutForRepairResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // deliveryOrderId is required; search = part of the container number
    private static async Task<IResult> GetGateOutCandidates(
        Guid deliveryOrderId,
        string? search,
        int? page,
        int? pageSize,
        IQueryHandler<GetGateOutCandidatesQuery, Result<PagedResult<GateOutCandidate>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetGateOutCandidatesQuery(deliveryOrderId, search, page ?? 1, pageSize ?? 20);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // containerNumber is a required query string value (?containerNumber=MSCU1234566)
    private static async Task<IResult> GetPosition(
        string containerNumber,
        IQueryHandler<GetContainerPositionQuery, Result<ContainerPositionResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetContainerPositionQuery(containerNumber), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // Both filters are optional: ?blockId=...&row=1 for one row of the yard map
    private static async Task<IResult> GetInYard(
        Guid? blockId,
        int? row,
        IQueryHandler<GetContainersInYardQuery, Result<IReadOnlyList<YardContainerResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetContainersInYardQuery(blockId, row), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // Every filter is optional; repair = None | Abnormal | InRepair | AnyOpen
    private static async Task<IResult> SearchYard(
        string? search,
        Guid? blockId,
        ContainerCategory? category,
        ContainerSize? size,
        Guid? operatorId,
        YardRepairState? repair,
        int? minDwellDays,
        int? maxDwellDays,
        int? page,
        int? pageSize,
        DateOnly? gateInFrom,
        DateOnly? gateInTo,
        YardSort? sort,
        IValidator<SearchYardContainersQuery> validator,
        IQueryHandler<SearchYardContainersQuery, Result<PagedResult<YardContainerResponse>>> handler,
        CancellationToken cancellationToken)
    {
        // Query string values are validated as a query object (the validation filter only reads the body)
        var query = new SearchYardContainersQuery(search, blockId, category, size, operatorId, repair,
            minDwellDays, maxDwellDays, page ?? 1, pageSize ?? 20, gateInFrom, gateInTo, sort ?? YardSort.Position);
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // Feedback 7 + 8: all filters optional; from/to are Vietnam dates, the event says which time they apply to
    private static async Task<IResult> SearchHistory(
        string? search,
        Guid? containerId,
        Guid? operatorId,
        DateOnly? from,
        DateOnly? to,
        VisitHistoryEvent? @event,
        ContainerCategory? category,
        ContainerSize? size,
        VisitState? state,
        GateOutPurpose? purpose,
        int? page,
        int? pageSize,
        IValidator<SearchVisitHistoryQuery> validator,
        IQueryHandler<SearchVisitHistoryQuery, Result<PagedResult<VisitHistoryRow>>> handler,
        CancellationToken cancellationToken)
    {
        // Query string values are validated as a query object (the validation filter only reads the body)
        var query = new SearchVisitHistoryQuery(search, containerId, operatorId, from, to,
            @event ?? VisitHistoryEvent.Any, category, size, state, purpose, page ?? 1, pageSize ?? 20);
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetVisitHistory(
        Guid visitId,
        IQueryHandler<GetVisitHistoryQuery, Result<VisitHistoryDetail>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetVisitHistoryQuery(visitId), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetContainerVisitSummary(
        Guid containerId,
        IQueryHandler<GetContainerVisitSummaryQuery, Result<ContainerVisitSummary>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetContainerVisitSummaryQuery(containerId), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    // ?date=2026-10-22 (Vietnam date); without it the handler uses today in Vietnam
    private static async Task<IResult> GetDaily(
        DateOnly? date,
        IQueryHandler<GetDailyVisitsQuery, Result<DailyVisitsResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetDailyVisitsQuery(date), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }
}
