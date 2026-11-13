using System.Text.Json;

namespace PoeActTimer.Core;

public sealed class Storage
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    readonly string directory;

    public Storage(string directory) => this.directory = directory;

    public string HistoryPath => Full("history.json");

    public AppConfig LoadConfig() => Load<AppConfig>("config.json") ?? new AppConfig();
    public void SaveConfig(AppConfig config) => Save("config.json", config);

    public TimerState LoadState() => Load<TimerState>("state.json") ?? new TimerState();
    public void SaveState(TimerState state) => Save("state.json", state);

    public List<RunState> LoadHistory() => Load<List<RunState>>("history.json") ?? new List<RunState>();

    public void AppendHistory(RunState run)
    {
        var history = LoadHistory();
        int index = history.FindIndex(x => x.Id == run.Id);
        if (index >= 0) history[index] = run; else history.Add(run);
        Save("history.json", history);
    }

    public void SaveHistoryIfMissing()
    {
        if (!File.Exists(HistoryPath)) Save("history.json", new List<RunState>());
    }

    string Full(string name) => Path.Combine(directory, name);

    T? Load<T>(string name) where T : class
    {
        var path = Full(name);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            File.Move(path, path + ".bak", overwrite: true);
            return null;
        }
    }

    void Save<T>(string name, T value)
    {
        var path = Full(name);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
        File.Move(tmp, path, overwrite: true);
    }
}
