// Port de src/game/cards.test.ts (Vitest → NUnit).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ElViaje.Game;

namespace ElViaje.Game.Tests
{
    public class CardsTests
    {
        [Test]
        public void MazoDeAventuraTiene52Cartas()
        {
            Assert.AreEqual(52, Cards.AdventureDeckIds().Count);
        }

        [Test]
        public void CadaRegionAporta13Cartas()
        {
            foreach (var region in Cards.Regions)
            {
                string rid = Cards.RegionId(region);
                var ids = Cards.AdventureDeckIds().Where(id => id.StartsWith(rid)).ToList();
                Assert.AreEqual(13, ids.Count, $"región {rid}");
                var kinds = ids.Select(id => Cards.GetCard(id).Kind).ToList();
                Assert.AreEqual(7, kinds.Count(k => k == CardKind.Camino));
                Assert.AreEqual(2, kinds.Count(k => k == CardKind.Pueblo));
                Assert.AreEqual(3, kinds.Count(k => k == CardKind.Heroe));
                Assert.AreEqual(1, kinds.Count(k => k == CardKind.General));
            }
        }

        [Test]
        public void TodosLosIdsSonUnicos()
        {
            var ids = Cards.AdventureDeckIds();
            Assert.AreEqual(ids.Count, new HashSet<string>(ids).Count);
        }

        [Test]
        public void PueblosOtorganBonus1Y2PorRegion()
        {
            foreach (var region in Cards.Regions)
            {
                string rid = Cards.RegionId(region);
                var bonuses = Cards.AdventureDeckIds()
                    .Where(id => id.StartsWith($"{rid}-pueblo"))
                    .Select(id => Cards.GetCard(id).VillageBonus ?? 0)
                    .OrderBy(b => b)
                    .ToList();
                CollectionAssert.AreEqual(new[] { 1, 2 }, bonuses);
            }
        }

        [Test]
        public void HeroesDeRegionTienenPoderBase3()
        {
            var heroes = Cards.AdventureDeckIds().Where(id => Cards.GetCard(id).Kind == CardKind.Heroe).ToList();
            Assert.AreEqual(12, heroes.Count);
            foreach (var id in heroes) Assert.AreEqual(3, Cards.GetCard(id).Power);
        }

        [Test]
        public void AldeaDevastadaSoloConectaALaDerecha()
        {
            var aldea = Cards.Catalog.Values.First(c => c.Name == "Aldea Devastada");
            CollectionAssert.AreEqual(new[] { Dir.Right }, aldea.Connections);
        }

        [Test]
        public void CastilloYReyExistenFueraDelMazo()
        {
            Assert.AreEqual(CardKind.Castillo, Cards.GetCard("castillo").Kind);
            Assert.AreEqual(CardKind.Rey, Cards.GetCard("rey").Kind);
            CollectionAssert.DoesNotContain(Cards.AdventureDeckIds(), "castillo");
            CollectionAssert.DoesNotContain(Cards.AdventureDeckIds(), "rey");
        }

        [Test]
        public void PoderesDeLosGeneralesEscalan()
        {
            CollectionAssert.AreEqual(new[] { 4, 7, 10, 13 }, Cards.GeneralPowers);
        }

        [Test]
        public void GetCardLanzaConIdDesconocido()
        {
            Assert.Throws<System.ArgumentException>(() => Cards.GetCard("no-existe"));
        }
    }
}
