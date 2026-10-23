using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Storage;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

// Signed-in user for handlers that record who did something
internal sealed class FakeCurrentUser(string email = "staff@depot.test") : ICurrentUser
{
    public string? UserId => "user-1";
    public string? Email => email;
    public bool IsAuthenticated => true;
}

// Photo storage in memory, so tests do not write to disk
internal sealed class FakePhotoStorage : IRepairPhotoStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        var name = $"{Guid.NewGuid():N}{extension}";
        Files[name] = copy.ToArray();
        return name;
    }

    public Stream? OpenRead(string storedFileName) =>
        Files.TryGetValue(storedFileName, out var bytes) ? new MemoryStream(bytes) : null;
}

/*
    Feedback 4: a repair is approved by approving its estimate. Tests that only need the case to reach
    RepairApproved use this: one item (2 000 000 VND, paid by the line operator), submitted, approved by HMM.
*/
internal static class RepairEstimateSteps
{
    public static async Task<Guid> ApproveAsync(AppDbContext dbContext, Guid caseId, TimeProvider clock)
    {
        var user = new FakeCurrentUser();
        var cancellationToken = TestContext.Current.CancellationToken;

        var created = await new CreateRepairEstimateCommandHandler(dbContext, clock, user)
            .HandleAsync(new CreateRepairEstimateCommand(caseId, "VND", null, "EIR-001"), cancellationToken);
        var estimateId = created.Value;
        await new UpdateRepairEstimateCommandHandler(dbContext).HandleAsync(
            new UpdateRepairEstimateCommand(estimateId, "VND", null, "EIR-001",
                [new RepairEstimateLineInput(null, "Móp cửa", "Nắn, sơn lại", 2_000_000m, CostPayer.LineOperator, null)]),
            cancellationToken);
        await new SubmitRepairEstimateCommandHandler(dbContext, clock, user)
            .HandleAsync(new SubmitRepairEstimateCommand(estimateId), cancellationToken);
        var decided = await new DecideRepairEstimateCommandHandler(dbContext, clock, user).HandleAsync(
            new DecideRepairEstimateCommand(estimateId, RepairEstimateDecision.Approve, "HMM", "Mr. Lee", "Email 21/10", null),
            cancellationToken);
        if (decided.IsFailure)
            throw new InvalidOperationException(decided.Error!.Code);
        return estimateId;
    }
}
