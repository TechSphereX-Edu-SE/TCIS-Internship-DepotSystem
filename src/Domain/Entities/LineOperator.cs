using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>Hãng khai thác container (line operator), ví dụ MSC, CMA, HMM.</summary>
public sealed class LineOperator : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    private LineOperator() { }

    public LineOperator(string code, string name)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
    }

    public void Rename(string name) => Name = name.Trim();
}
