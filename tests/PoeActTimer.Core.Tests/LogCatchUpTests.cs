using System.Globalization;
using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public class LogCatchUpTests
{
    static readonly DateTime T0 = new(2026, 11, 13, 12, 0, 0);
    static DateTime S(double s) => T0.AddSeconds(s);
    static string Stamp(double s) => S(s).ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
    static string L(double s, string body) => $"{Stamp(s)} 1 abc [INFO Client 1] {body}";

    static TimerState RunningAct1()
    {
        var state = new TimerState
        {
            Run = new RunState { StartedAt = T0 },
            InMenu = false,
            AwaitFirstZone = false,
            SavedAt = T0,
        };
        return state;
    }

    [Fact]
    public void ReplaysSplitsLoadingAndTime()
    {
        var state = RunningAct1();
        var timer = new RunTimer(state, TimeSpan.FromSeconds(20));
        string[] lines =
        [
            L(10, "Generating level 13 area \"1_2_1\" with seed 1"),
            L(12, "[SCENE] Set Source [Coast]"),
            L(12, "[LOADING SCREEN] (Coast) Duration = 2.0 seconds"),
            L(70, "Generating level 14 area \"1_2_2\" with seed 1"),
        ];

        LogCatchUp.Replay(timer, lines, T0, S(500), gameRunningNow: false);

        Assert.Equal(10, state.Run!.ActTime(1).TotalSeconds);
        Assert.Equal(58, state.Run.ActTime(2).TotalSeconds);
        Assert.Equal(2, state.Run.CurrentAct);
        var span = Assert.Single(state.Run.Recovered);
        Assert.Equal(T0, span.From);
        Assert.Equal(S(70), span.To);
        Assert.Equal(68, TimeSpan.FromTicks(span.CountedTicks).TotalSeconds);
    }

    [Fact]
    public void GapBetweenGameSessionsIsNotCounted()
    {
        var state = RunningAct1();
        var timer = new RunTimer(state, TimeSpan.FromSeconds(20));
        string[] lines =
        [
            L(10, "[WINDOW] Gained focus"),
            $"{Stamp(3600)} ***** LOG FILE OPENING *****",
            L(3610, "Async connecting to lon.login.pathofexile.com:20487"),
            L(3612, "Generating level 13 area \"1_1_town\" with seed 1"),
            L(3613, "[SCENE] Set Source [Lioneye's Watch]"),
        ];

        LogCatchUp.Replay(timer, lines, T0, S(3623), gameRunningNow: true);

        Assert.Equal(20, state.Run!.Total.TotalSeconds);
    }

    [Fact]
    public void LostFocusInLogPauses()
    {
        var state = RunningAct1();
        var timer = new RunTimer(state, TimeSpan.FromSeconds(20));
        string[] lines =
        [
            L(10, "[WINDOW] Lost focus"),
            L(40, "[WINDOW] Gained focus"),
        ];

        LogCatchUp.Replay(timer, lines, T0, S(50), gameRunningNow: true);

        Assert.Equal(20, state.Run!.Total.TotalSeconds);
    }

    [Fact]
    public void ClosedGameStopsAtLastEvent()
    {
        var state = RunningAct1();
        var timer = new RunTimer(state, TimeSpan.FromSeconds(20));
        string[] lines = [L(30, "[WINDOW] Gained focus")];

        LogCatchUp.Replay(timer, lines, T0, S(1000), gameRunningNow: false);

        Assert.Equal(30, state.Run!.Total.TotalSeconds);
    }
}
