namespace TechSpherex.CleanArchitecture.Domain.Services;

/*
    Q4 (23/10/2026): damage photos are JPG or PNG, at most 5 MB each.
    The declared type is not trusted on its own: the first bytes must be a real JPG / PNG header,
    so a renamed file of another kind is refused.
*/
public static class RepairPhotoRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    // Content type -> file extension used for the stored file
    public static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png"
    };

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool IsAllowedType(string? contentType) =>
        contentType is not null && Extensions.ContainsKey(contentType.ToLowerInvariant());

    // True when the file starts with the signature of its declared type
    public static bool MatchesSignature(ReadOnlySpan<byte> header, string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => header.StartsWith(JpegSignature),
            "image/png" => header.StartsWith(PngSignature),
            _ => false
        };
}
