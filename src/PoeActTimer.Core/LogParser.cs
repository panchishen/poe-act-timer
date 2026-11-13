using System.Globalization;
using System.Text.RegularExpressions;

namespace PoeActTimer.Core;

public static class LogParser
{
    const RegexOptions Opt = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    const string TimestampFormat = "yyyy/MM/dd HH:mm:ss";

    static readonly Regex LogOpening = new(@"^(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}) \*\*\*\*\* LOG FILE OPENING \*\*\*\*\*$", Opt);
    static readonly Regex Header = new(@"^(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}) \d+ \S+ \[\w+ Client \d+\] (.*)$", Opt);
    static readonly Regex Generating = new(@"^Generating level (\d+) area ""([^""]+)""", Opt);
    static readonly Regex Scene = new(@"^\[SCENE\] Set Source \[(.*)\]$", Opt);
    static readonly Regex Loading = new(@"^\[LOADING SCREEN\] .* Duration = ([\d.]+) seconds", Opt);
    static readonly Regex Login = new(@"^Async connecting to \S*login\.pathofexile\.com", Opt);
    static readonly Regex Focus = new(@"^\[WINDOW\] (Lost|Gained) focus", Opt);
    static readonly Regex LevelUpRu = new(@"^: (\S+) \([^)]+\) достигает (\d+) уровня", Opt);
    static readonly Regex LevelUpEn = new(@"^: (\S+) \([^)]+\) is now level (\d+)", Opt);
    static readonly Regex EnteredRu = new(@"^: (?:<<set:[^>]*>>)*Вы вошли в область ", Opt);
    static readonly Regex EnteredEn = new(@"^: (?:<<set:[^>]*>>)*You have entered ", Opt);

    public static IReadOnlyList<LogEvent> Parse(string line)
    {
        var trimmed = line.TrimEnd('\r', '\n');

        var opening = LogOpening.Match(trimmed);
        if (opening.Success)
            return TryTimestamp(opening.Groups[1].Value, out var openedAt) ? [new GameStarted(openedAt)] : [];

        var header = Header.Match(trimmed);
        if (!header.Success || !TryTimestamp(header.Groups[1].Value, out var at)) return [];
        var body = header.Groups[2].Value;
        Match m;

        if (body == "Closing game gracefully") return [new GameClosed(at)];
        if ((m = Generating.Match(body)).Success)
            return TryInt(m.Groups[1].Value, out var areaLevel) ? [new AreaGenerated(at, areaLevel, m.Groups[2].Value)] : [];
        if ((m = Scene.Match(body)).Success)
        {
            var scene = m.Groups[1].Value;
            return scene is "(null)" or "(unknown)" ? [] : [new AreaEntered(at, scene)];
        }
        if ((m = Loading.Match(body)).Success)
            return TryDouble(m.Groups[1].Value, out var seconds) ? [new LoadingScreen(at, seconds)] : [];
        if (Login.IsMatch(body)) return [new LoginConnecting(at)];
        if ((m = Focus.Match(body)).Success) return [new FocusChanged(at, m.Groups[1].Value == "Gained")];
        if ((m = LevelUpRu.Match(body)).Success)
            return TryInt(m.Groups[2].Value, out var ruLevel)
                ? [new LevelUp(at, m.Groups[1].Value, ruLevel), new LanguageDetected(at, "ru")]
                : [];
        if ((m = LevelUpEn.Match(body)).Success)
            return TryInt(m.Groups[2].Value, out var enLevel)
                ? [new LevelUp(at, m.Groups[1].Value, enLevel), new LanguageDetected(at, "en")]
                : [];
        if (EnteredRu.IsMatch(body)) return [new LanguageDetected(at, "ru")];
        if (EnteredEn.IsMatch(body)) return [new LanguageDetected(at, "en")];
        return [];
    }

    static bool TryTimestamp(string s, out DateTime value) =>
        DateTime.TryParseExact(s, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    static bool TryInt(string s, out int value) =>
        int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    static bool TryDouble(string s, out double value) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
