namespace TechSpherex.CleanArchitecture.Domain.Services;

/*
    Container number (identification number) rules.
    Format follows ISO 6346: 3-letter owner code + equipment category identifier + 6-digit serial
    + 1 check character.
    - Equipment category: 'U' = freight container (every container type: dry, reefer, flat rack...).
      ISO also has 'J' (detachable equipment) and 'Z' (trailer/chassis), which are not containers,
      so the depot only accepts 'U'. The container type is carried by the ISO code (IsoCodeRules).
    - Serial: issued from 000001, so 000000 is not valid (Appendix I; ISO does not conflict).
    - Check character: Modulo 11 as in ISO 6346, but remainder 10 -> 'X' as Appendix I says
      (ISO would use '0'). Project decision to keep the spec's check digit.
    Valid examples: CSQU3054383, CMAU1234564, CMAU100000X, HMMU2468109.
*/
public static class ContainerNumberRules
{
    public const int Length = 11;

    // ISO 6346 equipment category identifier for freight containers
    public const char FreightContainerCategory = 'U';

    public static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool HasValidFormat(string value) =>
        value.Length == Length
        && value[..3].All(char.IsAsciiLetterUpper)
        && value[3] == FreightContainerCategory
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
