using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    Partner company (Q2, 23/10/2026): one catalog for container owners and line operators,
    e.g. MSC, CMA, HMM, or a leasing company. The class and table keep their original name
    (LineOperator) because visits, delivery orders and statistics already point to it.
    A company is recorded once and marked with its roles (feedback 2: "một công ty có thể có nhiều vai trò").
*/
public sealed class LineOperator : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    // Optional tax code (MST): shown in the owner dropdown when known
    public string? TaxCode { get; private set; }

    // Can own containers (Container.OwnerId)
    public bool IsOwner { get; private set; }

    // Can operate containers: operating assignments, visits and delivery orders
    public bool IsOperator { get; private set; }

    private LineOperator() { }

    // Defaults keep the old meaning (a line operator) for code that only gives code and name
    public LineOperator(string code, string name, string? taxCode = null, bool isOwner = false, bool isOperator = true)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        TaxCode = Normalize(taxCode);
        IsOwner = isOwner;
        IsOperator = isOperator;
    }

    public void Rename(string name) => Name = name.Trim();

    /*
        Name, tax code and roles; the code never changes (it identifies the company in orders and reports).
        At least one role, otherwise the partner could not be used anywhere. Whether a role can be removed
        (no container owned, no operation running) is checked by the handler, which needs queries.
    */
    public Result UpdateProfile(string name, string? taxCode, bool isOwner, bool isOperator)
    {
        if (!isOwner && !isOperator)
            return Result.Failure(Error.Validation("Partner.NoRole", "A partner must be an owner, an operator, or both."));

        Name = name.Trim();
        TaxCode = Normalize(taxCode);
        IsOwner = isOwner;
        IsOperator = isOperator;
        return Result.Success();
    }

    // Grants a role without touching the others (used by the sample data seeder)
    public void AddOwnerRole() => IsOwner = true;

    private static string? Normalize(string? taxCode) =>
        string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim();
}
