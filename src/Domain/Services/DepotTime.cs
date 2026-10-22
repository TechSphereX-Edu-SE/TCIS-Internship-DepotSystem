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

    /*
        First instant of a Vietnam date (00:00 +07:00), used to turn a date range into a time range.
        Returned in UTC: PostgreSQL (Npgsql) only accepts offset 0 when comparing with timestamptz.
    */
    public static DateTimeOffset StartOf(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();

    /*
        Days a container has stayed in the yard, counted in Vietnam calendar days:
        0 on the gate-in day, 1 on the next day, and so on (the time of day does not matter).
    */
    public static int DwellDays(DateTimeOffset gateInAt, DateTimeOffset asOf) =>
        DateOf(asOf).DayNumber - DateOf(gateInAt).DayNumber;
}
