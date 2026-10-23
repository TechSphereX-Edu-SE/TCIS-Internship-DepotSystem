using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Cancel;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Confirm;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Create;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.End;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.GetActive;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.GetForContainer;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

/*
    Feedback 2: which line operator operates a container, and for which period.
    Listing and recording are nested under the container; confirm / end / cancel act on one assignment.
*/
public static class OperatingAssignmentEndpoints
{
    public static void MapOperatingAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        var byContainer = app.MapGroup("/api/containers/{containerId:guid}/operating-assignments")
            .WithTags("Operating assignments")
            .RequireAuthorization();

        byContainer.MapGet("/", GetForContainer)
            .WithName("GetContainerOperatingAssignments")
            .WithSummary("Get every operating assignment of a container, newest first");

        byContainer.MapGet("/active", GetActive)
            .WithName("GetActiveOperatingAssignment")
            .WithSummary("Get the confirmed assignment on a date (default today, Vietnam time)");

        byContainer.MapPost("/", Create)
            .WithName("CreateOperatingAssignment")
            .WithSummary("Record which line operator operates the container for a period");

        var group = app.MapGroup("/api/operating-assignments")
            .WithTags("Operating assignments")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/confirm", Confirm)
            .WithName("ConfirmOperatingAssignment")
            .WithSummary("Confirm a pending assignment after checking the contract / instruction");

        group.MapPost("/{id:guid}/end", End)
            .WithName("EndOperatingAssignment")
            .WithSummary("Set the last day of a confirmed assignment");

        group.MapPost("/{id:guid}/cancel", Cancel)
            .WithName("CancelOperatingAssignment")
            .WithSummary("Cancel an assignment recorded by mistake (not once a visit used it)");
    }

    private static async Task<IResult> GetForContainer(
        Guid containerId,
        IQueryHandler<GetContainerAssignmentsQuery, Result<IReadOnlyList<OperatingAssignmentResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetContainerAssignmentsQuery(containerId), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetActive(
        Guid containerId,
        DateOnly? date,
        IQueryHandler<GetActiveAssignmentQuery, Result<OperatingAssignmentResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetActiveAssignmentQuery(containerId, date), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Create(
        Guid containerId,
        CreateOperatingAssignmentRequest request,
        IValidator<CreateOperatingAssignmentCommand> validator,
        ICommandHandler<CreateOperatingAssignmentCommand, Result<CreateOperatingAssignmentResponse>> handler,
        CancellationToken cancellationToken)
    {
        // Validate the command (container id from the route + body), not the raw request
        var command = new CreateOperatingAssignmentCommand(containerId, request.OperatorId, request.ValidFrom,
            request.ValidTo, request.Reference, request.Note, request.Confirm);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/api/containers/{containerId}/operating-assignments", result.Value)
            : result.ToProblemDetails();
    }

    private static async Task<IResult> Confirm(
        Guid id,
        ICommandHandler<ConfirmOperatingAssignmentCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ConfirmOperatingAssignmentCommand(id), cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> End(
        Guid id,
        EndOperatingAssignmentRequest request,
        ICommandHandler<EndOperatingAssignmentCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new EndOperatingAssignmentCommand(id, request.ValidTo), cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> Cancel(
        Guid id,
        CancelOperatingAssignmentRequest request,
        IValidator<CancelOperatingAssignmentCommand> validator,
        ICommandHandler<CancelOperatingAssignmentCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var command = new CancelOperatingAssignmentCommand(id, request.Reason);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }
}

public sealed record CreateOperatingAssignmentRequest(
    Guid OperatorId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Reference,
    string? Note,
    bool Confirm);

public sealed record EndOperatingAssignmentRequest(DateOnly ValidTo);

public sealed record CancelOperatingAssignmentRequest(string Reason);
