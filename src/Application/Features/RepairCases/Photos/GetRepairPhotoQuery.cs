using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Photos;

public sealed record GetRepairPhotoQuery(Guid Id) : IQuery<Result<RepairPhotoFile>>;

// The stream is disposed by the endpoint result after it is sent
public sealed record RepairPhotoFile(Stream Content, string ContentType, string FileName);
