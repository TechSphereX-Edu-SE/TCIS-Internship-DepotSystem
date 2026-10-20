using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

// Customer receiving containers under a delivery order
public sealed class Customer : AuditableEntity
{
    // Tax code (MST)
    public string TaxCode { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    private Customer() { }

    public Customer(string taxCode, string name)
    {
        TaxCode = taxCode.Trim();
        Name = name.Trim();
    }

    public void Rename(string name) => Name = name.Trim();
}
