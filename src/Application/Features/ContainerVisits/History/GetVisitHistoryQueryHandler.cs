using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;

/*
    Reads the visit and everything recorded against it, each part with its own small query
    (a visit has few moves, cases and documents, so this stays cheap and easy to follow).
*/
public sealed class GetVisitHistoryQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetVisitHistoryQuery, Result<VisitHistoryDetail>>
{
    public async Task<Result<VisitHistoryDetail>> HandleAsync(
        GetVisitHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var visit = await dbContext.ContainerVisits
            .AsNoTracking()
            .Include(v => v.Block)
            .Include(v => v.GateInBlock)
            .Include(v => v.DeliveryOrder!).ThenInclude(o => o.Customer)
            .FirstOrDefaultAsync(v => v.Id == query.VisitId, cancellationToken);
        if (visit is null)
            return Result.Failure<VisitHistoryDetail>(Error.NotFound("ContainerVisit.NotFound",
                $"Visit with ID '{query.VisitId}' was not found."));

        var row = (await dbContext.ContainerVisits
            .AsNoTracking()
            .Where(v => v.Id == visit.Id)
            .ToHistoryRowsAsync(dbContext, timeProvider.GetUtcNow(), cancellationToken)).Single();

        var transfers = await dbContext.ExternalRepairTransfers
            .AsNoTracking()
            .Where(t => t.GateOutVisitId == visit.Id || t.ReturnVisitId == visit.Id)
            .ToListAsync(cancellationToken);
        var leftFor = transfers.FirstOrDefault(t => t.GateOutVisitId == visit.Id);

        var gateIn = new VisitGateIn(
            visit.GateInAt, visit.GateInVehicleNo, visit.ConditionIn, visit.ConditionNoteIn, visit.Grade,
            visit.GateInBlock is { } gateInBlock
                ? new VisitPosition(gateInBlock.Id, gateInBlock.Code, gateInBlock.IsVirtual,
                    visit.GateInBay, visit.GateInRow, visit.GateInTier)
                : null);

        var gateOut = visit.GateOutAt is { } gateOutAt
            ? new VisitGateOut(gateOutAt, visit.GateOutVehicleNo ?? string.Empty, visit.ExitPurpose,
                visit.DeliveryOrder?.OrderNumber, visit.DeliveryOrder?.Customer.Name, leftFor?.Facility)
            : null;

        // Only an open visit has a position now; a closed one shows where it was through its moves
        var current = visit.IsInYard
            ? new VisitPosition(visit.BlockId, visit.Block.Code, visit.Block.IsVirtual, visit.Bay, visit.Row, visit.Tier)
            : null;

        var cases = await LoadRepairCasesAsync(visit.Id, cancellationToken);

        return Result.Success(new VisitHistoryDetail(
            row, gateIn, gateOut, current,
            await LoadMovesAsync(visit.Id, cancellationToken),
            cases,
            await LoadDocumentsAsync(visit, transfers, cases.Select(c => c.Id).ToList(), cancellationToken)));
    }

    // Moves in time order, with block codes (archived blocks included: history keeps their codes)
    private async Task<List<VisitMove>> LoadMovesAsync(Guid visitId, CancellationToken cancellationToken)
    {
        var moves = await dbContext.ContainerMoves
            .AsNoTracking()
            .Where(m => m.VisitId == visitId)
            .OrderBy(m => m.MovedAt)
            .ToListAsync(cancellationToken);
        if (moves.Count == 0)
            return [];

        var blockIds = moves.SelectMany(m => new[] { m.FromBlockId, m.ToBlockId }).Distinct().ToList();
        var blocks = await dbContext.Blocks
            .AsNoTracking()
            .Where(b => blockIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => (b.Code, b.IsVirtual), cancellationToken);

        VisitPosition At(Guid blockId, int? bay, int? row, int? tier)
        {
            var (code, isVirtual) = blocks.GetValueOrDefault(blockId, (string.Empty, false));
            return new VisitPosition(blockId, code, isVirtual, bay, row, tier);
        }

        return moves
            .Select(m => new VisitMove(
                m.MovedAt,
                At(m.FromBlockId, m.FromBay, m.FromRow, m.FromTier),
                At(m.ToBlockId, m.ToBay, m.ToRow, m.ToTier),
                m.MovedBy))
            .ToList();
    }

    private Task<List<VisitRepairCase>> LoadRepairCasesAsync(Guid visitId, CancellationToken cancellationToken) =>
        dbContext.RepairCases
            .AsNoTracking()
            .Where(c => c.VisitId == visitId)
            .OrderBy(c => c.OpenedAt)
            .Select(c => new VisitRepairCase(
                c.Id, c.Status, c.RepairNeeded, c.OpenedAt, c.ClosedAt,
                dbContext.RepairFindings
                    .Where(f => f.RepairCaseId == c.Id)
                    .OrderBy(f => f.ReportedAt)
                    .Select(f => f.Description)
                    .FirstOrDefault(),
                dbContext.RepairPhotos.Count(p => p.RepairCaseId == c.Id)))
            .ToListAsync(cancellationToken);

    // Documents in time order; an entry without a time (an assignment never confirmed) goes first
    private async Task<List<VisitDocument>> LoadDocumentsAsync(
        ContainerVisit visit, List<ExternalRepairTransfer> transfers, List<Guid> caseIds,
        CancellationToken cancellationToken)
    {
        var documents = new List<VisitDocument>();

        if (visit.OperatingAssignmentId is { } assignmentId)
        {
            var assignment = await dbContext.OperatingAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);
            if (assignment is not null)
                documents.Add(new VisitDocument(VisitDocumentKind.OperatingAssignment,
                    assignment.Reference, assignment.ConfirmedAt, assignment.Id));
        }

        if (visit.DeliveryOrder is { } order)
            documents.Add(new VisitDocument(VisitDocumentKind.DeliveryOrder, order.OrderNumber, visit.GateOutAt, order.Id));

        foreach (var transfer in transfers)
        {
            if (transfer.ReturnVisitId == visit.Id)
                documents.Add(new VisitDocument(VisitDocumentKind.ExternalRepairReturn,
                    $"{transfer.Reference} · {transfer.Facility}", transfer.ReturnedAt, transfer.RepairCaseId));
            if (transfer.GateOutVisitId == visit.Id)
                documents.Add(new VisitDocument(VisitDocumentKind.ExternalRepairOut,
                    $"{transfer.Reference} · {transfer.Facility}", transfer.GatedOutAt, transfer.RepairCaseId));
        }

        if (caseIds.Count > 0)
        {
            var photos = await dbContext.RepairPhotos
                .AsNoTracking()
                .Where(p => caseIds.Contains(p.RepairCaseId))
                .Select(p => new VisitDocument(VisitDocumentKind.RepairPhoto, p.OriginalFileName, p.UploadedAt, p.RepairCaseId))
                .ToListAsync(cancellationToken);
            documents.AddRange(photos);
        }

        return documents.OrderBy(d => d.At ?? DateTimeOffset.MinValue).ToList();
    }
}
