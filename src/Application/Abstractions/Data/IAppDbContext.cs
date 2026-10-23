
using TechSpherex.CleanArchitecture.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace TechSpherex.CleanArchitecture.Application.Abstractions.Data;

public interface IAppDbContext
{
    DbSet<TodoItem> Todos { get; }
    DbSet<Container> Containers { get; }

    // Depot
    DbSet<LineOperator> LineOperators { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Block> Blocks { get; }
    DbSet<ContainerVisit> ContainerVisits { get; }
    DbSet<DeliveryOrder> DeliveryOrders { get; }
    DbSet<OperatingAssignment> OperatingAssignments { get; }
    DbSet<RepairCase> RepairCases { get; }
    DbSet<RepairFinding> RepairFindings { get; }
    DbSet<RepairCaseEvent> RepairCaseEvents { get; }
    DbSet<RepairPhoto> RepairPhotos { get; }
    DbSet<ExternalRepairTransfer> ExternalRepairTransfers { get; }
    DbSet<ContainerMove> ContainerMoves { get; }
    DbSet<RepairEstimate> RepairEstimates { get; }
    DbSet<RepairEstimateLine> RepairEstimateLines { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
