
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace TechSpherex.CleanArchitecture.Infrastructure.Persistence;

public static class AppDbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await context.Database.MigrateAsync();

        await SeedRolesAsync(roleManager, logger);
        await SeedAdminUserAsync(userManager, logger);
        await SeedSampleTodosAsync(context, logger);
        await SeedDepotMasterDataAsync(context, logger);
    }

    // Depot sample data: partners, customers, yard blocks, containers, operating assignments.
    private static async Task SeedDepotMasterDataAsync(AppDbContext context, ILogger logger)
    {
        if (!await context.LineOperators.AnyAsync())
        {
            // The three lines own and operate containers; Triton is a leasing company that only owns them
            context.LineOperators.AddRange(
                new LineOperator("MSC", "Mediterranean Shipping Company", isOwner: true, isOperator: true),
                new LineOperator("CMA", "CMA CGM", isOwner: true, isOperator: true),
                new LineOperator("HMM", "Hyundai Merchant Marine", isOwner: true, isOperator: true),
                new LineOperator("TRI", "Triton International", isOwner: true, isOperator: false));
        }

        if (!await context.Customers.AnyAsync())
        {
            context.Customers.AddRange(
                new Customer("0301234567", "Công ty TNHH Xuất nhập khẩu Minh An"),
                new Customer("0309876543", "Công ty CP Logistics Phương Nam"));
        }

        if (!await context.Blocks.AnyAsync())
        {
            // Allowed types are explicit: block B takes reefers only, A every other type, WAIT any type
            var notReefer = Block.AllCategories.Where(c => c != ContainerCategory.Reefer);
            context.Blocks.AddRange(
                Block.CreatePhysical("A", maxBays: 20, maxRows: 6, maxTiers: 5, "Khu container khô", notReefer),
                Block.CreatePhysical("B", maxBays: 10, maxRows: 4, maxTiers: 4, "Khu container lạnh", [ContainerCategory.Reefer]),
                Block.CreateVirtual("WAIT", "Khu chờ sửa chữa / giám định", Block.AllCategories));
        }

        if (!await context.Containers.AnyAsync())
        {
            // ISO 6346 container numbers (category U, valid check digit) with ISO codes matching size and type
            context.Containers.AddRange(
                new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, new DateOnly(2019, 5, 1), "MSC"),
                new Container("MSCU5551234", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2250, new DateOnly(2020, 3, 1), "MSC"),
                new Container("CMAU7654327", "42G1", ContainerSize.Ft40, ContainerCategory.Dry, 30480, 3750, new DateOnly(2018, 8, 1), "CMA"),
                new Container("CMAU9001110", "45G1", ContainerSize.Ft40, ContainerCategory.Dry, 30480, 3900, new DateOnly(2021, 1, 1), "CMA"),
                new Container("HMMU2468109", "45R1", ContainerSize.Ft40, ContainerCategory.Reefer, 34000, 4800, new DateOnly(2022, 6, 1), "HMM"),
                new Container("HMMU3007776", "22R1", ContainerSize.Ft20, ContainerCategory.Reefer, 30480, 3000, new DateOnly(2022, 9, 1), "HMM"));
        }

        var saved = await context.SaveChangesAsync();
        saved += await SeedSampleOwnersAndAssignmentsAsync(context);
        if (saved > 0)
            logger.LogInformation("Seeded {Count} depot master data rows", saved);
    }

    /*
        Sample containers -> (owner code, operator code). HMMU2468109 is the feedback example:
        HMM owns it, CMA rents it, so CMA is the operator at gate-in.
        Runs on new and on upgraded databases: an owner is only set when missing, and assignments
        are only added while there is none at all, so real data is never overwritten.
    */
    private static readonly (string Number, string Owner, string Operator)[] SampleRelations =
    [
        ("MSCU1234566", "MSC", "MSC"),
        ("MSCU5551234", "MSC", "MSC"),
        ("CMAU7654327", "CMA", "CMA"),
        ("CMAU9001110", "CMA", "CMA"),
        ("HMMU2468109", "HMM", "CMA"),
        ("HMMU3007776", "HMM", "HMM")
    ];

    private static async Task<int> SeedSampleOwnersAndAssignmentsAsync(AppDbContext context)
    {
        var numbers = SampleRelations.Select(r => r.Number).ToList();
        var containers = await context.Containers
            .Where(c => numbers.Contains(c.ContainerNumber))
            .ToDictionaryAsync(c => c.ContainerNumber);
        var partners = await context.LineOperators.ToDictionaryAsync(p => p.Code);
        var addAssignments = !await context.OperatingAssignments.AnyAsync();
        var confirmedAt = DateTimeOffset.UtcNow;

        foreach (var (number, ownerCode, operatorCode) in SampleRelations)
        {
            if (!containers.TryGetValue(number, out var container))
                continue;

            if (container.OwnerId is null && partners.TryGetValue(ownerCode, out var owner))
            {
                owner.AddOwnerRole();
                container.SetOwner(owner);
            }

            if (!addAssignments || !partners.TryGetValue(operatorCode, out var lineOperator))
                continue;

            // Open-ended and already confirmed, so the sample containers can be gated in straight away
            var assignment = OperatingAssignment.Create(
                container, lineOperator, new DateOnly(2026, 1, 1), validTo: null, "SEED", "Sample data");
            if (assignment.IsSuccess && assignment.Value!.Confirm(confirmedAt).IsSuccess)
                context.OperatingAssignments.Add(assignment.Value);
        }

        return await context.SaveChangesAsync();
    }


    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        string[] roles = ["Admin", "User"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role: {Role}", role);
            }
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, ILogger logger)
    {
        const string adminEmail = "admin@TechSpherex.dev";

        if (await userManager.FindByEmailAsync(adminEmail) is not null)
            return;

        var admin = new ApplicationUser
        {
            FirstName = "Admin",
            LastName = "User",
            Email = adminEmail,
            UserName = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, "Admin@123");

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            logger.LogInformation("Seeded admin user: {Email}", adminEmail);
        }
    }

    private static async Task SeedSampleTodosAsync(AppDbContext context, ILogger logger)
    {
        if (await context.Todos.AnyAsync())
            return;

        var todos = new List<TodoItem>
        {
            new() { Title = "Explore the Clean Architecture template", Description = "Read through the layers: Domain → Application → Infrastructure → Api" },
            new() { Title = "Run the API with Aspire", Description = "Use 'dotnet run' in the AppHost project to start PostgreSQL, Redis, and the API" },
            new() { Title = "Try the Scalar API docs", Description = "Navigate to /scalar/v1 to explore and test the endpoints" },
            new() { Title = "Add your first feature", Description = "Create a new entity, command/query handlers, and endpoint following the Todos pattern" },
            new() { Title = "Check the architecture tests", Description = "Run 'dotnet test' to verify dependency rules are enforced" }
        };

        context.Todos.AddRange(todos);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} sample todos", todos.Count);
    }
}
