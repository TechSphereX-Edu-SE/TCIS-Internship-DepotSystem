using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

// Container lifecycle in the yard (spec section 3): gate-in now; move, position and gate-out come next
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
    }

    private static async Task<IResult> GateIn(
        GateInCommand command,
        ICommandHandler<GateInCommand, Result<GateInResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        // 200 instead of 201: there is no GET-by-id for a visit yet (current position API is step 3)
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }
}
