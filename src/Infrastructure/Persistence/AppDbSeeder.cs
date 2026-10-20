
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

    // Depot sample data: line operators, customers, yard blocks, containers
    private static async Task SeedDepotMasterDataAsync(AppDbContext context, ILogger logger)
    {
        if (!await context.LineOperators.AnyAsync())
        {
            context.LineOperators.AddRange(
                new LineOperator("MSC", "Mediterranean Shipping Company"),
                new LineOperator("CMA", "CMA CGM"),
                new LineOperator("HMM", "Hyundai Merchant Marine"));
        }

        if (!await context.Customers.AnyAsync())
        {
            context.Customers.AddRange(
                new Customer("0301234567", "Công ty TNHH Xuất nhập khẩu Minh An"),
                new Customer("0309876543", "Công ty CP Logistics Phương Nam"));
        }

        if (!await context.Blocks.AnyAsync())
        {
            context.Blocks.AddRange(
                Block.CreatePhysical("A", maxBays: 20, maxRows: 6, maxTiers: 5, "Khu container khô"),
                Block.CreatePhysical("B", maxBays: 10, maxRows: 4, maxTiers: 4, "Khu container lạnh"),
                Block.CreateVirtual("WAIT", "Khu chờ sửa chữa / giám định"));
        }

        if (!await context.Containers.AnyAsync())
        {
            // Container numbers with valid check digits
            context.Containers.AddRange(
                new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, new DateOnly(2019, 5, 1), "MSC"),
                new Container("MSCU5551234", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2250, new DateOnly(2020, 3, 1), "MSC"),
                new Container("CMAU7654327", "42G1", ContainerSize.Ft40, ContainerCategory.Dry, 30480, 3750, new DateOnly(2018, 8, 1), "CMA CGM"),
                new Container("CMAU9001110", "45G1", ContainerSize.Ft40, ContainerCategory.Dry, 30480, 3900, new DateOnly(2021, 1, 1), "CMA CGM"),
                new Container("HMMU2468109", "45R1", ContainerSize.Ft40, ContainerCategory.Reefer, 34000, 4800, new DateOnly(2022, 6, 1), "HMM"),
                new Container("HMMU3007776", "22R1", ContainerSize.Ft20, ContainerCategory.Reefer, 30480, 3000, new DateOnly(2022, 9, 1), "HMM"));
        }

        var saved = await context.SaveChangesAsync();
        if (saved > 0)
            logger.LogInformation("Seeded {Count} depot master data rows", saved);
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
