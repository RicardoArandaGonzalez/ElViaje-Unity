// Port de src/game/engine.test.ts (Vitest → NUnit).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ElViaje.Game;

namespace ElViaje.Game.Tests
{
    public class EngineTests
    {
        private static GameState Base() =>
            Engine.CreateInitialState(new NewGameOptions { Seed = 42, Difficulty = Difficulty.Medio, Starter = Starter.Heroe });

        private static PlacedCard Road(int x, int y, params Dir[] connections) => new()
        {
            CardId = $"r{x},{y}", X = x, Y = y, Kind = CardKind.Camino, Name = "Camino", Connections = connections.ToList(),
        };

        private static PartyMember Knight() => new() { CardId = "inicial-heroe", Name = "Caballero", BasePower = 3 };

        // ---- createInitialState ----
        [Test]
        public void EsDeterministaParaLaMismaSemilla()
        {
            var a = Engine.CreateInitialState(new NewGameOptions { Seed = 7 });
            var b = Engine.CreateInitialState(new NewGameOptions { Seed = 7 });
            CollectionAssert.AreEqual(a.Hand, b.Hand);
            CollectionAssert.AreEqual(a.Deck, b.Deck);
            Assert.AreEqual(a.Rng, b.Rng);
        }

        [Test]
        public void ManoInicialNuncaContieneGeneral()
        {
            for (uint seed = 0; seed < 300; seed++)
                foreach (var difficulty in new[] { Difficulty.Facil, Difficulty.Medio, Difficulty.Dificil })
                {
                    var s = Engine.CreateInitialState(new NewGameOptions { Seed = seed, Difficulty = difficulty, Starter = Starter.Heroe });
                    Assert.IsFalse(s.Hand.Any(id => Cards.GetCard(id).Kind == CardKind.General), $"seed {seed}");
                }
        }

        [Test]
        public void DealWithoutGeneralsSacaGeneralesYConservaCartas()
        {
            var withGeneralFirst = new List<string> { "bosque-general", "bosque-camino-0", "bosque-camino-1", "bosque-camino-2" };
            var dealt = Engine.DealWithoutGenerals(withGeneralFirst, 3);
            Assert.AreNotEqual(CardKind.General, Cards.GetCard(dealt[0]).Kind);
            CollectionAssert.AreEquivalent(withGeneralFirst, dealt);
        }

        [Test]
        public void SemillasDistintasProducenManosDistintas()
        {
            var a = Engine.CreateInitialState(new NewGameOptions { Seed = 1 });
            var b = Engine.CreateInitialState(new NewGameOptions { Seed = 2 });
            Assert.IsFalse(a.Hand.SequenceEqual(b.Hand));
        }

        [Test]
        public void EmpiezaEnPlayTurno1ConParty()
        {
            var s = Base();
            Assert.AreEqual(Phase.Play, s.Phase);
            Assert.AreEqual(1, s.Turn);
            Assert.AreEqual(1, s.Party.Members.Count);
            Assert.AreEqual(3, Engine.GetPartyPower(s));
        }

        [Test]
        public void FacilDa4CartasMedioYDificil3()
        {
            Assert.AreEqual(4, Engine.CreateInitialState(new NewGameOptions { Seed = 1, Difficulty = Difficulty.Facil }).Hand.Count);
            Assert.AreEqual(3, Engine.CreateInitialState(new NewGameOptions { Seed = 1, Difficulty = Difficulty.Medio }).Hand.Count);
            Assert.AreEqual(3, Engine.CreateInitialState(new NewGameOptions { Seed = 1, Difficulty = Difficulty.Dificil }).Hand.Count);
        }

        // ---- inmutabilidad y determinismo ----
        [Test]
        public void NoMutaElEstadoDeEntrada()
        {
            var s = Base();
            int handBefore = s.Hand.Count;
            var phaseBefore = s.Phase;
            Engine.ApplyMove(s, GameAction.Roll()); // acción inválida en play
            Assert.AreEqual(phaseBefore, s.Phase);
            Assert.AreEqual(handBefore, s.Hand.Count);
        }

        [Test]
        public void MismaAccionMismoResultado()
        {
            var s = Base();
            var card = s.Hand.First(id => Engine.GetPlacements(s, id).Count > 0);
            var move = GameAction.PlayCard(card, 0, 0);
            var a = Engine.ApplyMove(s, move);
            var b = Engine.ApplyMove(s, move);
            Assert.AreEqual(a.Rng, b.Rng);
            Assert.AreEqual(a.Phase, b.Phase);
            CollectionAssert.AreEqual(a.Grid.Keys.OrderBy(k => k).ToList(), b.Grid.Keys.OrderBy(k => k).ToList());
            CollectionAssert.AreEqual(a.Hand, b.Hand);
        }

        // ---- colocación inicial y flujo del turno ----
        [Test]
        public void PrimeraCartaEnElCentroYPartyEncima()
        {
            var s = Base();
            var card = s.Hand.First(id => Engine.GetPlacements(s, id).Count > 0);
            var s2 = Engine.ApplyMove(s, GameAction.PlayCard(card, 0, 0));
            Assert.IsTrue(s2.Grid.ContainsKey(Geometry.Key(0, 0)));
            Assert.AreEqual(0, s2.Party.X);
            Assert.AreEqual(0, s2.Party.Y);
            Assert.AreEqual(Phase.Roll, s2.Phase);
        }

        [Test]
        public void TrasJugarYTirarEntraEnMove()
        {
            var s = Base();
            var card = s.Hand.First(id => Engine.GetPlacements(s, id).Count > 0);
            var g = Engine.ApplyMove(s, GameAction.PlayCard(card, 0, 0));
            g = Engine.ApplyMove(g, GameAction.Roll());
            Assert.AreEqual(Phase.Move, g.Phase);
            Assert.GreaterOrEqual(g.MovesLeft, 1);
            Assert.GreaterOrEqual(g.LastRoll ?? 0, 1);
        }

        [Test]
        public void ImpideMovimientosIlegales()
        {
            var s = Base();
            var card = s.Hand.First(id => Engine.GetPlacements(s, id).Count > 0);
            var g = Engine.ApplyMove(s, GameAction.PlayCard(card, 0, 0));
            g = Engine.ApplyMove(g, GameAction.Roll());
            var g2 = Engine.ApplyMove(g, GameAction.Step(99, 99));
            Assert.AreEqual(g.Party.X, g2.Party.X);
            Assert.AreEqual(g.Party.Y, g2.Party.Y);
        }

        // ---- poder del Party con pueblos ----
        [Test]
        public void BonusDePuebloSeSumaAHeroesDeRegion()
        {
            var s = Base();
            s.Party.Members.Add(new PartyMember { CardId = "bosque-heroe-0", Name = "Arquero", BasePower = 3, Region = Region.Bosque });
            Assert.AreEqual(6, Engine.GetPartyPower(s));
            s.VillageBonus[Region.Bosque] = 2;
            Assert.AreEqual(8, Engine.GetPartyPower(s));
        }

        // ---- combate contra Generales ----
        private static GameState StartGeneralCombat(System.Action<GameState> setup)
        {
            var s = Base();
            setup(s);
            return Engine.ApplyMove(s, GameAction.InvokeGeneral());
        }

        private static (int r, int c) AWrongCell(PendingCombat pc, HashSet<string> used)
        {
            for (int r = 0; r < pc.GridN; r++)
                for (int c = 0; c < pc.GridN; c++)
                    if ((r != pc.HeartR || c != pc.HeartC) && !used.Contains($"{r},{c}")) return (r, c);
            throw new System.Exception("no wrong cell");
        }

        [Test]
        public void EncontrarElCorazonDerrotaAlGeneral()
        {
            var c = StartGeneralCombat(s =>
            {
                s.Grid[Geometry.Key(0, 0)] = Road(0, 0, Dir.Left, Dir.Right);
                s.Party = new Party { X = 0, Y = 0, Members = new List<PartyMember> { Knight() } };
                s.Hand = new List<string> { "bosque-general" };
            });
            Assert.AreEqual(Phase.Combat, c.Phase);
            var pc = c.PendingCombat;
            Assert.AreEqual(4, pc.GeneralPower);
            var g = Engine.ApplyMove(c, GameAction.CombatSelect(pc.HeartR, pc.HeartC));
            Assert.AreEqual(1, g.GeneralsDefeated);
            Assert.AreEqual(Phase.Roll, g.Phase);
        }

        [Test]
        public void SinIntentosEsGameOver()
        {
            var c = StartGeneralCombat(s =>
            {
                s.Grid[Geometry.Key(0, 0)] = Road(0, 0, Dir.Left, Dir.Right);
                s.Party = new Party { X = 0, Y = 0, Members = new List<PartyMember> { Knight() } };
                s.Hand = new List<string> { "bosque-general" };
            });
            var g = c;
            var used = new HashSet<string>();
            int total = c.PendingCombat.AttemptsTotal;
            for (int i = 0; i < total; i++)
            {
                var w = AWrongCell(g.PendingCombat, used);
                used.Add($"{w.r},{w.c}");
                g = Engine.ApplyMove(g, GameAction.CombatSelect(w.r, w.c));
            }
            Assert.AreEqual(GameStatus.Lost, Engine.CheckGameOver(g).Status);
        }

        [Test]
        public void RetirarseBloqueaYRetrocede()
        {
            var c = StartGeneralCombat(s =>
            {
                s.Grid[Geometry.Key(0, 0)] = Road(0, 0, Dir.Right);
                s.Grid[Geometry.Key(1, 0)] = Road(1, 0, Dir.Left);
                s.Party = new Party { X = 1, Y = 0, Members = new List<PartyMember> { Knight() } };
                s.VisitedThisTurn = new List<string> { Geometry.Key(0, 0), Geometry.Key(1, 0) };
                s.Hand = new List<string> { "bosque-general" };
            });
            var g = Engine.ApplyMove(c, GameAction.CombatRetreat());
            Assert.AreEqual(Region.Bosque, g.Grid[Geometry.Key(1, 0)].BlockedByGeneralRegion);
            Assert.AreEqual(0, g.Party.X);
            Assert.AreEqual(0, g.Party.Y);
        }

        [Test]
        public void NoSePuedeRetirarTrasPrimeraCasilla()
        {
            var c = StartGeneralCombat(s =>
            {
                s.Grid[Geometry.Key(0, 0)] = Road(0, 0, Dir.Right);
                s.Grid[Geometry.Key(1, 0)] = Road(1, 0, Dir.Left);
                s.Party = new Party { X = 1, Y = 0, Members = new List<PartyMember> { Knight() } };
                s.VisitedThisTurn = new List<string> { Geometry.Key(0, 0), Geometry.Key(1, 0) };
                s.Hand = new List<string> { "bosque-general" };
            });
            var w = AWrongCell(c.PendingCombat, new HashSet<string>());
            var afterSelect = Engine.ApplyMove(c, GameAction.CombatSelect(w.r, w.c));
            Assert.IsTrue(afterSelect.PendingCombat.Started);
            var afterRetreat = Engine.ApplyMove(afterSelect, GameAction.CombatRetreat());
            Assert.AreEqual(Phase.Combat, afterRetreat.Phase);
            Assert.AreEqual(1, afterRetreat.Party.X);
        }

        [Test]
        public void CuartoGeneralHaceAparecerElCastillo()
        {
            var c = StartGeneralCombat(s =>
            {
                s.Grid[Geometry.Key(0, 0)] = Road(0, 0, Dir.Right);
                s.Grid[Geometry.Key(1, 0)] = Road(1, 0, Dir.Left, Dir.Right);
                s.Party = new Party { X = 0, Y = 0, Members = new List<PartyMember> { Knight() } };
                s.GeneralsDefeated = 3;
                s.Hand = new List<string> { "volcan-general" };
            });
            Assert.AreEqual(13, c.PendingCombat.GeneralPower);
            var pc = c.PendingCombat;
            var g = Engine.ApplyMove(c, GameAction.CombatSelect(pc.HeartR, pc.HeartC));
            Assert.AreEqual(4, g.GeneralsDefeated);
            Assert.IsTrue(g.CastleSpawned);
        }

        // ---- combate final contra el Rey ----
        [Test]
        public void CorrupcionAumentaPoderDelRey()
        {
            var s = Base();
            Assert.AreEqual(16, Engine.GetReyPower(s));
            s.StepsSinceCastle = 8;
            Assert.AreEqual(24, Engine.GetReyPower(s));
        }

        private static PlacedCard Castle(int x, int y) => new()
        {
            CardId = "castillo", X = x, Y = y, Kind = CardKind.Castillo, Name = "Castillo del Rey Demonio",
            Connections = new List<Dir> { Dir.Left, Dir.Up, Dir.Right, Dir.Down },
        };

        [Test]
        public void EntrarAlCastilloIniciaElMinijuego()
        {
            var s = Base();
            s.Grid[Geometry.Key(0, 0)] = Road(0, 0, Dir.Right);
            s.Grid[Geometry.Key(1, 0)] = Castle(1, 0);
            s.Party = new Party { X = 0, Y = 0, Members = new List<PartyMember> { Knight() } };
            s.CastleSpawned = true;
            s.Phase = Phase.Move;
            s.MovesLeft = 1;
            s.VisitedThisTurn = new List<string> { Geometry.Key(0, 0) };
            var g = Engine.ApplyMove(s, GameAction.Step(1, 0));
            Assert.AreEqual(Phase.Combat, g.Phase);
            Assert.IsTrue(g.PendingCombat.IsRey);
            Assert.AreEqual(2, g.PendingCombat.HeartsTotal);
            Assert.AreEqual(GameStatus.Playing, Engine.CheckGameOver(g).Status);
        }

        [Test]
        public void DestruirLos2CorazonesGana()
        {
            var s = Base();
            s.Grid[Geometry.Key(0, 0)] = Road(0, 0, Dir.Right);
            s.Grid[Geometry.Key(1, 0)] = Castle(1, 0);
            s.Party = new Party { X = 0, Y = 0, Members = new List<PartyMember> { Knight() } };
            s.CastleSpawned = true;
            s.Phase = Phase.Move;
            s.MovesLeft = 1;
            s.VisitedThisTurn = new List<string> { Geometry.Key(0, 0) };
            var g = Engine.ApplyMove(s, GameAction.Step(1, 0));
            var pc1 = g.PendingCombat;
            var h = Engine.ApplyMove(g, GameAction.CombatSelect(pc1.HeartR, pc1.HeartC));
            Assert.AreEqual(1, h.PendingCombat.HeartsFound);
            var pc2 = h.PendingCombat;
            var win = Engine.ApplyMove(h, GameAction.CombatSelect(pc2.HeartR, pc2.HeartC));
            Assert.AreEqual(GameStatus.Won, Engine.CheckGameOver(win).Status);
        }

        // ---- condiciones de fin y jugadas legales ----
        [Test]
        public void RobarConMazoVacioEsGameOver()
        {
            var s = Base();
            s.Phase = Phase.Draw;
            s.Deck = new List<string>();
            s.Hand = new List<string>();
            var g = Engine.ApplyMove(s, GameAction.Draw());
            Assert.AreEqual(GameStatus.Lost, Engine.CheckGameOver(g).Status);
        }

        [Test]
        public void GetLegalMovesVacioSiTermino()
        {
            var s = Base();
            s.Status = GameStatus.Won;
            Assert.AreEqual(0, Engine.GetLegalMoves(s).Count);
        }

        [Test]
        public void EnPlayOfreceColocarPrimeraCarta()
        {
            var s = Base();
            var moves = Engine.GetLegalMoves(s);
            Assert.IsTrue(moves.Any(m => m.Type == ActionType.PlayCard));
        }
    }
}
