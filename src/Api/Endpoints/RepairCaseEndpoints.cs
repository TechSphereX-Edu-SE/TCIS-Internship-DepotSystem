using FluentValidation;
using TechSpherex.CleanArchitecture.Api.Extensions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;
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
            .WithSummary("Move the case one step (inspect, start, complete, accept or reject); approval goes through an estimate");

        group.MapPost("/{id:guid}/photos", UploadPhoto)
            .DisableAntiforgery()
            .WithName("UploadRepairPhoto")
            .WithSummary("Upload a JPG/PNG photo (max 5 MB) to an open case, optionally for one finding");

        group.MapPost("/{id:guid}/external-repair", RequestExternal)
            .WithName("RequestExternalRepair")
            .WithSummary("Ask to send the container to an outside repair facility (confirmed at gate-out)");

        group.MapPost("/{id:guid}/estimates", CreateEstimate)
            .WithName("CreateRepairEstimate")
            .WithSummary("Start a new estimate version (draft); a later version copies the items of the previous one");

        // Feedback 4: repair costs, one version at a time (draft -> submitted -> approved / rejected / changes requested)
        var estimates = app.MapGroup("/api/repair-estimates")
            .WithTags("Repair cases")
            .RequireAuthorization();

        estimates.MapPut("/{id:guid}", UpdateEstimate)
            .WithName("UpdateRepairEstimate")
            .WithSummary("Save a draft: currency, note, evidence and the whole item table");

        estimates.MapPost("/{id:guid}/submit", SubmitEstimate)
            .WithName("SubmitRepairEstimate")
            .WithSummary("Lock the draft and send it for approval");

        estimates.MapPost("/{id:guid}/decision", DecideEstimate)
            .WithName("DecideRepairEstimate")
            .WithSummary("Record the approving party's answer; the first approval moves the case to RepairApproved");

        estimates.MapPut("/{id:guid}/lines/{lineId:guid}/payer", SetLinePayer)
            .WithName("SetRepairEstimateLinePayer")
            .WithSummary("Set who pays one item and the allocation state (expected, agreed, disputed)");

        var external = app.MapGroup("/api/external-repairs")
            .WithTags("Repair cases")
            .RequireAuthorization();

        external.MapGet("/", GetExternal)
            .WithName("GetExternalRepairs")
            .WithSummary("Outside repair transfers (e.g. status=Requested: waiting to leave at gate-out)");

        external.MapPost("/{id:guid}/cancel", CancelExternal)
            .WithName("CancelExternalRepair")
            .WithSummary("Withdraw an outside repair transfer that has not left yet");

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
        bool? external,
        string? facility,
        CostPayer? payer,
        CostPayerStatus? payerStatus,
        IQueryHandler<GetRepairCasesQuery, Result<PagedResult<RepairCaseSummary>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetRepairCasesQuery(page ?? 1, pageSize ?? 20, search, status, open, operatorId, containerId,
            reportedFrom, reportedTo, external, facility, payer, payerStatus);
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

    private static async Task<IResult> RequestExternal(
        Guid id,
        ExternalRepairRequest request,
        IValidator<RequestExternalRepairCommand> validator,
        ICommandHandler<RequestExternalRepairCommand, Result<Guid>> handler,
        CancellationToken cancellationToken)
    {
        var command = new RequestExternalRepairCommand(id, request.Facility, request.Reason, request.Reference);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(new { id = result.Value }) : result.ToProblemDetails();
    }

    private static async Task<IResult> GetExternal(
        ExternalRepairStatus? status,
        string? search,
        int? page,
        int? pageSize,
        IQueryHandler<GetExternalRepairsQuery, Result<PagedResult<ExternalRepairResponse>>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetExternalRepairsQuery(status, search, page ?? 1, pageSize ?? 20), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> CancelExternal(
        Guid id,
        CancelExternalRepairRequest request,
        IValidator<CancelExternalRepairCommand> validator,
        ICommandHandler<CancelExternalRepairCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var command = new CancelExternalRepairCommand(id, request.Reason);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> CreateEstimate(
        Guid id,
        CreateEstimateRequest request,
        IValidator<CreateRepairEstimateCommand> validator,
        ICommandHandler<CreateRepairEstimateCommand, Result<Guid>> handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateRepairEstimateCommand(id, request.Currency, request.Note, request.Evidence);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(new { id = result.Value }) : result.ToProblemDetails();
    }

    private static async Task<IResult> UpdateEstimate(
        Guid id,
        UpdateEstimateRequest request,
        IValidator<UpdateRepairEstimateCommand> validator,
        ICommandHandler<UpdateRepairEstimateCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRepairEstimateCommand(id, request.Currency, request.Note, request.Evidence, request.Lines ?? []);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> SubmitEstimate(
        Guid id,
        ICommandHandler<SubmitRepairEstimateCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SubmitRepairEstimateCommand(id), cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> DecideEstimate(
        Guid id,
        EstimateDecisionRequest request,
        IValidator<DecideRepairEstimateCommand> validator,
        ICommandHandler<DecideRepairEstimateCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var command = new DecideRepairEstimateCommand(id, request.Decision, request.ApproverParty, request.ApproverName,
            request.ApprovalReference, request.Note);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    private static async Task<IResult> SetLinePayer(
        Guid id,
        Guid lineId,
        LinePayerRequest request,
        IValidator<SetEstimateLinePayerCommand> validator,
        ICommandHandler<SetEstimateLinePayerCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var command = new SetEstimateLinePayerCommand(id, lineId, request.Payer, request.PayerName, request.Status, request.Note);
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var result = await handler.HandleAsync(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
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

public sealed record ExternalRepairRequest(string Facility, string Reason, string Reference);

public sealed record CancelExternalRepairRequest(string Reason);

public sealed record CreateEstimateRequest(string Currency, string? Note, string? Evidence);

public sealed record UpdateEstimateRequest(string Currency, string? Note, string? Evidence, List<RepairEstimateLineInput>? Lines);

public sealed record EstimateDecisionRequest(
    RepairEstimateDecision Decision, string? ApproverParty, string? ApproverName, string? ApprovalReference, string? Note);

public sealed record LinePayerRequest(CostPayer Payer, string? PayerName, CostPayerStatus Status, string? Note);
