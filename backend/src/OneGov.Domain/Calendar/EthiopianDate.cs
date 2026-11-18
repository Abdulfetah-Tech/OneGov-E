namespace OneGov.Domain.Calendar;

/// <summary>
/// A date in the Ethiopian (Ge'ez) calendar: 12 months of 30 days plus Pagume (5 days, or 6 in a leap year).
/// A year is a leap year when <c>year % 4 == 3</c>; the extra day is Pagume 6.
/// <para>
/// Conversion uses a continuous day count anchored to a well-known pair
/// (1 Meskerem 2018 E.C. = 2025-09-11) so no epoch constant has to be trusted.
/// <c>default(EthiopianDate)</c> is NOT a valid date; always construct with <see cref="Create"/> or <see cref="TryCreate"/>.
/// </para>
/// </summary>
public readonly record struct EthiopianDate
{
    // Days since 1 Meskerem 1 E.C. (index 0) for the given date. Leap years before year y: floor(y / 4).
    private static int DayIndex(int year, int month, int day) =>
        (365 * (year - 1)) + (year / 4) + (30 * (month - 1)) + (day - 1);

    private static readonly int GregorianDayNumberOffset =
        new DateOnly(2025, 9, 11).DayNumber - DayIndex(2018, 1, 1);

    private EthiopianDate(int year, int month, int day)
    {
        Year = year;
        Month = month;
        Day = day;
    }

    public int Year { get; }

    public int Month { get; }

    public int Day { get; }

    public static bool IsLeapYear(int year) => year % 4 == 3;

    public static int DaysInMonth(int year, int month) => month switch
    {
        >= 1 and <= 12 => 30,
        13 => IsLeapYear(year) ? 6 : 5,
        _ => throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 13."),
    };

    public static bool TryCreate(int year, int month, int day, out EthiopianDate date)
    {
        date = default;
        if (year < 1 || month < 1 || month > 13 || day < 1 || day > DaysInMonth(year, month))
        {
            return false;
        }

        date = new EthiopianDate(year, month, day);
        return true;
    }

    public static EthiopianDate Create(int year, int month, int day) =>
        TryCreate(year, month, day, out var date)
            ? date
            : throw new ArgumentOutOfRangeException(nameof(day), $"{year}-{month}-{day} is not a valid Ethiopian date.");

    public static EthiopianDate FromGregorian(DateOnly gregorian)
    {
        var n = gregorian.DayNumber - GregorianDayNumberOffset;
        if (n < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gregorian), "Date is before the start of the Ethiopian calendar.");
        }

        var year = (int)(n / 365.25) + 1;
        while (DayIndex(year + 1, 1, 1) <= n)
        {
            year++;
        }

        while (DayIndex(year, 1, 1) > n)
        {
            year--;
        }

        var remainder = n - DayIndex(year, 1, 1);
        return new EthiopianDate(year, (remainder / 30) + 1, (remainder % 30) + 1);
    }

    public DateOnly ToGregorian()
    {
        if (Year < 1)
        {
            throw new InvalidOperationException("Default or uninitialised EthiopianDate has no Gregorian equivalent.");
        }

        return DateOnly.FromDayNumber(GregorianDayNumberOffset + DayIndex(Year, Month, Day));
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}-{Day:D2} (E.C.)";
}
