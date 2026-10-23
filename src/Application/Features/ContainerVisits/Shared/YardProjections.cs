using System.Linq.Expressions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;

/*
    Shapes of a container in the yard, shared by the yard map, the yard search, the position lookup
    and the move result, so every screen shows the same repair marker (feedback 5).
    The repair part comes from the open case of the container (at most one, unique index):
    its id, its status and its latest finding. Passed cases are closed and give no marker.
*/
public static class YardProjections
{
    public static Expression<Func<ContainerVisit, YardContainerResponse>> ToYardContainer(IAppDbContext dbContext) =>
        v => new YardContainerResponse(
            v.Id, v.ContainerId, v.Container.ContainerNumber, v.Container.IsoCode,
            v.Container.Size, v.Container.Category, v.Container.Condition,
            v.LineOperator.Code, v.BlockId, v.Block.Code,
            v.Bay, v.Row, v.Tier, v.GateInAt,
            dbContext.RepairCases
                .Where(c => c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.Passed)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefault(),
            dbContext.RepairCases
                .Where(c => c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.Passed)
                .Select(c => (RepairCaseStatus?)c.Status)
                .FirstOrDefault(),
            dbContext.RepairFindings
                .Where(f => dbContext.RepairCases.Any(c =>
                    c.Id == f.RepairCaseId && c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.Passed))
                .OrderByDescending(f => f.ReportedAt)
                .Select(f => f.Description)
                .FirstOrDefault());

    public static Expression<Func<ContainerVisit, ContainerPositionResponse>> ToPosition(IAppDbContext dbContext) =>
        v => new ContainerPositionResponse(
            v.Id, v.ContainerId, v.Container.ContainerNumber, v.Container.Size,
            v.LineOperator.Code, v.BlockId, v.Block.Code, v.Block.IsVirtual,
            v.Bay, v.Row, v.Tier, v.GateInAt,
            v.Container.IsoCode, v.Container.Category, v.Container.Condition,
            dbContext.RepairCases
                .Where(c => c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.Passed)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefault(),
            dbContext.RepairCases
                .Where(c => c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.Passed)
                .Select(c => (RepairCaseStatus?)c.Status)
                .FirstOrDefault(),
            dbContext.RepairFindings
                .Where(f => dbContext.RepairCases.Any(c =>
                    c.Id == f.RepairCaseId && c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.Passed))
                .OrderByDescending(f => f.ReportedAt)
                .Select(f => f.Description)
                .FirstOrDefault());
}
