// Port de src/game/geometry.test.ts (Vitest → NUnit).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ElViaje.Game;

namespace ElViaje.Game.Tests
{
    public class GeometryTests
    {
        private static PlacedCard Placed(int x, int y, params Dir[] connections) => new()
        {
            CardId = "x", X = x, Y = y, Kind = CardKind.Camino, Name = "test", Connections = connections.ToList(),
        };

        private static GameState StateWith(Dictionary<string, PlacedCard> grid) => new()
        {
            Status = GameStatus.Playing, Difficulty = Difficulty.Medio, Rng = 1, Phase = Phase.Play, Turn = 1,
            Grid = grid, Party = new Party { X = 0, Y = 0 },
            VillageBonus = new Dictionary<Region, int> { [Region.Bosque] = 0, [Region.Planicies] = 0, [Region.Montanas] = 0, [Region.Volcan] = 0 },
        };

        [Test]
        public void OppositeInvierteCadaDireccion()
        {
            Assert.AreEqual(Dir.Down, Geometry.Opposite(Dir.Up));
            Assert.AreEqual(Dir.Up, Geometry.Opposite(Dir.Down));
            Assert.AreEqual(Dir.Right, Geometry.Opposite(Dir.Left));
            Assert.AreEqual(Dir.Left, Geometry.Opposite(Dir.Right));
        }

        [Test]
        public void DosCaminosEnfrentadosConectan()
        {
            var grid = new Dictionary<string, PlacedCard>
            {
                [Geometry.Key(0, 0)] = Placed(0, 0, Dir.Right),
                [Geometry.Key(1, 0)] = Placed(1, 0, Dir.Left),
            };
            CollectionAssert.Contains(Geometry.ConnectedNeighbors(grid, 0, 0), (1, 0));
        }

        [Test]
        public void AdyacenciaSinEnfrentarNoConecta()
        {
            var grid = new Dictionary<string, PlacedCard>
            {
                [Geometry.Key(0, 0)] = Placed(0, 0, Dir.Right),
                [Geometry.Key(1, 0)] = Placed(1, 0, Dir.Up, Dir.Down),
            };
            Assert.AreEqual(0, Geometry.ConnectedNeighbors(grid, 0, 0).Count);
        }

        [Test]
        public void CaminoSoloLegalDondeConecta()
        {
            var grid = new Dictionary<string, PlacedCard> { [Geometry.Key(0, 0)] = Placed(0, 0, Dir.Right) };
            var camino = Cards.GetCard("bosque-camino-0"); // left/right
            var places = Geometry.PlacementsForCard(StateWith(grid), camino);
            Assert.IsTrue(places.Any(p => p.X == 1 && p.Y == 0));
            Assert.IsFalse(places.Any(p => p.X == -1 && p.Y == 0));
        }

        [Test]
        public void HeroeHeredaEjeHorizontal()
        {
            var grid = new Dictionary<string, PlacedCard> { [Geometry.Key(0, 0)] = Placed(0, 0, Dir.Right) };
            var hero = Cards.GetCard("bosque-heroe-0");
            var places = Geometry.PlacementsForCard(StateWith(grid), hero);
            var right = places.First(p => p.X == 1 && p.Y == 0);
            Assert.AreEqual(Orientation.Horizontal, right.Orientation);
            CollectionAssert.AreEquivalent(new[] { Dir.Left, Dir.Right }, right.Connections);
        }

        [Test]
        public void HeroeHeredaEjeVertical()
        {
            var grid = new Dictionary<string, PlacedCard> { [Geometry.Key(0, 0)] = Placed(0, 0, Dir.Down) };
            var hero = Cards.GetCard("bosque-heroe-0");
            var places = Geometry.PlacementsForCard(StateWith(grid), hero);
            var below = places.First(p => p.X == 0 && p.Y == 1);
            Assert.AreEqual(Orientation.Vertical, below.Orientation);
            CollectionAssert.AreEquivalent(new[] { Dir.Up, Dir.Down }, below.Connections);
        }

        [Test]
        public void TravelDistancesMidePorRutas()
        {
            var grid = new Dictionary<string, PlacedCard>
            {
                [Geometry.Key(0, 0)] = Placed(0, 0, Dir.Right),
                [Geometry.Key(1, 0)] = Placed(1, 0, Dir.Left, Dir.Right),
                [Geometry.Key(2, 0)] = Placed(2, 0, Dir.Left),
            };
            var dist = Geometry.TravelDistances(grid, 0, 0);
            Assert.AreEqual(0, dist[Geometry.Key(0, 0)]);
            Assert.AreEqual(1, dist[Geometry.Key(1, 0)]);
            Assert.AreEqual(2, dist[Geometry.Key(2, 0)]);
        }

        [Test]
        public void OpenEndsDetectaSalidas()
        {
            var grid = new Dictionary<string, PlacedCard> { [Geometry.Key(0, 0)] = Placed(0, 0, Dir.Right) };
            var ends = Geometry.OpenEnds(grid, 0, 0);
            Assert.IsTrue(ends.Any(e => e.x == 1 && e.y == 0));
        }
    }
}
