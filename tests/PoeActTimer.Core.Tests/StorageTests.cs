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
        var backup = Assert.Single(Directory.GetFiles(dir, "state.json.*.bak"));
        Assert.Equal("{ not json", File.ReadAllText(backup));
        Assert.False(File.Exists(Path.Combine(dir, "state.json")));
        Assert.False(storage.StateReadOnly);
    }

    [Fact]
    public void CorruptFileThatCannotBeMovedIsNeverOverwritten()
    {
        var statePath = Path.Combine(dir, "state.json");
        var historyPath = Path.Combine(dir, "history.json");
        File.WriteAllText(statePath, "{ broken state");
        File.WriteAllText(historyPath, "{ broken history");
        var messages = new List<string>();
        var storage = new Storage(dir, TimeSpan.Zero) { Log = messages.Add };

        // Readers are allowed, delete/move is not: the backup move fails.
        using (new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (new FileStream(historyPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Null(storage.LoadState().Run);
            Assert.True(storage.StateReadOnly);
            Assert.False(storage.AppendHistory(new RunState()));
            Assert.Equal("{ broken history", File.ReadAllText(historyPath));
            Assert.Empty(Directory.GetFiles(dir, "*.bak"));
            Assert.DoesNotContain(messages, m => m.Contains("moved to"));
        }
        // The lock is gone, but state.json stays read-only for this session.
        storage.SaveState(new TimerState { LogOffset = 5 });
        Assert.Equal("{ broken state", File.ReadAllText(statePath));
        // history.json is re-read on every append: now it is backed up before a new one is written.
        Assert.True(storage.AppendHistory(new RunState()));
        Assert.Equal("{ broken history", File.ReadAllText(Assert.Single(Directory.GetFiles(dir, "history.json.*.bak"))));
    }

    [Fact]
    public void SecondCorruptionKeepsFirstBackup()
    {
        var path = Path.Combine(dir, "state.json");
        File.WriteAllText(path, "{ first");
        new Storage(dir).LoadState();
        File.WriteAllText(path, "{ second");
        new Storage(dir).LoadState();
        var contents = Directory.GetFiles(dir, "state.json.*.bak").Select(File.ReadAllText).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "{ first", "{ second" }, contents);
    }

    [Fact]
    public async Task LoadStateRetriesUntilLockIsReleased()
    {
        var path = Path.Combine(dir, "state.json");
        new Storage(dir).SaveState(new TimerState { LogOffset = 9 });
        var storage = new Storage(dir);
        var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var release = Task.Run(async () => { await Task.Delay(300); lockStream.Dispose(); });

        var loaded = storage.LoadState();

        await release;
        Assert.Equal(9, loaded.LogOffset);
        Assert.False(storage.StateReadOnly);
    }

    [Fact]
    public void LockedStateBecomesReadOnlyAndIsNotOverwritten()
    {
        var path = Path.Combine(dir, "state.json");
        new Storage(dir).SaveState(new TimerState { LogOffset = 9 });
        var storage = new Storage(dir);
        var messages = new List<string>();
        storage.Log = messages.Add;
        TimerState loaded;
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            loaded = storage.LoadState();

        Assert.Equal(0, loaded.LogOffset);
        Assert.True(storage.StateReadOnly);
        Assert.NotEmpty(messages);
        storage.SaveState(new TimerState { LogOffset = 5 });
        Assert.Equal(9, new Storage(dir).LoadState().LogOffset);
    }

    [Fact]
    public void LockedConfigBecomesReadOnlyAndIsNotOverwritten()
    {
        var path = Path.Combine(dir, "config.json");
        new Storage(dir).SaveConfig(new AppConfig { AfkSeconds = 42 });
        var storage = new Storage(dir);
        AppConfig loaded;
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            loaded = storage.LoadConfig();

        Assert.Equal(20, loaded.AfkSeconds);
        Assert.True(storage.ConfigReadOnly);
        storage.SaveConfig(new AppConfig { AfkSeconds = 7 });
        Assert.Equal(42, new Storage(dir).LoadConfig().AfkSeconds);
    }

    [Fact]
    public void SaveStateFlushesPendingHistory()
    {
        var storage = new Storage(dir);
        var state = new TimerState();
        var run = new RunState { Abandoned = true };
        storage.QueueHistory(state, run);
        storage.SaveState(state);
        Assert.Empty(state.PendingHistory);
        Assert.Equal(run.Id, Assert.Single(storage.LoadHistory()).Id);
        Assert.Empty(storage.LoadState().PendingHistory);
    }

    [Fact]
    public void PendingHistorySurvivesUnwritableHistory()
    {
        var storage = new Storage(dir);
        var first = new RunState();
        Assert.True(storage.AppendHistory(first));
        var state = new TimerState();
        var run = new RunState { Abandoned = true };
        using (new FileStream(storage.HistoryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            storage.QueueHistory(state, run);
            storage.SaveState(state);
            Assert.False(storage.AppendHistory(new RunState()));
        }
        Assert.Equal(run.Id, Assert.Single(state.PendingHistory).Id);
        var reloaded = new Storage(dir).LoadState();
        Assert.Equal(run.Id, Assert.Single(reloaded.PendingHistory).Id);

        storage.SaveState(reloaded);
        Assert.Empty(reloaded.PendingHistory);
        Assert.Equal(new[] { first.Id, run.Id }, storage.LoadHistory().Select(r => r.Id));
    }

    [Fact]
    public void QueueHistoryReplacesSameRun()
    {
        var storage = new Storage(dir);
        var state = new TimerState();
        var run = new RunState();
        storage.QueueHistory(state, run);
        storage.QueueHistory(state, run);
        Assert.Single(state.PendingHistory);
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

    [Fact]
    public void SaveWhileFileLockedDoesNotThrow()
    {
        var storage = new Storage(dir);
        var path = Path.Combine(dir, "state.json");
        File.WriteAllText(path, "{}");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            storage.SaveState(new TimerState { LogOffset = 5 });
        }
        Assert.Equal(0, storage.LoadState().LogOffset);
        storage.SaveState(new TimerState { LogOffset = 7 });
        Assert.Equal(7, storage.LoadState().LogOffset);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void LoadWhileFileLockedReturnsDefaultsWithoutBackup()
    {
        var storage = new Storage(dir);
        storage.SaveState(new TimerState { LogOffset = 9 });
        var path = Path.Combine(dir, "state.json");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(0, storage.LoadState().LogOffset);
        }
        Assert.False(File.Exists(path + ".bak"));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void AppendHistoryWhileLockedDoesNotOverwrite()
    {
        var storage = new Storage(dir);
        var original = new RunState();
        storage.AppendHistory(original);
        using (new FileStream(storage.HistoryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Write | FileShare.Delete))
        {
            storage.AppendHistory(new RunState());
        }
        Assert.Equal(original.Id, Assert.Single(storage.LoadHistory()).Id);
    }
}
