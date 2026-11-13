using System.Text.Encodings.Web;
using System.Text.Json;

namespace PoeActTimer.Core;

public sealed class Storage
{
    const int LoadAttempts = 5;
    // Local files only: keep Cyrillic names and "—" readable instead of \uXXXX escapes.
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    readonly string directory;
    readonly TimeSpan retryDelay;

    public Storage(string directory, TimeSpan? retryDelay = null)
    {
        this.directory = directory;
        this.retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(200);
    }

    /// <summary>Receives a message when a file can't be read or written.</summary>
    public Action<string>? Log { get; set; }

    /// <summary>state.json existed but could not be read: it is never overwritten this session.</summary>
    public bool StateReadOnly { get; private set; }

    /// <summary>config.json existed but could not be read: it is never overwritten this session.</summary>
    public bool ConfigReadOnly { get; private set; }

    public string HistoryPath => Full("history.json");

    public AppConfig LoadConfig()
    {
        var config = Load<AppConfig>("config.json", LoadAttempts, out bool unreadable);
        ConfigReadOnly = unreadable;
        return config ?? new AppConfig();
    }

    public void SaveConfig(AppConfig config)
    {
        if (!ConfigReadOnly) Save("config.json", config);
    }

    public TimerState LoadState()
    {
        var state = Load<TimerState>("state.json", LoadAttempts, out bool unreadable);
        StateReadOnly = unreadable;
        return state ?? new TimerState();
    }

    /// <summary>Flushes pending history first, then writes state.json (unless it is read-only).</summary>
    public void SaveState(TimerState state)
    {
        FlushPendingHistory(state);
        if (!StateReadOnly) Save("state.json", state);
    }

    public List<RunState> LoadHistory() => Load<List<RunState>>("history.json", 1, out _) ?? new List<RunState>();

    /// <summary>Upserts the run into history.json; false if the history could not be read or written.</summary>
    public bool AppendHistory(RunState run)
    {
        var history = Load<List<RunState>>("history.json", 1, out bool unreadable) ?? new List<RunState>();
        if (unreadable) return false; // never overwrite history with a partial list
        int index = history.FindIndex(x => x.Id == run.Id);
        if (index >= 0) history[index] = run; else history.Add(run);
        return Save("history.json", history);
    }

    /// <summary>Queues an archived run; it stays in state.json until history.json accepts it.</summary>
    public void QueueHistory(TimerState state, RunState run)
    {
        int index = state.PendingHistory.FindIndex(x => x.Id == run.Id);
        if (index >= 0) state.PendingHistory[index] = run; else state.PendingHistory.Add(run);
    }

    public void FlushPendingHistory(TimerState state)
    {
        foreach (var run in state.PendingHistory.ToList())
        {
            if (!AppendHistory(run)) break;
            state.PendingHistory.Remove(run);
        }
    }

    public void SaveHistoryIfMissing()
    {
        try
        {
            if (!File.Exists(HistoryPath)) Save("history.json", new List<RunState>());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    string Full(string name) => Path.Combine(directory, name);

    T? Load<T>(string name, int attempts, out bool unreadable) where T : class
    {
        unreadable = false;
        var path = Full(name);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < attempts)
                {
                    Thread.Sleep(retryDelay);
                    continue;
                }
                unreadable = true;
                if (attempts > 1)
                    Log?.Invoke($"{name} could not be read ({ex.Message}); it will not be overwritten this session.");
                return null;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                var backup = BackupPath(path);
                try { File.Move(path, backup); }
                catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException) { }
                Log?.Invoke($"{name} is corrupt ({ex.Message}); moved to {Path.GetFileName(backup)}.");
                return null;
            }
        }
    }

    /// <summary>A timestamped backup name, so a later corruption never overwrites an earlier backup.</summary>
    static string BackupPath(string path)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var candidate = $"{path}.{stamp}.bak";
        for (int n = 2; File.Exists(candidate); n++) candidate = $"{path}.{stamp}-{n}.bak";
        return candidate;
    }

    bool Save<T>(string name, T value)
    {
        var path = Full(name);
        var tmp = path + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(tmp); }
            catch (Exception delEx) when (delEx is IOException or UnauthorizedAccessException) { }
            return false;
        }
    }
}
