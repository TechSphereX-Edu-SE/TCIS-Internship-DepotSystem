namespace TechSpherex.CleanArchitecture.Domain.Services;

/*
    The depot works on Vietnam time (UTC+7, no daylight saving).
    Business dates - a delivery order's expiry date ("last day containers may be released"),
    throughput per day - are Vietnam dates, so an instant is converted to +07:00 before
    taking its date. Using the server's or UTC date would shift events between 00:00 and 07:00
    to the previous day.
*/
public static class DepotTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    // Vietnam calendar date of an instant, whatever offset the instant carries
    public static DateOnly DateOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(instant.ToOffset(Offset).DateTime);
}
