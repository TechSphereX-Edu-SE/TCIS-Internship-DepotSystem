using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Get;

// Findings and history oldest first (they read as a story); photos in upload order
public sealed class GetRepairCaseQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetRepairCaseQuery, Result<RepairCaseDetail>>
{
    public async Task<Result<RepairCaseDetail>> HandleAsync(GetRepairCaseQuery query, CancellationToken cancellationToken = default)
    {
        var repairCase = await dbContext.RepairCases
            .AsNoTracking()
            .Include(c => c.Container).ThenInclude(c => c.Owner)
            .Include(c => c.Visit).ThenInclude(v => v!.LineOperator)
            .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
        if (repairCase is null)
            return Result.Failure<RepairCaseDetail>(ApplyRepairActionCommandHandler.NotFound(query.Id));

        var findings = await dbContext.RepairFindings.AsNoTracking()
            .Where(f => f.RepairCaseId == repairCase.Id)
            .OrderBy(f => f.ReportedAt)
            .Select(f => new RepairFindingResponse(f.Id, f.Description, f.Source, f.VisitId, f.ReportedAt, f.ReportedBy))
            .ToListAsync(cancellationToken);

        var photos = await dbContext.RepairPhotos.AsNoTracking()
            .Where(p => p.RepairCaseId == repairCase.Id)
            .OrderBy(p => p.UploadedAt)
            .Select(p => new RepairPhotoResponse(p.Id, p.FindingId, p.OriginalFileName, p.ContentType, p.SizeBytes, p.UploadedAt, p.UploadedBy))
            .ToListAsync(cancellationToken);

        var history = await dbContext.RepairCaseEvents.AsNoTracking()
            .Where(e => e.RepairCaseId == repairCase.Id)
            .OrderBy(e => e.At)
            .Select(e => new RepairCaseEventResponse(e.Id, e.Kind, e.FromStatus, e.ToStatus, e.Note, e.At, e.By))
            .ToListAsync(cancellationToken);

        // Current position (the case never moves the container)
        var openVisit = await dbContext.ContainerVisits.AsNoTracking()
            .Include(v => v.Block)
            .FirstOrDefaultAsync(v => v.ContainerId == repairCase.ContainerId && v.GateOutAt == null, cancellationToken);

        var transfers = await dbContext.ExternalRepairTransfers.AsNoTracking()
            .Where(t => t.RepairCaseId == repairCase.Id)
            .OrderByDescending(t => t.RequestedAt)
            .Select(ExternalRepairProjection.ToResponse(dbContext.ContainerVisits))
            .ToListAsync(cancellationToken);
        var canRequestExternal = repairCase.IsOpen && openVisit is not null &&
                                 ExternalRepairTransfer.AllowedCaseStatuses.Contains(repairCase.Status) &&
                                 !transfers.Any(t => t.Status is ExternalRepairStatus.Requested or ExternalRepairStatus.GatedOut);

        // Steps that pass the case are not offered while a transfer is active (see ApplyRepairActionCommandHandler)
        var hasActiveTransfer = transfers.Any(t => t.Status is ExternalRepairStatus.Requested or ExternalRepairStatus.GatedOut);
        var actions = RepairCase.ActionsFrom(repairCase.Status)
            .Where(a => !hasActiveTransfer || RepairCase.Transitions[a].To != RepairCaseStatus.Passed)
            .ToList();

        var container = repairCase.Container;
        return Result.Success(new RepairCaseDetail(
            repairCase.Id, container.Id, container.ContainerNumber, container.IsoCode, container.Size, container.Category,
            container.Owner?.Code, repairCase.Status, repairCase.RepairNeeded, repairCase.OpenedAt, repairCase.ClosedAt,
            repairCase.VisitId, repairCase.Visit?.LineOperator.Code, repairCase.Visit?.ConditionIn,
            openVisit is not null, openVisit?.Block.Code, openVisit?.Bay, openVisit?.Row, openVisit?.Tier,
            actions, findings, photos, history, canRequestExternal, transfers));
    }
}
