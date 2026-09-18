// ============================================================================
// BoardView — vista del juego en UI Toolkit (construida en C#, sin UXML aún).
// Prototipo funcional: cajas de colores. Dibuja tablero, mano, barra de acción
// y el minijuego de combate. Solo lee el GameState y emite callbacks; toda la
// lógica vive en el motor (ElViaje.Game).
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ElViaje.Game;

namespace ElViaje.App
{
    public class BoardView
    {
        const int CELL = 62;
        const int COMBAT_CELL = 26;

        readonly VisualElement root;
        readonly GameController controller;

        public string SelectedCardId;

        // Callbacks (los conecta GameApp)
        public Action<string> OnSelectCard;
        public Action<int, int> OnPlace;
        public Action<int, int> OnStep;
        public Action OnDraw, OnRoll, OnEndMove, OnInvokeGeneral, OnNewGame;
        public Action<string> OnDiscard;
        public Action<int, int> OnCombatSelect;
        public Action OnCombatRetreat;

        public BoardView(VisualElement root, GameController controller)
        {
            this.root = root;
            this.controller = controller;
        }

        // -------------------------------------------------------------------
        // Paleta
        // -------------------------------------------------------------------
        static Color Bg => new(0.10f, 0.09f, 0.12f);
        static Color Panel => new(0.16f, 0.15f, 0.19f);
        static Color Ink => new(0.92f, 0.90f, 0.82f);
        static Color Highlight => new(0.85f, 0.65f, 0.20f);
        static Color Step => new(0.25f, 0.55f, 0.85f);

        static Color KindColor(CardKind k) => k switch
        {
            CardKind.Camino => new Color(0.35f, 0.33f, 0.30f),
            CardKind.Pueblo => new Color(0.20f, 0.45f, 0.25f),
            CardKind.Heroe => new Color(0.22f, 0.38f, 0.62f),
            CardKind.General => new Color(0.55f, 0.16f, 0.16f),
            CardKind.Castillo => new Color(0.40f, 0.22f, 0.50f),
            CardKind.Rey => new Color(0.15f, 0.05f, 0.08f),
            _ => new Color(0.3f, 0.3f, 0.3f),
        };

        static string PhaseEs(Phase p) => p switch
        {
            Phase.Draw => "Robar",
            Phase.Play => "Jugar",
            Phase.Roll => "Tirar dado",
            Phase.Move => "Mover",
            Phase.Combat => "Combate",
            Phase.World => "Turno del Mundo",
            Phase.Final => "Final",
            _ => p.ToString(),
        };

        static string Arrows(List<Dir> conns)
        {
            string s = "";
            if (conns.Contains(Dir.Up)) s += "↑";
            if (conns.Contains(Dir.Down)) s += "↓";
            if (conns.Contains(Dir.Left)) s += "←";
            if (conns.Contains(Dir.Right)) s += "→";
            return s;
        }

        // -------------------------------------------------------------------
        // Helpers de estilo
        // -------------------------------------------------------------------
        static Label MakeLabel(string text, int size = 12, Color? color = null)
        {
            var l = new Label(text);
            l.style.color = new StyleColor(color ?? Ink);
            l.style.fontSize = size;
            l.style.marginRight = 10;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            return l;
        }

        static Button MakeButton(string text, Action onClick, Color? bg = null)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.style.backgroundColor = new StyleColor(bg ?? Panel);
            b.style.color = new StyleColor(Ink);
            b.style.marginRight = 6;
            b.style.marginTop = 4;
            b.style.paddingLeft = 10;
            b.style.paddingRight = 10;
            b.style.paddingTop = 6;
            b.style.paddingBottom = 6;
            b.style.borderTopLeftRadius = 6;
            b.style.borderTopRightRadius = 6;
            b.style.borderBottomLeftRadius = 6;
            b.style.borderBottomRightRadius = 6;
            return b;
        }

        static VisualElement Row()
        {
            var v = new VisualElement();
            v.style.flexDirection = FlexDirection.Row;
            v.style.flexWrap = Wrap.Wrap;
            v.style.alignItems = Align.Center;
            return v;
        }

        // -------------------------------------------------------------------
        // Render principal
        // -------------------------------------------------------------------
        public void Render(GameState s)
        {
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = new StyleColor(Bg);
            CardSprites.ApplyMesa(root); // mesa de madera de fondo
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            root.Add(TopBar(s));

            var go = Engine.CheckGameOver(s);
            if (go.Over) { root.Add(EndPanel(s, go)); return; }

            if (s.Phase == Phase.Combat && s.PendingCombat != null)
            {
                root.Add(CombatPanel(s));
                return;
            }

            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 8;
            scroll.style.marginBottom = 8;
            scroll.Add(BoardGrid(s));
            root.Add(scroll);

            root.Add(ActionBar(s));
        }

        VisualElement TopBar(GameState s)
        {
            var bar = Row();
            bar.style.backgroundColor = new StyleColor(Panel);
            bar.style.paddingLeft = 10;
            bar.style.paddingRight = 10;
            bar.style.paddingTop = 6;
            bar.style.paddingBottom = 6;
            bar.style.borderTopLeftRadius = 8;
            bar.style.borderTopRightRadius = 8;
            bar.style.borderBottomLeftRadius = 8;
            bar.style.borderBottomRightRadius = 8;

            var title = MakeLabel("El Viaje del Héroe", 15, Highlight);
            title.style.marginRight = 18;
            bar.Add(title);
            bar.Add(MakeLabel($"Turno {s.Turn}"));
            bar.Add(MakeLabel($"Fase: {PhaseEs(s.Phase)}"));
            bar.Add(MakeLabel($"Poder: {Engine.GetPartyPower(s)}"));
            bar.Add(MakeLabel($"Generales: {s.GeneralsDefeated}/4"));
            bar.Add(MakeLabel($"Mano: {s.Hand.Count}  Mazo: {s.Deck.Count}"));

            var nueva = MakeButton("Nueva partida", () => OnNewGame?.Invoke());
            bar.Add(nueva);
            return bar;
        }

        // -------------------------------------------------------------------
        // Tablero
        // -------------------------------------------------------------------
        VisualElement BoardGrid(GameState s)
        {
            var placements = (SelectedCardId != null && s.Phase == Phase.Play)
                ? controller.Placements(SelectedCardId) : new List<Placement>();
            var steps = s.Phase == Phase.Move ? controller.StepTargets() : new List<(int x, int y)>();

            bool any = false;
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            void Inc(int x, int y)
            {
                if (!any) { minX = maxX = x; minY = maxY = y; any = true; }
                else { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
            }
            foreach (var k in s.Grid.Keys) { var (x, y) = Geometry.ParseKey(k); Inc(x, y); }
            Inc(s.Party.X, s.Party.Y);
            foreach (var p in placements) Inc(p.X, p.Y);
            foreach (var t in steps) Inc(t.x, t.y);
            // margen de una celda alrededor para ver la frontera
            minX -= 1; minY -= 1; maxX += 1; maxY += 1;

            var col = new VisualElement();
            for (int y = minY; y <= maxY; y++)
            {
                var rowEl = new VisualElement();
                rowEl.style.flexDirection = FlexDirection.Row;
                for (int x = minX; x <= maxX; x++)
                    rowEl.Add(Cell(s, x, y, placements, steps));
                col.Add(rowEl);
            }
            return col;
        }

        VisualElement Cell(GameState s, int x, int y, List<Placement> placements, List<(int x, int y)> steps)
        {
            string key = Geometry.Key(x, y);
            bool isParty = s.Party.X == x && s.Party.Y == y;

            if (s.Grid.TryGetValue(key, out var card))
            {
                var box = new VisualElement();
                Size(box);
                box.style.justifyContent = Justify.Center;
                box.style.alignItems = Align.Center;

                var rect = CardSprites.GetSpriteRect(card.Kind, card.Region, card.Connections);
                if (rect.HasValue)
                {
                    CardSprites.ApplyTo(box, rect.Value);
                }
                else
                {
                    // Sin arte (general / castillo / rey): caja de color + texto.
                    box.style.backgroundColor = new StyleColor(KindColor(card.Kind));
                    var arrows = new Label(Arrows(card.Connections));
                    arrows.style.color = new StyleColor(Ink);
                    arrows.style.fontSize = 14;
                    box.Add(arrows);
                    var name = new Label(Shorten(card.Name, 12));
                    name.style.color = new StyleColor(Ink);
                    name.style.fontSize = 8;
                    name.style.whiteSpace = WhiteSpace.Normal;
                    name.style.unityTextAlign = TextAnchor.MiddleCenter;
                    box.Add(name);
                }

                if (isParty)
                {
                    box.style.borderTopWidth = 3;
                    box.style.borderBottomWidth = 3;
                    box.style.borderLeftWidth = 3;
                    box.style.borderRightWidth = 3;
                    SetBorderColor(box, Highlight);
                    var hero = new Label("★");
                    hero.style.color = new StyleColor(Highlight);
                    hero.style.fontSize = 14;
                    hero.style.position = Position.Absolute;
                    hero.style.bottom = 0;
                    hero.style.right = 2;
                    box.Add(hero);
                }
                return box;
            }

            bool isPlacement = placements.Exists(p => p.X == x && p.Y == y);
            bool isStep = steps.Exists(t => t.x == x && t.y == y);

            if (isPlacement)
            {
                var b = new Button(() => OnPlace?.Invoke(x, y)) { text = "＋" };
                Size(b);
                b.style.backgroundColor = new StyleColor(Highlight);
                b.style.color = new StyleColor(Bg);
                b.style.fontSize = 20;
                return b;
            }
            if (isStep)
            {
                var b = new Button(() => OnStep?.Invoke(x, y)) { text = "•" };
                Size(b);
                b.style.backgroundColor = new StyleColor(Step);
                b.style.color = new StyleColor(Ink);
                b.style.fontSize = 20;
                return b;
            }

            var empty = new VisualElement();
            Size(empty);
            empty.style.backgroundColor = new StyleColor(new Color(1, 1, 1, 0.03f));
            return empty;
        }

        static void Size(VisualElement e)
        {
            e.style.width = CELL;
            e.style.height = CELL;
            e.style.marginLeft = 1;
            e.style.marginRight = 1;
            e.style.marginTop = 1;
            e.style.marginBottom = 1;
            e.style.paddingLeft = 0;
            e.style.paddingRight = 0;
            e.style.paddingTop = 0;
            e.style.paddingBottom = 0;
        }

        static void SetBorderColor(VisualElement e, Color c)
        {
            e.style.borderTopColor = new StyleColor(c);
            e.style.borderBottomColor = new StyleColor(c);
            e.style.borderLeftColor = new StyleColor(c);
            e.style.borderRightColor = new StyleColor(c);
        }

        static string Shorten(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        // -------------------------------------------------------------------
        // Barra de acción según la fase
        // -------------------------------------------------------------------
        VisualElement ActionBar(GameState s)
        {
            var bar = Row();
            bar.style.backgroundColor = new StyleColor(Panel);
            bar.style.paddingLeft = 10;
            bar.style.paddingRight = 10;
            bar.style.paddingBottom = 8;
            bar.style.borderTopLeftRadius = 8;
            bar.style.borderTopRightRadius = 8;
            bar.style.borderBottomLeftRadius = 8;
            bar.style.borderBottomRightRadius = 8;

            switch (s.Phase)
            {
                case Phase.Draw:
                    bar.Add(MakeButton("🂠 Robar carta", () => OnDraw?.Invoke(), Highlight));
                    break;

                case Phase.Play:
                {
                    bar.Add(MakeLabel("Tu mano:"));
                    var legal = controller.LegalMoves();
                    bool canDiscard = legal.Exists(m => m.Type == ActionType.Discard);
                    foreach (var id in s.Hand)
                        bar.Add(HandCard(id));
                    if (canDiscard)
                    {
                        bar.Add(MakeLabel("· Sin jugada legal, descarta:"));
                        foreach (var id in s.Hand)
                            if (Cards.GetCard(id).Kind != CardKind.General)
                                bar.Add(MakeButton($"🗑 {Shorten(Cards.GetCard(id).Name, 12)}", () => OnDiscard?.Invoke(id)));
                    }
                    else if (SelectedCardId != null)
                        bar.Add(MakeLabel("→ elige una casilla ＋"));
                    break;
                }

                case Phase.Roll:
                    bar.Add(MakeButton("🎲 Tirar el dado", () => OnRoll?.Invoke(), Highlight));
                    break;

                case Phase.Move:
                    bar.Add(MakeLabel($"Movimiento restante: {s.MovesLeft}"));
                    bar.Add(MakeButton("Terminar movimiento", () => OnEndMove?.Invoke()));
                    break;

                case Phase.World:
                    bar.Add(MakeLabel("El Mundo está jugando…"));
                    break;
            }
            return bar;
        }

        // Mini-carta de la mano: sprite + nombre, clicable. Los Generales invocan.
        VisualElement HandCard(string id)
        {
            var card = Cards.GetCard(id);
            bool isGeneral = card.Kind == CardKind.General;
            bool sel = id == SelectedCardId;

            var b = new Button(() =>
            {
                if (isGeneral) OnInvokeGeneral?.Invoke();
                else OnSelectCard?.Invoke(id);
            });
            b.style.width = 78;
            b.style.height = 106;
            b.style.marginRight = 6;
            b.style.marginTop = 4;
            b.style.paddingLeft = 3;
            b.style.paddingRight = 3;
            b.style.paddingTop = 3;
            b.style.paddingBottom = 3;
            b.style.alignItems = Align.Center;
            b.style.backgroundColor = new StyleColor(sel ? Highlight : Panel);
            b.style.borderTopLeftRadius = 6;
            b.style.borderTopRightRadius = 6;
            b.style.borderBottomLeftRadius = 6;
            b.style.borderBottomRightRadius = 6;

            var art = new VisualElement();
            art.style.width = 68;
            art.style.height = 64;
            var rect = CardSprites.GetSpriteRect(card.Kind, card.Region, card.Connections);
            if (rect.HasValue) CardSprites.ApplyTo(art, rect.Value);
            else art.style.backgroundColor = new StyleColor(KindColor(card.Kind));
            b.Add(art);

            var label = new Label(isGeneral ? "⚔ Invocar" : Shorten(card.Name, 14));
            label.style.color = new StyleColor(sel ? Bg : Ink);
            label.style.fontSize = 8;
            label.style.marginTop = 2;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.Add(label);
            return b;
        }

        // -------------------------------------------------------------------
        // Combate (minijuego de búsqueda del Corazón)
        // -------------------------------------------------------------------
        VisualElement CombatPanel(GameState s)
        {
            var pc = s.PendingCombat;

            // Panel a pantalla completa con la imagen del jefe de fondo.
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.marginTop = 8;
            panel.style.overflow = Overflow.Hidden;
            panel.style.borderTopLeftRadius = 10;
            panel.style.borderTopRightRadius = 10;
            panel.style.borderBottomLeftRadius = 10;
            panel.style.borderBottomRightRadius = 10;
            CardSprites.ApplyBoss(panel, pc.Region);

            // Velo oscuro para legibilidad sobre el arte.
            var scrim = new VisualElement();
            scrim.style.flexGrow = 1;
            scrim.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
            scrim.style.alignItems = Align.Center;
            scrim.style.paddingTop = 14;
            scrim.style.paddingBottom = 14;
            scrim.style.paddingLeft = 12;
            scrim.style.paddingRight = 12;
            panel.Add(scrim);

            string title = pc.IsRey ? "¡El Rey Demonio!" : $"¡{Cards.GetCard(pc.CardId).Name}!";
            var h = MakeLabel(title, 24, Highlight);
            h.style.marginRight = 0;
            h.style.marginBottom = 4;
            h.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.style.unityTextAlign = TextAnchor.MiddleCenter;
            scrim.Add(h);

            var info = MakeLabel(
                $"Intentos: {pc.AttemptsUsed}/{pc.AttemptsTotal}    " +
                $"Poder {(pc.IsRey ? "Rey" : "General")}: {pc.GeneralPower}    Party: {pc.PartyPower}" +
                (pc.IsRey ? $"    Corazones: {pc.HeartsFound}/{pc.HeartsTotal}" : ""), 12);
            info.style.marginRight = 0;
            info.style.unityTextAlign = TextAnchor.MiddleCenter;
            scrim.Add(info);

            // Antes del primer clic se puede retirar; después no.
            if (!pc.Started)
            {
                var retreat = MakeButton("🏃 Retirarse", () => OnCombatRetreat?.Invoke());
                retreat.style.marginTop = 6;
                retreat.style.marginBottom = 4;
                scrim.Add(retreat);
            }

            var reveal = new Dictionary<string, int>();
            foreach (var v in pc.Revealed) reveal[$"{v.R},{v.C}"] = v.Dist;

            var gridCol = new VisualElement();
            gridCol.style.marginTop = 8;
            for (int r = 0; r < pc.GridN; r++)
            {
                var rowEl = new VisualElement();
                rowEl.style.flexDirection = FlexDirection.Row;
                for (int c = 0; c < pc.GridN; c++)
                {
                    string k = $"{r},{c}";
                    if (reveal.TryGetValue(k, out int dist))
                    {
                        var cellEl = new VisualElement();
                        CombatSize(cellEl);
                        var dc = DistColor(dist, pc.GridN);
                        cellEl.style.backgroundColor = new StyleColor(new Color(dc.r, dc.g, dc.b, 0.85f));
                        cellEl.style.justifyContent = Justify.Center;
                        cellEl.style.alignItems = Align.Center;
                        var lbl = new Label(dist.ToString());
                        lbl.style.color = new StyleColor(Color.white);
                        lbl.style.fontSize = 10;
                        cellEl.Add(lbl);
                        rowEl.Add(cellEl);
                    }
                    else
                    {
                        int rr = r, cc = c;
                        var b = new Button(() => OnCombatSelect?.Invoke(rr, cc)) { text = "" };
                        CombatSize(b);
                        b.style.backgroundColor = new StyleColor(new Color(0.1f, 0.09f, 0.12f, 0.5f));
                        rowEl.Add(b);
                    }
                }
                gridCol.Add(rowEl);
            }
            scrim.Add(gridCol);

            return panel;
        }

        static void CombatSize(VisualElement e)
        {
            e.style.width = COMBAT_CELL;
            e.style.height = COMBAT_CELL;
            e.style.marginLeft = 1;
            e.style.marginRight = 1;
            e.style.marginTop = 1;
            e.style.marginBottom = 1;
            e.style.paddingLeft = 0;
            e.style.paddingRight = 0;
            e.style.paddingTop = 0;
            e.style.paddingBottom = 0;
        }

        static Color DistColor(int dist, int n)
        {
            int maxD = Math.Max(1, 2 * (n - 1));
            float ratio = Mathf.Min(1f, (float)dist / maxD);
            return Color.HSVToRGB(Mathf.Lerp(0.02f, 0.6f, ratio), 0.75f, 0.75f);
        }

        // -------------------------------------------------------------------
        // Fin de partida
        // -------------------------------------------------------------------
        VisualElement EndPanel(GameState s, GameOverInfo go)
        {
            var panel = new VisualElement();
            panel.style.marginTop = 20;
            panel.style.alignItems = Align.Center;
            string txt = go.Status == GameStatus.Won ? "🏆 ¡Victoria!" : "💀 Derrota";
            var h = MakeLabel(txt, 28, go.Status == GameStatus.Won ? Highlight : new Color(0.8f, 0.3f, 0.3f));
            panel.Add(h);
            if (!string.IsNullOrEmpty(s.EndReason))
            {
                var r = MakeLabel(s.EndReason, 13);
                r.style.whiteSpace = WhiteSpace.Normal;
                r.style.marginTop = 8;
                panel.Add(r);
            }
            var nueva = MakeButton("Nueva partida", () => OnNewGame?.Invoke(), Highlight);
            nueva.style.marginTop = 14;
            panel.Add(nueva);
            return panel;
        }
    }
}
