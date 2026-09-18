// ============================================================================
// CardSprites — recorte de los spritesheets de cartas y fondo de mesa.
// Port de src/components/cardVisuals.ts (mismos rects medidos por píxel).
// Aplica el sprite como background de un VisualElement replicando la matemática
// de background-size / background-position en porcentajes (igual que la web).
// Texturas cargadas desde Assets/Resources/Art/ (sin cableado en el Inspector).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ElViaje.Game;

namespace ElViaje.App
{
    public struct SpriteRect
    {
        public string Sheet;   // "main" | "hero"
        public int Sx, Sy, Sw, Sh;
    }

    public static class CardSprites
    {
        const int SHEET_W = 1536, SHEET_H = 1024; // main y hero comparten dimensiones

        // --- Columnas por combinación de caminos (orden canónico L,U,R,D) ---
        static readonly Dictionary<string, (int x0, int x1)> ComboCol = new()
        {
            ["left,up,right,down"] = (26, 142),
            ["left,up,right"] = (142, 253),
            ["left,right,down"] = (253, 359),
            ["up,right,down"] = (359, 465),
            ["left,up,down"] = (465, 570),
            ["left,right"] = (570, 674),
            ["up,down"] = (674, 779),
            ["right,down"] = (779, 878),
            ["up,right"] = (878, 974),
            ["left,down"] = (974, 1070),
            ["left,up"] = (1070, 1158),
            ["left"] = (1158, 1241),
            ["right"] = (1241, 1326),
            ["up"] = (1326, 1411),
            ["down"] = (1411, 1491),
        };

        static readonly Dictionary<Region, (int y0, int y1)> RegionRow = new()
        {
            [Region.Bosque] = (71, 181),
            [Region.Planicies] = (262, 373),
            [Region.Montanas] = (452, 564),
            [Region.Volcan] = (642, 752),
        };

        static readonly Dictionary<Region, (int x0, int x1)> PuebloCol = new()
        {
            [Region.Bosque] = (52, 192),
            [Region.Planicies] = (209, 352),
            [Region.Montanas] = (369, 518),
            [Region.Volcan] = (535, 675),
        };
        static readonly (int y0, int y1) PuebloRow = (846, 990);
        static readonly (int x0, int y0, int x1, int y1) HeroDefault = (832, 812, 1026, 1006);

        static readonly SpriteRect HeroHorizontal = new() { Sheet = "hero", Sx = 42, Sy = 71, Sw = 685, Sh = 685 };
        static readonly SpriteRect HeroVertical = new() { Sheet = "hero", Sx = 805, Sy = 71, Sw = 686, Sh = 685 };

        static readonly Dir[] Order = { Dir.Left, Dir.Up, Dir.Right, Dir.Down };
        static string DirStr(Dir d) => d switch
        {
            Dir.Left => "left", Dir.Up => "up", Dir.Right => "right", Dir.Down => "down", _ => "",
        };

        static string ComboKey(List<Dir> conns)
        {
            var parts = new List<string>();
            foreach (var d in Order) if (conns.Contains(d)) parts.Add(DirStr(d));
            return string.Join(",", parts);
        }

        /// <summary>Rect del sprite de una carta, o null si no tiene arte (general/castillo/rey).</summary>
        public static SpriteRect? GetSpriteRect(CardKind kind, Region? region, List<Dir> connections)
        {
            if (kind == CardKind.Heroe)
            {
                bool h = connections.Contains(Dir.Left) && connections.Contains(Dir.Right);
                bool v = connections.Contains(Dir.Up) && connections.Contains(Dir.Down);
                if (h && !v) return HeroHorizontal;
                if (v && !h) return HeroVertical;
                return new SpriteRect { Sheet = "main", Sx = HeroDefault.x0, Sy = HeroDefault.y0, Sw = HeroDefault.x1 - HeroDefault.x0, Sh = HeroDefault.y1 - HeroDefault.y0 };
            }
            if (kind == CardKind.Pueblo && region.HasValue)
            {
                var (x0, x1) = PuebloCol[region.Value];
                var (y0, y1) = PuebloRow;
                return new SpriteRect { Sheet = "main", Sx = x0, Sy = y0, Sw = x1 - x0, Sh = y1 - y0 };
            }
            if (kind == CardKind.Camino && region.HasValue)
            {
                if (!ComboCol.TryGetValue(ComboKey(connections), out var col)) return null;
                var (y0, y1) = RegionRow[region.Value];
                return new SpriteRect { Sheet = "main", Sx = col.x0, Sy = y0, Sw = col.x1 - col.x0, Sh = y1 - y0 };
            }
            return null;
        }

        // -------------------------------------------------------------------
        // Carga de texturas (cache)
        // -------------------------------------------------------------------
        static Texture2D main, hero, wood;

        static Texture2D Load(string name)
        {
            var t = Resources.Load<Texture2D>("Art/" + name);
            if (t == null) Debug.LogError($"CardSprites: no encuentro Assets/Resources/Art/{name}.png");
            return t;
        }

        static Texture2D Sheet(string id) => id == "hero"
            ? (hero != null ? hero : hero = Load("hero-tiles"))
            : (main != null ? main : main = Load("tiles"));

        public static Texture2D Wood => wood != null ? wood : wood = Load("wood-bg");

        static readonly Dictionary<Region, Texture2D> bossCache = new();
        public static Texture2D Boss(Region r)
        {
            if (bossCache.TryGetValue(r, out var t) && t != null) return t;
            t = Load("boss-" + Cards.RegionId(r));
            bossCache[r] = t;
            return t;
        }

        // Imágenes sueltas (dado, libros) por nombre de archivo.
        static readonly Dictionary<string, Texture2D> imgCache = new();
        public static Texture2D Image(string name)
        {
            if (imgCache.TryGetValue(name, out var t) && t != null) return t;
            t = Load(name);
            imgCache[name] = t;
            return t;
        }

        // -------------------------------------------------------------------
        // Aplicar como fondo
        // -------------------------------------------------------------------
        public static void ApplyTo(VisualElement ve, SpriteRect r)
        {
            var tex = Sheet(r.Sheet);
            if (tex == null) return;

            float bgW = (float)SHEET_W / r.Sw * 100f;
            float bgH = (float)SHEET_H / r.Sh * 100f;
            float posX = SHEET_W == r.Sw ? 0f : (float)r.Sx / (SHEET_W - r.Sw) * 100f;
            float posY = SHEET_H == r.Sh ? 0f : (float)r.Sy / (SHEET_H - r.Sh) * 100f;

            ve.style.backgroundImage = new StyleBackground(tex);
            ve.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
            ve.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(Length.Percent(bgW), Length.Percent(bgH)));
            ve.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Left, Length.Percent(posX)));
            ve.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Top, Length.Percent(posY)));
        }

        /// <summary>Fondo de mesa de madera cubriendo el elemento.</summary>
        public static void ApplyMesa(VisualElement ve) => ApplyCover(ve, Wood);

        /// <summary>Fondo con la imagen del General/Rey de la región, cubriendo el elemento.</summary>
        public static void ApplyBoss(VisualElement ve, Region region) => ApplyCover(ve, Boss(region));

        static void ApplyCover(VisualElement ve, Texture2D tex) => ApplyFit(ve, tex, BackgroundSizeType.Cover);

        /// <summary>Fondo con una imagen suelta, mostrada completa (contain) y centrada.</summary>
        public static void ApplyImageContain(VisualElement ve, string name) => ApplyFit(ve, Image(name), BackgroundSizeType.Contain);

        /// <summary>Fondo con una imagen suelta, cubriendo el elemento (cover) y centrada.</summary>
        public static void ApplyImageCover(VisualElement ve, string name) => ApplyFit(ve, Image(name), BackgroundSizeType.Cover);

        static void ApplyFit(VisualElement ve, Texture2D tex, BackgroundSizeType fit)
        {
            if (tex == null) return;
            ve.style.backgroundImage = new StyleBackground(tex);
            ve.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
            ve.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(fit));
            ve.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
            ve.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
        }
    }
}
