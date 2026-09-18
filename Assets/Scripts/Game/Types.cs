// ============================================================================
// Tipos del dominio (lógica pura, sin Unity). Port de src/game/types.ts.
// Clases mutables con Clone() profundo (equivalente a structuredClone en JS).
// ============================================================================
using System;
using System.Collections.Generic;

namespace ElViaje.Game
{
    /// <summary>Una carta ya colocada en el mapa.</summary>
    public sealed class PlacedCard
    {
        public string CardId;
        public int X;
        public int Y;
        public CardKind Kind;
        public string Name;
        public Region? Region;
        public List<Dir> Connections = new();
        public bool VillageActivated;
        public bool HeroRecruited;
        /// <summary>Un General bloquea esta posición (null = no bloqueada).</summary>
        public Region? BlockedByGeneralRegion;

        public PlacedCard Clone() => new()
        {
            CardId = CardId,
            X = X,
            Y = Y,
            Kind = Kind,
            Name = Name,
            Region = Region,
            Connections = new List<Dir>(Connections),
            VillageActivated = VillageActivated,
            HeroRecruited = HeroRecruited,
            BlockedByGeneralRegion = BlockedByGeneralRegion,
        };
    }

    /// <summary>Miembro del Party.</summary>
    public sealed class PartyMember
    {
        public string CardId;
        public string Name;
        public int BasePower;
        public Region? Region;          // null = héroe/heroína inicial
        public bool IsHeroine;

        public PartyMember Clone() => new()
        {
            CardId = CardId, Name = Name, BasePower = BasePower, Region = Region, IsHeroine = IsHeroine,
        };
    }

    public sealed class LogEntry
    {
        public int Turn;
        public string Text;
        public LogKind Kind;

        public LogEntry Clone() => new() { Turn = Turn, Text = Text, Kind = Kind };
    }

    /// <summary>Evento de la crónica (la prosa se genera en la capa de UI).</summary>
    public sealed class ChronicleEvent
    {
        public string Kind;   // start | pueblo | hero | general | final | defeat | victory
        public string Name;   // nombre de carta / enemigo
        public string Road;   // último camino recorrido antes del evento (o null)
        public string Extra;  // usos varios (p. ej. tipo de la primera carta)

        public ChronicleEvent Clone() => new() { Kind = Kind, Name = Name, Road = Road, Extra = Extra };
    }

    /// <summary>Una colocación candidata para una carta.</summary>
    public sealed class Placement
    {
        public int X;
        public int Y;
        public Orientation Orientation = Orientation.None;
        public List<Dir> Connections = new();
    }

    public sealed class RevealedCell
    {
        public int R;
        public int C;
        public int Dist;

        public RevealedCell Clone() => new() { R = R, C = C, Dist = Dist };
    }

    /// <summary>Combate pendiente (minijuego de búsqueda del Corazón).</summary>
    public sealed class PendingCombat
    {
        public string Source;           // "player" | "world"
        public Region Region;
        public string CardId;
        public bool ReturnToMove;
        public bool IsRey;
        public int GeneralPower;
        public int PartyPower;
        public int GridN;
        public int AttemptsTotal;
        public int AttemptsUsed;
        public bool Started;
        public int HeartR;
        public int HeartC;
        public int HeartsTotal;
        public int HeartsFound;
        public List<RevealedCell> Revealed = new();

        public PendingCombat Clone()
        {
            var copy = new PendingCombat
            {
                Source = Source, Region = Region, CardId = CardId, ReturnToMove = ReturnToMove,
                IsRey = IsRey, GeneralPower = GeneralPower, PartyPower = PartyPower, GridN = GridN,
                AttemptsTotal = AttemptsTotal, AttemptsUsed = AttemptsUsed, Started = Started,
                HeartR = HeartR, HeartC = HeartC, HeartsTotal = HeartsTotal, HeartsFound = HeartsFound,
                Revealed = new List<RevealedCell>(Revealed.Count),
            };
            foreach (var v in Revealed) copy.Revealed.Add(v.Clone());
            return copy;
        }
    }

    public sealed class WorldPlacementOption
    {
        public int X;
        public int Y;
        public List<Dir> Connections = new();

        public WorldPlacementOption Clone() => new()
        {
            X = X, Y = Y, Connections = new List<Dir>(Connections),
        };
    }

    public sealed class PendingWorldPlacement
    {
        public string CardId;
        public List<WorldPlacementOption> Options = new();

        public PendingWorldPlacement Clone()
        {
            var copy = new PendingWorldPlacement { CardId = CardId, Options = new List<WorldPlacementOption>(Options.Count) };
            foreach (var o in Options) copy.Options.Add(o.Clone());
            return copy;
        }
    }

    public sealed class Party
    {
        public int X;
        public int Y;
        public List<PartyMember> Members = new();

        public Party Clone()
        {
            var copy = new Party { X = X, Y = Y, Members = new List<PartyMember>(Members.Count) };
            foreach (var m in Members) copy.Members.Add(m.Clone());
            return copy;
        }
    }

    public sealed class Stats
    {
        public int Steps;
        public int CardsPlayed;
        public int VillagesActivated;
        public int HeroesRecruited;
        public int GeneralsFought;

        public Stats Clone() => new()
        {
            Steps = Steps, CardsPlayed = CardsPlayed, VillagesActivated = VillagesActivated,
            HeroesRecruited = HeroesRecruited, GeneralsFought = GeneralsFought,
        };
    }

    /// <summary>Estado completo de la partida (mutable; se clona antes de aplicar).</summary>
    public sealed class GameState
    {
        public GameStatus Status = GameStatus.Playing;
        public string EndReason;
        public Difficulty Difficulty;

        /// <summary>Semilla del RNG determinista (avanza con cada uso).</summary>
        public uint Rng;

        public Phase Phase;
        public int Turn;

        /// <summary>Mapa: clave "x,y" -> carta colocada.</summary>
        public Dictionary<string, PlacedCard> Grid = new();

        public Party Party = new();

        public List<string> Deck = new();
        public List<string> Hand = new();
        public List<string> Discard = new();

        public string PossessedGeneralId;

        public int? LastRoll;
        public int MovesLeft;
        public List<string> VisitedThisTurn = new();

        public PendingCombat PendingCombat;

        public int GeneralsDefeated;
        public Dictionary<Region, int> VillageBonus = new();
        public bool CastleSpawned;
        public int StepsSinceCastle;

        public Stats Stats = new();
        public List<LogEntry> Log = new();

        /// <summary>Historial estructurado para la crónica final (§crónica).</summary>
        public List<ChronicleEvent> Chronicle = new();
        /// <summary>Último camino recorrido, para contextualizar los eventos.</summary>
        public string LastRoad;

        public PendingWorldPlacement PendingWorldPlacement;

        public GameState Clone()
        {
            var copy = new GameState
            {
                Status = Status,
                EndReason = EndReason,
                Difficulty = Difficulty,
                Rng = Rng,
                Phase = Phase,
                Turn = Turn,
                Party = Party.Clone(),
                Deck = new List<string>(Deck),
                Hand = new List<string>(Hand),
                Discard = new List<string>(Discard),
                PossessedGeneralId = PossessedGeneralId,
                LastRoll = LastRoll,
                MovesLeft = MovesLeft,
                VisitedThisTurn = new List<string>(VisitedThisTurn),
                PendingCombat = PendingCombat?.Clone(),
                GeneralsDefeated = GeneralsDefeated,
                CastleSpawned = CastleSpawned,
                StepsSinceCastle = StepsSinceCastle,
                Stats = Stats.Clone(),
                PendingWorldPlacement = PendingWorldPlacement?.Clone(),
                Grid = new Dictionary<string, PlacedCard>(Grid.Count),
                VillageBonus = new Dictionary<Region, int>(VillageBonus),
                Log = new List<LogEntry>(Log.Count),
                Chronicle = new List<ChronicleEvent>(Chronicle.Count),
                LastRoad = LastRoad,
            };
            foreach (var kv in Grid) copy.Grid[kv.Key] = kv.Value.Clone();
            foreach (var e in Log) copy.Log.Add(e.Clone());
            foreach (var e in Chronicle) copy.Chronicle.Add(e.Clone());
            return copy;
        }
    }

    // -----------------------------------------------------------------------
    // Acciones (equivalente al union GameAction de TS).
    // -----------------------------------------------------------------------
    public enum ActionType
    {
        Draw, PlayCard, Discard, InvokeGeneral, Roll, Step, EndMove,
        CombatSelect, CombatRetreat, WorldStep, ResolveWorldPlacement,
    }

    public sealed class GameAction
    {
        public ActionType Type;
        public string CardId;
        public int X;
        public int Y;
        public Orientation Orientation = Orientation.None;
        public int R;
        public int C;

        public static GameAction Draw() => new() { Type = ActionType.Draw };
        public static GameAction PlayCard(string cardId, int x, int y, Orientation o = Orientation.None)
            => new() { Type = ActionType.PlayCard, CardId = cardId, X = x, Y = y, Orientation = o };
        public static GameAction Discard(string cardId) => new() { Type = ActionType.Discard, CardId = cardId };
        public static GameAction InvokeGeneral() => new() { Type = ActionType.InvokeGeneral };
        public static GameAction Roll() => new() { Type = ActionType.Roll };
        public static GameAction Step(int x, int y) => new() { Type = ActionType.Step, X = x, Y = y };
        public static GameAction EndMove() => new() { Type = ActionType.EndMove };
        public static GameAction CombatSelect(int r, int c) => new() { Type = ActionType.CombatSelect, R = r, C = c };
        public static GameAction CombatRetreat() => new() { Type = ActionType.CombatRetreat };
        public static GameAction WorldStep() => new() { Type = ActionType.WorldStep };
        public static GameAction ResolveWorldPlacement(int x, int y)
            => new() { Type = ActionType.ResolveWorldPlacement, X = x, Y = y };
    }

    public struct GameOverInfo
    {
        public bool Over;
        public GameStatus Status;
        public string Reason;
    }

    public struct CombatForecast
    {
        public int Attempts;
        public CombatTier Tier;
        public string Label;
    }

    public struct NewGameOptions
    {
        public uint Seed;
        public Difficulty Difficulty;
        public Starter Starter;
    }
}
