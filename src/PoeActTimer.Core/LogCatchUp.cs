namespace PoeActTimer.Core;

public static class LogCatchUp
{
    /// <summary>A gap between log events longer than this is not credited: nobody can be sure it was play.</summary>
    public static readonly TimeSpan DefaultMaxGap = TimeSpan.FromMinutes(15);

    /// <param name="idleNow">Current system idle time; when it reaches the AFK threshold the final
    /// segment is credited only up to the moment the input stopped.</param>
    /// <param name="maxGap">Longest gap between consecutive events (and in the final segment) that is credited.</param>
    public static void Replay(RunTimer timer, IEnumerable<string> lines, DateTime resumeFrom, DateTime now,
        bool gameRunningNow, TimeSpan idleNow = default, TimeSpan? maxGap = null)
    {
        var cap = maxGap ?? DefaultMaxGap;
        var state = timer.State;
        var runId = state.Run?.Id;
        var before = state.Run?.Total ?? TimeSpan.Zero;
        var last = resumeFrom;

        timer.Start(resumeFrom, Snap(state));
        foreach (var line in lines)
        {
            foreach (var e in LogParser.Parse(line))
            {
                if (e.At - last > cap) timer.Halt(e.At);
                timer.Apply(e, e.At, Snap(state));
                timer.Tick(e.At, Snap(state)); // re-evaluate status: the event may have changed log focus
                if (e.At > last) last = e.At;
            }
        }

        if (gameRunningNow)
        {
            var creditTo = idleNow >= timer.AfkThreshold ? now - idleNow : now;
            if (creditTo > last && creditTo - last <= cap) timer.Tick(creditTo, Snap(state));
            timer.Halt(now);
            timer.Tick(now, Snap(state)); // no credit after Halt; refreshes status
        }
        else timer.Halt(now);

        if (state.Run is not null && state.Run.Id == runId)
        {
            var counted = state.Run.Total - before;
            var to = gameRunningNow ? now : last;
            if (to > resumeFrom)
                state.Run.Recovered.Add(new RecoveredSpan(resumeFrom, to, counted.Ticks));
        }
    }

    static GameSnapshot Snap(TimerState state) => new(true, state.LogFocused, TimeSpan.Zero);
}
