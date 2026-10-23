using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    Photo of a repair case (Q4: file on the server disk, this row holds what is needed to find
    and serve it). FindingId links it to one finding when the photo shows that damage.
*/
public sealed class RepairPhoto : BaseEntity
{
    public Guid RepairCaseId { get; private set; }
    public Guid? FindingId { get; private set; }
    public string OriginalFileName { get; private set; } = default!;
    public string StoredFileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public string? UploadedBy { get; private set; }

    private RepairPhoto() { }

    // The handler checks type, size and content (RepairPhotoRules) before the file is stored
    public static RepairPhoto Create(
        RepairCase repairCase, Guid? findingId, string originalFileName, string storedFileName,
        string contentType, long sizeBytes, DateTimeOffset at, string? actor) =>
        new()
        {
            RepairCaseId = repairCase.Id,
            FindingId = findingId,
            OriginalFileName = Path.GetFileName(originalFileName.Trim()),
            StoredFileName = storedFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedAt = at,
            UploadedBy = actor
        };
}
