// ============================================================================
// SaveSystem — guarda/carga la partida en PlayerPrefs como JSON usando el
// JsonUtility de Unity (sin paquetes externos). Como JsonUtility no serializa
// diccionarios ni nullables, se mapea el GameState a un DTO plano.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ElViaje.Game;

namespace ElViaje.App
{
    public static class SaveSystem
    {
        const string Key = "elviaje.save.v1";

        public static bool HasSave =>
            PlayerPrefs.HasKey(Key) && !string.IsNullOrEmpty(PlayerPrefs.GetString(Key));

        public static void Save(GameState s)
        {
            if (s == null) return;
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(ToDto(s)));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning($"SaveSystem: no se pudo guardar: {e.Message}"); }
        }

        public static GameState Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, null);
                if (string.IsNullOrEmpty(json)) return null;
                var dto = JsonUtility.FromJson<SaveDto>(json);
                return dto == null ? null : FromDto(dto);
            }
            catch (Exception e) { Debug.LogWarning($"SaveSystem: no se pudo cargar: {e.Message}"); return null; }
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        // -------------------------------------------------------------------
        // Helpers de mapeo enum/nullable
        // -------------------------------------------------------------------
        static int RegOut(Region? r) => r.HasValue ? (int)r.Value : -1;
        static Region? RegIn(int i) => i < 0 ? (Region?)null : (Region)i;
        static List<int> DirsOut(List<Dir> d) => d.Select(x => (int)x).ToList();
        static List<Dir> DirsIn(List<int> d) => (d ?? new List<int>()).Select(x => (Dir)x).ToList();
        static string StrOut(string s) => s ?? "";
        static string StrIn(string s) => string.IsNullOrEmpty(s) ? null : s;

        // -------------------------------------------------------------------
        // GameState -> DTO
        // -------------------------------------------------------------------
        static SaveDto ToDto(GameState s)
        {
            var d = new SaveDto
            {
                status = (int)s.Status,
                endReason = StrOut(s.EndReason),
                difficulty = (int)s.Difficulty,
                rng = s.Rng,
                phase = (int)s.Phase,
                turn = s.Turn,
                partyX = s.Party.X,
                partyY = s.Party.Y,
                deck = new List<string>(s.Deck),
                hand = new List<string>(s.Hand),
                discard = new List<string>(s.Discard),
                possessedGeneralId = StrOut(s.PossessedGeneralId),
                lastRoll = s.LastRoll ?? -1,
                movesLeft = s.MovesLeft,
                visitedThisTurn = new List<string>(s.VisitedThisTurn),
                generalsDefeated = s.GeneralsDefeated,
                castleSpawned = s.CastleSpawned,
                stepsSinceCastle = s.StepsSinceCastle,
                bonusBosque = s.VillageBonus.TryGetValue(Region.Bosque, out var b) ? b : 0,
                bonusPlanicies = s.VillageBonus.TryGetValue(Region.Planicies, out var p) ? p : 0,
                bonusMontanas = s.VillageBonus.TryGetValue(Region.Montanas, out var m) ? m : 0,
                bonusVolcan = s.VillageBonus.TryGetValue(Region.Volcan, out var v) ? v : 0,
                steps = s.Stats.Steps,
                cardsPlayed = s.Stats.CardsPlayed,
                villagesActivated = s.Stats.VillagesActivated,
                heroesRecruited = s.Stats.HeroesRecruited,
                generalsFought = s.Stats.GeneralsFought,
                members = s.Party.Members.Select(x => new MemberDto
                {
                    cardId = x.CardId, name = x.Name, basePower = x.BasePower, region = RegOut(x.Region), isHeroine = x.IsHeroine,
                }).ToList(),
                grid = s.Grid.Values.Select(c => new PlacedDto
                {
                    cardId = c.CardId, x = c.X, y = c.Y, kind = (int)c.Kind, name = c.Name, region = RegOut(c.Region),
                    conns = DirsOut(c.Connections), villageActivated = c.VillageActivated, heroRecruited = c.HeroRecruited,
                    blockedRegion = RegOut(c.BlockedByGeneralRegion),
                }).ToList(),
                log = s.Log.Select(l => new LogDto { turn = l.Turn, text = l.Text, kind = (int)l.Kind }).ToList(),
                lastRoad = StrOut(s.LastRoad),
                chronicle = s.Chronicle.Select(e => new ChronDto { kind = e.Kind, name = StrOut(e.Name), road = StrOut(e.Road), extra = StrOut(e.Extra) }).ToList(),
                hasCombat = s.PendingCombat != null,
                hasWorld = s.PendingWorldPlacement != null,
            };

            if (s.PendingCombat != null)
            {
                var pc = s.PendingCombat;
                d.combat = new CombatDto
                {
                    source = pc.Source, region = (int)pc.Region, cardId = pc.CardId, returnToMove = pc.ReturnToMove,
                    isRey = pc.IsRey, generalPower = pc.GeneralPower, partyPower = pc.PartyPower, gridN = pc.GridN,
                    attemptsTotal = pc.AttemptsTotal, attemptsUsed = pc.AttemptsUsed, started = pc.Started,
                    heartR = pc.HeartR, heartC = pc.HeartC, heartsTotal = pc.HeartsTotal, heartsFound = pc.HeartsFound,
                    revealed = pc.Revealed.Select(r => new RevealedDto { r = r.R, c = r.C, dist = r.Dist }).ToList(),
                };
            }
            if (s.PendingWorldPlacement != null)
            {
                d.world = new WorldDto
                {
                    cardId = s.PendingWorldPlacement.CardId,
                    options = s.PendingWorldPlacement.Options.Select(o => new OptionDto
                    {
                        x = o.X, y = o.Y, conns = DirsOut(o.Connections),
                    }).ToList(),
                };
            }
            return d;
        }

        // -------------------------------------------------------------------
        // DTO -> GameState
        // -------------------------------------------------------------------
        static GameState FromDto(SaveDto d)
        {
            var s = new GameState
            {
                Status = (GameStatus)d.status,
                EndReason = StrIn(d.endReason),
                Difficulty = (Difficulty)d.difficulty,
                Rng = (uint)d.rng,
                Phase = (Phase)d.phase,
                Turn = d.turn,
                Party = new Party { X = d.partyX, Y = d.partyY },
                Deck = new List<string>(d.deck ?? new List<string>()),
                Hand = new List<string>(d.hand ?? new List<string>()),
                Discard = new List<string>(d.discard ?? new List<string>()),
                PossessedGeneralId = StrIn(d.possessedGeneralId),
                LastRoll = d.lastRoll < 0 ? (int?)null : d.lastRoll,
                MovesLeft = d.movesLeft,
                VisitedThisTurn = new List<string>(d.visitedThisTurn ?? new List<string>()),
                GeneralsDefeated = d.generalsDefeated,
                CastleSpawned = d.castleSpawned,
                StepsSinceCastle = d.stepsSinceCastle,
                VillageBonus = new Dictionary<Region, int>
                {
                    [Region.Bosque] = d.bonusBosque, [Region.Planicies] = d.bonusPlanicies,
                    [Region.Montanas] = d.bonusMontanas, [Region.Volcan] = d.bonusVolcan,
                },
                Stats = new Stats
                {
                    Steps = d.steps, CardsPlayed = d.cardsPlayed, VillagesActivated = d.villagesActivated,
                    HeroesRecruited = d.heroesRecruited, GeneralsFought = d.generalsFought,
                },
            };

            foreach (var mm in d.members ?? new List<MemberDto>())
                s.Party.Members.Add(new PartyMember { CardId = mm.cardId, Name = mm.name, BasePower = mm.basePower, Region = RegIn(mm.region), IsHeroine = mm.isHeroine });

            foreach (var c in d.grid ?? new List<PlacedDto>())
                s.Grid[Geometry.Key(c.x, c.y)] = new PlacedCard
                {
                    CardId = c.cardId, X = c.x, Y = c.y, Kind = (CardKind)c.kind, Name = c.name, Region = RegIn(c.region),
                    Connections = DirsIn(c.conns), VillageActivated = c.villageActivated, HeroRecruited = c.heroRecruited,
                    BlockedByGeneralRegion = RegIn(c.blockedRegion),
                };

            foreach (var l in d.log ?? new List<LogDto>())
                s.Log.Add(new LogEntry { Turn = l.turn, Text = l.text, Kind = (LogKind)l.kind });

            s.LastRoad = StrIn(d.lastRoad);
            foreach (var e in d.chronicle ?? new List<ChronDto>())
                s.Chronicle.Add(new ChronicleEvent { Kind = e.kind, Name = StrIn(e.name), Road = StrIn(e.road), Extra = StrIn(e.extra) });

            if (d.hasCombat && d.combat != null)
            {
                var pc = d.combat;
                s.PendingCombat = new PendingCombat
                {
                    Source = pc.source, Region = (Region)pc.region, CardId = pc.cardId, ReturnToMove = pc.returnToMove,
                    IsRey = pc.isRey, GeneralPower = pc.generalPower, PartyPower = pc.partyPower, GridN = pc.gridN,
                    AttemptsTotal = pc.attemptsTotal, AttemptsUsed = pc.attemptsUsed, Started = pc.started,
                    HeartR = pc.heartR, HeartC = pc.heartC, HeartsTotal = pc.heartsTotal, HeartsFound = pc.heartsFound,
                    Revealed = (pc.revealed ?? new List<RevealedDto>()).Select(r => new RevealedCell { R = r.r, C = r.c, Dist = r.dist }).ToList(),
                };
            }
            if (d.hasWorld && d.world != null)
            {
                s.PendingWorldPlacement = new PendingWorldPlacement
                {
                    CardId = d.world.cardId,
                    Options = (d.world.options ?? new List<OptionDto>()).Select(o => new WorldPlacementOption
                    {
                        X = o.x, Y = o.y, Connections = DirsIn(o.conns),
                    }).ToList(),
                };
            }
            return s;
        }

        // -------------------------------------------------------------------
        // DTOs (planos, [Serializable] para JsonUtility)
        // -------------------------------------------------------------------
        [Serializable]
        class SaveDto
        {
            public int status, difficulty, phase, turn;
            public string endReason, possessedGeneralId;
            public long rng;
            public int partyX, partyY, lastRoll, movesLeft, generalsDefeated, stepsSinceCastle;
            public bool castleSpawned;
            public int bonusBosque, bonusPlanicies, bonusMontanas, bonusVolcan;
            public int steps, cardsPlayed, villagesActivated, heroesRecruited, generalsFought;
            public List<string> deck, hand, discard, visitedThisTurn;
            public List<MemberDto> members;
            public List<PlacedDto> grid;
            public List<LogDto> log;
            public bool hasCombat, hasWorld;
            public CombatDto combat;
            public WorldDto world;
            public string lastRoad;
            public List<ChronDto> chronicle;
        }

        [Serializable] class ChronDto { public string kind, name, road, extra; }

        [Serializable] class MemberDto { public string cardId, name; public int basePower, region; public bool isHeroine; }
        [Serializable] class PlacedDto { public string cardId, name; public int x, y, kind, region, blockedRegion; public List<int> conns; public bool villageActivated, heroRecruited; }
        [Serializable] class LogDto { public int turn, kind; public string text; }
        [Serializable] class RevealedDto { public int r, c, dist; }
        [Serializable] class CombatDto { public string source, cardId; public int region, generalPower, partyPower, gridN, attemptsTotal, attemptsUsed, heartR, heartC, heartsTotal, heartsFound; public bool returnToMove, isRey, started; public List<RevealedDto> revealed; }
        [Serializable] class OptionDto { public int x, y; public List<int> conns; }
        [Serializable] class WorldDto { public string cardId; public List<OptionDto> options; }
    }
}
