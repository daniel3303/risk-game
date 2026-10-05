using AwesomeAssertions;
using Xunit;
namespace Risk.UnitTests.Rules;

public sealed class WorldMapTests
{
    [Fact]
    public void Parse_ClassicWorld_HasCanonicalContinentSizesAndSeaBorders()
    {
        var map = TestWorld.Map();
        map.Continents.Select(c => map.Territories.Count(t => t.Continent == c.Id)).Should().Equal(9, 4, 7, 6, 12, 4);
        map.Territories[0].Neighbors.Should().Contain(29);
        map.Territories[2].Neighbors.Should().Contain(13);
        map.Territories[11].Neighbors.Should().Contain(20);
        map.Territories[37].Neighbors.Should().Contain(38);
        map.Territories.Should().OnlyContain(t => t.Shape.Length >= 3);
    }
}
