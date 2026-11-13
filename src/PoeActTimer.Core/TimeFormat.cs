namespace PoeActTimer.Core;

public static class TimeFormat
{
    public static string Format(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        int hours = (int)t.TotalHours;
        return hours > 0
            ? $"{hours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
    }
}
