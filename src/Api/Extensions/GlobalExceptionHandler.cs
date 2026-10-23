
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechSpherex.CleanArchitecture.Domain.Common.Rules;
namespace TechSpherex.CleanArchitecture.Api.Extensions;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Two requests changed the same data at the same time: not a server bug, the user just retries
        if (IsConcurrentUpdate(exception))
        {
            logger.LogWarning(exception, "Concurrent update rejected: {Message}", exception.Message);
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = "The data was changed by another operation at the same time. Reload and try again.",
                Extensions = { ["errorCode"] = "Data.ConcurrentUpdate" }
            }, cancellationToken);
            return true;
        }

        logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var problemDetails = exception switch
        {
            BusinessRuleException brEx => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Business rule violated",
                Detail = brEx.Message,
                Extensions = { ["ruleCode"] = brEx.RuleCode }
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
                Detail = httpContext.RequestServices
                    .GetRequiredService<IHostEnvironment>()
                    .IsDevelopment()
                        ? exception.Message
                        : "Please try again later or contact support."
            }
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    /*
        The handlers check the rules first, so these database errors only happen when another request
        committed in between:
          - DbUpdateConcurrencyException: the row version (xmin) changed since it was read
          - 23505 unique violation: e.g. two gate-ins into the same slot, or the same container twice
          - 23503 foreign key violation: e.g. a gate-in into a block deleted a moment earlier
    */
    private static bool IsConcurrentUpdate(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation } };
}

