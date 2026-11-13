using System.Diagnostics;
using System.Globalization;
using PoeActTimer.Core;
using PoeActTimer.Infra;
using PoeActTimer.UI;

namespace PoeActTimer;

public sealed class AppController : IDisposable
{
    readonly Storage storage = new(AppContext.BaseDirectory) { Log = LogMessage };
    readonly AppConfig config;
    readonly TimerState state;
    readonly RunTimer timer;
    readonly GameWatcher watcher = new();
    readonly OverlayForm overlay = new();
    readonly TrayIcon tray;
    readonly HotkeyManager hotkeys = new();
    readonly System.Windows.Forms.Timer loop = new() { Interval = 250 };
    Strings strings;
    LogTailer? tailer;
    DateTime lastSave = DateTime.MinValue, lastLogProbe = DateTime.MinValue;
    TimerStatus lastSavedStatus;
    GameSnapshot lastSnap;
    bool disposed, dialogOpen;
    static readonly TimeSpan LiveMaxGap = TimeSpan.FromSeconds(5);

    public AppController()
    {
        config = storage.LoadConfig();
        config.Opacity = Math.Clamp(config.Opacity, 0.2, 1.0);
        config.AfkSeconds = Math.Max(5, config.AfkSeconds);
        if (string.IsNullOrWhiteSpace(config.HotkeyPause)) config.HotkeyPause = "Ctrl+Alt+P";
        if (string.IsNullOrWhiteSpace(config.HotkeyToggle)) config.HotkeyToggle = "Ctrl+Alt+E";
        if (string.IsNullOrWhiteSpace(config.HotkeyReset)) config.HotkeyReset = "Ctrl+Alt+R";
        state = storage.LoadState();
        timer = new RunTimer(state, TimeSpan.FromSeconds(config.AfkSeconds), LiveMaxGap);
        // Archived runs wait in state.json until history.json accepts them (flushed on every save).
        timer.RunArchived += run => storage.QueueHistory(state, run);
        timer.LanguageChanged += OnLanguage;
        strings = Strings.For(config.Language ?? DefaultLanguage());
        if (!FolderWritable(AppContext.BaseDirectory))
            MessageBox.Show(strings.FolderNotWritable, "PoE Act Timer", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        tray = new TrayIcon(Safe(TogglePause), Safe(StartNow), Safe(Reset), Safe(OpenHistory), Safe(ChooseLog), Safe(Shutdown));

        overlay.BackgroundOpacity = config.Opacity;
        overlay.Location = InitialLocation();
        overlay.PauseClicked += Safe(TogglePause);
        overlay.ExpandToggled += Safe(ToggleExpanded);
        overlay.Moved += p =>
        {
            try { config.OverlayX = p.X; config.OverlayY = p.Y; storage.SaveConfig(config); }
            catch (Exception) { }
        };
        overlay.Show();
        overlay.Expanded = config.Expanded;

        // A failed registration is ignored: tray and overlay buttons still work.
        hotkeys.Register(config.HotkeyPause, Safe(TogglePause));
        hotkeys.Register(config.HotkeyToggle, Safe(ToggleExpanded));
        hotkeys.Register(config.HotkeyReset, Safe(Reset));

        lastSnap = watcher.Snapshot();
        TryAttachLog(DateTime.Now, allowCatchUp: true);
        loop.Tick += (_, _) => Step();
        loop.Start();
        Step();
    }

    static Action Safe(Action action) => () =>
    {
        try { action(); }
        catch (Exception ex) { LogError(ex); }
    };

    static void LogError(Exception ex) => LogMessage($"{ex.GetType().Name}: {ex.Message}");

    internal static void LogMessage(string text)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "error.log");
            var info = new FileInfo(path);
            if (info.Exists && info.Length > 1_000_000) info.Delete();
            File.AppendAllText(path, $"{DateTime.Now:O} {text}{Environment.NewLine}");
        }
        catch (Exception)
        {
        }
    }

    static bool FolderWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    void TryAttachLog(DateTime now, bool allowCatchUp)
    {
        try { AttachLog(now, allowCatchUp); }
        catch (Exception ex)
        {
            LogError(ex);
            tailer = null;
            timer.Start(now, lastSnap);
        }
    }

    static string DefaultLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";

    Point InitialLocation()
    {
        if (config.OverlayX is int x && config.OverlayY is int y &&
            Screen.AllScreens.Any(s => s.WorkingArea.Contains(x + 20, y + 20)))
            return new Point(x, y);
        var area = Screen.PrimaryScreen!.WorkingArea;
        return new Point(area.Right - (int)(240 * overlay.DeviceDpi / 96f) - 16, area.Top + 16);
    }

    void AttachLog(DateTime now, bool allowCatchUp)
    {
        var path = ClientLogLocator.Find(config.LogPath);
        if (path is null)
        {
            tailer = null;
            timer.Start(now, lastSnap);
            return;
        }
        if (config.LogPath != path) { config.LogPath = path; storage.SaveConfig(config); }

        long length = LogTailer.CurrentLength(path);
        // Replay even without an active run: it restores menu/first-zone state and catches a run
        // that started while the timer was off. Without a run nothing is credited.
        bool catchUp = allowCatchUp && state.LogPath == path
            && state.LogOffset > 0 && state.LogOffset <= length;
        if (catchUp)
        {
            tailer = new LogTailer(path, state.LogOffset);
            bool finishedBefore = state.Run is { FinishedAt: not null };
            LogCatchUp.Replay(timer, tailer.ReadAllNewLines(), state.SavedAt, now, lastSnap.GameRunning, lastSnap.Idle);
            // A run that finished during replay was archived before its recovered span was added: re-queue it.
            if (!finishedBefore && state.Run is { FinishedAt: not null } finished)
                storage.QueueHistory(state, finished);
        }
        else
        {
            tailer = new LogTailer(path, length);
            timer.Start(now, lastSnap);
        }
        state.LogPath = path;
        Save(now);
    }

    void Step()
    {
        if (disposed) return;
        try
        {
            var now = DateTime.Now;
            lastSnap = watcher.Snapshot();
            // Before applying new lines: a sleep gap must not be credited.
            timer.Tick(now, lastSnap, LiveMaxGap);

            if (tailer is null && (now - lastLogProbe).TotalSeconds >= 5)
            {
                lastLogProbe = now;
                if (ClientLogLocator.Find(config.LogPath) is not null) TryAttachLog(now, allowCatchUp: false);
            }

            if (tailer is not null)
            {
                try
                {
                    foreach (var line in tailer.ReadNewLines())
                        foreach (var e in LogParser.Parse(line))
                            timer.Apply(e, now, lastSnap);
                }
                catch (IOException)
                {
                }
            }

            timer.Tick(now, lastSnap, LiveMaxGap);
            overlay.SetTopMost(lastSnap.Focused);
            Present();
            if (timer.Status != lastSavedStatus || (now - lastSave).TotalSeconds >= 5) Save(now);
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    void Present()
    {
        var run = state.Run;
        var acts = new TimeSpan[11];
        if (run is not null)
            for (int act = 1; act <= 10; act++) acts[act] = run.ActTime(act);
        var view = new OverlayView(
            timer.Status,
            run?.Total ?? TimeSpan.Zero,
            run?.CurrentAct ?? 0,
            acts,
            run?.CharacterName,
            TimeSpan.FromTicks(run?.LoadingTicks ?? 0),
            state.ManualPause,
            tailer is null);
        var hints = new List<(string, string)>
        {
            (strings.HotkeyPause, config.HotkeyPause),
            (strings.HotkeyToggle, config.HotkeyToggle),
            (strings.HotkeyReset, config.HotkeyReset),
        };
        overlay.Present(view, strings, hints);
        tray.Update(strings, timer.Status, state.ManualPause);
    }

    void Save(DateTime now)
    {
        state.SavedAt = now;
        state.LogOffset = tailer?.Offset ?? state.LogOffset;
        storage.SaveState(state);
        lastSave = now;
        lastSavedStatus = timer.Status;
    }

    void OnLanguage(string language)
    {
        if (config.Language == language) return;
        config.Language = language;
        storage.SaveConfig(config);
        strings = Strings.For(language);
    }

    void TogglePause()
    {
        if (disposed) return;
        timer.ToggleManualPause(DateTime.Now, watcher.Snapshot());
        Save(DateTime.Now);
        Present();
    }

    void ToggleExpanded()
    {
        if (disposed) return;
        overlay.Expanded = !overlay.Expanded;
        config.Expanded = overlay.Expanded;
        storage.SaveConfig(config);
        Present();
    }

    bool Confirm(string text)
    {
        dialogOpen = true;
        try
        {
            using var owner = new Form { TopMost = true, ShowInTaskbar = false };
            return MessageBox.Show(owner, text, "PoE Act Timer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
        finally { dialogOpen = false; }
    }

    void Reset()
    {
        if (disposed || dialogOpen) return;
        if (state.Run is null || !Confirm(strings.ResetConfirm) || disposed) return;
        timer.ResetRun(DateTime.Now, watcher.Snapshot());
        Save(DateTime.Now);
        Present();
    }

    void StartNow()
    {
        if (disposed || dialogOpen) return;
        if (state.Run is { FinishedAt: null } && (!Confirm(strings.StartNowConfirm) || disposed)) return;
        timer.StartRunNow(DateTime.Now, watcher.Snapshot());
        Save(DateTime.Now);
        Present();
    }

    void OpenHistory()
    {
        if (disposed) return;
        storage.SaveHistoryIfMissing();
        Process.Start(new ProcessStartInfo(storage.HistoryPath) { UseShellExecute = true });
    }

    void ChooseLog()
    {
        if (disposed || dialogOpen) return;
        string fileName;
        dialogOpen = true;
        try
        {
            using var dialog = new OpenFileDialog { Filter = "Client.txt|Client.txt|*.txt|*.txt", FileName = "Client.txt" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            fileName = dialog.FileName;
        }
        finally { dialogOpen = false; }
        if (disposed) return;
        config.LogPath = fileName;
        storage.SaveConfig(config);
        TryAttachLog(DateTime.Now, allowCatchUp: false);
    }

    public void Shutdown()
    {
        if (disposed) return;
        loop.Stop();
        Save(DateTime.Now);
        Dispose();
        Application.Exit();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        loop.Dispose();
        hotkeys.Dispose();
        tray.Dispose();
        overlay.Dispose();
    }
}
