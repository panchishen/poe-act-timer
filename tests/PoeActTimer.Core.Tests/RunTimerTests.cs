using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public class RunTimerTests
{
    static readonly DateTime T0 = new(2026, 11, 13, 12, 0, 0);
    static readonly GameSnapshot Play = new(true, true, TimeSpan.Zero);
    static readonly GameSnapshot Unfocused = new(true, false, TimeSpan.Zero);
    static DateTime S(double seconds) => T0.AddSeconds(seconds);
    static GameSnapshot Idle(double seconds) => new(true, true, TimeSpan.FromSeconds(seconds));

    static RunTimer NewRun()
    {
        var t = new RunTimer(new TimerState(), TimeSpan.FromSeconds(20));
        t.Start(T0, Play);
        Gen(t, 0, "1_1_1");
        Enter(t, 0);
        return t;
    }

    static void Gen(RunTimer t, double s, string id) => t.Apply(new AreaGenerated(S(s), 1, id), S(s), Play);
    static void Enter(RunTimer t, double s) => t.Apply(new AreaEntered(S(s), "Zone"), S(s), Play);
    static void Login(RunTimer t, double s) => t.Apply(new LoginConnecting(S(s)), S(s), Play);
    static void Level(RunTimer t, double s, string name) => t.Apply(new LevelUp(S(s), name, 2), S(s), Play);
    static double Total(RunTimer t) => t.State.Run!.Total.TotalSeconds;
    static double Act(RunTimer t, int act) => t.State.Run!.ActTime(act).TotalSeconds;

    static RunTimer RunAtAct3()
    {
        var t = NewRun();
        Gen(t, 10, "1_2_1"); Enter(t, 10);
        Gen(t, 20, "1_3_1"); Enter(t, 20);
        return t;
    }

    [Fact]
    public void StartsRunOnTwilightStrandAfterLogin()
    {
        var t = NewRun();
        Assert.NotNull(t.State.Run);
        Assert.Equal(1, t.State.Run!.CurrentAct);
        Assert.Equal(TimerStatus.Running, t.Status);
    }

    [Fact]
    public void NoRunWithoutStrand()
    {
        var t = new RunTimer(new TimerState(), TimeSpan.FromSeconds(20));
        t.Start(T0, Play);
        Gen(t, 0, "1_3_town"); Enter(t, 0);
        t.Tick(S(60), Play);
        Assert.Null(t.State.Run);
        Assert.Equal(TimerStatus.NoRun, t.Status);
    }

    [Fact]
    public void CountsTimeWhilePlaying()
    {
        var t = NewRun();
        t.Tick(S(60), Play);
        Assert.Equal(60, Total(t));
        Assert.Equal(60, Act(t, 1));
    }

    [Fact]
    public void AfkIsDeductedRetroactivelyAndResumesOnInput()
    {
        var t = NewRun();
        t.Tick(S(30), Play);
        t.Tick(S(40), Idle(5));
        Assert.Equal(40, Total(t));
        t.Tick(S(60), Idle(25));
        Assert.Equal(TimerStatus.Afk, t.Status);
        Assert.Equal(35, Total(t));
        t.Tick(S(100), Idle(65));
        Assert.Equal(35, Total(t));
        t.Tick(S(101), Play);
        Assert.Equal(TimerStatus.Running, t.Status);
        t.Tick(S(111), Play);
        Assert.Equal(45, Total(t));
    }

    [Fact]
    public void LoadingIsDeductedAndSummed()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        t.Apply(new LoadingScreen(S(10), 3.0), S(10), Play);
        Assert.Equal(7, Total(t));
        Assert.Equal(3, TimeSpan.FromTicks(t.State.Run!.LoadingTicks).TotalSeconds);
    }

    [Fact]
    public void LoadingDuringPauseIsNotDeductedTwice()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        t.Tick(S(20), Unfocused);
        Assert.Equal(TimerStatus.Minimized, t.Status);
        t.Apply(new LoadingScreen(S(25), 4.0), S(25), Unfocused);
        Assert.Equal(20, Total(t));
        Assert.Equal(0, t.State.Run!.LoadingTicks);
    }

    [Fact]
    public void LongGapBetweenLiveTicksIsNotCredited()
    {
        var t = NewRun();
        var gap = TimeSpan.FromSeconds(5);
        t.Tick(S(10), Play);
        Assert.Equal(10, Total(t));
        t.Tick(S(3610), Play, gap);
        Assert.Equal(10, Total(t));
        t.Tick(S(3615), Play, gap);
        Assert.Equal(15, Total(t));
    }

    [Fact]
    public void LongGapIsCreditedWithoutMaxGap()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        t.Tick(S(3610), Play);
        Assert.Equal(3610, Total(t));
    }

    [Fact]
    public void ManualPauseDoesNotLeakIntoNewRun()
    {
        var t = RunAtAct3();
        t.ToggleManualPause(S(25), Play);
        Assert.Equal(TimerStatus.ManualPause, t.Status);
        Login(t, 30);
        Gen(t, 31, "1_1_1"); Enter(t, 31);
        Assert.False(t.State.ManualPause);
        Assert.Equal(TimerStatus.Running, t.Status);
    }

    [Fact]
    public void ResetRunClearsManualPause()
    {
        var t = NewRun();
        t.ToggleManualPause(S(5), Play);
        t.ResetRun(S(6), Play);
        Assert.False(t.State.ManualPause);
    }

    [Fact]
    public void AfkDeductionSpanningActSplit()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        Gen(t, 10, "1_2_1");
        t.Tick(S(15), Play);
        t.Tick(S(30), Idle(25));
        Assert.Equal(TimerStatus.Afk, t.Status);
        Assert.Equal(5, Act(t, 1));
        Assert.Equal(0, Act(t, 2));
    }

    [Fact]
    public void FinishWhileOtherCharacterDoesNotFinish()
    {
        var t = RunAtAct3();
        Login(t, 30);
        Gen(t, 31, "2_7_town"); Enter(t, 31);
        Gen(t, 40, "2_11_endgame_town"); Enter(t, 40);
        Assert.Null(t.State.Run!.FinishedAt);
        Assert.Equal(TimerStatus.OtherCharacter, t.Status);
    }

    [Fact]
    public void LoadingSpanningActSplitDeductsFromBothActs()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        Gen(t, 10, "1_2_1");
        t.Tick(S(12), Play);
        t.Apply(new LoadingScreen(S(12), 4.0), S(12), Play);
        Assert.Equal(8, Act(t, 1));
        Assert.Equal(0, Act(t, 2));
    }

    [Fact]
    public void ManualPauseStopsAndResumes()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        t.ToggleManualPause(S(10), Play);
        Assert.Equal(TimerStatus.ManualPause, t.Status);
        t.Tick(S(50), Play);
        Assert.Equal(10, Total(t));
        t.ToggleManualPause(S(50), Play);
        t.Tick(S(55), Play);
        Assert.Equal(15, Total(t));
    }

    [Fact]
    public void UnfocusedGameDoesNotCount()
    {
        var t = NewRun();
        t.Tick(S(10), Unfocused);
        t.Tick(S(70), Unfocused);
        Assert.Equal(10, Total(t));
        t.Tick(S(80), new GameSnapshot(false, false, TimeSpan.Zero));
        Assert.Equal(TimerStatus.Minimized, t.Status);
        Assert.Equal(10, Total(t));
    }

    [Fact]
    public void SplitsOnFirstEntryToHigherActOnly()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        Gen(t, 10, "1_2_1");
        Assert.Equal(2, t.State.Run!.CurrentAct);
        t.Tick(S(25), Play);
        Gen(t, 25, "1_1_town");
        Assert.Equal(2, t.State.Run!.CurrentAct);
        t.Tick(S(30), Play);
        Assert.Equal(10, Act(t, 1));
        Assert.Equal(20, Act(t, 2));
    }

    [Fact]
    public void FinishesOnKaruiShores()
    {
        var t = NewRun();
        RunState? archived = null;
        t.RunArchived += r => archived = r;
        for (int act = 2; act <= 10; act++) Gen(t, act, $"2_{act}_1");
        t.Tick(S(20), Play);
        Gen(t, 20, "2_11_endgame_town");
        Assert.Equal(TimerStatus.Finished, t.Status);
        Assert.NotNull(archived);
        Assert.False(archived!.Abandoned);
        Assert.Equal(S(20), archived.FinishedAt);
        var total = Total(t);
        t.Tick(S(100), Play);
        Assert.Equal(total, Total(t));
    }

    [Fact]
    public void MenuPausesAndOwnActZoneResumes()
    {
        var t = RunAtAct3();
        t.Tick(S(30), Play);
        Login(t, 30);
        t.Tick(S(90), Play);
        Assert.Equal(TimerStatus.Menu, t.Status);
        Gen(t, 90, "1_3_town"); Enter(t, 90);
        Assert.Equal(TimerStatus.Running, t.Status);
        t.Tick(S(100), Play);
        Assert.Equal(40, Total(t));
    }

    [Theory]
    [InlineData("2_7_town")]
    [InlineData("2_11_endgame_town")]
    [InlineData("MapWorldsCrimsonTownship")]
    public void FurtherZoneAfterLoginIsOtherCharacter(string id)
    {
        var t = RunAtAct3();
        t.Tick(S(30), Play);
        Login(t, 30);
        Gen(t, 31, id); Enter(t, 31);
        Assert.Equal(TimerStatus.OtherCharacter, t.Status);
        t.Tick(S(100), Play);
        Assert.Equal(30, Total(t));
        Assert.Equal(3, t.State.Run!.CurrentAct);
        Assert.Null(t.State.Run.FinishedAt);
    }

    [Fact]
    public void OtherCharacterDoesNotSplit()
    {
        var t = RunAtAct3();
        Login(t, 30);
        Gen(t, 31, "2_7_town"); Enter(t, 31);
        Gen(t, 40, "2_8_1");
        Assert.Equal(3, t.State.Run!.CurrentAct);
    }

    [Fact]
    public void NeutralZoneAfterLoginKeepsPreviousOwnership()
    {
        var t = RunAtAct3();
        Login(t, 30);
        Gen(t, 31, "HideoutPrimevalRuins"); Enter(t, 31);
        Assert.Equal(TimerStatus.Running, t.Status);

        Login(t, 40);
        Gen(t, 41, "2_7_town"); Enter(t, 41);
        Login(t, 50);
        Gen(t, 51, "HideoutPrimevalRuins"); Enter(t, 51);
        Assert.Equal(TimerStatus.OtherCharacter, t.Status);
    }

    [Fact]
    public void FirstLevelUpNamesTheRunAndLaterLevelUpsDecideOwnership()
    {
        var t = RunAtAct3();
        Level(t, 30, "Hero_Name");
        Assert.Equal("Hero_Name", t.State.Run!.CharacterName);
        Level(t, 40, "Alt_Name");
        Assert.Equal(TimerStatus.OtherCharacter, t.Status);
        Level(t, 50, "Hero_Name");
        Assert.Equal(TimerStatus.Running, t.Status);
    }

    [Fact]
    public void ForeignLevelUpDoesNotNameTheRun()
    {
        var t = RunAtAct3();
        Login(t, 30);
        Gen(t, 31, "2_7_town"); Enter(t, 31);
        Level(t, 40, "Alt_Name");
        Assert.Null(t.State.Run!.CharacterName);
    }

    [Fact]
    public void NewCharacterArchivesUnfinishedRun()
    {
        var t = RunAtAct3();
        var oldId = t.State.Run!.Id;
        RunState? archived = null;
        t.RunArchived += r => archived = r;
        Login(t, 30);
        Gen(t, 31, "1_1_1");
        Assert.NotNull(archived);
        Assert.True(archived!.Abandoned);
        Assert.Equal(oldId, archived.Id);
        Assert.NotEqual(oldId, t.State.Run!.Id);
        Assert.Equal(1, t.State.Run.CurrentAct);
    }

    [Fact]
    public void RelogInStrandDuringAct1KeepsRun()
    {
        var t = NewRun();
        var id = t.State.Run!.Id;
        t.Tick(S(10), Play);
        Login(t, 10);
        Gen(t, 12, "1_1_1"); Enter(t, 12);
        Assert.Equal(id, t.State.Run!.Id);
        Assert.Equal(10, Total(t));
    }

    [Fact]
    public void WalkingBackToStrandDoesNotRestart()
    {
        var t = NewRun();
        var id = t.State.Run!.Id;
        Gen(t, 10, "1_1_town");
        Gen(t, 20, "1_1_1");
        Assert.Equal(id, t.State.Run!.Id);
    }

    [Fact]
    public void GameRestartGapIsNotCounted()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        t.Apply(new GameStarted(S(3600)), S(3600), Play);
        Assert.Equal(TimerStatus.Menu, t.Status);
        Gen(t, 3605, "1_1_town"); Enter(t, 3610);
        t.Tick(S(3620), Play);
        Assert.Equal(20, Total(t));
    }

    [Fact]
    public void HaltSkipsInterval()
    {
        var t = NewRun();
        t.Tick(S(10), Play);
        t.Halt(S(100));
        t.Tick(S(110), Play);
        Assert.Equal(20, Total(t));
    }

    [Fact]
    public void ResetArchivesAndClearsRun()
    {
        var t = NewRun();
        RunState? archived = null;
        t.RunArchived += r => archived = r;
        t.Tick(S(10), Play);
        t.ResetRun(S(10), Play);
        Assert.True(archived!.Abandoned);
        Assert.Null(t.State.Run);
        Assert.Equal(TimerStatus.NoRun, t.Status);
    }

    [Fact]
    public void StartRunNowUsesLastSeenAct()
    {
        var t = new RunTimer(new TimerState(), TimeSpan.FromSeconds(20));
        t.Start(T0, Play);
        Gen(t, 0, "1_3_5");
        t.StartRunNow(S(5), Play);
        Assert.Equal(3, t.State.Run!.CurrentAct);
        Assert.Equal(TimerStatus.Running, t.Status);
        t.Tick(S(15), Play);
        Assert.Equal(10, Act(t, 3));
    }

    [Fact]
    public void LanguageEventIsRaised()
    {
        var t = NewRun();
        string? lang = null;
        t.LanguageChanged += l => lang = l;
        t.Apply(new LanguageDetected(S(1), "en"), S(1), Play);
        Assert.Equal("en", lang);
    }
}
