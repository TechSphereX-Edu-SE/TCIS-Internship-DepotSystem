using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
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

        var container = repairCase.Container;
        return Result.Success(new RepairCaseDetail(
            repairCase.Id, container.Id, container.ContainerNumber, container.IsoCode, container.Size, container.Category,
            container.Owner?.Code, repairCase.Status, repairCase.RepairNeeded, repairCase.OpenedAt, repairCase.ClosedAt,
            repairCase.VisitId, repairCase.Visit?.LineOperator.Code, repairCase.Visit?.ConditionIn,
            openVisit is not null, openVisit?.Block.Code, openVisit?.Bay, openVisit?.Row, openVisit?.Tier,
            RepairCase.ActionsFrom(repairCase.Status), findings, photos, history));
    }
}
