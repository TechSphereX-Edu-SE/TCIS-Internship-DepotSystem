using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.GetAll;

public sealed record GetAllBlocksQuery(int Page = 1, int PageSize = 20)
    : IQuery<Result<PagedResult<BlockResponse>>>;
