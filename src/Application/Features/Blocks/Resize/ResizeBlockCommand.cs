using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Resize;

// Expand or shrink a physical block ("mo rong, thu hep bai")
public sealed record ResizeBlockCommand(Guid Id, int MaxBays, int MaxRows, int MaxTiers) : ICommand;
