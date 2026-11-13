using System.Text.Json;
using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public class HistoryTextTests
{
    static RunState SampleRun()
    {
        var run = new RunState { CurrentAct = 3, LoadingTicks = TimeSpan.FromSeconds(95).Ticks };
        run.ActTicks[1] = TimeSpan.FromMinutes(28).Add(TimeSpan.FromSeconds(5)).Ticks;
        run.ActTicks[2] = TimeSpan.FromMinutes(40).Ticks;
        run.ActTicks[3] = TimeSpan.FromSeconds(7).Ticks;
        run.Recovered.Add(new RecoveredSpan(new DateTime(2026, 11, 13, 1, 0, 0), new DateTime(2026, 11, 13, 1, 5, 0), TimeSpan.FromSeconds(250).Ticks));
        return run;
    }

    [Fact]
    public void FormatHmsAlwaysShowsHours()
    {
        Assert.Equal("0:00:07", TimeFormat.FormatHms(TimeSpan.FromSeconds(7)));
        Assert.Equal("1:08:05", TimeFormat.FormatHms(new TimeSpan(1, 8, 5)));
        Assert.Equal("26:00:00", TimeFormat.FormatHms(TimeSpan.FromHours(26)));
    }

    [Fact]
    public void RunExposesReadableTexts()
    {
        var run = SampleRun();
        Assert.Equal("1:08:12", run.TotalText);
        Assert.Equal("0:01:35", run.LoadingText);
        Assert.Equal(10, run.ActsText.Length);
        Assert.Equal("0:28:05", run.ActsText[0]);
        Assert.Equal("0:40:00", run.ActsText[1]);
        Assert.Equal("0:00:07", run.ActsText[2]);
        Assert.Equal("—", run.ActsText[3]);
        Assert.Equal("—", run.ActsText[9]);
        Assert.Equal("0:04:10", run.Recovered[0].CountedText);
    }

    [Fact]
    public void ReadableTextsAreSerializedAndIgnoredOnLoad()
    {
        var run = SampleRun();
        var json = JsonSerializer.Serialize(run);
        Assert.Contains("\"TotalText\":\"1:08:12\"", json);
        Assert.Contains("\"ActsText\":[", json);
        Assert.Contains("\"LoadingText\":\"0:01:35\"", json);
        Assert.Contains("\"CountedText\":\"0:04:10\"", json);

        var loaded = JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(run.Id, loaded.Id);
        Assert.Equal(run.ActTicks, loaded.ActTicks);
        Assert.Equal(run.LoadingTicks, loaded.LoadingTicks);
        Assert.Equal(run.Recovered[0], loaded.Recovered[0]);
        Assert.Equal("1:08:12", loaded.TotalText);
    }

    [Fact]
    public void HistoryFileIsReadableAndRoundTrips()
    {
        var dir = Directory.CreateTempSubdirectory("poe-history-").FullName;
        try
        {
            var storage = new Storage(dir);
            var run = SampleRun();
            run.CharacterName = "Герой_Тест";
            Assert.True(storage.AppendHistory(run));
            var text = File.ReadAllText(storage.HistoryPath);
            Assert.Contains("\"—\"", text);
            Assert.Contains("Герой_Тест", text);
            Assert.Contains("\"TotalText\": \"1:08:12\"", text);
            var loaded = Assert.Single(storage.LoadHistory());
            Assert.Equal(run.ActTicks, loaded.ActTicks);
            Assert.Equal("Герой_Тест", loaded.CharacterName);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
