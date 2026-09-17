// ============================================================================
// Catálogo de cartas (Distribución Beta v2.1, §33). Port de src/game/cards.ts.
// ============================================================================
using System;
using System.Collections.Generic;

namespace ElViaje.Game
{
    public sealed class CardDef
    {
        public string Id;
        public CardKind Kind;
        public string Name;
        public Region? Region;          // null en castillo / rey / iniciales
        public List<Dir> Connections;
        public int? Power;              // héroes
        public int? VillageBonus;       // pueblos (+1 / +2)
    }

    public static class Cards
    {
        public static readonly Dir[] All = { Dir.Left, Dir.Up, Dir.Right, Dir.Down };

        public static readonly Region[] Regions =
            { Game.Region.Bosque, Game.Region.Planicies, Game.Region.Montanas, Game.Region.Volcan };

        /// <summary>Poder de un General según cuántos van derrotados (§23).</summary>
        public static readonly int[] GeneralPowers = { 4, 7, 10, 13 };

        /// <summary>Poder base del Rey Demonio antes de la corrupción (§30).</summary>
        public const int ReyBasePower = 16;

        public static string RegionId(Region r) => r switch
        {
            Game.Region.Bosque => "bosque",
            Game.Region.Planicies => "planicies",
            Game.Region.Montanas => "montanas",
            Game.Region.Volcan => "volcan",
            _ => "bosque",
        };

        public static Region RegionFromId(string id) => id switch
        {
            "bosque" => Game.Region.Bosque,
            "planicies" => Game.Region.Planicies,
            "montanas" => Game.Region.Montanas,
            "volcan" => Game.Region.Volcan,
            _ => throw new ArgumentException($"Región desconocida: {id}"),
        };

        public static string RegionLabel(Region r) => r switch
        {
            Game.Region.Bosque => "Bosque",
            Game.Region.Planicies => "Planicies",
            Game.Region.Montanas => "Montañas",
            Game.Region.Volcan => "Volcán",
            _ => "",
        };

        private struct RawRoad { public string Name; public Dir[] C; public RawRoad(string n, Dir[] c) { Name = n; C = c; } }
        private struct RawTown { public string Name; public Dir[] C; public int Bonus; public RawTown(string n, Dir[] c, int b) { Name = n; C = c; Bonus = b; } }

        private static Dir[] D(params Dir[] d) => d;

        private static readonly Dictionary<Region, RawRoad[]> Caminos = new()
        {
            [Game.Region.Bosque] = new[]
            {
                new RawRoad("Camino del Bosque", D(Dir.Left, Dir.Right)),
                new RawRoad("Camino del Riachuelo", D(Dir.Left, Dir.Up)),
                new RawRoad("Camino de Flores", D(Dir.Left, Dir.Right, Dir.Down)),
                new RawRoad("Camino de la Arboleda", D(Dir.Up, Dir.Right, Dir.Down)),
                new RawRoad("Camino del Río", D(Dir.Left, Dir.Up, Dir.Down)),
                new RawRoad("Camino junto al Lago", D(Dir.Left, Dir.Up, Dir.Right)),
                new RawRoad("Camino junto a la Granja", D(Dir.Up, Dir.Down)),
            },
            [Game.Region.Planicies] = new[]
            {
                new RawRoad("Camino del Pastizal", D(Dir.Left, Dir.Right)),
                new RawRoad("Camino Perdido", D(Dir.Left)),
                new RawRoad("Camino de Arena", D(Dir.Up, Dir.Down)),
                new RawRoad("Camino de la Manada", D(Dir.Left, Dir.Right, Dir.Down)),
                new RawRoad("Camino del Río Seco", D(Dir.Up, Dir.Down)),
                new RawRoad("Camino del Desierto", D(Dir.Left, Dir.Down, Dir.Right)),
                new RawRoad("Camino Despejado", D(Dir.Left, Dir.Up, Dir.Down, Dir.Right)),
            },
            [Game.Region.Montanas] = new[]
            {
                new RawRoad("Camino del Acantilado", D(Dir.Up, Dir.Down)),
                new RawRoad("Camino del Pico Montañoso", D(Dir.Down)),
                new RawRoad("Camino de Roca Escarpada", D(Dir.Up, Dir.Right, Dir.Down)),
                new RawRoad("Camino de la Avalancha", D(Dir.Up)),
                new RawRoad("Camino de la Cascada", D(Dir.Up, Dir.Down)),
                new RawRoad("Camino del Caído", D(Dir.Down)),
                new RawRoad("Camino del Puente Viejo", D(Dir.Up, Dir.Down)),
            },
            [Game.Region.Volcan] = new[]
            {
                new RawRoad("Camino de Cenizas", D(Dir.Up, Dir.Right, Dir.Down)),
                new RawRoad("Camino del Calor Infernal", D(Dir.Up, Dir.Right, Dir.Down)),
                new RawRoad("Camino de Roca Fundida", D(Dir.Up, Dir.Right, Dir.Down)),
                new RawRoad("Camino de la Desesperación", D(Dir.Left, Dir.Up, Dir.Down)),
                new RawRoad("Camino junto a la Lava", D(Dir.Left, Dir.Up, Dir.Down)),
                new RawRoad("Camino de la Batalla", D(Dir.Left, Dir.Up, Dir.Down)),
                new RawRoad("Camino del Puente de Hierro", D(Dir.Left, Dir.Up, Dir.Down)),
            },
        };

        private static readonly Dictionary<Region, RawTown[]> Pueblos = new()
        {
            [Game.Region.Bosque] = new[] { new RawTown("Comarca", All, 1), new RawTown("Villa Molinos", All, 2) },
            [Game.Region.Planicies] = new[] { new RawTown("Fortaleza", All, 1), new RawTown("Aldea Nómada", All, 2) },
            [Game.Region.Montanas] = new[] { new RawTown("Escuela de Magia", All, 1), new RawTown("Aldea Nevada", All, 2) },
            [Game.Region.Volcan] = new[] { new RawTown("Campamento Imperial", All, 1), new RawTown("Aldea Devastada", D(Dir.Right), 2) },
        };

        private static readonly Dictionary<Region, string[]> HeroesByRegion = new()
        {
            [Game.Region.Bosque] = new[] { "Arquero de los Bosques", "Druida de los Bosques", "Asesino de los Bosques" },
            [Game.Region.Planicies] = new[] { "Chamán de las Planicies", "Guerrero de las Planicies", "Bardo de las Planicies" },
            [Game.Region.Montanas] = new[] { "Bárbaro de las Montañas", "Mago de las Montañas", "Monje de las Montañas" },
            [Game.Region.Volcan] = new[] { "Paladín del Volcán", "Clérigo del Volcán", "Nigromante del Volcán" },
        };

        private static readonly Dictionary<Region, string> GeneralName = new()
        {
            [Game.Region.Bosque] = "General Demonio de los Bosques",
            [Game.Region.Planicies] = "General Demonio de las Planicies",
            [Game.Region.Montanas] = "General Demonio de las Montañas",
            [Game.Region.Volcan] = "General Demonio del Volcán",
        };

        public static readonly Dictionary<string, CardDef> Catalog = BuildCatalog();

        private static Dictionary<string, CardDef> BuildCatalog()
        {
            var cat = new Dictionary<string, CardDef>();
            void Add(CardDef c) => cat[c.Id] = c;

            foreach (var region in Regions)
            {
                string rid = RegionId(region);
                var caminos = Caminos[region];
                for (int i = 0; i < caminos.Length; i++)
                    Add(new CardDef { Id = $"{rid}-camino-{i}", Kind = CardKind.Camino, Name = caminos[i].Name, Region = region, Connections = new List<Dir>(caminos[i].C) });

                var pueblos = Pueblos[region];
                for (int i = 0; i < pueblos.Length; i++)
                    Add(new CardDef { Id = $"{rid}-pueblo-{i}", Kind = CardKind.Pueblo, Name = pueblos[i].Name, Region = region, Connections = new List<Dir>(pueblos[i].C), VillageBonus = pueblos[i].Bonus });

                var heroes = HeroesByRegion[region];
                for (int i = 0; i < heroes.Length; i++)
                    Add(new CardDef { Id = $"{rid}-heroe-{i}", Kind = CardKind.Heroe, Name = heroes[i], Region = region, Connections = new List<Dir>(All), Power = 3 });

                Add(new CardDef { Id = $"{rid}-general", Kind = CardKind.General, Name = GeneralName[region], Region = region, Connections = new List<Dir>() });
            }

            Add(new CardDef { Id = "castillo", Kind = CardKind.Castillo, Name = "Castillo del Rey Demonio", Connections = new List<Dir>(All) });
            Add(new CardDef { Id = "rey", Kind = CardKind.Rey, Name = "Rey Demonio", Connections = new List<Dir>() });
            Add(new CardDef { Id = "inicial-heroe", Kind = CardKind.Heroe, Name = "Caballero", Connections = new List<Dir>(All), Power = 3 });
            Add(new CardDef { Id = "inicial-heroina", Kind = CardKind.Heroe, Name = "Mago", Connections = new List<Dir>(All), Power = 3 });

            return cat;
        }

        public static CardDef GetCard(string id)
        {
            if (Catalog.TryGetValue(id, out var c)) return c;
            throw new ArgumentException($"Carta desconocida: {id}");
        }

        /// <summary>Los 52 ids del Mazo de Aventura (excluye finales e iniciales).</summary>
        public static List<string> AdventureDeckIds()
        {
            var ids = new List<string>();
            foreach (var region in Regions)
            {
                string rid = RegionId(region);
                for (int i = 0; i < Caminos[region].Length; i++) ids.Add($"{rid}-camino-{i}");
                for (int i = 0; i < Pueblos[region].Length; i++) ids.Add($"{rid}-pueblo-{i}");
                for (int i = 0; i < HeroesByRegion[region].Length; i++) ids.Add($"{rid}-heroe-{i}");
                ids.Add($"{rid}-general");
            }
            return ids;
        }
    }
}
