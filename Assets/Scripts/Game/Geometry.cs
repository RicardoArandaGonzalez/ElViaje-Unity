// ============================================================================
// Geometría del mapa: conexiones, adyacencia, rutas y colocaciones legales.
// Port de src/game/geometry.ts.
// Nota de determinismo: JS itera Object.keys en orden de inserción; C# no
// garantiza el orden de un Dictionary, así que ordenamos las claves con
// SortedGridKeys() donde el orden de iteración es observable.
// ============================================================================
using System;
using System.Collections.Generic;

namespace ElViaje.Game
{
    public static class Geometry
    {
        public static readonly Dir[] Dirs = { Dir.Up, Dir.Down, Dir.Left, Dir.Right };

        public static (int dx, int dy) Delta(Dir d) => d switch
        {
            Dir.Up => (0, -1),
            Dir.Down => (0, 1),
            Dir.Left => (-1, 0),
            Dir.Right => (1, 0),
            _ => (0, 0),
        };

        public static Dir Opposite(Dir d) => d switch
        {
            Dir.Up => Dir.Down,
            Dir.Down => Dir.Up,
            Dir.Left => Dir.Right,
            Dir.Right => Dir.Left,
            _ => d,
        };

        public static string Key(int x, int y) => $"{x},{y}";

        public static (int x, int y) ParseKey(string k)
        {
            int comma = k.IndexOf(',');
            int x = int.Parse(k.Substring(0, comma));
            int y = int.Parse(k.Substring(comma + 1));
            return (x, y);
        }

        public static PlacedCard CellAt(Dictionary<string, PlacedCard> grid, int x, int y)
            => grid.TryGetValue(Key(x, y), out var c) ? c : null;

        /// <summary>Claves del grid en orden determinista (por y, luego x).</summary>
        private static List<string> SortedGridKeys(Dictionary<string, PlacedCard> grid)
        {
            var keys = new List<string>(grid.Keys);
            keys.Sort((a, b) =>
            {
                var (ax, ay) = ParseKey(a);
                var (bx, by) = ParseKey(b);
                if (ay != by) return ay.CompareTo(by);
                return ax.CompareTo(bx);
            });
            return keys;
        }

        private static bool ConnectsTo(Dictionary<string, PlacedCard> grid, int x, int y, List<Dir> connections, Dir d)
        {
            if (!connections.Contains(d)) return false;
            var (dx, dy) = Delta(d);
            var neighbor = CellAt(grid, x + dx, y + dy);
            if (neighbor == null) return false;
            return neighbor.Connections.Contains(Opposite(d));
        }

        public static List<Dir> ConnectedDirs(Dictionary<string, PlacedCard> grid, int x, int y)
        {
            var cell = CellAt(grid, x, y);
            var outp = new List<Dir>();
            if (cell == null) return outp;
            foreach (var d in Dirs)
                if (ConnectsTo(grid, x, y, cell.Connections, d)) outp.Add(d);
            return outp;
        }

        public static List<(int x, int y)> ConnectedNeighbors(Dictionary<string, PlacedCard> grid, int x, int y)
        {
            var outp = new List<(int, int)>();
            foreach (var d in ConnectedDirs(grid, x, y))
            {
                var (dx, dy) = Delta(d);
                outp.Add((x + dx, y + dy));
            }
            return outp;
        }

        /// <summary>Celdas vacías adyacentes a alguna carta (la "frontera").</summary>
        public static List<(int x, int y)> FrontierCells(Dictionary<string, PlacedCard> grid)
        {
            var seen = new HashSet<string>();
            var outp = new List<(int, int)>();
            foreach (var k in SortedGridKeys(grid))
            {
                var (x, y) = ParseKey(k);
                foreach (var d in Dirs)
                {
                    var (dx, dy) = Delta(d);
                    int nx = x + dx, ny = y + dy;
                    string nk = Key(nx, ny);
                    if (!grid.ContainsKey(nk) && seen.Add(nk)) outp.Add((nx, ny));
                }
            }
            return outp;
        }

        /// <summary>Colocaciones legales de una carta concreta (§6, §7, §17).</summary>
        public static List<Placement> PlacementsForCard(GameState state, CardDef card)
        {
            var grid = state.Grid;
            var outp = new List<Placement>();

            foreach (var (x, y) in FrontierCells(grid))
            {
                if (card.Kind == CardKind.Heroe)
                {
                    var axes = new List<Orientation>(); // orden: como Dirs (up,down,left,right)
                    foreach (var d in Dirs)
                    {
                        var (dx, dy) = Delta(d);
                        var n = CellAt(grid, x + dx, y + dy);
                        if (n != null && n.Connections.Contains(Opposite(d)))
                        {
                            var axis = (d == Dir.Left || d == Dir.Right) ? Orientation.Horizontal : Orientation.Vertical;
                            if (!axes.Contains(axis)) axes.Add(axis);
                        }
                    }
                    foreach (var orientation in axes)
                    {
                        var connections = orientation == Orientation.Horizontal
                            ? new List<Dir> { Dir.Left, Dir.Right }
                            : new List<Dir> { Dir.Up, Dir.Down };
                        outp.Add(new Placement { X = x, Y = y, Orientation = orientation, Connections = connections });
                    }
                }
                else
                {
                    bool valid = false;
                    foreach (var d in Dirs)
                        if (ConnectsTo(grid, x, y, card.Connections, d)) { valid = true; break; }
                    if (valid) outp.Add(new Placement { X = x, Y = y, Connections = new List<Dir>(card.Connections) });
                }
            }
            return outp;
        }

        public static bool HasLegalPlacement(GameState state, CardDef card)
            => PlacementsForCard(state, card).Count > 0;

        /// <summary>Distancia de recorrido (en cartas) por Conexiones Válidas (BFS).</summary>
        public static Dictionary<string, int> TravelDistances(Dictionary<string, PlacedCard> grid, int fromX, int fromY)
        {
            var dist = new Dictionary<string, int>();
            string start = Key(fromX, fromY);
            if (!grid.ContainsKey(start)) return dist;
            dist[start] = 0;
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((fromX, fromY));
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int baseDist = dist[Key(cur.x, cur.y)];
                foreach (var n in ConnectedNeighbors(grid, cur.x, cur.y))
                {
                    string nk = Key(n.x, n.y);
                    if (!dist.ContainsKey(nk))
                    {
                        dist[nk] = baseDist + 1;
                        queue.Enqueue(n);
                    }
                }
            }
            return dist;
        }

        /// <summary>Extremos Abiertos Válidos (§9). fromDist = int.MaxValue si inalcanzable.</summary>
        public static List<(int x, int y, int fromDist)> OpenEnds(Dictionary<string, PlacedCard> grid, int fromX, int fromY)
        {
            var dist = TravelDistances(grid, fromX, fromY);
            var outp = new List<(int, int, int)>();
            var seen = new HashSet<string>();
            foreach (var k in SortedGridKeys(grid))
            {
                var cell = grid[k];
                var (x, y) = ParseKey(k);
                foreach (var d in cell.Connections)
                {
                    var (dx, dy) = Delta(d);
                    int nx = x + dx, ny = y + dy;
                    string nk = Key(nx, ny);
                    if (grid.ContainsKey(nk)) continue;
                    if (!seen.Add(nk)) continue;
                    int fromDist = dist.TryGetValue(k, out var dFrom) ? dFrom + 1 : int.MaxValue;
                    outp.Add((nx, ny, fromDist));
                }
            }
            return outp;
        }
    }
}
