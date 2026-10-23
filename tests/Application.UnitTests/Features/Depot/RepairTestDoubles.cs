using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Storage;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

// Signed-in user for handlers that record who did something
internal sealed class FakeCurrentUser(string email = "staff@depot.test") : ICurrentUser
{
    public string? UserId => "user-1";
    public string? Email => email;
    public bool IsAuthenticated => true;
}

// Photo storage in memory, so tests do not write to disk
internal sealed class FakePhotoStorage : IRepairPhotoStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        var name = $"{Guid.NewGuid():N}{extension}";
        Files[name] = copy.ToArray();
        return name;
    }

    public Stream? OpenRead(string storedFileName) =>
        Files.TryGetValue(storedFileName, out var bytes) ? new MemoryStream(bytes) : null;
}
