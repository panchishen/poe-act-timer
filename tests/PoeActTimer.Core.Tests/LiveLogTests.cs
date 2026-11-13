using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public class LiveLogTests
{
    static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0);

    [Fact]
    public void FreshEventIsLive()
    {
        Assert.True(LiveLog.IsLive(new GameStarted(Now), Now));
        Assert.True(LiveLog.IsLive(new GameStarted(Now.AddSeconds(-59)), Now));
        Assert.True(LiveLog.IsLive(new GameStarted(Now.AddSeconds(2)), Now)); // clock jitter
    }

    [Fact]
    public void EventOlderThanAMinuteIsHistory()
    {
        // After truncation the tailer re-reads from 0: old lines must not be applied as "now".
        Assert.False(LiveLog.IsLive(new GameStarted(Now.AddSeconds(-61)), Now));
        Assert.False(LiveLog.IsLive(new AreaGenerated(Now.AddHours(-3), 1, "1_1_1"), Now));
    }

    [Fact]
    public void ReplayedOldLoginDoesNotArchiveTheActiveRun()
    {
        var state = new TimerState();
        var timer = new RunTimer(state, TimeSpan.FromSeconds(20));
        var play = new GameSnapshot(true, true, TimeSpan.Zero);
        timer.Start(Now.AddMinutes(-30), play);
        timer.Apply(new AreaGenerated(Now.AddMinutes(-30), 1, "1_1_1"), Now.AddMinutes(-30), play);
        timer.Apply(new AreaGenerated(Now.AddMinutes(-20), 1, "1_2_1"), Now.AddMinutes(-20), play);
        var id = state.Run!.Id;
        bool archived = false;
        timer.RunArchived += _ => archived = true;

        var old = Now.AddHours(-5);
        LogEvent[] replayed = { new GameStarted(old), new LoginConnecting(old), new AreaGenerated(old, 1, "1_1_1") };
        foreach (var e in replayed.Where(e => LiveLog.IsLive(e, Now)))
            timer.Apply(e, Now, play);

        Assert.False(archived);
        Assert.Equal(id, state.Run!.Id);
    }
}
