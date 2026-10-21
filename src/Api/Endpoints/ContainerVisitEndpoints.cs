using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetPosition;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

// Container lifecycle in the yard (spec section 3): gate-in, move, current position; gate-out comes next
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

        group.MapGet("/position", GetPosition)
            .WithName("GetContainerPosition")
            .WithSummary("Get the current yard position of a container by its container number");
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

    // containerNumber is a required query string value (?containerNumber=MSCU1234566)
    private static async Task<IResult> GetPosition(
        string containerNumber,
        IQueryHandler<GetContainerPositionQuery, Result<ContainerPositionResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetContainerPositionQuery(containerNumber), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }
}
