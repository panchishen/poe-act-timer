namespace PoeActTimer.Core;

public abstract record LogEvent(DateTime At);
public sealed record AreaGenerated(DateTime At, int Level, string AreaId) : LogEvent(At);
public sealed record AreaEntered(DateTime At, string SceneName) : LogEvent(At);
public sealed record LoadingScreen(DateTime At, double Seconds) : LogEvent(At);
public sealed record LoginConnecting(DateTime At) : LogEvent(At);
public sealed record LevelUp(DateTime At, string CharacterName, int Level) : LogEvent(At);
public sealed record FocusChanged(DateTime At, bool Focused) : LogEvent(At);
public sealed record GameStarted(DateTime At) : LogEvent(At);
public sealed record GameClosed(DateTime At) : LogEvent(At);
public sealed record LanguageDetected(DateTime At, string Language) : LogEvent(At);
