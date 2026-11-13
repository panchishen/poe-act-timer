using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public class LogParserTests
{
    static readonly DateTime At = new(2026, 11, 13, 19, 43, 42);
    const string H = "2026/11/13 19:43:42 56807250 cffb065b ";

    [Fact]
    public void ParsesAreaGenerated()
    {
        var e = Assert.Single(LogParser.Parse(H + "[DEBUG Client 12996] Generating level 28 area \"1_3_8_2\" with seed 202765639"));
        Assert.Equal(new AreaGenerated(At, 28, "1_3_8_2"), e);
    }

    [Fact]
    public void ParsesSceneAsAreaEntered()
    {
        var e = Assert.Single(LogParser.Parse(H + "[INFO Client 12996] [SCENE] Set Source [<if:gen>{Трущоб}<else>{Трущобы}]"));
        Assert.Equal(new AreaEntered(At, "<if:gen>{Трущоб}<else>{Трущобы}"), e);
    }

    [Theory]
    [InlineData("(null)")]
    [InlineData("(unknown)")]
    public void IgnoresServiceScenes(string scene) =>
        Assert.Empty(LogParser.Parse(H + $"[INFO Client 12996] [SCENE] Set Source [{scene}]"));

    [Fact]
    public void ParsesLoadingScreen()
    {
        var e = Assert.Single(LogParser.Parse(H + "[INFO Client 12996] [LOADING SCREEN] (<if:gen>{Рынка}<else>{Рынок}) Duration = 2.25663 seconds"));
        var l = Assert.IsType<LoadingScreen>(e);
        Assert.Equal(2.25663, l.Seconds, 5);
    }

    [Fact]
    public void ParsesLoginConnecting() =>
        Assert.IsType<LoginConnecting>(Assert.Single(LogParser.Parse(H + "[INFO Client 12996] Async connecting to lon.login.pathofexile.com:20487")));

    [Theory]
    [InlineData("Lost", false)]
    [InlineData("Gained", true)]
    public void ParsesFocus(string word, bool focused) =>
        Assert.Equal(new FocusChanged(At, focused), Assert.Single(LogParser.Parse(H + $"[INFO Client 12996] [WINDOW] {word} focus")));

    [Fact]
    public void ParsesGameStarted() =>
        Assert.Equal(new GameStarted(new DateTime(2026, 11, 13, 18, 20, 34)),
            Assert.Single(LogParser.Parse("2026/11/13 18:20:34 ***** LOG FILE OPENING *****")));

    [Fact]
    public void ParsesGameClosed() =>
        Assert.IsType<GameClosed>(Assert.Single(LogParser.Parse(H + "[INFO Client 25780] Closing game gracefully")));

    [Fact]
    public void IgnoresHeaderlessClosingLine() =>
        Assert.Empty(LogParser.Parse("2026/11/13 19:43:42 Closing game gracefully"));

    [Theory]
    [InlineData("#Spammer: Closing game gracefully")]
    [InlineData("#Spammer: ***** LOG FILE OPENING *****")]
    public void IgnoresChatMimickingServiceLines(string body) =>
        Assert.Empty(LogParser.Parse(H + "[INFO Client 12996] " + body));

    [Fact]
    public void IgnoresImpossibleHeaderDateTime() =>
        Assert.Empty(LogParser.Parse("2026/13/45 99:99:99 1 abc [INFO Client 1] [WINDOW] Lost focus"));

    [Fact]
    public void IgnoresMalformedLoadingDuration() =>
        Assert.Empty(LogParser.Parse(H + "[INFO Client 12996] [LOADING SCREEN] (x) Duration = 1.2.3 seconds"));

    [Fact]
    public void IgnoresOversizedAreaLevel() =>
        Assert.Empty(LogParser.Parse(H + "[DEBUG Client 12996] Generating level 99999999999 area \"1_1_1\" with seed 1"));

    [Fact]
    public void IgnoresOversizedLevelUp() =>
        Assert.Empty(LogParser.Parse(H + "[INFO Client 12996] : Hero_Name (Охотница) достигает 99999999999 уровня"));

    [Fact]
    public void ParsesRussianLevelUpAndLanguage()
    {
        var events = LogParser.Parse(H + "[INFO Client 12996] : Hero_Name (Охотница) достигает 28 уровня");
        Assert.Equal(2, events.Count);
        Assert.Equal(new LevelUp(At, "Hero_Name", 28), events[0]);
        Assert.Equal(new LanguageDetected(At, "ru"), events[1]);
    }

    [Fact]
    public void ParsesEnglishLevelUpAndLanguage()
    {
        var events = LogParser.Parse(H + "[INFO Client 12996] : Hero_Name (Huntress) is now level 28");
        Assert.Equal(2, events.Count);
        Assert.Equal(new LevelUp(At, "Hero_Name", 28), events[0]);
        Assert.Equal(new LanguageDetected(At, "en"), events[1]);
    }

    [Fact]
    public void DetectsRussianFromAreaMessage() =>
        Assert.Equal(new LanguageDetected(At, "ru"),
            Assert.Single(LogParser.Parse(H + "[INFO Client 12996] : <<set:MS>><<set:M>><<set:S>>Вы вошли в область Лагерь Сарна.")));

    [Fact]
    public void DetectsEnglishFromAreaMessage() =>
        Assert.Equal(new LanguageDetected(At, "en"),
            Assert.Single(LogParser.Parse(H + "[INFO Client 12996] : You have entered Sarn Encampment.")));

    [Theory]
    [InlineData("#Spammer: [LOADING SCREEN] (x) Duration = 999 seconds")]
    [InlineData("#Spammer: Generating level 68 area \"2_11_endgame_town\" with seed 1")]
    [InlineData("@From Friend: : Hero_Name (Witch) достигает 90 уровня")]
    [InlineData("Аукуна, Чёрная сехема: Гиены не воют. Хороший знак!")]
    public void IgnoresChatAndDialogue(string body) =>
        Assert.Empty(LogParser.Parse(H + "[INFO Client 12996] " + body));

    [Fact]
    public void IgnoresGarbageAndTrailingCarriageReturn()
    {
        Assert.Empty(LogParser.Parse(""));
        Assert.Empty(LogParser.Parse("Skill Instance Id = 5"));
        Assert.Single(LogParser.Parse(H + "[INFO Client 12996] [WINDOW] Lost focus\r"));
    }
}
