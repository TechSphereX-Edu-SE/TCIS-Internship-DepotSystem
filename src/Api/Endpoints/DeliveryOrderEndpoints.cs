using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Cancel;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Create;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.GetAll;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

// Delivery orders from line operators (spec section 3): create, view, cancel (Q7)
public static class DeliveryOrderEndpoints
{
    public static void MapDeliveryOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/delivery-orders")
            .WithTags("DeliveryOrders")
            .RequireAuthorization();

        group.MapGet("/", GetAll)
            .WithName("GetAllDeliveryOrders")
            .WithSummary("Get delivery orders (search, status, usable for gate-out, line operator)");

        group.MapGet("/{id:guid}", GetById)
            .WithName("GetDeliveryOrderById")
            .WithSummary("Get a delivery order by ID");

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<CreateDeliveryOrderCommand>>()
            .WithName("CreateDeliveryOrder")
            .WithSummary("Create a delivery order with the quantity of each container type and size");

        group.MapPost("/{id:guid}/cancel", Cancel)
            .WithName("CancelDeliveryOrder")
            .WithSummary("Cancel an order nothing was released under, with a reason");
    }

    private static async Task<IResult> GetAll(
        int? page,
        int? pageSize,
        string? search,
        DeliveryOrderStatus? status,
        bool? usable,
        Guid? operatorId,
        IQueryHandler<GetAllDeliveryOrdersQuery, Result<PagedResult<DeliveryOrderResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetAllDeliveryOrdersQuery(page ?? 1, pageSize ?? 20, search, status, usable ?? false, operatorId);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueryHandler<GetDeliveryOrderQuery, Result<DeliveryOrderResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetDeliveryOrderQuery(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Create(
        CreateDeliveryOrderCommand command,
        ICommandHandler<CreateDeliveryOrderCommand, Result<CreateDeliveryOrderResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, "GetDeliveryOrderById", new { id = result.Value!.Id })
            : result.ToProblemDetails();
    }

    private static async Task<IResult> Cancel(
        Guid id,
        CancelDeliveryOrderRequest request,
        IValidator<CancelDeliveryOrderCommand> validator,
        ICommandHandler<CancelDeliveryOrderCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        // Validate the command (id from the route + body), not the raw request
        var command = new CancelDeliveryOrderCommand(id, request.Reason);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }
}

public sealed record CancelDeliveryOrderRequest(string Reason);
