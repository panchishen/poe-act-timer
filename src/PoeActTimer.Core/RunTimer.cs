namespace PoeActTimer.Core;

public enum TimerStatus { NoRun, Running, Afk, Menu, Minimized, OtherCharacter, ManualPause, Finished }

public readonly record struct GameSnapshot(bool GameRunning, bool Focused, TimeSpan Idle);

public sealed class RunTimer
{
    static readonly TimeSpan SegmentWindow = TimeSpan.FromMinutes(10);

    readonly TimeSpan afkThreshold;
    readonly List<Segment> segments = new();
    DateTime lastTick;
    bool started;

    public RunTimer(TimerState state, TimeSpan afkThreshold)
    {
        State = state;
        this.afkThreshold = afkThreshold;
    }

    public TimerState State { get; }
    public TimerStatus Status { get; private set; } = TimerStatus.NoRun;
    public event Action<RunState>? RunArchived;
    public event Action<string>? LanguageChanged;

    bool IsActive => State.Run is { FinishedAt: null };

    public void Start(DateTime at, GameSnapshot snap)
    {
        lastTick = at;
        started = true;
        Status = Compute(snap);
    }

    public void Halt(DateTime at)
    {
        if (at > lastTick) lastTick = at;
    }

    public void Tick(DateTime now, GameSnapshot snap)
    {
        if (!started) { Start(now, snap); return; }
        var next = Compute(snap);
        if (now > lastTick)
        {
            if (Status == TimerStatus.Running) Credit(lastTick, now);
            lastTick = now;
        }
        if (next == TimerStatus.Afk && Status != TimerStatus.Afk) Deduct(now - snap.Idle, now);
        Status = next;
    }

    public void Apply(LogEvent e, DateTime at, GameSnapshot snap)
    {
        if (!started) Start(at, snap);
        else if (e is GameStarted) Halt(at);
        else Tick(at, snap);
        Handle(e, at);
        Status = Compute(snap);
    }

    public void ToggleManualPause(DateTime now, GameSnapshot snap)
    {
        Tick(now, snap);
        State.ManualPause = !State.ManualPause;
        Status = Compute(snap);
    }

    public void ResetRun(DateTime now, GameSnapshot snap)
    {
        Tick(now, snap);
        if (IsActive) Archive(abandoned: true);
        State.Run = null;
        segments.Clear();
        Status = Compute(snap);
    }

    public void StartRunNow(DateTime now, GameSnapshot snap)
    {
        Tick(now, snap);
        if (IsActive) Archive(abandoned: true);
        StartRun(now);
        State.Run!.CurrentAct = Math.Max(1, State.LastAct);
        State.InMenu = false;
        State.AwaitFirstZone = false;
        Status = Compute(snap);
    }

    void Handle(LogEvent e, DateTime at)
    {
        var s = State;
        switch (e)
        {
            case GameStarted:
            case LoginConnecting:
                s.InMenu = true;
                s.AwaitFirstZone = true;
                break;
            case GameClosed:
                s.InMenu = true;
                break;
            case AreaEntered:
                s.InMenu = false;
                break;
            case FocusChanged f:
                s.LogFocused = f.Focused;
                break;
            case LanguageDetected l:
                LanguageChanged?.Invoke(l.Language);
                break;
            case LoadingScreen l when IsActive && !s.Foreign:
                var duration = TimeSpan.FromSeconds(l.Seconds);
                s.Run!.LoadingTicks += duration.Ticks;
                Deduct(at - duration, at);
                break;
            case LevelUp u:
                OnLevelUp(u.CharacterName);
                break;
            case AreaGenerated g:
                OnAreaGenerated(g.AreaId, at);
                break;
        }
    }

    void OnAreaGenerated(string areaId, DateTime at)
    {
        var s = State;
        var zone = Zones.Classify(areaId);
        if (zone.Kind == ZoneKind.Act) s.LastAct = zone.Act;

        if (s.AwaitFirstZone)
        {
            s.AwaitFirstZone = false;
            if (areaId == Zones.TwilightStrand)
            {
                if (IsActive && s.Run!.CurrentAct == 1) { s.Foreign = false; return; }
                if (IsActive) Archive(abandoned: true);
                StartRun(at);
                return;
            }
            if (!IsActive) return;
            s.Foreign = zone.Kind switch
            {
                ZoneKind.Act => zone.Act > s.Run!.CurrentAct,
                ZoneKind.Epilogue or ZoneKind.Endgame => true,
                _ => s.Foreign,
            };
            return;
        }

        if (!IsActive || s.Foreign) return;
        if (zone.Kind == ZoneKind.Act && zone.Act > s.Run!.CurrentAct)
        {
            s.Run.CurrentAct = zone.Act;
        }
        else if (zone.Kind == ZoneKind.Epilogue)
        {
            s.Run!.FinishedAt = at;
            Archive(abandoned: false);
        }
    }

    void OnLevelUp(string name)
    {
        var s = State;
        if (!IsActive) return;
        if (s.Run!.CharacterName is null)
        {
            if (!s.Foreign) s.Run.CharacterName = name;
            return;
        }
        s.Foreign = !string.Equals(name, s.Run.CharacterName, StringComparison.Ordinal);
    }

    void StartRun(DateTime at)
    {
        State.Run = new RunState { StartedAt = at };
        State.Foreign = false;
        segments.Clear();
    }

    void Archive(bool abandoned)
    {
        var run = State.Run!;
        run.Abandoned = abandoned;
        RunArchived?.Invoke(run);
    }

    TimerStatus Compute(GameSnapshot snap)
    {
        var s = State;
        if (s.Run is null) return TimerStatus.NoRun;
        if (s.Run.FinishedAt is not null) return TimerStatus.Finished;
        if (s.ManualPause) return TimerStatus.ManualPause;
        if (!snap.GameRunning || !snap.Focused) return TimerStatus.Minimized;
        if (s.InMenu) return TimerStatus.Menu;
        if (s.Foreign) return TimerStatus.OtherCharacter;
        if (snap.Idle >= afkThreshold) return TimerStatus.Afk;
        return TimerStatus.Running;
    }

    readonly record struct Segment(DateTime From, DateTime To, int Act);

    void Credit(DateTime from, DateTime to)
    {
        var run = State.Run!;
        int act = run.CurrentAct;
        run.ActTicks[act] += (to - from).Ticks;
        if (segments.Count > 0 && segments[^1].To == from && segments[^1].Act == act)
            segments[^1] = segments[^1] with { To = to };
        else
            segments.Add(new Segment(from, to, act));
        var horizon = to - SegmentWindow;
        segments.RemoveAll(x => x.To < horizon);
    }

    void Deduct(DateTime from, DateTime to)
    {
        if (State.Run is null || to <= from) return;
        var kept = new List<Segment>(segments.Count + 1);
        foreach (var seg in segments)
        {
            var overlapFrom = seg.From > from ? seg.From : from;
            var overlapTo = seg.To < to ? seg.To : to;
            if (overlapTo <= overlapFrom) { kept.Add(seg); continue; }
            State.Run.ActTicks[seg.Act] -= (overlapTo - overlapFrom).Ticks;
            if (seg.From < overlapFrom) kept.Add(seg with { To = overlapFrom });
            if (overlapTo < seg.To) kept.Add(seg with { From = overlapTo });
        }
        segments.Clear();
        segments.AddRange(kept);
    }
}
