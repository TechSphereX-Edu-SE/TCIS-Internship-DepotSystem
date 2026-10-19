using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

public enum ContainerStatus
{
    InYard = 0,
    Loaded = 1,
    Departed = 2,
    Damaged = 3
}

public sealed class Container : AuditableEntity
{
    public string ContainerNumber { get; private set; } = default!;
    public string ContainerType { get; private set; } = default!;
    public ContainerStatus Status { get; private set; } = ContainerStatus.InYard;
    public DateTimeOffset? DepartedAt { get; private set; }

    private Container() { } // EF Core dùng khi đọc từ DB

    public Container(string containerNumber, string containerType)
    {
        ContainerNumber = containerNumber.Trim().ToUpperInvariant();
        ContainerType = containerType.Trim().ToUpperInvariant();
    }

    public void MarkAsLoaded() => Status = ContainerStatus.Loaded;

    public void MarkAsDamaged() => Status = ContainerStatus.Damaged;

    public void MarkAsDeparted()
    {
        if (Status == ContainerStatus.Departed) return;
        Status = ContainerStatus.Departed;
        DepartedAt = DateTimeOffset.UtcNow;
    }

    public void ReturnToYard()
    {
        Status = ContainerStatus.InYard;
        DepartedAt = null;
    }
}
