using System.Security.Cryptography;
using System.Text;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence.Demo;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

/*
    Feedback 10: tool to create / complete / reset the demo database.
    - Only mapped in Development with Demo:Enabled = true (the AppHost sets it), so a deployed API has no
      such routes at all.
    - Caller: the Aspire dashboard commands (header X-Demo-Key = Demo:Key, a new key on every AppHost run)
      or an Admin token (Scalar). Anyone else gets 403.
    - No date parameter: the seeder places events itself from its fixed anchor; business endpoints still
      take every time from the server clock.
    - Reset deletes the depot data, so it also needs ?confirm=reset-demo, and never runs at startup.
*/
public static class DemoEndpoints
{
    public const string KeyHeader = "X-Demo-Key";
    public const string ResetConfirmation = "reset-demo";

    public static void MapDemoEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Demo:Enabled"))
            return;

        var group = app.MapGroup("/api/demo")
            .WithTags("Demo")
            .AddEndpointFilter(async (context, next) =>
                IsAllowed(context.HttpContext, app.Configuration["Demo:Key"])
                    ? await next(context)
                    : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Demo.Forbidden",
                        detail: $"Demo commands need the {KeyHeader} header of the AppHost or an Admin token."));

        group.MapPost("/seed", Seed)
            .WithName("SeedDemoData")
            .WithSummary("Create the demo data, or add what is missing (never changes existing rows or dates)");

        group.MapPost("/reset", Reset)
            .WithName("ResetDemoData")
            .WithSummary("Delete all depot data and create the demo again with a new anchor (needs confirm=reset-demo)");
    }

    private static async Task<IResult> Seed(DemoDataSeeder seeder, CancellationToken cancellationToken) =>
        TypedResults.Ok(await seeder.SeedAsync(cancellationToken));

    private static async Task<IResult> Reset(string? confirm, DemoDataSeeder seeder, CancellationToken cancellationToken)
    {
        if (confirm != ResetConfirmation)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Demo.ResetNotConfirmed",
                detail: $"Add ?confirm={ResetConfirmation} to delete the depot data and create the demo again.");

        return TypedResults.Ok(await seeder.ResetAsync(cancellationToken));
    }

    // Constant-time comparison of the key, so its value cannot be guessed from response times
    private static bool IsAllowed(HttpContext httpContext, string? demoKey)
    {
        if (httpContext.User.IsInRole("Admin"))
            return true;

        var sent = httpContext.Request.Headers[KeyHeader].ToString();
        return !string.IsNullOrEmpty(demoKey) && sent.Length > 0 &&
               CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(demoKey));
    }
}
