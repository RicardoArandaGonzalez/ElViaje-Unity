// ============================================================================
// Motor del juego — funciones puras y deterministas. Port de src/game/engine.ts.
// CreateInitialState / GetLegalMoves / ApplyMove / CheckGameOver / GetAIMove
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace ElViaje.Game
{
    public static class Engine
    {
        // -------------------------------------------------------------------
        // Parámetros de dificultad
        // -------------------------------------------------------------------
        private struct DiffConfig { public int ReyBase; public int InitialHand; public DiffConfig(int r, int h) { ReyBase = r; InitialHand = h; } }

        private static DiffConfig Diff(Difficulty d) => d switch
        {
            Difficulty.Facil => new DiffConfig(14, 4),
            Difficulty.Medio => new DiffConfig(Cards.ReyBasePower, 3),
            Difficulty.Dificil => new DiffConfig(20, 3),
            _ => new DiffConfig(Cards.ReyBasePower, 3),
        };

        /// <summary>+1 de Poder por cada Héroe reclutable (nueva escala).</summary>
        public const int HeroRecruitPower = 1;
        /// <summary>Tamaño de cuadrícula de cada General (por orden) y del Rey.</summary>
        public static readonly int[] GeneralGrids = { 8, 10, 12, 14 };
        public const int ReyGrid = 16;

        // -------------------------------------------------------------------
        // Utilidades internas
        // -------------------------------------------------------------------
        private static void PushLog(GameState s, LogKind kind, string text)
            => s.Log.Add(new LogEntry { Turn = s.Turn, Kind = kind, Text = text });

        private static int MemberPower(PartyMember m, Dictionary<Region, int> villageBonus)
            => m.BasePower + (m.Region.HasValue ? villageBonus[m.Region.Value] : 0);

        public static int GetPartyPower(GameState s)
        {
            int acc = 0;
            foreach (var m in s.Party.Members) acc += MemberPower(m, s.VillageBonus);
            return acc;
        }

        public static int GetCurrentGeneralPower(GameState s)
        {
            int idx = Math.Min(s.GeneralsDefeated, Cards.GeneralPowers.Length - 1);
            return Cards.GeneralPowers[idx];
        }

        public static int GetReyPower(GameState s) => Diff(s.Difficulty).ReyBase + s.StepsSinceCastle;

        public static int Manhattan(int r1, int c1, int r2, int c2) => Math.Abs(r1 - r2) + Math.Abs(c1 - c2);

        public static CombatForecast CombatForecastFor(int partyPower, int targetPower, bool isRey = false)
        {
            int delta = partyPower - targetPower;
            int baseA = isRey ? 5 : 2;
            int bonus = (delta >= 0 ? 1 : 0) + (delta >= 4 ? 1 : 0);
            int maxA = isRey ? 7 : 4;
            int attempts = Math.Max(baseA, Math.Min(maxA, baseA + bonus));
            CombatTier tier = delta < 0 ? CombatTier.Low : delta < 4 ? CombatTier.Mid : CombatTier.High;
            string label = tier == CombatTier.Low ? "En desventaja" : tier == CombatTier.Mid ? "Igualado" : "Con ventaja";
            return new CombatForecast { Attempts = attempts, Tier = tier, Label = label };
        }

        public static int CombatGridFor(int generalsDefeated, bool isRey)
            => isRey ? ReyGrid : GeneralGrids[Math.Min(generalsDefeated, GeneralGrids.Length - 1)];

        private static int MovementBonus(GameState s) => s.Party.Members.Any(m => m.IsHeroine) ? 1 : 0;

        public static bool IsPossessed(GameState s) => s.Hand.Any(id => Cards.GetCard(id).Kind == CardKind.General);
        private static string PossessedGeneralId(GameState s)
            => s.Hand.FirstOrDefault(id => Cards.GetCard(id).Kind == CardKind.General);

        // -------------------------------------------------------------------
        // createInitialState
        // -------------------------------------------------------------------
        public static List<string> DealWithoutGenerals(IReadOnlyList<string> deck, int handSize)
        {
            var d = new List<string>(deck);
            int size = Math.Min(handSize, d.Count);
            for (int i = 0; i < size; i++)
            {
                if (Cards.GetCard(d[i]).Kind != CardKind.General) continue;
                for (int j = size; j < d.Count; j++)
                {
                    if (Cards.GetCard(d[j]).Kind != CardKind.General)
                    {
                        (d[i], d[j]) = (d[j], d[i]);
                        break;
                    }
                }
            }
            return d;
        }

        public static GameState CreateInitialState(NewGameOptions opts)
        {
            var difficulty = opts.Difficulty;
            var starter = opts.Starter;
            var cfg = Diff(difficulty);

            var (shuffled, seed) = Rng.Shuffle(Cards.AdventureDeckIds(), opts.Seed);
            var deck = DealWithoutGenerals(shuffled, cfg.InitialHand);

            var hand = deck.Take(cfg.InitialHand).ToList();
            var rest = deck.Skip(cfg.InitialHand).ToList();

            PartyMember starterMember = starter == Starter.Heroe
                ? new PartyMember { CardId = "inicial-heroe", Name = "Caballero", BasePower = 3 }
                : new PartyMember { CardId = "inicial-heroina", Name = "Mago", BasePower = 2, IsHeroine = true };

            var state = new GameState
            {
                Status = GameStatus.Playing,
                Difficulty = difficulty,
                Rng = seed,
                Phase = Phase.Play,
                Turn = 1,
                Grid = new Dictionary<string, PlacedCard>(),
                Party = new Party { X = 0, Y = 0, Members = new List<PartyMember> { starterMember } },
                Deck = rest,
                Hand = hand,
                Discard = new List<string>(),
                LastRoll = null,
                MovesLeft = 0,
                VisitedThisTurn = new List<string>(),
                GeneralsDefeated = 0,
                VillageBonus = new Dictionary<Region, int>
                {
                    [Region.Bosque] = 0, [Region.Planicies] = 0, [Region.Montanas] = 0, [Region.Volcan] = 0,
                },
                CastleSpawned = false,
                StepsSinceCastle = 0,
                Stats = new Stats(),
                Log = new List<LogEntry>(),
            };

            PushLog(state, LogKind.System, $"Comienza la aventura con {starterMember.Name}. Coloca la primera carta del mapa.");
            return state;
        }

        // -------------------------------------------------------------------
        // Selectores para la UI
        // -------------------------------------------------------------------
        public static List<Placement> GetPlacements(GameState s, string cardId)
        {
            var card = Cards.GetCard(cardId);
            if (card.Kind == CardKind.General) return new List<Placement>();
            if (s.Grid.Count == 0)
            {
                var connections = card.Kind == CardKind.Heroe
                    ? new List<Dir> { Dir.Left, Dir.Right }
                    : new List<Dir>(card.Connections);
                return new List<Placement>
                {
                    new() { X = 0, Y = 0, Connections = connections, Orientation = card.Kind == CardKind.Heroe ? Orientation.Horizontal : Orientation.None },
                };
            }
            return Geometry.PlacementsForCard(s, card);
        }

        private static bool AnyPlayable(GameState s)
            => s.Hand.Any(id => Cards.GetCard(id).Kind != CardKind.General && GetPlacements(s, id).Count > 0);

        public static List<(int x, int y)> GetStepTargets(GameState s)
        {
            if (s.Phase != Phase.Move || s.MovesLeft <= 0) return new List<(int, int)>();
            return Geometry.ConnectedNeighbors(s.Grid, s.Party.X, s.Party.Y)
                .Where(n => !s.VisitedThisTurn.Contains(Geometry.Key(n.x, n.y)))
                .ToList();
        }

        // -------------------------------------------------------------------
        // getLegalMoves
        // -------------------------------------------------------------------
        public static List<GameAction> GetLegalMoves(GameState s)
        {
            var moves = new List<GameAction>();
            if (s.Status != GameStatus.Playing) return moves;

            switch (s.Phase)
            {
                case Phase.Draw:
                    moves.Add(GameAction.Draw());
                    break;
                case Phase.Play:
                    foreach (var id in s.Hand)
                        foreach (var p in GetPlacements(s, id))
                            moves.Add(GameAction.PlayCard(id, p.X, p.Y, p.Orientation));
                    if (IsPossessed(s)) moves.Add(GameAction.InvokeGeneral());
                    if (!AnyPlayable(s))
                        foreach (var id in s.Hand)
                            if (Cards.GetCard(id).Kind != CardKind.General) moves.Add(GameAction.Discard(id));
                    break;
                case Phase.Roll:
                    moves.Add(GameAction.Roll());
                    break;
                case Phase.Move:
                    foreach (var t in GetStepTargets(s)) moves.Add(GameAction.Step(t.x, t.y));
                    moves.Add(GameAction.EndMove());
                    break;
                case Phase.Combat:
                    var pc = s.PendingCombat;
                    if (pc != null)
                    {
                        for (int r = 0; r < pc.GridN; r++)
                            for (int c = 0; c < pc.GridN; c++)
                                if (!pc.Revealed.Any(v => v.R == r && v.C == c)) moves.Add(GameAction.CombatSelect(r, c));
                        if (!pc.Started) moves.Add(GameAction.CombatRetreat());
                    }
                    break;
                case Phase.World:
                    if (s.PendingWorldPlacement != null)
                        foreach (var o in s.PendingWorldPlacement.Options)
                            moves.Add(GameAction.ResolveWorldPlacement(o.X, o.Y));
                    else
                        moves.Add(GameAction.WorldStep());
                    break;
                case Phase.Final:
                    break;
            }
            return moves;
        }

        // -------------------------------------------------------------------
        // applyMove
        // -------------------------------------------------------------------
        public static GameState ApplyMove(GameState state, GameAction action)
        {
            if (state.Status != GameStatus.Playing) return state;
            var s = state.Clone();

            return action.Type switch
            {
                ActionType.Draw => DoDraw(s),
                ActionType.PlayCard => DoPlayCard(s, action),
                ActionType.Discard => DoDiscard(s, action.CardId),
                ActionType.InvokeGeneral => DoInvokeGeneral(s),
                ActionType.Roll => DoRoll(s),
                ActionType.Step => DoStep(s, action.X, action.Y),
                ActionType.EndMove => DoEndMove(s),
                ActionType.CombatSelect => DoCombatSelect(s, action.R, action.C),
                ActionType.CombatRetreat => DoCombatRetreat(s),
                ActionType.WorldStep => DoWorldStep(s),
                ActionType.ResolveWorldPlacement => DoResolveWorldPlacement(s, action.X, action.Y),
                _ => s,
            };
        }

        // -------------------------------------------------------------------
        // Fases del turno
        // -------------------------------------------------------------------
        private static GameState DoDraw(GameState s)
        {
            if (s.Phase != Phase.Draw) return s;
            if (IsPossessed(s)) { s.Phase = Phase.Play; return s; }
            if (s.Deck.Count == 0) return Lose(s, "El Mazo de Aventura se agotó (§32).");
            string id = s.Deck[0];
            s.Deck.RemoveAt(0);
            s.Hand.Add(id);
            var card = Cards.GetCard(id);
            if (card.Kind == CardKind.General)
            {
                s.PossessedGeneralId = id;
                PushLog(s, LogKind.Combat, $"Robaste a {card.Name}: quedas Poseído (§27).");
            }
            else PushLog(s, LogKind.Player, $"Robas: {card.Name}.");
            s.Phase = Phase.Play;
            return s;
        }

        private static GameState DoPlayCard(GameState s, GameAction action)
        {
            if (s.Phase != Phase.Play) return s;
            string cardId = action.CardId;
            if (!s.Hand.Contains(cardId)) return s;
            var card = Cards.GetCard(cardId);
            var legal = GetPlacements(s, cardId).FirstOrDefault(p =>
                p.X == action.X && p.Y == action.Y &&
                (action.Orientation == Orientation.None || p.Orientation == action.Orientation));
            if (legal == null) return s;

            bool firstCard = s.Grid.Count == 0;
            s.Grid[Geometry.Key(action.X, action.Y)] = new PlacedCard
            {
                CardId = cardId, X = action.X, Y = action.Y, Kind = card.Kind, Name = card.Name,
                Region = card.Region, Connections = new List<Dir>(legal.Connections),
            };
            s.Hand.Remove(cardId);
            s.Stats.CardsPlayed++;
            PushLog(s, LogKind.Player, $"Juegas {card.Name} en ({action.X}, {action.Y}).");

            if (firstCard)
            {
                s.Party.X = action.X;
                s.Party.Y = action.Y;
                ResolveTile(s, action.X, action.Y);
            }
            s.Phase = Phase.Roll;
            return s;
        }

        private static GameState DoDiscard(GameState s, string cardId)
        {
            if (s.Phase != Phase.Play) return s;
            if (AnyPlayable(s)) return s;
            if (!s.Hand.Contains(cardId)) return s;
            if (Cards.GetCard(cardId).Kind == CardKind.General) return s;
            s.Hand.Remove(cardId);
            s.Discard.Add(cardId);
            PushLog(s, LogKind.Player, $"Sin jugada legal: descartas {Cards.GetCard(cardId).Name} (§15).");
            s.Phase = Phase.Roll;
            return s;
        }

        private static GameState DoInvokeGeneral(GameState s)
        {
            if (s.Phase != Phase.Play) return s;
            string gid = PossessedGeneralId(s);
            if (gid == null) return s;
            var card = Cards.GetCard(gid);
            PushLog(s, LogKind.Combat, $"Invocas a {card.Name} sobre tu Party.");
            return SetupCombat(s, new CombatSetup { Source = "player", Region = card.Region.Value, CardId = gid });
        }

        private static GameState DoRoll(GameState s)
        {
            if (s.Phase != Phase.Roll) return s;
            var r = Rng.RollDie(s.Rng);
            s.Rng = r.seed;
            int bonus = MovementBonus(s);
            int total = r.value + bonus;
            s.LastRoll = total;
            s.MovesLeft = total;
            s.VisitedThisTurn = new List<string> { Geometry.Key(s.Party.X, s.Party.Y) };
            PushLog(s, LogKind.Player, $"Tiras el dado: {r.value}{(bonus != 0 ? $" +{bonus}" : "")} = {total} de movimiento.");
            s.Phase = Phase.Move;
            return s;
        }

        private static GameState DoStep(GameState s, int x, int y)
        {
            if (s.Phase != Phase.Move || s.MovesLeft <= 0) return s;
            if (!GetStepTargets(s).Any(t => t.x == x && t.y == y)) return s;

            s.Party.X = x;
            s.Party.Y = y;
            s.MovesLeft--;
            s.VisitedThisTurn.Add(Geometry.Key(x, y));
            s.Stats.Steps++;
            if (s.CastleSpawned) s.StepsSinceCastle++;

            var cell = s.Grid.TryGetValue(Geometry.Key(x, y), out var pc0) ? pc0 : null;
            if (cell != null && cell.BlockedByGeneralRegion.HasValue)
            {
                PushLog(s, LogKind.Combat, $"Vuelves a enfrentar al General que bloquea ({cell.Name}).");
                var region = cell.BlockedByGeneralRegion.Value;
                return SetupCombat(s, new CombatSetup
                {
                    Source = "world", Region = region, CardId = $"{Cards.RegionId(region)}-general", ReturnToMove = true,
                });
            }

            var result = ResolveTile(s, x, y);
            if (result == "castle")
                return SetupCombat(s, new CombatSetup { Source = "world", Region = Region.Volcan, CardId = "rey", IsRey = true });

            if (s.MovesLeft <= 0) return DoEndMove(s);
            return s;
        }

        private static GameState DoEndMove(GameState s)
        {
            if (s.Phase != Phase.Move) return s;
            s.MovesLeft = 0;
            s.Phase = Phase.World;
            PushLog(s, LogKind.System, "Terminas tu movimiento. Turno del Mundo.");
            return s;
        }

        private static string ResolveTile(GameState s, int x, int y)
        {
            if (!s.Grid.TryGetValue(Geometry.Key(x, y), out var cell)) return "none";

            if (cell.Kind == CardKind.Castillo) return "castle";

            if (cell.Kind == CardKind.Pueblo && !cell.VillageActivated)
            {
                cell.VillageActivated = true;
                var def = Cards.GetCard(cell.CardId);
                int bonus = def.VillageBonus ?? 0;
                if (cell.Region.HasValue) s.VillageBonus[cell.Region.Value] += bonus;
                s.Stats.VillagesActivated++;
                PushLog(s, LogKind.Player, $"Activas el Pueblo {cell.Name}: +{bonus} Poder a los héroes de {cell.Region}.");
            }

            if (cell.Kind == CardKind.Heroe && !cell.HeroRecruited)
            {
                cell.HeroRecruited = true;
                s.Party.Members.Add(new PartyMember
                {
                    CardId = cell.CardId, Name = cell.Name, BasePower = HeroRecruitPower, Region = cell.Region,
                });
                s.Stats.HeroesRecruited++;
                PushLog(s, LogKind.Player, $"¡{cell.Name} se une a tu Party! (§20)");
            }

            return "none";
        }

        // -------------------------------------------------------------------
        // Combate contra Generales (§24–28)
        // -------------------------------------------------------------------
        private struct CombatSetup
        {
            public string Source;
            public Region Region;
            public string CardId;
            public bool ReturnToMove;
            public bool IsRey;
        }

        private static (int r, int c) PlaceHeart(GameState s, int N)
        {
            var a = Rng.NextInt(s.Rng, 0, N - 1);
            s.Rng = a.seed;
            var b = Rng.NextInt(s.Rng, 0, N - 1);
            s.Rng = b.seed;
            return (a.value, b.value);
        }

        private static GameState SetupCombat(GameState s, CombatSetup opts)
        {
            bool isRey = opts.IsRey;
            int generalPower = isRey ? GetReyPower(s) : GetCurrentGeneralPower(s);
            int partyPower = GetPartyPower(s);
            int gridN = CombatGridFor(s.GeneralsDefeated, isRey);
            var fc = CombatForecastFor(partyPower, generalPower, isRey);
            var (hr, hc) = PlaceHeart(s, gridN);
            s.PendingCombat = new PendingCombat
            {
                Source = opts.Source,
                Region = opts.Region,
                CardId = opts.CardId,
                ReturnToMove = opts.ReturnToMove,
                IsRey = isRey,
                GeneralPower = generalPower,
                PartyPower = partyPower,
                GridN = gridN,
                AttemptsTotal = fc.Attempts,
                AttemptsUsed = 0,
                Started = false,
                HeartR = hr,
                HeartC = hc,
                HeartsTotal = isRey ? 2 : 1,
                HeartsFound = 0,
                Revealed = new List<RevealedCell>(),
            };
            s.Phase = Phase.Combat;
            s.Stats.GeneralsFought++;
            return s;
        }

        private static GameState CombatFinish(GameState s, PendingCombat pc)
        {
            s.PendingCombat = null;
            if (s.Status != GameStatus.Playing) return s;
            if (pc.Source == "player") s.Phase = Phase.Roll;
            else if (pc.ReturnToMove && s.MovesLeft > 0) s.Phase = Phase.Move;
            else if (pc.Source == "world") return EndWorld(s);
            else s.Phase = s.MovesLeft > 0 ? Phase.Move : Phase.World;
            return s;
        }

        private static GameState CombatVictory(GameState s, PendingCombat pc)
        {
            if (pc.IsRey)
            {
                s.Status = GameStatus.Won;
                s.EndReason = "¡Derrotaste al Rey Demonio! Destruiste sus Corazones.";
                PushLog(s, LogKind.Combat, s.EndReason);
                s.PendingCombat = null;
                return s;
            }
            s.GeneralsDefeated++;
            PushLog(s, LogKind.Combat, $"¡Derrotas al General Demonio de {Cards.RegionLabel(pc.Region)}! (Corazón destruido).");
            RemoveGeneralCard(s, pc.CardId);
            if (s.Grid.TryGetValue(Geometry.Key(s.Party.X, s.Party.Y), out var cell) && cell.BlockedByGeneralRegion.HasValue)
                cell.BlockedByGeneralRegion = null;
            if (s.GeneralsDefeated == 4) SpawnCastle(s);
            return CombatFinish(s, pc);
        }

        private static GameState DoCombatSelect(GameState s, int r, int c)
        {
            if (s.Phase != Phase.Combat || s.PendingCombat == null) return s;
            var pc = s.PendingCombat;
            if (r < 0 || c < 0 || r >= pc.GridN || c >= pc.GridN) return s;
            if (pc.Revealed.Any(v => v.R == r && v.C == c)) return s;

            pc.Started = true;
            pc.AttemptsUsed++;

            if (r == pc.HeartR && c == pc.HeartC)
            {
                pc.HeartsFound++;
                if (pc.HeartsFound < pc.HeartsTotal)
                {
                    var (nr, nc) = PlaceHeart(s, pc.GridN);
                    pc.HeartR = nr;
                    pc.HeartC = nc;
                    pc.Revealed = new List<RevealedCell>();
                    PushLog(s, LogKind.Combat, $"¡Destruyes un Corazón del Rey! Aparece el siguiente ({pc.HeartsFound}/{pc.HeartsTotal}).");
                    return s;
                }
                return CombatVictory(s, pc);
            }

            int dist = Manhattan(r, c, pc.HeartR, pc.HeartC);
            pc.Revealed.Add(new RevealedCell { R = r, C = c, Dist = dist });
            if (pc.AttemptsUsed >= pc.AttemptsTotal)
                return Lose(s, pc.IsRey
                    ? "El Rey Demonio te venció: agotaste tus intentos sin hallar sus Corazones."
                    : "Agotaste tus intentos sin encontrar el Corazón del General.");
            return s;
        }

        private static GameState DoCombatRetreat(GameState s)
        {
            if (s.Phase != Phase.Combat || s.PendingCombat == null) return s;
            var pc = s.PendingCombat;
            if (pc.Started) return s;

            PushLog(s, LogKind.Combat, "Te retiras del combate. El General bloquea la casilla y retrocedes (§26).");
            RemoveGeneralCard(s, pc.CardId);
            if (!pc.IsRey)
            {
                if (s.Grid.TryGetValue(Geometry.Key(s.Party.X, s.Party.Y), out var here)) here.BlockedByGeneralRegion = pc.Region;
            }

            var retreatTargets = Geometry.ConnectedNeighbors(s.Grid, s.Party.X, s.Party.Y)
                .Where(n => s.Grid.TryGetValue(Geometry.Key(n.x, n.y), out var cc) && !cc.BlockedByGeneralRegion.HasValue)
                .ToList();
            string cameFrom = s.VisitedThisTurn.Count >= 2 ? s.VisitedThisTurn[s.VisitedThisTurn.Count - 2] : null;
            (int x, int y)? preferred = null;
            foreach (var n in retreatTargets)
                if (Geometry.Key(n.x, n.y) == cameFrom) { preferred = n; break; }
            if (preferred == null && retreatTargets.Count > 0) preferred = retreatTargets[0];
            if (preferred == null) return Lose(s, "No hay ruta alternativa para retroceder (§26).");

            s.Party.X = preferred.Value.x;
            s.Party.Y = preferred.Value.y;
            s.MovesLeft = 0;
            return CombatFinish(s, pc);
        }

        private static void RemoveGeneralCard(GameState s, string cardId)
        {
            if (s.Hand.Contains(cardId))
            {
                s.Hand.Remove(cardId);
                s.Discard.Add(cardId);
                if (s.PossessedGeneralId == cardId) s.PossessedGeneralId = null;
            }
        }

        // -------------------------------------------------------------------
        // Castillo y combate final (§29–31)
        // -------------------------------------------------------------------
        private static void SpawnCastle(GameState s)
        {
            s.CastleSpawned = true;
            var ends = Geometry.OpenEnds(s.Grid, s.Party.X, s.Party.Y);
            if (ends.Count == 0)
            {
                PushLog(s, LogKind.System, "No hay Extremos Abiertos; el Castillo aparece cerca del Party.");
                return;
            }
            // El más lejano (§29). Empates: primero en orden determinista.
            ends.Sort((a, b) => b.fromDist.CompareTo(a.fromDist));
            var spot = ends[0];
            s.Grid[Geometry.Key(spot.x, spot.y)] = new PlacedCard
            {
                CardId = "castillo", X = spot.x, Y = spot.y, Kind = CardKind.Castillo,
                Name = "Castillo del Rey Demonio", Connections = new List<Dir> { Dir.Up, Dir.Down, Dir.Left, Dir.Right },
            };
            string dist = spot.fromDist == int.MaxValue ? "∞" : spot.fromDist.ToString();
            PushLog(s, LogKind.System, $"¡Han caído los 4 Generales! El Castillo aparece a {dist} pasos. Comienza la Corrupción (§30).");
        }

        // -------------------------------------------------------------------
        // Turno del Mundo (§12–14, §19)
        // -------------------------------------------------------------------
        private static GameState DoWorldStep(GameState s)
        {
            if (s.Phase != Phase.World || s.PendingWorldPlacement != null) return s;
            if (s.Deck.Count == 0) return Lose(s, "El Mundo debía robar y el Mazo está vacío (§32).");
            string id = s.Deck[0];
            s.Deck.RemoveAt(0);
            var card = Cards.GetCard(id);

            if (card.Kind == CardKind.General)
            {
                PushLog(s, LogKind.World, $"El Mundo revela a {card.Name}: aparece sobre tu Party.");
                return SetupCombat(s, new CombatSetup { Source = "world", Region = card.Region.Value, CardId = id, ReturnToMove = false });
            }

            var placements = Geometry.PlacementsForCard(s, card);
            if (placements.Count == 0)
            {
                s.Discard.Add(id);
                PushLog(s, LogKind.World, $"El Mundo no puede colocar {card.Name}: se descarta (§14).");
                return EndWorld(s);
            }

            var dist = Geometry.TravelDistances(s.Grid, s.Party.X, s.Party.Y);
            const int INF = 999;
            var scored = placements.Select(p =>
            {
                int best = int.MaxValue;
                foreach (var n in Geometry.ConnectedNeighbors(s.Grid, p.X, p.Y))
                    if (dist.TryGetValue(Geometry.Key(n.x, n.y), out var d)) best = Math.Min(best, d + 1);
                if (best == int.MaxValue) best = INF;
                return (p, dist: best);
            }).ToList();

            bool wantFarthest = card.Kind == CardKind.Heroe;
            scored.Sort((a, b) => wantFarthest ? b.dist.CompareTo(a.dist) : a.dist.CompareTo(b.dist));
            int target = scored[0].dist;
            var tied = scored.Where(x => x.dist == target).ToList();

            if (tied.Count > 1)
            {
                s.PendingWorldPlacement = new PendingWorldPlacement
                {
                    CardId = id,
                    Options = tied.Select(t => new WorldPlacementOption
                    {
                        X = t.p.X, Y = t.p.Y, Connections = new List<Dir>(t.p.Connections),
                    }).ToList(),
                };
                PushLog(s, LogKind.World, $"El Mundo revela {card.Name}: elige entre {tied.Count} posiciones empatadas.");
                return s;
            }

            PlaceWorldCard(s, id, scored[0].p);
            return EndWorld(s);
        }

        private static GameState DoResolveWorldPlacement(GameState s, int x, int y)
        {
            if (s.Phase != Phase.World || s.PendingWorldPlacement == null) return s;
            var pending = s.PendingWorldPlacement;
            var opt = pending.Options.FirstOrDefault(o => o.X == x && o.Y == y);
            if (opt == null) return s;
            var card = Cards.GetCard(pending.CardId);
            Orientation orientation = card.Kind == CardKind.Heroe
                ? (opt.Connections.Contains(Dir.Left) ? Orientation.Horizontal : Orientation.Vertical)
                : Orientation.None;
            PlaceWorldCard(s, pending.CardId, new Placement { X = x, Y = y, Connections = new List<Dir>(opt.Connections), Orientation = orientation });
            s.PendingWorldPlacement = null;
            return EndWorld(s);
        }

        private static void PlaceWorldCard(GameState s, string cardId, Placement p)
        {
            var card = Cards.GetCard(cardId);
            s.Grid[Geometry.Key(p.X, p.Y)] = new PlacedCard
            {
                CardId = cardId, X = p.X, Y = p.Y, Kind = card.Kind, Name = card.Name,
                Region = card.Region, Connections = new List<Dir>(p.Connections),
            };
            PushLog(s, LogKind.World, $"El Mundo coloca {card.Name} en ({p.X}, {p.Y}).");
        }

        private static GameState EndWorld(GameState s)
        {
            if (s.Status != GameStatus.Playing) return s;
            s.Turn++;
            s.LastRoll = null;
            s.MovesLeft = 0;
            s.VisitedThisTurn = new List<string>();
            s.Phase = Phase.Draw;
            return s;
        }

        private static GameState Lose(GameState s, string reason)
        {
            s.Status = GameStatus.Lost;
            s.EndReason = reason;
            PushLog(s, LogKind.System, $"GAME OVER: {reason}");
            return s;
        }

        // -------------------------------------------------------------------
        // checkGameOver / getAIMove
        // -------------------------------------------------------------------
        public static GameOverInfo CheckGameOver(GameState s)
            => new() { Over = s.Status != GameStatus.Playing, Status = s.Status, Reason = s.EndReason };

        public static GameAction GetAIMove(GameState s)
        {
            if (s.Status != GameStatus.Playing) return null;
            if (s.Phase == Phase.World)
            {
                if (s.PendingWorldPlacement != null)
                {
                    var first = s.PendingWorldPlacement.Options[0];
                    return GameAction.ResolveWorldPlacement(first.X, first.Y);
                }
                return GameAction.WorldStep();
            }
            return null;
        }
    }
}
