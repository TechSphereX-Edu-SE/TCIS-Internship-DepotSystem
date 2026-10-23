using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.GetAll;

/*
    IncludeArchived = false (default): only blocks that can take containers, for the gate-in and move dropdowns.
    IncludeArchived = true: the Block catalog also lists archived blocks, after the active ones.
*/
public sealed record GetAllBlocksQuery(int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : IQuery<Result<PagedResult<BlockResponse>>>;
