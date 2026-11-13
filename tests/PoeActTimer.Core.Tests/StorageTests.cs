using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public sealed class StorageTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("poe-storage-").FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    [Fact]
    public void DefaultsWhenFilesMissing()
    {
        var storage = new Storage(dir);
        Assert.Equal(20, storage.LoadConfig().AfkSeconds);
        Assert.Null(storage.LoadState().Run);
        Assert.Empty(storage.LoadHistory());
    }

    [Fact]
    public void RoundTripsState()
    {
        var storage = new Storage(dir);
        var run = new RunState { StartedAt = new DateTime(2026, 11, 13), CharacterName = "Hero_Name", CurrentAct = 4 };
        run.ActTicks[1] = TimeSpan.FromMinutes(28).Ticks;
        run.Recovered.Add(new RecoveredSpan(new DateTime(2026, 11, 13, 1, 0, 0), new DateTime(2026, 11, 13, 1, 5, 0), 100));
        storage.SaveState(new TimerState { Run = run, LogOffset = 123, Foreign = true });

        var loaded = storage.LoadState();

        Assert.Equal(run.Id, loaded.Run!.Id);
        Assert.Equal("Hero_Name", loaded.Run.CharacterName);
        Assert.Equal(4, loaded.Run.CurrentAct);
        Assert.Equal(TimeSpan.FromMinutes(28), loaded.Run.ActTime(1));
        Assert.Equal(100, Assert.Single(loaded.Run.Recovered).CountedTicks);
        Assert.Equal(123, loaded.LogOffset);
        Assert.True(loaded.Foreign);
    }

    [Fact]
    public void CorruptStateIsBackedUp()
    {
        File.WriteAllText(Path.Combine(dir, "state.json"), "{ not json");
        var storage = new Storage(dir);
        Assert.Null(storage.LoadState().Run);
        Assert.True(File.Exists(Path.Combine(dir, "state.json.bak")));
        Assert.False(File.Exists(Path.Combine(dir, "state.json")));
    }

    [Fact]
    public void AppendHistoryReplacesSameRun()
    {
        var storage = new Storage(dir);
        var run = new RunState { Abandoned = true };
        storage.AppendHistory(run);
        run.Abandoned = false;
        storage.AppendHistory(run);
        storage.AppendHistory(new RunState());
        var history = storage.LoadHistory();
        Assert.Equal(2, history.Count);
        Assert.False(history[0].Abandoned);
    }
}
