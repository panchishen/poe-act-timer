using PoeActTimer.Core;

namespace PoeActTimer.UI;

public sealed record OverlayView(
    TimerStatus Status,
    TimeSpan Total,
    int CurrentAct,
    TimeSpan[] ActTimes,
    string? CharacterName,
    TimeSpan Loading,
    bool ManualPause,
    bool LogMissing);
