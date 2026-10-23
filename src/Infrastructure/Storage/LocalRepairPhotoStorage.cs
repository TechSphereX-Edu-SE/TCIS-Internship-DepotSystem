using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using TechSpherex.CleanArchitecture.Application.Abstractions.Storage;
namespace TechSpherex.CleanArchitecture.Infrastructure.Storage;

/*
    Repair photos on the local disk (Q4). Folder: RepairPhotos:RootPath from configuration,
    relative to the working directory of the Api (default App_Data/repair-photos), so the files stay
    between runs. In a container, mount a volume on that folder.
    File names are generated here (32 hex characters + extension), never taken from the upload,
    and a read only accepts that pattern, so a request cannot reach another path.
*/
public sealed partial class LocalRepairPhotoStorage(IConfiguration configuration) : IRepairPhotoStorage
{
    private readonly string _root = Path.GetFullPath(configuration["RepairPhotos:RootPath"] ?? Path.Combine("App_Data", "repair-photos"));

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var name = $"{Guid.NewGuid():N}{extension}";
        await using var file = new FileStream(Path.Combine(_root, name), FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, cancellationToken);
        return name;
    }

    public Stream? OpenRead(string storedFileName)
    {
        if (!StoredName().IsMatch(storedFileName))
            return null;

        var path = Path.Combine(_root, storedFileName);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    [GeneratedRegex("^[0-9a-f]{32}\\.(jpg|png)$")]
    private static partial Regex StoredName();
}
