using OneGov.Domain.Calendar;

namespace OneGov.Domain.Tests;

public class EthiopianDateTests
{
    // (Ethiopian y, m, d) <-> (Gregorian y, m, d). Pairs were checked independently of the implementation.
    public static TheoryData<int, int, int, int, int, int> KnownPairs => new()
    {
        { 2018, 1, 1, 2025, 9, 11 },   // Enkutatash (new year) 2018
        { 2016, 1, 1, 2023, 9, 12 },   // new year after a leap year shifts to 12 September
        { 2015, 13, 6, 2023, 9, 11 },  // Pagume 6 exists in leap year 2015
        { 2017, 13, 5, 2025, 9, 10 },  // last day of 2017
        { 2017, 4, 29, 2025, 1, 7 },   // Genna (Ethiopian Christmas)
        { 2017, 6, 23, 2025, 3, 2 },   // Adwa Victory Day
        { 2019, 3, 6, 2026, 11, 15 },
    };

    [Theory]
    [MemberData(nameof(KnownPairs))]
    public void Converts_in_both_directions(int ey, int em, int ed, int gy, int gm, int gd)
    {
        var ethiopian = EthiopianDate.Create(ey, em, ed);
        var gregorian = new DateOnly(gy, gm, gd);

        Assert.Equal(gregorian, ethiopian.ToGregorian());
        Assert.Equal(ethiopian, EthiopianDate.FromGregorian(gregorian));
    }

    [Fact]
    public void Round_trips_every_day_from_1900_to_2100()
    {
        for (var day = new DateOnly(1900, 1, 1); day < new DateOnly(2100, 1, 1); day = day.AddDays(1))
        {
            var ethiopian = EthiopianDate.FromGregorian(day);

            Assert.InRange(ethiopian.Month, 1, 13);
            Assert.InRange(ethiopian.Day, 1, EthiopianDate.DaysInMonth(ethiopian.Year, ethiopian.Month));
            Assert.Equal(day, ethiopian.ToGregorian());
        }
    }

    [Theory]
    [InlineData(2015, true)]
    [InlineData(2019, true)]
    [InlineData(2016, false)]
    [InlineData(2018, false)]
    public void Leap_years_are_those_with_remainder_three(int year, bool expected) =>
        Assert.Equal(expected, EthiopianDate.IsLeapYear(year));

    [Fact]
    public void Pagume_has_six_days_only_in_leap_years()
    {
        Assert.True(EthiopianDate.TryCreate(2015, 13, 6, out _));
        Assert.False(EthiopianDate.TryCreate(2016, 13, 6, out _));
        Assert.Equal(5, EthiopianDate.DaysInMonth(2016, 13));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(2018, 0, 1)]
    [InlineData(2018, 14, 1)]
    [InlineData(2018, 1, 31)]
    [InlineData(2018, 5, 0)]
    public void Rejects_invalid_dates(int year, int month, int day)
    {
        Assert.False(EthiopianDate.TryCreate(year, month, day, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => EthiopianDate.Create(year, month, day));
    }

    [Fact]
    public void Default_value_cannot_be_converted() =>
        Assert.Throws<InvalidOperationException>(() => default(EthiopianDate).ToGregorian());

    [Fact]
    public void Dates_before_the_calendar_epoch_are_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EthiopianDate.FromGregorian(new DateOnly(5, 1, 1)));
}
