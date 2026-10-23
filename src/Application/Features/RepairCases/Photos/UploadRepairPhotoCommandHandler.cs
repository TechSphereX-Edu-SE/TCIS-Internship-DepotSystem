using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Abstractions.Storage;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Photos;

/*
    Order of checks:
      1. The case exists (404) and is open (409): a closed case keeps its photos as they are.
      2. The finding, when given, belongs to this case (400).
      3. The bytes really are a JPG / PNG of at most 5 MB (400), whatever the declared type says.
    The file is written before the row is saved; if saving the row fails, an orphan file is left
    on disk, which is harmless (no row points to it).
*/
public sealed class UploadRepairPhotoCommandHandler(
    IAppDbContext dbContext, IRepairPhotoStorage storage, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<UploadRepairPhotoCommand, Result<RepairPhotoResponse>>
{
    public async Task<Result<RepairPhotoResponse>> HandleAsync(
        UploadRepairPhotoCommand command, CancellationToken cancellationToken = default)
    {
        var repairCase = await dbContext.RepairCases.FindAsync([command.CaseId], cancellationToken);
        if (repairCase is null)
            return Result.Failure<RepairPhotoResponse>(ApplyRepairActionCommandHandler.NotFound(command.CaseId));
        if (!repairCase.IsOpen)
            return Result.Failure<RepairPhotoResponse>(RepairCase.Closed());

        if (command.FindingId is { } findingId &&
            !await dbContext.RepairFindings.AnyAsync(f => f.Id == findingId && f.RepairCaseId == repairCase.Id, cancellationToken))
            return Result.Failure<RepairPhotoResponse>(Error.Validation("RepairPhoto.FindingNotInCase",
                "The finding does not belong to this repair case."));

        // At most 5 MB, so reading it into memory is fine; this also checks the real size
        using var buffer = new MemoryStream();
        await command.Content.CopyToAsync(buffer, cancellationToken);
        var contentType = command.ContentType.ToLowerInvariant();
        if (buffer.Length == 0 || buffer.Length > RepairPhotoRules.MaxBytes ||
            !RepairPhotoRules.IsAllowedType(contentType) ||
            !RepairPhotoRules.MatchesSignature(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)), contentType))
            return Result.Failure<RepairPhotoResponse>(Error.Validation("RepairPhoto.InvalidFile",
                "The file is not a JPG or PNG photo of at most 5 MB."));

        buffer.Position = 0;
        var storedName = await storage.SaveAsync(buffer, RepairPhotoRules.Extensions[contentType], cancellationToken);

        var photo = RepairPhoto.Create(repairCase, command.FindingId, command.FileName, storedName, contentType,
            buffer.Length, timeProvider.GetUtcNow(), currentUser.Email);
        dbContext.RepairPhotos.Add(photo);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new RepairPhotoResponse(photo.Id, photo.FindingId, photo.OriginalFileName,
            photo.ContentType, photo.SizeBytes, photo.UploadedAt, photo.UploadedBy));
    }
}
