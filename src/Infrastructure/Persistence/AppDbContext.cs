using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Tenancy;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence;

// Copyright (c) 2026 TechSpherex
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options), IAppDbContext
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();
    public DbSet<Container> Containers => Set<Container>();   // Update Container

    // Depot
    public DbSet<LineOperator> LineOperators => Set<LineOperator>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<ContainerVisit> ContainerVisits => Set<ContainerVisit>();
    public DbSet<DeliveryOrder> DeliveryOrders => Set<DeliveryOrder>();
    public DbSet<OperatingAssignment> OperatingAssignments => Set<OperatingAssignment>();
    public DbSet<RepairCase> RepairCases => Set<RepairCase>();
    public DbSet<RepairFinding> RepairFindings => Set<RepairFinding>();
    public DbSet<RepairCaseEvent> RepairCaseEvents => Set<RepairCaseEvent>();
    public DbSet<RepairPhoto> RepairPhotos => Set<RepairPhoto>();
    public DbSet<ExternalRepairTransfer> ExternalRepairTransfers => Set<ExternalRepairTransfer>();
    public DbSet<ContainerMove> ContainerMoves => Set<ContainerMove>();
    public DbSet<RepairEstimate> RepairEstimates => Set<RepairEstimate>();
    public DbSet<RepairEstimateLine> RepairEstimateLines => Set<RepairEstimateLine>();
    // Feedback 10: anchor of the demo data (only the demo seeder uses it, so it is not on IAppDbContext)
    public DbSet<DemoDataset> DemoDatasets => Set<DemoDataset>();

    /*
        Feedback 10: the demo seeder replays past events through the normal handlers. While it runs, the audit
        times (CreatedAt / LastModifiedAt) follow its clock, so an order or estimate created "120 days ago" also
        shows that date. Null (always outside the seeder) = real time. The seeder clears it when done,
        because pooled contexts are reused.
    */
    internal TimeProvider? AuditClock { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Multi-tenant global query filter
        // Applies to all entities implementing ITenantEntity
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ApplyTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, [builder]);
            }
        }
    }

    private static void ApplyTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == "default");
        builder.Entity<TEntity>().HasIndex(e => e.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities();
        SetTenantId();
        return base.SaveChangesAsync(cancellationToken);
    }

    // Copyright (c) 2026 TechSpherex
    private void UpdateAuditableEntities()
    {
        var entries = ChangeTracker.Entries<AuditableEntity>();
        var now = AuditClock?.GetUtcNow() ?? DateTimeOffset.UtcNow;
        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.LastModifiedAt = now;
                    break;
            }
        }
    }

    private void SetTenantId()
    {
        // Resolve ITenantProvider from the current scope via IServiceProvider
        // This avoids constructor injection which is incompatible with DbContextPooling (Aspire)
        var serviceProvider = this.GetInfrastructure();
        var tenantProvider = serviceProvider.GetService<ITenantProvider>();

        if (tenantProvider?.TenantId is null) return;

        var tenantEntries = ChangeTracker.Entries<ITenantEntity>()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in tenantEntries)
        {
            entry.Entity.TenantId = tenantProvider.TenantId;
        }
    }
}
