using System.Text.Json.Serialization;

namespace PoeActTimer.Core;

public sealed record RecoveredSpan(DateTime From, DateTime To, long CountedTicks)
{
    /// <summary>Readable copy for history.json; ignored when loading.</summary>
    public string CountedText => TimeFormat.FormatHms(TimeSpan.FromTicks(CountedTicks));
}

public sealed class RunState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? CharacterName { get; set; }
    public int CurrentAct { get; set; } = 1;
    public long[] ActTicks { get; set; } = new long[11];
    public long LoadingTicks { get; set; }
    public bool Abandoned { get; set; }
    public List<RecoveredSpan> Recovered { get; set; } = new();

    [JsonIgnore]
    public TimeSpan Total => TimeSpan.FromTicks(ActTicks.Sum());

    // Readable copies for history.json (h:mm:ss). Read-only, so they are ignored when loading;
    // the raw ticks above stay the source of truth.
    public string TotalText => TimeFormat.FormatHms(Total);

    /// <summary>Acts 1..10; "—" for an act that was not played.</summary>
    public string[] ActsText => Enumerable.Range(1, 10)
        .Select(act => ActTicks[act] > 0 || act == CurrentAct ? TimeFormat.FormatHms(ActTime(act)) : "—")
        .ToArray();

    public string LoadingText => TimeFormat.FormatHms(TimeSpan.FromTicks(LoadingTicks));

    public TimeSpan ActTime(int act) => TimeSpan.FromTicks(ActTicks[act]);
}

public sealed class TimerState
{
    public RunState? Run { get; set; }
    public bool InMenu { get; set; } = true;
    public bool AwaitFirstZone { get; set; } = true;
    public bool Foreign { get; set; }
    public bool ManualPause { get; set; }
    public bool LogFocused { get; set; } = true;
    public int LastAct { get; set; }
    /// <summary>Set when the first zone after the latest login was the Twilight Strand.</summary>
    public DateTime? StrandLoginAt { get; set; }
    public string? LogPath { get; set; }
    public long LogOffset { get; set; }
    public DateTime SavedAt { get; set; }
    /// <summary>Archived runs not yet written to history.json (kept here so they are never lost).</summary>
    public List<RunState> PendingHistory { get; set; } = new();
}
