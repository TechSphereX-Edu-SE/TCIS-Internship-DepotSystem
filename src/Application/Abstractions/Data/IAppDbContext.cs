
using TechSpherex.CleanArchitecture.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace TechSpherex.CleanArchitecture.Application.Abstractions.Data;

public interface IAppDbContext
{
    DbSet<TodoItem> Todos { get; }
    DbSet<Container> Containers { get; }   // Update container
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
