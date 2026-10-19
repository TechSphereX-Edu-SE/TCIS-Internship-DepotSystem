using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>Khách hàng nhận container theo lệnh giao.</summary>
public sealed class Customer : AuditableEntity
{
    /// <summary>Mã số thuế (MST).</summary>
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
