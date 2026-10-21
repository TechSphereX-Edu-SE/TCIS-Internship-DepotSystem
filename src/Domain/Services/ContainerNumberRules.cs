using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Services;

/*
    Container number rules per Appendix I of the "Depot Management System" spec:
    3-letter owner code + 1-letter type code + 6-digit serial + 1 check character.
    - Type code: the 4th letter identifies the container type (see TypeCodes).
    - Serial: issued from 000001, so 000000 is not valid.
    - Check character uses Modulo 11: remainder 0 -> '0'; 1-9 -> the remainder; 10 -> 'X'.
    Valid examples: CSQU3054383, CMAU1234564, CMAU100000X, HMMR2468107.
*/
public static class ContainerNumberRules
{
    public const int Length = 11;

    /*
        Type code letter per container type.
        U, R, S, F come from Appendix I. B, V, P are project choices for the
        three types the spec does not list (Specialized uses P because S is Open Top).
    */
    public static readonly IReadOnlyDictionary<ContainerCategory, char> TypeCodes =
        new Dictionary<ContainerCategory, char>
        {
            [ContainerCategory.Dry] = 'U',
            [ContainerCategory.Reefer] = 'R',
            [ContainerCategory.OpenTop] = 'S',
            [ContainerCategory.FlatRack] = 'F',
            [ContainerCategory.Bunker] = 'B',
            [ContainerCategory.Ventilated] = 'V',
            [ContainerCategory.Specialized] = 'P'
        };

    public static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool HasValidFormat(string value) =>
        value.Length == Length
        && value[..4].All(char.IsAsciiLetterUpper)
        && value[4..10].All(char.IsAsciiDigit)
        && value[4..10] != "000000"
        && (char.IsAsciiDigit(value[10]) || value[10] == 'X');

    /*
        Calculates the check character from the first 10 characters.
        Letter values and position weights follow ISO 6346 (the spec does not define them):
        A=10, B=12, C=13... (skipping multiples of 11); character i is multiplied by 2^i.
        The sum modulo 11 gives the remainder; remainder 10 maps to 'X' (Appendix I).
    */
    public static char CalculateCheckDigit(string firstTenCharacters)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var c = firstTenCharacters[i];
            var value = char.IsAsciiLetter(c) ? LetterValue(c) : c - '0';
            sum += value * (1 << i);
        }

        var remainder = sum % 11;
        return remainder == 10 ? 'X' : (char)('0' + remainder);
    }

    public static bool IsValid(string? value)
    {
        var number = Normalize(value);
        if (!HasValidFormat(number))
            return false;

        return CalculateCheckDigit(number) == number[10];
    }

    // True when the 4th letter of the number matches the container type
    public static bool MatchesCategory(string? value, ContainerCategory category)
    {
        var number = Normalize(value);
        return number.Length >= 4
            && TypeCodes.TryGetValue(category, out var code)
            && number[3] == code;
    }

    private static int LetterValue(char letter)
    {
        // Start at A=10 and count up, skipping multiples of 11
        var value = 10;
        for (var c = 'A'; c < char.ToUpperInvariant(letter); c++)
        {
            value++;
            if (value % 11 == 0) value++;
        }

        return value;
    }
}
