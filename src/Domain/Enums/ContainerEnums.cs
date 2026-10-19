namespace TechSpherex.CleanArchitecture.Domain.Enums;

/// <summary>Kích thước container. Bay lẻ chứa 20ft, bay chẵn chứa 40ft.</summary>
public enum ContainerSize
{
    Ft20 = 20,
    Ft40 = 40
}

/// <summary>Loại container theo Phụ lục II tài liệu Depot.</summary>
public enum ContainerCategory
{
    Dry = 0,
    Reefer = 1,
    OpenTop = 2,
    FlatRack = 3,
    Bunker = 4,
    Ventilated = 5,
    Specialized = 6
}

/// <summary>Tình trạng vỏ container.</summary>
public enum ContainerCondition
{
    Normal = 0,
    Damaged = 1
}

/// <summary>Phân loại container khi vào bãi.</summary>
public enum ContainerGrade
{
    A = 0,
    B = 1,
    C = 2
}
