namespace PoeActTimer.Core;

public static class LiveLog
{
    /// <summary>Lines whose own timestamp is older than this are history, not live play.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);

    /// <summary>The live loop applies events with the current time. After the log was truncated or
    /// replaced the tailer re-reads it from the start, so old lines (an old login, the strand) must
    /// be skipped instead of being applied as if they happened now. Catch-up does not use this:
    /// it applies old events by their own timestamps.</summary>
    public static bool IsLive(LogEvent e, DateTime now) => e.At >= now - MaxAge;
}
