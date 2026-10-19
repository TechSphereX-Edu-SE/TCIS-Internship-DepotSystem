using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

public sealed class Container : AuditableEntity
{
    public string ContainerNumber { get; private set; } = string.Empty;

    public string ContainerType { get; private set; } = string.Empty;

    public string Status { get; private set; } = "InYard";

    private Container() { }

    public Container(string containerNumber, string containerType)
    {
        ContainerNumber = containerNumber;
        ContainerType = containerType;
    }
}
