namespace PoeActTimer.Core;

public static class LogCatchUp
{
    public static void Replay(RunTimer timer, IEnumerable<string> lines, DateTime resumeFrom, DateTime now, bool gameRunningNow)
    {
        var state = timer.State;
        var runId = state.Run?.Id;
        var before = state.Run?.Total ?? TimeSpan.Zero;
        var last = resumeFrom;

        timer.Start(resumeFrom, Snap(state));
        foreach (var line in lines)
        {
            foreach (var e in LogParser.Parse(line))
            {
                timer.Apply(e, e.At, Snap(state));
                timer.Tick(e.At, Snap(state)); // re-evaluate status: the event may have changed log focus
                if (e.At > last) last = e.At;
            }
        }

        if (gameRunningNow) timer.Tick(now, Snap(state));
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
