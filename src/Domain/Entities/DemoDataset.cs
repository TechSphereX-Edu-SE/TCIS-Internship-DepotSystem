using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    Feedback 10 (database demo): remembers when the demo data was first created.
    Every demo event is placed relative to AnchorAt, so running the demo seed again later never moves
    old dates to today: missing demo rows get the same dates they would have had on the first run.
    One row per dataset key; a demo reset removes it and the next seed starts a new anchor.
*/
public sealed class DemoDataset : BaseEntity
{
    public string Key { get; private set; } = default!;
    public DateTimeOffset AnchorAt { get; private set; }
    public DateTimeOffset? LastSeededAt { get; private set; }

    private DemoDataset() { }

    public static DemoDataset Start(string key, DateTimeOffset anchorAt) => new()
    {
        Key = key,
        AnchorAt = anchorAt
    };

    public void MarkSeeded(DateTimeOffset at) => LastSeededAt = at;
}
