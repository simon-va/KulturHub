using System.Globalization;

namespace KulturHub.Infrastructure.ImageGeneration;

public static class DateFormatHelper
{
    private static readonly CultureInfo German = new("de-DE");

    public static string FormatShortDate(DateTime date)
        => date.ToString("dd.MM.", German);

    public static string FormatDayHeader(DateOnly date)
        => date.ToString("dddd, dd. MMMM", German);
}
