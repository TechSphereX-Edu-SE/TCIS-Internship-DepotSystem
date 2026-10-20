using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Create;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Get;
using TechSpherex.CleanArchitecture.Application.Features.Customers.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization();

        group.MapGet("/", GetAll)
            .WithName("GetAllCustomers")
            .WithSummary("Get customers, optionally searching by tax code or name");

        group.MapGet("/{id:guid}", GetById)
            .WithName("GetCustomerById")
            .WithSummary("Get a customer by ID");

        group.MapPost("/", Create)
            .AddEndpointFilter<ValidationFilter<CreateCustomerCommand>>()
            .WithName("CreateCustomer")
            .WithSummary("Create a customer");

        group.MapPut("/{id:guid}", Update)
            .WithName("UpdateCustomer")
            .WithSummary("Rename a customer");
    }

    private static async Task<IResult> GetAll(
        string? search,
        int? page,
        int? pageSize,
        IQueryHandler<GetAllCustomersQuery, Result<PagedResult<CustomerResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetAllCustomersQuery(search, page ?? 1, pageSize ?? 20);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueryHandler<GetCustomerQuery, Result<CustomerResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetCustomerQuery(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Create(
        CreateCustomerCommand command,
        ICommandHandler<CreateCustomerCommand, Result<CreateCustomerResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? TypedResults.CreatedAtRoute(result.Value, "GetCustomerById", new { id = result.Value!.Id })
            : result.ToProblemDetails();
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateCustomerRequest request,
        IValidator<UpdateCustomerCommand> validator,
        ICommandHandler<UpdateCustomerCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        // Validate the command (id from the route + body), not the raw request
        var command = new UpdateCustomerCommand(id, request.Name);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }
}

public sealed record UpdateCustomerRequest(string Name);
