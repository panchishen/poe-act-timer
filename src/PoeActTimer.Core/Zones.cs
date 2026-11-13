using System.Globalization;
using System.Text.RegularExpressions;

namespace PoeActTimer.Core;

public enum ZoneKind { Act, Epilogue, Endgame, Neutral }

public readonly record struct ZoneInfo(ZoneKind Kind, int Act);

public static class Zones
{
    public const string TwilightStrand = "1_1_1";

    static readonly Regex Campaign = new(@"^[12]_(\d+)_", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex LeagueTown = new(@"Town\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ZoneInfo Classify(string areaId)
    {
        var m = Campaign.Match(areaId);
        if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var act))
        {
            if (act is >= 1 and <= 10) return new(ZoneKind.Act, act);
            if (act == 11) return new(ZoneKind.Epilogue, 11);
        }
        if (areaId.StartsWith("Map", StringComparison.Ordinal) || LeagueTown.IsMatch(areaId))
            return new(ZoneKind.Endgame, 0);
        return new(ZoneKind.Neutral, 0);
    }
}
