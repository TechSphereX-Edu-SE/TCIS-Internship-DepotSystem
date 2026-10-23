using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Get;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Photos;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Report;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Api.Endpoints;

/*
    Feedback 4: inspection & repair cases (Giám định & sửa chữa).
    Photos are uploaded as multipart/form-data. The API uses bearer tokens, not cookies,
    so the antiforgery check of form endpoints does not apply (DisableAntiforgery).
*/
public static class RepairCaseEndpoints
{
    public static void MapRepairCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/repair-cases")
            .WithTags("Repair cases")
            .RequireAuthorization();

        group.MapGet("/", GetAll)
            .WithName("GetRepairCases")
            .WithSummary("List repair cases (number, status, line operator, reported dates; newest first)");

        group.MapGet("/{id:guid}", GetById)
            .WithName("GetRepairCaseById")
            .WithSummary("Get a repair case with its findings, photos, history and allowed actions");

        group.MapPost("/report", Report)
            .AddEndpointFilter<ValidationFilter<ReportDamageCommand>>()
            .WithName("ReportDamage")
            .WithSummary("Report a damage: added to the open case of the container, or opens a new case");

        group.MapPost("/{id:guid}/actions", Act)
            .WithName("ApplyRepairAction")
            .WithSummary("Move the case one step (inspect, approve, start, complete, accept or reject)");

        group.MapPost("/{id:guid}/photos", UploadPhoto)
            .DisableAntiforgery()
            .WithName("UploadRepairPhoto")
            .WithSummary("Upload a JPG/PNG photo (max 5 MB) to an open case, optionally for one finding");

        app.MapGet("/api/repair-photos/{id:guid}", GetPhoto)
            .WithTags("Repair cases")
            .RequireAuthorization()
            .WithName("GetRepairPhoto")
            .WithSummary("Download a repair photo");
    }

    private static async Task<IResult> GetAll(
        int? page,
        int? pageSize,
        string? search,
        RepairCaseStatus? status,
        bool? open,
        Guid? operatorId,
        Guid? containerId,
        DateOnly? reportedFrom,
        DateOnly? reportedTo,
        IQueryHandler<GetRepairCasesQuery, Result<PagedResult<RepairCaseSummary>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetRepairCasesQuery(page ?? 1, pageSize ?? 20, search, status, open, operatorId, containerId,
            reportedFrom, reportedTo);
        var result = await handler.HandleAsync(query, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueryHandler<GetRepairCaseQuery, Result<RepairCaseDetail>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetRepairCaseQuery(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Report(
        ReportDamageCommand command,
        ICommandHandler<ReportDamageCommand, Result<RecordedDamage>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> Act(
        Guid id,
        RepairActionRequest request,
        IValidator<ApplyRepairActionCommand> validator,
        ICommandHandler<ApplyRepairActionCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        // Validate the command (id from the route + body), not the raw request
        var command = new ApplyRepairActionCommand(id, request.Action, request.Note);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> UploadPhoto(
        Guid id,
        IFormFile file,
        [Microsoft.AspNetCore.Mvc.FromForm] Guid? findingId,
        IValidator<UploadRepairPhotoCommand> validator,
        ICommandHandler<UploadRepairPhotoCommand, Result<RepairPhotoResponse>> handler,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var command = new UploadRepairPhotoCommand(id, findingId, file.FileName, file.ContentType, file.Length, content);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetPhoto(
        Guid id,
        IQueryHandler<GetRepairPhotoQuery, Result<RepairPhotoFile>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetRepairPhotoQuery(id), cancellationToken);
        return result.IsSuccess
            ? TypedResults.File(result.Value!.Content, result.Value.ContentType, result.Value.FileName)
            : result.ToProblemDetails();
    }
}

public sealed record RepairActionRequest(RepairAction Action, string? Note);
