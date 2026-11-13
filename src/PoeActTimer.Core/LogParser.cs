using System.Globalization;
using System.Text.RegularExpressions;

namespace PoeActTimer.Core;

public static class LogParser
{
    const RegexOptions Opt = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    static readonly Regex Header = new(@"^(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}) (?:\d+ \S+ \[\w+ Client \d+\] )?(.*)$", Opt);
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
        var header = Header.Match(line.TrimEnd('\r', '\n'));
        if (!header.Success) return [];
        var at = DateTime.ParseExact(header.Groups[1].Value, "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        var body = header.Groups[2].Value;
        Match m;

        if (body == "***** LOG FILE OPENING *****") return [new GameStarted(at)];
        if (body == "Closing game gracefully") return [new GameClosed(at)];
        if ((m = Generating.Match(body)).Success)
            return [new AreaGenerated(at, Int(m.Groups[1].Value), m.Groups[2].Value)];
        if ((m = Scene.Match(body)).Success)
        {
            var scene = m.Groups[1].Value;
            return scene is "(null)" or "(unknown)" ? [] : [new AreaEntered(at, scene)];
        }
        if ((m = Loading.Match(body)).Success)
            return [new LoadingScreen(at, double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))];
        if (Login.IsMatch(body)) return [new LoginConnecting(at)];
        if ((m = Focus.Match(body)).Success) return [new FocusChanged(at, m.Groups[1].Value == "Gained")];
        if ((m = LevelUpRu.Match(body)).Success)
            return [new LevelUp(at, m.Groups[1].Value, Int(m.Groups[2].Value)), new LanguageDetected(at, "ru")];
        if ((m = LevelUpEn.Match(body)).Success)
            return [new LevelUp(at, m.Groups[1].Value, Int(m.Groups[2].Value)), new LanguageDetected(at, "en")];
        if (EnteredRu.IsMatch(body)) return [new LanguageDetected(at, "ru")];
        if (EnteredEn.IsMatch(body)) return [new LanguageDetected(at, "en")];
        return [];
    }

    static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);
}
