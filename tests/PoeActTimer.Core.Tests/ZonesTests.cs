using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public class ZonesTests
{
    [Theory]
    [InlineData("1_1_1", 1)]
    [InlineData("1_1_town", 1)]
    [InlineData("1_3_8_2", 3)]
    [InlineData("1_5_town", 5)]
    [InlineData("2_6_1", 6)]
    [InlineData("2_8_7_1_", 8)]
    [InlineData("2_10_town", 10)]
    public void CampaignZonesMapToActs(string id, int act) =>
        Assert.Equal(new ZoneInfo(ZoneKind.Act, act), Zones.Classify(id));

    [Fact]
    public void KaruiShoresIsEpilogue() =>
        Assert.Equal(new ZoneInfo(ZoneKind.Epilogue, 11), Zones.Classify("2_11_endgame_town"));

    [Theory]
    [InlineData("MapWorldsCrimsonTownship")]
    [InlineData("AfflictionTown10")]
    public void EndgameZones(string id) =>
        Assert.Equal(ZoneKind.Endgame, Zones.Classify(id).Kind);

    [Theory]
    [InlineData("HideoutPrimevalRuins")]
    [InlineData("ChayulaLeagueTowerBoss")]
    [InlineData("1_Labyrinth_boss_1")]
    public void OtherZonesAreNeutral(string id) =>
        Assert.Equal(new ZoneInfo(ZoneKind.Neutral, 0), Zones.Classify(id));
}
