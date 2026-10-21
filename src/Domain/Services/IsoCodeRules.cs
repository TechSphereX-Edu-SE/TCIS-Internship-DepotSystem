using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Services;

/*
    ISO 6346 size-type code (the "ISO Code" field of spec section 2). The spec does not define it,
    so the full ISO 6346 rules apply. 4 characters:
      1. Length:  2 = 20ft, 4 = 40ft (the two sizes the project supports)
      2. Height (and width):
           0 = 8ft, 2 = 8ft6, 4 = 9ft, 5 = 9ft6 (high cube), 6 = over 9ft6, 8 = 4ft3, 9 = 4ft or less
           C, D, E, F = 8ft6, 9ft, 9ft6, over 9ft6 with width over 2438 mm (pallet-wide)
      3. Type group letter (G, V, R, H, U, P, T, B, S)
      4. Type detail: a digit defined for that group, or its alternate letter
         (A B D G J M V W X Y for 0-9) used when stacking/racking strength is reduced
    Examples: 22G1 = 20ft dry, 42G1 = 40ft dry, 45G1 = 40ft high cube dry, 45R1 = 40ft high cube reefer.

    ISO groups per container type (Appendix II). ISO classifies by structure, the spec by trade name:
      Dry -> G (general), Ventilated -> V, Reefer -> R (integral reefer, as the spec describes a built-in
      cooling system), Open Top -> U, Flat Rack -> P (flat / platform), Specialized -> S (ISO "specialized
      carriers"). ISO has no "Bunker" group and three groups with no Appendix II name; project mapping:
      Bunker -> T (tank: dangerous goods, liquids, chemicals as the spec describes),
      Specialized also takes H (insulated / removable cooling) and B (bulk).
*/
public static class IsoCodeRules
{
    public const int Length = 4;

    public static readonly IReadOnlyDictionary<ContainerSize, char> LengthCodes =
        new Dictionary<ContainerSize, char>
        {
            [ContainerSize.Ft20] = '2',
            [ContainerSize.Ft40] = '4'
        };

    private const string HeightCodes = "0245689CDEF";

    // Detail digits defined by ISO 6346 for each type group (e.g. V has only V0, V2, V4)
    private static readonly IReadOnlyDictionary<char, string> DefinedDetails =
        new Dictionary<char, string>
        {
            ['G'] = "0123",
            ['V'] = "024",
            ['R'] = "0123",
            ['H'] = "01256",
            ['U'] = "012345",
            ['P'] = "012345",
            ['T'] = "0123456789",
            ['B'] = "013456",
            ['S'] = "012"
        };

    // Alternate detail letters: index = the digit it replaces (A = 0, B = 1, D = 2, ...)
    private const string AlternateDetails = "ABDGJMVWXY";

    public static readonly IReadOnlyDictionary<ContainerCategory, string> TypeGroups =
        new Dictionary<ContainerCategory, string>
        {
            [ContainerCategory.Dry] = "G",
            [ContainerCategory.Reefer] = "R",
            [ContainerCategory.OpenTop] = "U",
            [ContainerCategory.FlatRack] = "P",
            [ContainerCategory.Bunker] = "T",
            [ContainerCategory.Ventilated] = "V",
            [ContainerCategory.Specialized] = "SHB"
        };

    public static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    /*
        Returns null when the code is valid for the size and type, otherwise the reason.
        One method gives both the check and the message, so Create and Update validators
        share the same rule without repeating it.
    */
    public static string? Validate(string? isoCode, ContainerSize size, ContainerCategory category)
    {
        var code = Normalize(isoCode);

        if (code.Length != Length || !code.All(char.IsAsciiLetterOrDigit) || !char.IsAsciiLetter(code[2]))
            return "ISO code must be 4 characters: length, height, type group letter, type detail (e.g. 22G1, 45R1).";

        // Unknown enum values are reported by the IsInEnum rules, not here
        if (!LengthCodes.TryGetValue(size, out var lengthCode) || !TypeGroups.TryGetValue(category, out var groups))
            return null;

        if (code[0] != lengthCode)
            return $"ISO code of a {(int)size}ft container must start with '{lengthCode}'.";

        if (!HeightCodes.Contains(code[1]))
            return "ISO height code (2nd character) must be one of 0, 2, 4, 5, 6, 8, 9, C, D, E, F.";

        var group = code[2];
        if (!groups.Contains(group))
            return $"ISO type group for {category} must be {string.Join(" or ", groups.ToCharArray())} (got {group}).";

        if (!IsDefinedDetail(group, code[3]))
            return $"ISO type code {code[2..]} is not defined in ISO 6346. " +
                   $"Group {group} allows: {string.Join(", ", DefinedDetails[group].Select(d => $"{group}{d}"))}.";

        return null;
    }

    public static bool IsValid(string? isoCode, ContainerSize size, ContainerCategory category) =>
        Validate(isoCode, size, category) is null;

    // A detail is a defined digit, or the alternate letter of a defined digit
    private static bool IsDefinedDetail(char group, char detail)
    {
        if (char.IsAsciiDigit(detail))
            return DefinedDetails[group].Contains(detail);

        var index = AlternateDetails.IndexOf(detail);
        return index >= 0 && DefinedDetails[group].Contains((char)('0' + index));
    }
}
