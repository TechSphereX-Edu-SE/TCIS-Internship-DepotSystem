using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Abstractions.Storage;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Photos;

// 404 RepairPhoto.NotFound when the row is unknown, RepairPhoto.FileMissing when the file is gone from disk
public sealed class GetRepairPhotoQueryHandler(IAppDbContext dbContext, IRepairPhotoStorage storage)
    : IQueryHandler<GetRepairPhotoQuery, Result<RepairPhotoFile>>
{
    public async Task<Result<RepairPhotoFile>> HandleAsync(GetRepairPhotoQuery query, CancellationToken cancellationToken = default)
    {
        var photo = await dbContext.RepairPhotos.FindAsync([query.Id], cancellationToken);
        if (photo is null)
            return Result.Failure<RepairPhotoFile>(Error.NotFound("RepairPhoto.NotFound", $"Photo with ID '{query.Id}' was not found."));

        var content = storage.OpenRead(photo.StoredFileName);
        return content is null
            ? Result.Failure<RepairPhotoFile>(Error.NotFound("RepairPhoto.FileMissing",
                "The photo file is no longer on the server (check the photo folder volume)."))
            : Result.Success(new RepairPhotoFile(content, photo.ContentType, photo.OriginalFileName));
    }
}
