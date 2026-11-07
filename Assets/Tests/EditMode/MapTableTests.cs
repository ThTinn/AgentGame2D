using System.Text;
using DungeonOnline.Net;
using NUnit.Framework;

namespace DungeonOnline.Tests
{
    public class MapTableTests
    {
        // same shape as ServerJava/data/templates/maps.json
        private const string Json = @"{
            ""version"": 1,
            ""maps"": [
                { ""id"": 0, ""name"": ""Thành|Town"", ""xMin"": -8.6, ""xMax"": 8.6, ""fallLimitY"": -8,
                  ""spawn"": { ""x"": -7, ""y"": -3.5 },
                  ""solids"": [ { ""name"": ""Ground"", ""x"": -5, ""y"": -4.5, ""w"": 8, ""h"": 1, ""visible"": true },
                                { ""name"": ""Wall"", ""x"": -9.4, ""y"": 2, ""w"": 1, ""h"": 24, ""visible"": false } ],
                  ""camps"": [] },
                { ""id"": 1, ""name"": ""Dungeon"", ""spawn"": { ""x"": -12, ""y"": -3.5 },
                  ""camps"": [ { ""template"": 2, ""count"": 3, ""x"": 8, ""y"": -3.5, ""range"": 3, ""maxHp"": 1770, ""damage"": 33, ""level"": 10 } ] }
            ]
        }";

        [Test]
        public void Parse_ReadsMapsSolidsAndCamps()
        {
            // Act
            MapTable table = MapTable.Parse(Encoding.UTF8.GetBytes(Json));

            // Assert
            Assert.AreEqual(1, table.version);
            Assert.AreEqual(2, table.maps.Length);
            MapInfo town = table.Find(0);
            Assert.AreEqual("Thành|Town", town.name);
            Assert.AreEqual(-7f, town.spawn.x);
            Assert.AreEqual(-8f, town.fallLimitY);
            Assert.AreEqual(2, town.solids.Length);
            Assert.IsFalse(town.solids[1].visible);
            Assert.AreEqual(1770, table.Find(1).camps[0].maxHp);
        }

        [Test]
        public void Find_UnknownId_ReturnsNull()
        {
            // Arrange
            MapTable table = MapTable.Parse(Encoding.UTF8.GetBytes(Json));

            // Act / Assert
            Assert.IsNull(table.Find(42));
        }

        [Test]
        public void Parse_Empty_ReturnsEmptyTable()
        {
            // Act
            MapTable table = MapTable.Parse(new byte[0]);

            // Assert
            Assert.AreEqual(0, table.maps.Length);
        }
    }
}
