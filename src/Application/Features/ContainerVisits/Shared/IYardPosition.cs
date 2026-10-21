namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;

/*
    A target yard position in a request (gate-in, move).
    Lets the commands share one position validator instead of repeating the same rules.
*/
public interface IYardPosition
{
    Guid BlockId { get; }
    int? Bay { get; }
    int? Row { get; }
    int? Tier { get; }
}
