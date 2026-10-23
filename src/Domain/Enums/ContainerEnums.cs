namespace TechSpherex.CleanArchitecture.Domain.Enums;

// Container size. Odd bays hold 20ft, even bays hold 40ft
public enum ContainerSize
{
    Ft20 = 20,
    Ft40 = 40
}

// Container type per Appendix II of the depot spec
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

// Physical condition of the container
public enum ContainerCondition
{
    Normal = 0,
    Damaged = 1
}

/*
    Legacy grade (A/B/C) recorded at gate-in before 23/10/2026.
    Feedback: the mentor confirmed it was only a placeholder label, not a quality criterion,
    so it is no longer recorded. The enum stays only to read the grades of older visits.
*/
public enum ContainerGrade
{
    A = 0,
    B = 1,
    C = 2
}
