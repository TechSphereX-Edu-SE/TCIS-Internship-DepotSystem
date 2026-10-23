using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Photos;

// One uploaded photo of a case (Q4: JPG/PNG, at most 5 MB); FindingId ties it to one finding
public sealed record UploadRepairPhotoCommand(
    Guid CaseId,
    Guid? FindingId,
    string FileName,
    string ContentType,
    long Length,
    Stream Content)
    : ICommand<Result<RepairPhotoResponse>>;
