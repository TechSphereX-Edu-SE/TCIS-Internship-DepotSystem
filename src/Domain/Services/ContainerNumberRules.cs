namespace TechSpherex.CleanArchitecture.Domain.Services;

/// <summary>
/// Quy tắc số container theo ISO 6346 (Phụ lục I tài liệu Depot):
/// 4 chữ cái (3 chữ mã chủ sở hữu + 1 chữ mã loại) + 6 số serial + 1 số kiểm tra.
/// Ví dụ hợp lệ: CSQU3054383, MSCU1234566.
/// </summary>
public static class ContainerNumberRules
{
    public const int Length = 11;

    public static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    public static bool HasValidFormat(string value) =>
        value.Length == Length
        && value[..4].All(char.IsAsciiLetterUpper)
        && value[4..].All(char.IsAsciiDigit);

    /// <summary>
    /// Tính số kiểm tra từ 10 ký tự đầu (Modulo 11).
    /// Chữ cái quy đổi A=10, B=12, C=13... (bỏ qua 11, 22, 33);
    /// mỗi ký tự nhân 2^vị trí; tổng chia 11 lấy dư; dư 10 thì số kiểm tra là 0.
    /// </summary>
    public static int CalculateCheckDigit(string firstTenCharacters)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var c = firstTenCharacters[i];
            var value = char.IsAsciiLetter(c) ? LetterValue(c) : c - '0';
            sum += value * (1 << i);
        }

        return sum % 11 % 10;
    }

    public static bool IsValid(string? value)
    {
        var number = Normalize(value);
        if (!HasValidFormat(number))
            return false;

        return CalculateCheckDigit(number) == number[10] - '0';
    }

    private static int LetterValue(char letter)
    {
        // A=10 rồi tăng dần, bỏ qua các bội số của 11
        var value = 10;
        for (var c = 'A'; c < char.ToUpperInvariant(letter); c++)
        {
            value++;
            if (value % 11 == 0) value++;
        }

        return value;
    }
}
