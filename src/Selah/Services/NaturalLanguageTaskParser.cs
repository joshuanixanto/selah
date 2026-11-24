using System.Globalization;
using System.Text.RegularExpressions;

namespace Selah.Services;

public sealed record ParsedTask(string Title, DateTime? DueAt);

/// <summary>
/// Parses a deliberately small, predictable set of time expressions.
/// Unrecognized text remains part of the task title.
/// </summary>
public sealed partial class NaturalLanguageTaskParser
{
    [GeneratedRegex(@"\s+in\s+(\d+)\s*m\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InMinutesRegex();

    [GeneratedRegex(@"\s+in\s+(\d+)\s*h\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InHoursRegex();

    [GeneratedRegex(@"\s+tonight(?:\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TonightRegex();

    [GeneratedRegex(@"\s+tomorrow(?:\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TomorrowRegex();

    public ParsedTask Parse(string input, DateTime? now = null)
    {
        var title = (input ?? string.Empty).Trim();
        var referenceTime = now ?? DateTime.Now;
        if (title.Length == 0) return new ParsedTask(string.Empty, null);

        var minutes = InMinutesRegex().Match(title);
        if (minutes.Success && int.TryParse(minutes.Groups[1].Value, out var minuteCount))
            return new ParsedTask(title[..minutes.Index].Trim(), referenceTime.AddMinutes(minuteCount));

        var hours = InHoursRegex().Match(title);
        if (hours.Success && int.TryParse(hours.Groups[1].Value, out var hourCount))
            return new ParsedTask(title[..hours.Index].Trim(), referenceTime.AddHours(hourCount));

        var tonight = TonightRegex().Match(title);
        if (tonight.Success)
            return new ParsedTask(title[..tonight.Index].Trim(), ParseClock(tonight, referenceTime.Date.AddHours(20), referenceTime));

        var tomorrow = TomorrowRegex().Match(title);
        if (tomorrow.Success)
            return new ParsedTask(title[..tomorrow.Index].Trim(), ParseClock(tomorrow, referenceTime.Date.AddDays(1).AddHours(9), referenceTime));

        return new ParsedTask(title, null);
    }

    private static DateTime ParseClock(Match match, DateTime defaultTime, DateTime now)
    {
        if (!match.Groups[1].Success)
            return defaultTime <= now ? defaultTime.AddDays(1) : defaultTime;

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            (match.Groups[2].Success &&
             !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _)) ||
            hour > 23)
            return defaultTime;

        var minute = match.Groups[2].Success
            ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
            : 0;
        if (minute > 59) return defaultTime;

        var result = defaultTime.Date.AddHours(hour).AddMinutes(minute);
        return result <= now && result.Date == now.Date ? result.AddDays(1) : result;
    }
}
