using System.Globalization;
using System.Text.RegularExpressions;

namespace DesktopCompanion;

public record Parsed(string Title, DateTime? Date, TimeSpan? Time, string? Tag, bool HasDate, bool HasTime)
{
    public bool HasAny => HasDate || HasTime || Tag != null;

    public string DateChip
    {
        get
        {
            if (Date is not { } d) return "Later";
            var today = DateTime.Today;
            if (d.Date == today) return "Today, " + d.ToString("MMM d", CultureInfo.InvariantCulture);
            if (d.Date == today.AddDays(1)) return "Tomorrow, " + d.ToString("MMM d", CultureInfo.InvariantCulture);
            return d.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
        }
    }

    public string TimeChip => Time is { } t ? (DateTime.Today + t).ToString("h:mm tt", CultureInfo.InvariantCulture) : "No time";
    public string TagChip => Tag != null ? "#" + Tag : "No tag";
}

// Turns "Call mom tomorrow 10am #family" into a title, a due date/time and a tag.
public static class QuickParser
{
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    const string Months = "(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)";
    static readonly string[] Days = { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };

    static bool Take(ref string s, string pattern, out Match m)
    {
        m = Regex.Match(s, pattern, O);
        if (!m.Success) return false;
        s = s.Remove(m.Index, m.Length).Insert(m.Index, " ");
        return true;
    }

    public static Parsed Parse(string input, DateTime? now = null)
    {
        var today = (now ?? DateTime.Now).Date;
        var s = " " + input.Trim() + " ";
        string? tag = null;
        TimeSpan? time = null;
        DateTime? date = null;
        bool hasDate = false, later = false;

        if (Take(ref s, @"(?<=\s)#([\w-]+)(?=\s)", out var m)) tag = m.Groups[1].Value.ToLowerInvariant();

        if (Take(ref s, @"(?<=\s)(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)(?=[\s.,]|$)", out m))
        {
            int h = int.Parse(m.Groups[1].Value), mi = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            var pm = m.Groups[3].Value.Equals("pm", StringComparison.OrdinalIgnoreCase);
            if (h is >= 1 and <= 12 && mi < 60) { h = h % 12 + (pm ? 12 : 0); time = new TimeSpan(h, mi, 0); }
            else s = " " + input.Trim() + " ";
        }
        if (time == null && Take(ref s, @"(?<=\s)at\s+(\d{1,2})(?::(\d{2}))?(?=[\s.,]|$)", out m))
        {
            int h = int.Parse(m.Groups[1].Value), mi = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            if (h < 24 && mi < 60) time = new TimeSpan(h, mi, 0);
        }
        if (time == null && Take(ref s, @"(?<=\s)([01]?\d|2[0-3]):([0-5]\d)(?=[\s.,]|$)", out m))
            time = new TimeSpan(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), 0);

        if (Take(ref s, @"(?<=\s)(?:(?:on|by)\s+)?(?:today|tonight)(?=[\s.,]|$)", out _)) { date = today; hasDate = true; }
        else if (Take(ref s, @"(?<=\s)(?:(?:on|by)\s+)?(?:tomorrow|tmrw|tmr)(?=[\s.,]|$)", out _)) { date = today.AddDays(1); hasDate = true; }
        else if (Take(ref s, @"(?<=\s)in\s+(\d{1,3})\s+(day|days|week|weeks)(?=[\s.,]|$)", out m))
        {
            int n = int.Parse(m.Groups[1].Value);
            date = today.AddDays(m.Groups[2].Value.StartsWith("w", StringComparison.OrdinalIgnoreCase) ? n * 7 : n);
            hasDate = true;
        }
        else if (Take(ref s, @"(?<=\s)(?:on\s+|by\s+)?next\s+week(?=[\s.,]|$)", out _))
        {
            int add = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            date = today.AddDays(add == 0 ? 7 : add); hasDate = true;
        }
        else if (Take(ref s, @"(?<=\s)(?:(?:on|by)\s+)?(?:next\s+|this\s+)?(sunday|monday|tuesday|wednesday|thursday|friday|saturday)(?=[\s.,]|$)", out m))
        {
            int target = Array.IndexOf(Days, m.Groups[1].Value.ToLowerInvariant());
            int add = (target - (int)today.DayOfWeek + 7) % 7;
            date = today.AddDays(add == 0 ? 7 : add); hasDate = true;
        }
        else if (Take(ref s, @"(?<=\s)(?:(?:on|by)\s+)?" + Months + @"\.?\s+(\d{1,2})(?:st|nd|rd|th)?(?=[\s.,]|$)", out m)
              || Take(ref s, @"(?<=\s)(?:(?:on|by)\s+)?(\d{1,2})(?:st|nd|rd|th)?\s+" + Months + @"(?=[\s.,]|$)", out m))
        {
            var g1 = m.Groups[1].Value;
            bool dayFirst = char.IsDigit(g1[0]);
            int day = int.Parse(dayFirst ? g1 : m.Groups[2].Value);
            var monName = (dayFirst ? m.Groups[2].Value : g1).Substring(0, 3);
            int month = DateTime.ParseExact(monName, "MMM", CultureInfo.InvariantCulture).Month;
            if (day >= 1 && day <= DateTime.DaysInMonth(2024, month))
            {
                int year = today.Year;
                if (day > DateTime.DaysInMonth(year, month) || new DateTime(year, month, day) < today) year++;
                if (day <= DateTime.DaysInMonth(year, month)) { date = new DateTime(year, month, day); hasDate = true; }
            }
        }
        else if (Take(ref s, @"(?<=\s)(?:later|someday)(?=\s*$)", out _)) { later = true; hasDate = true; }

        if (!hasDate) date = today;
        if (later) date = null;

        var title = Regex.Replace(s, @"\s+", " ").Trim();
        string prev;
        do { prev = title; title = Regex.Replace(title, @"\s+(on|by|at|for|in|to)$", "", O).Trim(' ', ',', '.', '-'); } while (title != prev);
        if (title.Length == 0) title = Regex.Replace(input.Trim(), @"\s+", " ");

        return new Parsed(title, date, time, tag, hasDate, time != null);
    }
}
