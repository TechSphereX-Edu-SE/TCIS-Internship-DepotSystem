namespace TechSpherex.CleanArchitecture.Application.Abstractions.Storage;

/*
    Where repair photo files live (Q4: server disk, folder kept between runs).
    The database row (RepairPhoto) keeps the stored name; the file itself is only reached through here.
*/
public interface IRepairPhotoStorage
{
    // Saves the bytes under a new random name with the given extension (".jpg" / ".png") and returns that name
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    // The stored file, or null when it is missing (e.g. the folder was not kept)
    Stream? OpenRead(string storedFileName);
}
