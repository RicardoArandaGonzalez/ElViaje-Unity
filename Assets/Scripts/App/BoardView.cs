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
        int combatCell = 26; // tamaño de celda del minijuego (adaptable al tamaño de rejilla)

        readonly VisualElement root;
        readonly GameController controller;

        public string SelectedCardId;

        // Estado local de la vista (no forma parte del GameState)
        GameState current;
        bool bookOpen;
        string bookTab = "party";
        bool startOpen;
        int startStep = 1;
        Difficulty startDiff = Difficulty.Medio;

        // Navegación del tablero (zoom / desplazamiento)
        const float DefaultZoom = 1.6f;
        float boardScale = DefaultZoom;
        Vector2 boardOffset = Vector2.zero;
        bool boardInit;
        int gridMinX, gridMinY;
        VisualElement viewport, content;
        bool dragging;
        Vector2 dragStart, offsetStart;
        bool geomHooked, lastPortrait;

        // Personaje animado sobre el tablero.
        const float HeroW = 66f, HeroH = 84f, HeroFootPad = 24f;
        const float MoveDurationPerTileMs = 300f; // velocidad de desplazamiento por casilla
        VisualElement heroEl;
        bool walking;
        int heroFrame;

        // Callbacks (los conecta GameApp)
        public Action<string> OnSelectCard;
        public Action<int, int> OnPlace;
        public Action<int, int> OnStep;
        public Action OnDraw, OnRoll, OnEndMove, OnInvokeGeneral, OnNewGame;
        public Action<string> OnDiscard;
        public Action<int, int> OnCombatSelect;
        public Action OnCombatRetreat;
        public Action<Difficulty, Starter> OnStartGame;
        public Action OnToggleMute;
        public bool Muted;
        public bool HasSave;
        public Action OnContinue;
        public float Volume = 1f;
        public Action<float> OnSetVolume;

        bool menuOpen, settingsOpen;

        public void Refresh() => ReRender();

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
            current = s;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = new StyleColor(Bg);
            CardSprites.ApplyMesa(root); // mesa de madera de fondo
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;

            var go = Engine.CheckGameOver(s);
            if (go.Over)
            {
                root.Add(TopBar(s));
                root.Add(EndPanel(s, go));
            }
            else if (s.Phase == Phase.Combat && s.PendingCombat != null)
            {
                root.Add(TopBar(s));
                root.Add(CombatPanel(s));
            }
            else
            {
                // El tablero llena toda la pantalla por detrás; los paneles flotan
                // encima (semitransparentes) para que el terreno se vea debajo.
                BuildViewport(s);
                viewport.style.position = Position.Absolute;
                viewport.style.left = 0; viewport.style.right = 0;
                viewport.style.top = 0; viewport.style.bottom = 0;
                root.Add(viewport);

                var top = TopBar(s);
                top.style.position = Position.Absolute;
                top.style.left = 0; top.style.right = 0; top.style.top = 0;
                root.Add(top);

                var bottom = new VisualElement();
                bottom.style.position = Position.Absolute;
                bottom.style.left = 0; bottom.style.right = 0; bottom.style.bottom = 0;
                bottom.style.alignItems = Align.Center;
                bottom.Add(Controls(s));
                bottom.Add(HandBar(s));
                root.Add(bottom);

                ApplyBoardTransform();
                if (!boardInit)
                    viewport.RegisterCallback<GeometryChangedEvent>(OnViewportReady);
            }

            if (bookOpen) root.Add(BookOverlay(s));
            if (startOpen) root.Add(StartOverlay());
            if (menuOpen) root.Add(MenuOverlay());
            if (settingsOpen) root.Add(SettingsOverlay());
        }

        void ReRender() { if (current != null) Render(current); }

        // --- Pantalla de inicio ---
        public void OpenStart()
        {
            startOpen = true;
            startStep = 1;
            boardInit = false;              // re-encuadra el tablero en la nueva partida
            boardScale = DefaultZoom;
            boardOffset = Vector2.zero;
            RefreshStart();
        }

        void RefreshStart()
        {
            if (current != null) { Render(current); return; }
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = new StyleColor(Bg);
            CardSprites.ApplyMesa(root);
            root.Add(StartOverlay());
        }

        VisualElement TopBar(GameState s)
        {
            var bar = new VisualElement();
            bar.style.backgroundColor = new StyleColor(new Color(0.10f, 0.08f, 0.12f, 0.72f));
            bar.style.paddingLeft = 10;
            bar.style.paddingRight = 10;
            bar.style.paddingTop = 5;
            bar.style.paddingBottom = 5;
            bar.style.borderBottomLeftRadius = 10;
            bar.style.borderBottomRightRadius = 10;

            // Fila 1: indicaciones al jugador (ocupan el encabezado) + menú (☰).
            var top = Row();
            top.style.flexWrap = Wrap.NoWrap;
            top.style.alignItems = Align.Center;
            var hint = MakeLabel(Hint(s), 14, Highlight);
            hint.style.flexGrow = 1;
            hint.style.flexShrink = 1;
            hint.style.whiteSpace = WhiteSpace.Normal; // se ajusta a varias líneas si hace falta
            top.Add(hint);
            top.Add(BurgerButton(() => { menuOpen = !menuOpen; settingsOpen = false; ReRender(); }));
            bar.Add(top);

            // Fila 2: música + zoom + centrar (controles a la derecha).
            var row2 = Row();
            row2.style.flexWrap = Wrap.NoWrap;
            row2.style.justifyContent = Justify.FlexEnd;
            row2.style.marginTop = 4;

            var music = NavButton("♪", () => OnToggleMute?.Invoke());
            music.style.opacity = Muted ? 0.35f : 1f;
            row2.Add(music);
            row2.Add(NavButton("－", () => Zoom(1f / 1.2f)));
            var zoomLbl = MakeLabel($"{Mathf.RoundToInt(boardScale / DefaultZoom * 100)}%", 11);
            zoomLbl.style.marginLeft = 4;
            zoomLbl.style.marginRight = 4;
            row2.Add(zoomLbl);
            row2.Add(NavButton("＋", () => Zoom(1.2f)));
            row2.Add(NavButton("⊙", () => { if (current != null) CenterOnParty(current); }));
            bar.Add(row2);

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
            gridMinX = minX; gridMinY = minY; // para centrar el Party

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
            bool isStep = steps.Exists(t => t.x == x && t.y == y);
            bool isPlacement = placements.Exists(p => p.X == x && p.Y == y);

            if (s.Grid.TryGetValue(key, out var card))
            {
                var box = new VisualElement();
                Size(box);
                box.style.justifyContent = Justify.Center;
                box.style.alignItems = Align.Center;

                if (ApplyCardArt(box, card.Kind, card.Region, card.Connections))
                {
                    // arte aplicado
                }
                else
                {
                    // Sin arte (castillo / rey): caja de color + texto.
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
                    // Casilla actual: borde dorado (el sprite del personaje va encima).
                    box.style.borderTopWidth = 3;
                    box.style.borderBottomWidth = 3;
                    box.style.borderLeftWidth = 3;
                    box.style.borderRightWidth = 3;
                    SetBorderColor(box, Highlight);
                }
                else if (isStep)
                {
                    // Casilla del camino a la que el Party puede moverse: clicable.
                    box.style.borderTopWidth = 3;
                    box.style.borderBottomWidth = 3;
                    box.style.borderLeftWidth = 3;
                    box.style.borderRightWidth = 3;
                    SetBorderColor(box, Step);
                    var mark = new Label("»");
                    mark.style.color = new StyleColor(Color.white);
                    mark.style.fontSize = 22;
                    mark.style.unityFontStyleAndWeight = FontStyle.Bold;
                    box.Add(mark);
                    box.RegisterCallback<ClickEvent>(_ => RequestStep(x, y));
                }
                return box;
            }

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
                var b = new Button(() => RequestStep(x, y)) { text = "•" };
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

        /// <summary>Aplica el arte de una carta a un elemento. true si había arte (false → castillo/rey).</summary>
        public static bool ApplyCardArt(VisualElement box, CardKind kind, Region? region, List<Dir> connections)
        {
            var rect = CardSprites.GetSpriteRect(kind, region, connections);
            if (rect.HasValue) { CardSprites.ApplyTo(box, rect.Value); return true; }
            if (kind == CardKind.General && region.HasValue)
            {
                CardSprites.ApplyImageCover(box, "general-" + Cards.RegionId(region.Value));
                return true;
            }
            return false;
        }

        static string Hint(GameState s)
        {
            switch (s.Phase)
            {
                case Phase.Draw: return "Roba una carta del mazo →";
                case Phase.Play: return "Selecciona una carta de tu mano ↓";
                case Phase.Roll: return "Tira el dado →";
                case Phase.Move: return "Muévete libremente o termina el turno";
                case Phase.World: return "Turno del Mundo…";
                default: return "";
            }
        }

        // Botón de menú (tres barras dibujadas, sin depender de un glyph).
        Button BurgerButton(Action onClick)
        {
            var b = new Button(() => onClick());
            b.style.width = 38;
            b.style.height = 30;
            b.style.flexDirection = FlexDirection.Column;
            b.style.justifyContent = Justify.SpaceBetween;
            b.style.marginLeft = 3;
            b.style.paddingLeft = 8; b.style.paddingRight = 8;
            b.style.paddingTop = 7; b.style.paddingBottom = 7;
            b.style.backgroundColor = new StyleColor(Panel);
            b.style.borderTopLeftRadius = 6; b.style.borderTopRightRadius = 6;
            b.style.borderBottomLeftRadius = 6; b.style.borderBottomRightRadius = 6;
            for (int i = 0; i < 3; i++)
            {
                var line = new VisualElement();
                line.style.height = 3;
                line.style.width = Length.Percent(100);
                line.style.backgroundColor = new StyleColor(new Color(0.85f, 0.28f, 0.22f));
                line.style.borderTopLeftRadius = 2; line.style.borderTopRightRadius = 2;
                line.style.borderBottomLeftRadius = 2; line.style.borderBottomRightRadius = 2;
                b.Add(line);
            }
            return b;
        }

        Button NavButton(string glyph, Action onClick)
        {
            var b = new Button(() => onClick()) { text = glyph };
            b.style.width = 30;
            b.style.height = 28;
            b.style.marginLeft = 3;
            b.style.paddingLeft = 0;
            b.style.paddingRight = 0;
            b.style.paddingTop = 0;
            b.style.paddingBottom = 0;
            b.style.backgroundColor = new StyleColor(Panel);
            b.style.color = new StyleColor(Ink);
            b.style.fontSize = 15;
            b.style.borderTopLeftRadius = 6;
            b.style.borderTopRightRadius = 6;
            b.style.borderBottomLeftRadius = 6;
            b.style.borderBottomRightRadius = 6;
            return b;
        }

        bool IsPortrait()
        {
            float w = root.resolvedStyle.width;
            float h = root.resolvedStyle.height;
            if (w <= 1f || float.IsNaN(w)) return false; // por defecto apaisado hasta conocer el tamaño
            return h > w;
        }

        // Viewport con recorte; el contenido se traslada/escala (pan + zoom).
        void BuildViewport(GameState s)
        {
            viewport = new VisualElement();
            viewport.style.flexGrow = 1;
            viewport.style.overflow = Overflow.Hidden;
            viewport.style.position = Position.Relative;

            content = new VisualElement();
            content.style.position = Position.Absolute;
            content.Add(BoardGrid(s));
            viewport.Add(content);
            AddHero(s); // personaje animado (idle) sobre la casilla del Party

            viewport.RegisterCallback<PointerDownEvent>(e =>
            {
                dragging = true;
                dragStart = new Vector2(e.position.x, e.position.y);
                offsetStart = boardOffset;
            });
            viewport.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return;
                var d = new Vector2(e.position.x, e.position.y) - dragStart;
                boardOffset = offsetStart + d;
                ApplyBoardTransform();
            });
            viewport.RegisterCallback<PointerUpEvent>(_ => dragging = false);
            viewport.RegisterCallback<PointerLeaveEvent>(_ => dragging = false);
        }

        void ApplyBoardTransform()
        {
            if (content == null) return;
            content.style.transformOrigin = new TransformOrigin(0, 0);
            content.style.translate = new Translate(boardOffset.x, boardOffset.y);
            content.style.scale = new Scale(new Vector3(boardScale, boardScale, 1f));
        }

        void Zoom(float factor)
        {
            boardScale = Mathf.Clamp(boardScale * factor, 0.4f, 2.5f);
            ApplyBoardTransform();
            ReRender(); // refresca el % mostrado en la barra
        }

        // Centra el tablero cuando el viewport ya tiene tamaño (tras el primer layout).
        void OnViewportReady(GeometryChangedEvent e)
        {
            if (boardInit || viewport == null) return;
            var vb = viewport.layout;
            if (float.IsNaN(vb.width) || vb.width <= 1f) return;
            boardInit = true;
            boardScale = Compact ? DefaultZoom * 0.4f : DefaultZoom; // teléfono arranca al 40%
            if (current != null) CenterOnParty(current);
            viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportReady);
            ReRender(); // refresca el % de zoom mostrado
        }

        // Teléfono / pantalla estrecha: orientación vertical.
        static bool Compact => Screen.height > Screen.width;

        // Dimensiona 'el' manteniendo su aspecto, ajustándolo dentro del contenedor.
        // El libro ocupa un % del ancho disponible (con tope) y su alto se deriva
        // de su proporción. Fiable: el ancho lo resuelve el layout, no una medición.
        void FitByWidth(VisualElement el, float aspect, float widthPct, float maxW)
        {
            el.style.width = Length.Percent(widthPct);
            el.style.maxWidth = maxW;
            void SetH()
            {
                float w = el.resolvedStyle.width;
                if (w > 1f && !float.IsNaN(w)) el.style.height = w / aspect;
            }
            el.RegisterCallback<GeometryChangedEvent>(_ => SetH());
            SetH();
        }

        // Permite desplazar un ScrollView arrastrando con el dedo o el ratón
        // (para móvil, donde ocultamos la barra de scroll).
        void EnableDragScroll(ScrollView sv)
        {
            bool dragging = false;
            float startY = 0f, startOffset = 0f;
            sv.RegisterCallback<PointerDownEvent>(e =>
            {
                dragging = true;
                startY = e.position.y;
                startOffset = sv.scrollOffset.y;
                sv.CapturePointer(e.pointerId);
            });
            sv.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return;
                var off = sv.scrollOffset;
                off.y = startOffset - (e.position.y - startY);
                sv.scrollOffset = off;
            });
            sv.RegisterCallback<PointerUpEvent>(e =>
            {
                dragging = false;
                if (sv.HasPointerCapture(e.pointerId)) sv.ReleasePointer(e.pointerId);
            });
            sv.RegisterCallback<PointerCaptureOutEvent>(_ => dragging = false);
        }

        // Centra el Party en el viewport (cell = 62 + 2 de margen = 64).
        void CenterOnParty(GameState s)
        {
            if (viewport == null) return;
            var vb = viewport.layout;
            if (float.IsNaN(vb.width) || vb.width <= 1f) return;
            const float cell = 64f;
            float px = ((s.Party.X - gridMinX) + 0.5f) * cell;
            float py = ((s.Party.Y - gridMinY) + 0.5f) * cell;
            boardOffset = new Vector2(vb.width / 2f - px * boardScale, vb.height / 2f - py * boardScale);
            ApplyBoardTransform();
        }

        // -------------------------------------------------------------------
        // Personaje: idle continuo + caminar casilla por casilla (solo visual).
        // -------------------------------------------------------------------
        (float x, float y) CellCenter(int x, int y)
        {
            const float cell = 64f;
            return (((x - gridMinX) + 0.5f) * cell, ((y - gridMinY) + 0.5f) * cell);
        }

        string HeroPrefix(GameState s)
            => (s.Party.Members.Count > 0 && s.Party.Members[0].CardId == "inicial-heroina") ? "mago" : "cab";

        static void SetHeroFrame(VisualElement el, Texture2D tex)
        {
            if (tex == null) return;
            el.style.backgroundImage = new StyleBackground(tex);
            el.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
            el.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            el.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
            el.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Bottom));
        }

        void PlaceHeroAt(float cx, float cy)
        {
            heroEl.style.left = cx - HeroW / 2f;
            heroEl.style.top = cy - HeroH + HeroFootPad;
        }

        void AddHero(GameState s)
        {
            if (content == null || s.Party.Members.Count == 0) return;
            walking = false;
            heroFrame = 0;
            string prefix = HeroPrefix(s);

            heroEl = new VisualElement();
            heroEl.pickingMode = PickingMode.Ignore; // no bloquea el clic de las casillas
            heroEl.style.position = Position.Absolute;
            heroEl.style.width = HeroW;
            heroEl.style.height = HeroH;
            heroEl.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            var (cx, cy) = CellCenter(s.Party.X, s.Party.Y);
            PlaceHeroAt(cx, cy);
            SetHeroFrame(heroEl, CardSprites.Image($"Hero/{prefix}_idle_0"));
            content.Add(heroEl);

            var el = heroEl;
            el.schedule.Execute(() =>
            {
                if (walking) return;
                heroFrame = (heroFrame + 1) % 4;
                SetHeroFrame(el, CardSprites.Image($"Hero/{prefix}_idle_{heroFrame}"));
            }).Every(180);
        }

        // Clic en una casilla-destino: camina (visual) y luego aplica el paso.
        void RequestStep(int x, int y)
        {
            if (walking) return;
            if (heroEl == null || current == null) { OnStep?.Invoke(x, y); return; }
            AnimateWalk(current.Party.X, current.Party.Y, x, y, () => OnStep?.Invoke(x, y));
        }

        void AnimateWalk(int fromX, int fromY, int toX, int toY, Action done)
        {
            walking = true;
            string prefix = HeroPrefix(current);
            int dx = toX - fromX, dy = toY - fromY;
            bool horizontal = dx != 0;
            bool mirror = dx > 0;                    // derecha = espejo de "izquierda"
            string anim = horizontal ? "left" : "down"; // arriba reutiliza "down"
            heroEl.style.scale = new Scale(new Vector3(mirror ? -1f : 1f, 1f, 1f));

            var (fx, fy) = CellCenter(fromX, fromY);
            var (tx, ty) = CellCenter(toX, toY);
            float startMs = Time.realtimeSinceStartup * 1000f;
            var el = heroEl;
            IVisualElementScheduledItem item = null;
            item = el.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup * 1000f - startMs) / MoveDurationPerTileMs);
                PlaceHeroAt(Mathf.Lerp(fx, tx, t), Mathf.Lerp(fy, ty, t));
                int fr = Mathf.Min(7, (int)(t * 8));
                SetHeroFrame(el, CardSprites.Image($"Hero/{prefix}_{anim}_{fr}"));
                if (t >= 1f)
                {
                    item.Pause();
                    walking = false;
                    done?.Invoke();
                }
            }).Every(16);
        }

        Label SmallLabel(string t)
        {
            var l = new Label(t);
            l.style.color = new StyleColor(Ink);
            l.style.fontSize = 10;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 2;
            l.style.marginBottom = 6;
            return l;
        }

        // Panel de controles (horizontal): libro (grande) · mazo · dado · terminar.
        VisualElement Controls(GameState s)
        {
            bool cmp = Compact;
            float bookW = cmp ? 72 : 132, bookH = cmp ? 64 : 116;
            float dieSz = cmp ? 46 : 72;
            float gap = cmp ? 6 : 16;

            var c = new VisualElement();
            c.style.flexDirection = FlexDirection.Row;
            c.style.alignItems = Align.Center;
            c.style.flexWrap = Wrap.NoWrap;
            c.style.height = cmp ? 92 : 128;
            c.style.marginTop = 2;
            c.style.paddingLeft = 4; c.style.paddingRight = 4;
            c.style.backgroundColor = new StyleColor(new Color(0.10f, 0.08f, 0.07f, 0.45f));
            c.style.borderTopLeftRadius = 10; c.style.borderTopRightRadius = 10;
            c.style.borderBottomLeftRadius = 10; c.style.borderBottomRightRadius = 10;
            if (cmp) { c.style.width = Length.Percent(100); c.style.justifyContent = Justify.SpaceAround; }
            else c.style.justifyContent = Justify.Center;

            void Gap(VisualElement e, bool first) { if (!cmp && !first) e.style.marginLeft = gap; }

            // Libro
            var book = new Button(() => { bookOpen = !bookOpen; ReRender(); });
            book.style.width = bookW;
            book.style.height = bookH;
            book.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0));
            NoBorder(book);
            CardSprites.ApplyImageContain(book, "book-closed");
            Gap(book, true);
            c.Add(book);

            // Mazo (apilado)
            bool canDraw = s.Phase == Phase.Draw && !Engine.IsPossessed(s);
            var deck = DeckCard(s.Deck.Count, canDraw);
            Gap(deck, false);
            c.Add(deck);

            // Dado
            bool canRoll = s.Phase == Phase.Roll;
            var die = new Button(() => { if (canRoll) OnRoll?.Invoke(); });
            die.style.width = dieSz;
            die.style.height = dieSz;
            die.style.opacity = canRoll ? 1f : 0.4f;
            die.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0));
            NoBorder(die);
            CardSprites.ApplyImageContain(die, "die");
            Gap(die, false);
            c.Add(die);

            // Terminar turno: siempre visible, activo solo al moverse.
            bool canEnd = s.Phase == Phase.Move;
            var end = MakeButton(cmp ? "Terminar" : "Terminar turno", () => { if (canEnd) OnEndMove?.Invoke(); });
            end.style.opacity = canEnd ? 1f : 0.4f;
            end.style.fontSize = cmp ? 12 : 14;
            if (cmp) { end.style.paddingLeft = 8; end.style.paddingRight = 8; }
            if (canEnd) end.style.backgroundColor = new StyleColor(new Color(0.55f, 0.16f, 0.16f));
            Gap(end, false);
            c.Add(end);

            return c;
        }

        static void NoBorder(VisualElement e)
        {
            e.style.borderTopWidth = 0;
            e.style.borderBottomWidth = 0;
            e.style.borderLeftWidth = 0;
            e.style.borderRightWidth = 0;
        }

        // Mazo apilado: varias cartas con el dorso, desplazadas 2px arriba-derecha.
        Button DeckCard(int count, bool active)
        {
            int cw = Compact ? 46 : 74, ch = Compact ? 64 : 100;
            int layers = Mathf.Clamp(count / 12 + 1, 1, 5);
            int span = (layers - 1) * 2;

            var b = new Button(() => { if (active) OnDraw?.Invoke(); });
            b.style.width = cw + span;
            b.style.height = ch + span;
            b.style.paddingLeft = 0;
            b.style.paddingRight = 0;
            b.style.paddingTop = 0;
            b.style.paddingBottom = 0;
            b.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0));
            b.style.opacity = active ? 1f : 0.75f;
            NoBorder(b);

            for (int k = 0; k < layers; k++)
            {
                var lay = new VisualElement();
                lay.style.position = Position.Absolute;
                lay.style.width = cw;
                lay.style.height = ch;
                lay.style.left = k * 2;               // cada carta 2px a la derecha
                lay.style.top = (layers - 1 - k) * 2; // y 2px arriba
                lay.style.borderTopLeftRadius = 7;
                lay.style.borderTopRightRadius = 7;
                lay.style.borderBottomLeftRadius = 7;
                lay.style.borderBottomRightRadius = 7;
                if (k == layers - 1)
                {
                    // Solo la carta superior muestra el dorso.
                    CardSprites.ApplyImageCover(lay, "card-back");
                }
                else
                {
                    // Cantos de las cartas de abajo (color sólido, sin imagen).
                    lay.style.backgroundColor = new StyleColor(new Color(0.16f, 0.13f, 0.20f));
                    lay.style.borderTopWidth = 1; lay.style.borderBottomWidth = 1;
                    lay.style.borderLeftWidth = 1; lay.style.borderRightWidth = 1;
                    SetBorderColor(lay, new Color(0f, 0f, 0f, 0.5f));
                }
                b.Add(lay);
            }

            // Marca superior: contador + resaltado si se puede robar.
            var top = new VisualElement();
            top.style.position = Position.Absolute;
            top.style.left = span;
            top.style.top = 0;
            top.style.width = cw;
            top.style.height = ch;
            top.style.justifyContent = Justify.FlexEnd;
            top.style.alignItems = Align.Center;
            top.pickingMode = PickingMode.Ignore;
            if (active)
            {
                top.style.borderTopWidth = 2; top.style.borderBottomWidth = 2;
                top.style.borderLeftWidth = 2; top.style.borderRightWidth = 2;
                SetBorderColor(top, Highlight);
                top.style.borderTopLeftRadius = 7; top.style.borderTopRightRadius = 7;
                top.style.borderBottomLeftRadius = 7; top.style.borderBottomRightRadius = 7;
            }
            var c = new Label(count.ToString());
            c.style.color = new StyleColor(Ink);
            c.style.fontSize = 12;
            c.style.unityFontStyleAndWeight = FontStyle.Bold;
            c.style.marginBottom = 4;
            c.style.paddingLeft = 6; c.style.paddingRight = 6;
            c.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.55f));
            c.style.borderTopLeftRadius = 6; c.style.borderTopRightRadius = 6;
            c.style.borderBottomLeftRadius = 6; c.style.borderBottomRightRadius = 6;
            top.Add(c);
            b.Add(top);
            return b;
        }

        // Mano en abanico (sin recuadro).
        VisualElement HandBar(GameState s)
        {
            var wrap = new VisualElement();
            wrap.style.flexDirection = FlexDirection.Row;
            wrap.style.justifyContent = Justify.Center;
            wrap.style.alignItems = Align.FlexEnd;
            wrap.style.alignSelf = Align.Center; // la caja mide solo lo que ocupan las cartas
            wrap.style.height = Compact ? 100 : 122;
            wrap.style.marginTop = 0;

            bool stuck = controller.LegalMoves().Exists(m => m.Type == ActionType.Discard);
            int n = s.Hand.Count;
            for (int i = 0; i < n; i++)
            {
                float f = n > 1 ? (i / (float)(n - 1)) - 0.5f : 0f; // -0.5..0.5
                var card = FanCard(s.Hand[i], stuck);
                card.style.rotate = new Rotate(new Angle(f * 16f));
                card.style.top = Mathf.Abs(f) * 20f; // arco: extremos más abajo
                card.style.marginLeft = -12;
                card.style.marginRight = -12;
                card.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
                wrap.Add(card);
            }
            return wrap;
        }

        Button FanCard(string id, bool stuck)
        {
            var card = Cards.GetCard(id);
            bool isGeneral = card.Kind == CardKind.General;
            bool sel = id == SelectedCardId;

            var b = new Button(() =>
            {
                if (isGeneral) OnInvokeGeneral?.Invoke();
                else if (stuck) OnDiscard?.Invoke(id);
                else OnSelectCard?.Invoke(id);
            });
            bool cmp = Compact;
            b.style.width = cmp ? 66 : 82;
            b.style.height = cmp ? 92 : 114;
            b.style.paddingLeft = 3;
            b.style.paddingRight = 3;
            b.style.paddingTop = 3;
            b.style.paddingBottom = 3;
            b.style.alignItems = Align.Center;
            b.style.backgroundColor = new StyleColor(sel ? Highlight : new Color(0.12f, 0.10f, 0.14f, 0.96f));
            b.style.borderTopLeftRadius = 8;
            b.style.borderTopRightRadius = 8;
            b.style.borderBottomLeftRadius = 8;
            b.style.borderBottomRightRadius = 8;
            b.style.borderTopWidth = 2;
            b.style.borderBottomWidth = 2;
            b.style.borderLeftWidth = 2;
            b.style.borderRightWidth = 2;
            SetBorderColor(b, sel ? new Color(1f, 0.85f, 0.4f) : new Color(0, 0, 0, 0.5f));

            var art = new VisualElement();
            art.style.width = cmp ? 58 : 74;
            art.style.height = cmp ? 58 : 74;
            if (!ApplyCardArt(art, card.Kind, card.Region, card.Connections))
                art.style.backgroundColor = new StyleColor(KindColor(card.Kind));
            b.Add(art);

            var label = new Label(isGeneral ? "General Demonio" : Shorten(card.Name, 16));
            label.style.color = new StyleColor(sel ? Bg : Ink);
            label.style.fontSize = 8;
            label.style.marginTop = 2;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.Add(label);

            if (stuck && !isGeneral)
            {
                var tag = new Label("descartar");
                tag.style.color = new StyleColor(new Color(0.9f, 0.45f, 0.4f));
                tag.style.fontSize = 8;
                b.Add(tag);
            }
            return b;
        }

        static string DiffLabel(Difficulty d) => d switch
        {
            Difficulty.Facil => "Fácil", Difficulty.Medio => "Medio", Difficulty.Dificil => "Difícil", _ => "",
        };
        static string DiffDesc(Difficulty d) => d switch
        {
            Difficulty.Facil => "Mano 4 · Rey 14", Difficulty.Medio => "Mano 3 · Rey 16", Difficulty.Dificil => "Mano 3 · Rey 20", _ => "",
        };

        Button TextButton(string t, Action onClick)
        {
            var b = new Button(() => onClick()) { text = t };
            b.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0));
            b.style.color = new StyleColor(InkMuted);
            b.style.fontSize = 10;
            b.style.marginTop = 6;
            b.style.paddingTop = 2;
            b.style.paddingBottom = 2;
            b.style.borderTopWidth = 0;
            b.style.borderBottomWidth = 0;
            b.style.borderLeftWidth = 0;
            b.style.borderRightWidth = 0;
            return b;
        }

        // -------------------------------------------------------------------
        // Pantalla de inicio (libro abierto: dificultad → elección de héroe)
        // -------------------------------------------------------------------
        VisualElement StartOverlay()
        {
            bool cmp = Compact;
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.backgroundColor = new StyleColor(new Color(0.06f, 0.05f, 0.07f));
            CardSprites.ApplyMesa(overlay); // fondo de madera opaco (tapa la partida en curso)

            var tint = new VisualElement();
            tint.style.position = Position.Absolute;
            tint.style.left = 0; tint.style.right = 0; tint.style.top = 0; tint.style.bottom = 0;
            tint.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
            tint.pickingMode = PickingMode.Ignore;
            overlay.Add(tint);

            var book = new VisualElement();
            CardSprites.ApplyImageContain(book, "book-open-plain");
            book.style.overflow = Overflow.Hidden; // el contenido nunca se sale del libro
            overlay.Add(book);
            // En móvil el libro llena el ancho (sin tope); en escritorio se limita.
            FitByWidth(book, 1536f / 1024f, cmp ? 98f : 96f, cmp ? 3000f : 880f);

            // Las fuentes se dimensionan como fracción del ancho REAL del libro, para que
            // el texto sea la misma proporción en cualquier pantalla (iPad, S9, escritorio),
            // sin depender del modo de escalado del PanelSettings ni del aspect ratio.
            var fontAppliers = new List<Action<float>>();

            var pages = new VisualElement();
            pages.style.position = Position.Absolute;
            pages.style.left = Length.Percent(11);
            pages.style.top = Length.Percent(9);
            pages.style.right = Length.Percent(12);
            pages.style.bottom = Length.Percent(14);
            pages.style.flexDirection = FlexDirection.Row;
            book.Add(pages);

            var left = new VisualElement();
            left.style.flexGrow = 1; left.style.flexBasis = 0;
            left.style.justifyContent = Justify.Center;
            left.style.alignItems = Align.Stretch;
            left.style.paddingRight = Length.Percent(3);
            pages.Add(left);

            var right = new VisualElement();
            right.style.flexGrow = 1; right.style.flexBasis = 0;
            right.style.justifyContent = Justify.Center;
            right.style.alignItems = Align.Stretch;
            right.style.paddingLeft = Length.Percent(3);
            pages.Add(right);

            // Etiqueta cuyo tamaño es proporcional al ancho del libro.
            Label PLabel(string t, float ratio, float min, float max, Color col, bool bold)
            {
                var l = new Label(t);
                l.style.color = new StyleColor(col);
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.width = Length.Percent(100);
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
                fontAppliers.Add(bw => l.style.fontSize = Mathf.Clamp(bw * ratio, min, max));
                return l;
            }

            // Opción (dificultad / líder): nombre arriba, detalle debajo; proporcional al libro.
            Button PChoice(string title, string sub, bool selected, Action onClick)
            {
                var b = new Button(() => onClick());
                b.style.flexDirection = FlexDirection.Column;
                b.style.justifyContent = Justify.Center;
                b.style.alignItems = Align.Center;
                b.style.width = Length.Percent(100);
                b.style.marginLeft = 0; b.style.marginRight = 0; b.style.marginBottom = 0;
                b.style.backgroundColor = new StyleColor(selected ? new Color(0.85f, 0.55f, 0.2f, 0.35f) : new Color(0, 0, 0, 0.06f));
                b.style.borderTopWidth = 2; b.style.borderBottomWidth = 2;
                b.style.borderLeftWidth = 2; b.style.borderRightWidth = 2;
                SetBorderColor(b, selected ? Highlight : new Color(0, 0, 0, 0));
                b.style.borderTopLeftRadius = 6; b.style.borderTopRightRadius = 6;
                b.style.borderBottomLeftRadius = 6; b.style.borderBottomRightRadius = 6;
                b.style.minHeight = 0; // sin alto mínimo del botón por defecto

                var n = new Label(title);
                n.style.color = new StyleColor(InkDark);
                n.style.unityFontStyleAndWeight = FontStyle.Bold;
                n.style.whiteSpace = WhiteSpace.Normal;
                n.style.width = Length.Percent(100);
                n.style.unityTextAlign = TextAnchor.MiddleCenter;
                n.style.marginTop = 0; n.style.marginBottom = 0;
                b.Add(n);
                Label d = null;
                if (!string.IsNullOrEmpty(sub))
                {
                    d = new Label(sub);
                    d.style.color = new StyleColor(InkMuted);
                    d.style.whiteSpace = WhiteSpace.Normal;
                    d.style.width = Length.Percent(100);
                    d.style.unityTextAlign = TextAnchor.MiddleCenter;
                    b.Add(d);
                }

                fontAppliers.Add(bw =>
                {
                    n.style.fontSize = Mathf.Clamp(bw * 0.020f, 9, 15);
                    float ph = Mathf.Clamp(bw * 0.007f, 2, 6);   // padding horizontal
                    b.style.paddingTop = 1; b.style.paddingBottom = 1; // botón lo más bajo posible
                    b.style.paddingLeft = ph; b.style.paddingRight = ph;
                    b.style.marginTop = Mathf.Clamp(bw * 0.003f, 1, 3); // separación entre botones
                    if (d != null)
                    {
                        d.style.fontSize = Mathf.Clamp(bw * 0.015f, 7, 12);
                        d.style.marginTop = -Mathf.Clamp(bw * 0.004f, 1, 5); // junta nombre y subtítulo
                    }
                });
                return b;
            }

            // ---- Página izquierda ---- (rev: dificultades sin subtexto)
            left.Add(PLabel("El Viaje del Héroe", 0.028f, 12, 26, InkDark, true));
            if (startStep == 1 && HasSave)
            {
                var cont = MakeButton("▶ Continuar partida", () => { startOpen = false; OnContinue?.Invoke(); }, new Color(0.55f, 0.30f, 0.12f));
                cont.style.marginRight = 0;
                cont.style.whiteSpace = WhiteSpace.Normal;
                cont.style.unityTextAlign = TextAnchor.MiddleCenter;
                fontAppliers.Add(bw =>
                {
                    cont.style.fontSize = Mathf.Clamp(bw * 0.016f, 9, 12);
                    cont.style.marginTop = Mathf.Clamp(bw * 0.010f, 4, 10);
                    float cp = Mathf.Clamp(bw * 0.007f, 2, 6);
                    cont.style.paddingTop = cp; cont.style.paddingBottom = cp;
                });
                left.Add(cont);
            }
            if (startStep == 2)
            {
                // Ancla el contenido arriba (sin el hueco de centrado) para que
                // "Cambiar dificultad" quede visible dentro de la hoja.
                left.style.justifyContent = Justify.FlexStart;
                // Grupo compacto: "Dificultad: X" con "Cambiar dificultad" pegado debajo.
                var grp = new VisualElement();
                grp.style.alignItems = Align.Center;
                grp.style.marginTop = 12;
                var d = PLabel($"Dificultad: {DiffLabel(startDiff)}", 0.016f, 8, 12, InkDark, false);
                grp.Add(d);
                var chg = TextButton("← Cambiar dificultad", () => { startStep = 1; RefreshStart(); });
                chg.style.marginTop = 1;
                fontAppliers.Add(bw => chg.style.fontSize = Mathf.Clamp(bw * 0.015f, 8, 12));
                grp.Add(chg);
                left.Add(grp);
            }

            // ---- Página derecha ----
            if (startStep == 1)
            {
                foreach (var diff in new[] { Difficulty.Facil, Difficulty.Medio, Difficulty.Dificil })
                {
                    var dd = diff;
                    // Clic en una dificultad → avanza directo a elegir líder (sin botón aparte).
                    // Sin subtexto: solo el nombre de la dificultad (más compacto).
                    right.Add(PChoice(DiffLabel(dd), "", startDiff == dd,
                        () => { startDiff = dd; startStep = 2; RefreshStart(); }));
                }
            }
            else
            {
                // Ancla arriba para que Caballero y Mago (con su subtítulo) quepan enteros.
                right.style.justifyContent = Justify.FlexStart;
                right.style.paddingTop = 6;
                right.Add(PChoice("Caballero", "Poder base 3", false,
                    () => { startOpen = false; OnStartGame?.Invoke(startDiff, Starter.Heroe); }));
                right.Add(PChoice("Mago", "Movilidad +1 (1d6 + 1)", false,
                    () => { startOpen = false; OnStartGame?.Invoke(startDiff, Starter.Heroina); }));
            }

            // Aplica las fuentes cuando el libro ya tiene ancho resuelto (y en cada relayout).
            book.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float bw = book.resolvedStyle.width;
                if (bw > 1f && !float.IsNaN(bw))
                    foreach (var a in fontAppliers) a(bw);
            });

            return overlay;
        }

        // -------------------------------------------------------------------
        // Menú (☰) y Ajustes
        // -------------------------------------------------------------------
        Button MenuItem(string text, Action onClick)
        {
            var b = new Button(() => onClick()) { text = text };
            b.style.width = Length.Percent(100);
            b.style.marginTop = 4; b.style.marginBottom = 0;
            b.style.marginLeft = 0; b.style.marginRight = 0;
            b.style.paddingTop = 9; b.style.paddingBottom = 9;
            b.style.paddingLeft = 12; b.style.paddingRight = 12;
            b.style.backgroundColor = new StyleColor(new Color(0.20f, 0.18f, 0.24f));
            b.style.color = new StyleColor(Ink);
            b.style.fontSize = 14;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.borderTopLeftRadius = 6; b.style.borderTopRightRadius = 6;
            b.style.borderBottomLeftRadius = 6; b.style.borderBottomRightRadius = 6;
            NoBorder(b);
            return b;
        }

        VisualElement MenuOverlay()
        {
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0; overlay.style.right = 0; overlay.style.top = 0; overlay.style.bottom = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.35f));
            overlay.RegisterCallback<ClickEvent>(_ => { menuOpen = false; ReRender(); });

            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.top = 8; panel.style.right = 8;
            panel.style.width = 210;
            panel.style.paddingLeft = 8; panel.style.paddingRight = 8;
            panel.style.paddingTop = 8; panel.style.paddingBottom = 8;
            panel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.10f, 0.14f, 0.98f));
            panel.style.borderTopLeftRadius = 10; panel.style.borderTopRightRadius = 10;
            panel.style.borderBottomLeftRadius = 10; panel.style.borderBottomRightRadius = 10;
            panel.style.borderTopWidth = 2; panel.style.borderBottomWidth = 2;
            panel.style.borderLeftWidth = 2; panel.style.borderRightWidth = 2;
            SetBorderColor(panel, new Color(0.6f, 0.45f, 0.2f));
            panel.RegisterCallback<ClickEvent>(e => e.StopPropagation());

            panel.Add(MenuItem("🗺 Nueva partida", () => { menuOpen = false; OnNewGame?.Invoke(); }));
            panel.Add(MenuItem("⚙ Ajustes", () => { menuOpen = false; settingsOpen = true; ReRender(); }));

            overlay.Add(panel);
            return overlay;
        }

        VisualElement SettingsOverlay()
        {
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0; overlay.style.right = 0; overlay.style.top = 0; overlay.style.bottom = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.55f));
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.RegisterCallback<ClickEvent>(_ => { settingsOpen = false; ReRender(); });

            var panel = new VisualElement();
            panel.style.width = Length.Percent(88);
            panel.style.maxWidth = 360;
            panel.style.paddingLeft = 18; panel.style.paddingRight = 18;
            panel.style.paddingTop = 16; panel.style.paddingBottom = 16;
            panel.style.backgroundColor = new StyleColor(new Color(0.12f, 0.10f, 0.14f, 0.98f));
            panel.style.borderTopLeftRadius = 12; panel.style.borderTopRightRadius = 12;
            panel.style.borderBottomLeftRadius = 12; panel.style.borderBottomRightRadius = 12;
            panel.style.borderTopWidth = 2; panel.style.borderBottomWidth = 2;
            panel.style.borderLeftWidth = 2; panel.style.borderRightWidth = 2;
            SetBorderColor(panel, new Color(0.6f, 0.45f, 0.2f));
            panel.RegisterCallback<ClickEvent>(e => e.StopPropagation());

            var title = MakeLabel("Ajustes", 18, Highlight);
            title.style.marginBottom = 10;
            panel.Add(title);

            // Volumen
            var volLbl = MakeLabel("Volumen", 13);
            volLbl.style.marginBottom = 2;
            panel.Add(volLbl);
            var slider = new Slider(0f, 1f) { value = Volume };
            slider.style.marginBottom = 12;
            slider.RegisterValueChangedCallback(ev => { Volume = ev.newValue; OnSetVolume?.Invoke(Volume); });
            panel.Add(slider);

            // Idioma (sin función por ahora)
            var langLbl = MakeLabel("Idioma", 13);
            langLbl.style.marginBottom = 2;
            panel.Add(langLbl);
            var lang = new DropdownField(new List<string> { "Español" }, 0);
            lang.SetEnabled(false);
            lang.style.marginBottom = 14;
            panel.Add(lang);

            var close = MakeButton("Cerrar", () => { settingsOpen = false; ReRender(); }, Highlight);
            close.style.alignSelf = Align.Center;
            panel.Add(close);

            overlay.Add(panel);
            return overlay;
        }

        // -------------------------------------------------------------------
        // Libro de información (overlay con pestañas sobre la imagen del libro)
        // -------------------------------------------------------------------
        VisualElement BookOverlay(GameState s)
        {
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.6f));
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;

            var book = new VisualElement();
            CardSprites.ApplyImageContain(book, "book-" + bookTab);
            book.style.overflow = Overflow.Hidden; // el contenido nunca se sale del libro
            overlay.Add(book);
            FitByWidth(book, 1536f / 1024f, Compact ? 98f : 96f, Compact ? 3000f : 820f);

            var tune = bookTune[bookTab];

            // Pestañas: banda a la derecha del libro (zonas clicables sobre las pestañas impresas).
            var tabs = new VisualElement();
            tabs.style.position = Position.Absolute;
            tabs.style.right = Length.Percent(2);
            tabs.style.top = Length.Percent(10);
            tabs.style.flexDirection = FlexDirection.Column;
            tabs.style.width = Length.Percent(tune.tw);
            tabs.style.height = Length.Percent(tune.th);
            foreach (var id in new[] { "party", "bonos", "generales", "historia" })
            {
                string tid = id;
                var tb = new Button(() => { bookTab = tid; ReRender(); }) { text = "" };
                tb.style.flexGrow = 1;
                tb.style.minHeight = 0; // permite bandas de pestañas más bajas
                tb.style.marginTop = 0;
                tb.style.marginBottom = 0;
                tb.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0));
                tb.style.borderTopLeftRadius = 0;
                tb.style.borderTopRightRadius = 0;
                tb.style.borderBottomLeftRadius = 0;
                tb.style.borderBottomRightRadius = 0;
                tb.style.borderTopWidth = 0; tb.style.borderBottomWidth = 0; tb.style.borderLeftWidth = 0; tb.style.borderRightWidth = 0;
                tabs.Add(tb);
            }
            book.Add(tabs);

            // Página izquierda.
            var leftPage = new VisualElement();
            leftPage.style.position = Position.Absolute;
            leftPage.style.left = Length.Percent(12);
            leftPage.style.top = Length.Percent(15);
            leftPage.style.width = Length.Percent(tune.lw);
            leftPage.style.height = Length.Percent(tune.lh);
            leftPage.Add(BookLeft(s, bookTab));
            book.Add(leftPage);

            // Página derecha.
            var rightPage = new VisualElement();
            rightPage.style.position = Position.Absolute;
            rightPage.style.left = Length.Percent(50);
            rightPage.style.top = Length.Percent(15);
            rightPage.style.width = Length.Percent(tune.rw);
            rightPage.style.height = Length.Percent(tune.rh);
            rightPage.Add(BookRight(s, bookTab));
            book.Add(rightPage);

            // Tamaños de fuente y espaciado calibrados (@ ancho libro 242) escalados
            // proporcionalmente al ancho real → se ven igual en cualquier pantalla.
            ApplyBookTuned(book, leftPage, rightPage, tune);

            var close = MakeButton("✕ Cerrar", () => { bookOpen = false; ReRender(); }, new Color(0.4f, 0.1f, 0.1f));
            close.style.position = Position.Absolute;
            close.style.top = 12;
            close.style.right = 12;
            overlay.Add(close);

            return overlay;
        }

        // ============================================================
        // Dimensiones calibradas del libro (por pestaña). Fuentes/espaciado en px
        // medidos a ancho de libro 242 (S9); se escalan al ancho real en ApplyBookTuned.
        // ============================================================
        class TabTune { public float lw, lh, rw, rh, tw, th, fH, fB, fR, mg, pd; }
        readonly Dictionary<string, TabTune> bookTune = new()
        {
            { "party",     new TabTune { lw = 32, lh = 55, rw = 25, rh = 55, tw = 19, th = 10, fH = 10, fB = 6,  fR = 7, mg = 0f,   pd = 0.1f } },
            { "bonos",     new TabTune { lw = 30, lh = 55, rw = 27, rh = 55, tw = 18, th = 10, fH = 10, fB = 12, fR = 7, mg = 0.1f, pd = 0.1f } },
            { "generales", new TabTune { lw = 30, lh = 55, rw = 27, rh = 55, tw = 18, th = 10, fH = 8,  fB = 7,  fR = 7, mg = 0.1f, pd = 0.1f } },
            { "historia",  new TabTune { lw = 30, lh = 55, rw = 27, rh = 55, tw = 18, th = 10, fH = 8,  fB = 7,  fR = 7, mg = 0.1f, pd = 0.1f } },
        };

        // Aplica fuentes y espaciado calibrados, escalados al ancho real del libro.
        void ApplyBookTuned(VisualElement book, VisualElement leftPage, VisualElement rightPage, TabTune t)
        {
            void Apply()
            {
                float bw = book.resolvedStyle.width;
                if (bw <= 1f || float.IsNaN(bw)) return;
                float k = bw / 242f; // 242 = ancho de libro con el que se calibró (S9)
                foreach (var l in leftPage.Query<Label>().ToList())
                    l.style.fontSize = (l.name == "book-header" ? t.fH : t.fB) * k;
                foreach (var l in rightPage.Query<Label>().ToList())
                    l.style.fontSize = t.fR * k;
                ApplyItemsSpacing(leftPage.childCount > 0 ? leftPage[0] : null, t.mg * k, t.pd * k);
                var sv = rightPage.childCount > 0 ? rightPage[0] as ScrollView : null;
                ApplyItemsSpacing(sv?.contentContainer, t.mg * k, t.pd * k);
            }
            book.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            Apply();
        }

        void ApplyItemsSpacing(VisualElement container, float mg, float pd)
        {
            if (container == null) return;
            foreach (var child in container.Children())
            {
                child.style.marginTop = 0;
                child.style.marginBottom = mg;
                if (!(child is Label)) { child.style.paddingTop = pd; child.style.paddingBottom = pd; }
            }
        }

        static Color InkDark => new(0.20f, 0.12f, 0.05f);
        static Color InkMuted => new(0.42f, 0.30f, 0.17f);

        static string TitleFor(string tab) => tab switch
        {
            "party" => "PARTY",
            "bonos" => "BONOS DE REGIÓN",
            "generales" => "GENERALES",
            "historia" => "HISTORIA",
            _ => "",
        };

        Label BookTitle(string t)
        {
            var l = new Label(t);
            l.name = "book-header"; // el título usa su propio tamaño de fuente (izq.)
            l.style.color = new StyleColor(InkDark);
            l.style.fontSize = 26;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.letterSpacing = 3;
            return l;
        }

        Label Para(string t, int size, Color color, bool center = true)
        {
            var l = new Label(t);
            l.style.color = new StyleColor(color);
            l.style.fontSize = size;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.width = Length.Percent(100); // ocupa el ancho de su columna (evita el corte palabra-a-palabra)
            if (center) l.style.unityTextAlign = TextAnchor.MiddleCenter;
            return l;
        }

        // Página izquierda: título centrado (y en Party, poder total + descripción).
        VisualElement BookLeft(GameState s, string tab)
        {
            var col = new VisualElement();
            col.style.flexGrow = 1;
            col.style.justifyContent = Justify.Center;
            col.style.alignItems = Align.Center;

            col.Add(BookTitle(TitleFor(tab)));

            if (tab == "party")
            {
                var power = new Label($"⚔ {Engine.GetPartyPower(s)}");
                power.style.color = new StyleColor(InkDark);
                power.style.fontSize = 26;
                power.style.unityFontStyleAndWeight = FontStyle.Bold;
                power.style.marginTop = 6;
                col.Add(power);

                var cap = Para("Poder total del Party", 10, InkMuted);
                cap.style.marginBottom = 5;
                //col.Add(cap);

                foreach (var frase in new[]
                {
                    "Coloca las cartas de tu mano sobre la mesa para crear tu camino del héroe. Visita pueblos, recluta otros héroes y derrota al Rey Demonio.",

                })
                {
                    var f = Para(frase, 10, InkMuted);
                    f.style.marginBottom = 0;
                    col.Add(f);
                }
            }
            else if (tab == "generales")
            {
                var count = new Label($"{s.GeneralsDefeated} / 4");
                count.style.color = new StyleColor(InkDark);
                count.style.fontSize = 32;
                count.style.unityFontStyleAndWeight = FontStyle.Bold;
                count.style.marginTop = 8;
                col.Add(count);
                col.Add(DiamondDiagram());
            }
            return col;
        }

        // Diagrama de rombo: distancia Manhattan al Corazón (♥ en el centro).
        VisualElement DiamondDiagram()
        {
            var wrap = new VisualElement();
            wrap.style.marginTop = 6;
            wrap.style.alignItems = Align.Center;
            for (int r = -2; r <= 2; r++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                for (int c = -2; c <= 2; c++)
                {
                    bool center = r == 0 && c == 0;
                    var cell = new Label(center ? "♥" : (Mathf.Abs(r) + Mathf.Abs(c)).ToString());
                    cell.style.width = 6;
                    cell.style.fontSize = 15;
                    cell.style.unityTextAlign = TextAnchor.MiddleCenter;
                    cell.style.color = new StyleColor(center ? new Color(0.72f, 0.15f, 0.15f) : InkMuted);
                    row.Add(cell);
                }
                wrap.Add(row);
            }
            return wrap;
        }

        static Color TierColor(CombatTier t) => t switch
        {
            CombatTier.Low => new Color(0.72f, 0.20f, 0.20f),
            CombatTier.Mid => new Color(0.72f, 0.50f, 0.12f),
            CombatTier.High => new Color(0.25f, 0.50f, 0.22f),
            _ => InkDark,
        };

        static Color RegionColor(Region r) => r switch
        {
            Region.Bosque => new Color(0.30f, 0.50f, 0.25f),
            Region.Planicies => new Color(0.80f, 0.68f, 0.30f),
            Region.Montanas => new Color(0.52f, 0.56f, 0.66f),
            Region.Volcan => new Color(0.72f, 0.30f, 0.20f),
            _ => InkMuted,
        };

        // Fila de bono: punto de color + región + valor (destacado si > 0).
        VisualElement BonusRow(Region region, int bonus)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.2f, 0.1f, 0.30f));

            var dot = new VisualElement();
            dot.style.width = 11;
            dot.style.height = 11;
            dot.style.marginRight = 3;
            dot.style.borderTopLeftRadius = 6;
            dot.style.borderTopRightRadius = 6;
            dot.style.borderBottomLeftRadius = 6;
            dot.style.borderBottomRightRadius = 6;
            dot.style.backgroundColor = new StyleColor(RegionColor(region));
            row.Add(dot);

            var n = new Label(Cards.RegionLabel(region));
            n.style.color = new StyleColor(bonus > 0 ? InkDark : InkMuted);
            n.style.fontSize = 13;
            n.style.marginRight = 10; // separación mínima región → bono
            row.Add(n);

            var v = new Label("+" + bonus);
            v.style.color = new StyleColor(bonus > 0 ? InkDark : InkMuted);
            v.style.fontSize = 13;
            if (bonus > 0) v.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(v);
            return row;
        }

        // Página derecha: los datos de la pestaña.
        VisualElement BookRight(GameState s, string tab)
        {
            var col = new ScrollView(ScrollViewMode.Vertical);
            col.style.flexGrow = 1;
            col.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            col.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            col.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            EnableDragScroll(col);

            switch (tab)
            {
                case "party":
                    foreach (var m in s.Party.Members)
                    {
                        int bonus = m.Region.HasValue ? s.VillageBonus[m.Region.Value] : 0;
                        col.Add(DataRow(m.Name, (m.BasePower + bonus).ToString()));
                    }
                    break;

                case "bonos":
                    foreach (var region in Cards.Regions)
                        col.Add(BonusRow(region, s.VillageBonus[region]));
                    var bfoot = Para("Los Pueblos otorgan +1 o +2 Poder a los Héroes de su región (§22).", 10, InkMuted, center: false);
                    bfoot.style.marginTop = 8;
                    col.Add(bfoot);
                    break;

                case "generales":
                {
                    int party = Engine.GetPartyPower(s);
                    int next = Math.Min(s.GeneralsDefeated, Cards.GeneralPowers.Length - 1);
                    for (int i = 0; i < Cards.GeneralPowers.Length; i++)
                    {
                        int p = Cards.GeneralPowers[i];
                        var fc = Engine.CombatForecastFor(party, p, false);

                        var block = new VisualElement();
                        block.style.paddingTop = 2;
                        block.style.paddingBottom = 2;
                        block.style.borderBottomWidth = 1;
                        block.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.2f, 0.1f, 0.30f));

                        var t = new Label($"General {i + 1} · P {p}");
                        t.style.color = new StyleColor(InkDark);
                        t.style.fontSize = 11;
                        t.style.unityFontStyleAndWeight = FontStyle.Bold;
                        block.Add(t);

                        string prox = i == next ? "Próximo · " : "";
                        var sub = new Label($"{prox}● {fc.Label} · {fc.Attempts} intentos");
                        sub.style.color = new StyleColor(TierColor(fc.Tier));
                        sub.style.fontSize = 9;
                        sub.style.whiteSpace = WhiteSpace.Normal;
                        block.Add(sub);

                        col.Add(block);
                    }
                    var gfoot = Para("Iguala o supera su Poder para tener más intentos.", 9, InkMuted, center: false);
                    gfoot.style.marginTop = 4;
                    col.Add(gfoot);
                    break;
                }

                case "historia":
                    int start = Math.Max(0, s.Log.Count - 16);
                    for (int i = start; i < s.Log.Count; i++)
                    {
                        var l = Para("• " + s.Log[i].Text, 10, InkDark, center: false);
                        l.style.marginBottom = 4;
                        col.Add(l);
                    }
                    break;
            }
            return col;
        }

        // Fila "nombre .......... valor" con línea inferior tenue.
        VisualElement DataRow(string name, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.2f, 0.1f, 0.30f));

            var n = new Label(name);
            n.style.color = new StyleColor(InkDark);
            n.style.fontSize = 13;
            n.style.marginRight = 10; // separación mínima nombre → valor
            row.Add(n);

            var v = new Label(value);
            v.style.color = new StyleColor(InkDark);
            v.style.fontSize = 13;
            v.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(v);
            return row;
        }

        // -------------------------------------------------------------------
        // Combate (minijuego de búsqueda del Corazón)
        // -------------------------------------------------------------------
        VisualElement CombatPanel(GameState s)
        {
            var pc = s.PendingCombat;
            // Rejillas pequeñas (4–6): celdas cómodas que caben en el arte del jefe.
            bool cmp = Compact;
            combatCell = pc.GridN <= 6 ? (cmp ? 30 : 48) : (cmp ? 22 : 32);

            // Panel oscuro; el arte del jefe irá como recuadro detrás de la rejilla.
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.marginTop = 8;
            panel.style.overflow = Overflow.Hidden;
            panel.style.borderTopLeftRadius = 10;
            panel.style.borderTopRightRadius = 10;
            panel.style.borderBottomLeftRadius = 10;
            panel.style.borderBottomRightRadius = 10;
            panel.style.backgroundColor = new StyleColor(new Color(0.09f, 0.08f, 0.11f));

            var scrim = new VisualElement();
            scrim.style.flexGrow = 1;
            scrim.style.alignItems = Align.Center;
            scrim.style.paddingTop = 14;
            scrim.style.paddingBottom = 14;
            scrim.style.paddingLeft = 12;
            scrim.style.paddingRight = 12;
            panel.Add(scrim);

            string title = pc.IsRey ? "¡El Rey Demonio!" : $"¡{Cards.GetCard(pc.CardId).Name}!";
            var h = MakeLabel(title, cmp ? 15 : 24, Highlight);
            h.style.marginRight = 0;
            h.style.marginBottom = 4;
            h.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.style.unityTextAlign = TextAnchor.MiddleCenter;
            h.style.whiteSpace = WhiteSpace.Normal;
            h.style.flexShrink = 0;
            scrim.Add(h);

            var info = MakeLabel(
                $"Intentos: {pc.AttemptsUsed}/{pc.AttemptsTotal}    " +
                $"Poder {(pc.IsRey ? "Rey" : "General")}: {pc.GeneralPower}    Party: {pc.PartyPower}" +
                (pc.IsRey ? $"    Corazones: {pc.HeartsFound}/{pc.HeartsTotal}" : ""), cmp ? 10 : 12);
            info.style.marginRight = 0;
            info.style.unityTextAlign = TextAnchor.MiddleCenter;
            info.style.whiteSpace = WhiteSpace.Normal;
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
            gridCol.style.position = Position.Relative;

            // Recuadro con el arte del jefe DETRÁS de las casillas (coords quedan fuera).
            int step = combatCell + 2;                 // ancho/alto de celda + márgenes
            var bossBox = new VisualElement();
            bossBox.style.position = Position.Absolute;
            bossBox.style.left = step - 4;             // tras la columna de letras
            bossBox.style.top = combatCell - 4;        // tras la fila de números
            bossBox.style.width = pc.GridN * step + 4 + combatCell;   // margen extra der.
            bossBox.style.height = pc.GridN * step + 4 + combatCell;  // margen extra abajo
            bossBox.style.overflow = Overflow.Hidden;
            bossBox.style.borderTopLeftRadius = 6; bossBox.style.borderTopRightRadius = 6;
            bossBox.style.borderBottomLeftRadius = 6; bossBox.style.borderBottomRightRadius = 6;
            CardSprites.ApplyBoss(bossBox, pc.Region); // cover: llena el recuadro
            var dim = new VisualElement();
            dim.style.position = Position.Absolute;
            dim.style.left = 0; dim.style.right = 0; dim.style.top = 0; dim.style.bottom = 0;
            dim.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.28f));
            dim.pickingMode = PickingMode.Ignore;
            bossBox.Add(dim);
            gridCol.Add(bossBox);

            // Regla superior: esquina vacía + números de columna (1..N).
            var ruler = new VisualElement();
            ruler.style.flexDirection = FlexDirection.Row;
            ruler.Add(CoordLabel("")); // esquina
            for (int c = 0; c < pc.GridN; c++) ruler.Add(CoordLabel((c + 1).ToString()));
            gridCol.Add(ruler);

            for (int r = 0; r < pc.GridN; r++)
            {
                var rowEl = new VisualElement();
                rowEl.style.flexDirection = FlexDirection.Row;
                rowEl.style.alignItems = Align.Center;
                rowEl.Add(CoordLabel(((char)('A' + r)).ToString())); // letra de fila
                for (int c = 0; c < pc.GridN; c++)
                {
                    string k = $"{r},{c}";
                    if (reveal.TryGetValue(k, out int dist))
                    {
                        var cellEl = new VisualElement();
                        CombatSize(cellEl);
                        var dc = DistColor(dist, pc.GridN);
                        cellEl.style.backgroundColor = new StyleColor(new Color(dc.r, dc.g, dc.b, 0.92f));
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
                        b.style.backgroundColor = new StyleColor(new Color(0.09f, 0.08f, 0.11f, 0.22f));
                        rowEl.Add(b);
                    }
                }
                gridCol.Add(rowEl);
            }
            scrim.Add(gridCol); // rejilla pequeña: cabe sin scroll

            return panel;
        }

        Label CoordLabel(string text)
        {
            var l = new Label(text);
            l.style.width = combatCell + 2;   // ancho de celda + sus márgenes
            l.style.height = combatCell;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.color = new StyleColor(new Color(0.9f, 0.85f, 0.7f));
            l.style.fontSize = Mathf.Clamp(combatCell / 2 - 2, 9, 12);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        void CombatSize(VisualElement e)
        {
            e.style.width = combatCell;
            e.style.height = combatCell;
            // Borde cálido tenue para delinear la cuadrícula sobre el arte del jefe.
            e.style.borderTopWidth = 1; e.style.borderBottomWidth = 1;
            e.style.borderLeftWidth = 1; e.style.borderRightWidth = 1;
            var edge = new Color(0.93f, 0.86f, 0.66f, 0.30f);
            e.style.borderTopColor = new StyleColor(edge); e.style.borderBottomColor = new StyleColor(edge);
            e.style.borderLeftColor = new StyleColor(edge); e.style.borderRightColor = new StyleColor(edge);
            e.style.borderTopLeftRadius = 3; e.style.borderTopRightRadius = 3;
            e.style.borderBottomLeftRadius = 3; e.style.borderBottomRightRadius = 3;
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
        // Pantalla final: crónica del viaje sobre un pergamino, con capital "E".
        VisualElement EndPanel(GameState s, GameOverInfo go)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;

            var card = new VisualElement();
            card.style.width = Length.Percent(94);
            card.style.maxWidth = 640;
            card.style.maxHeight = Length.Percent(96);
            card.style.backgroundColor = new StyleColor(new Color(0.94f, 0.90f, 0.80f));
            card.style.paddingLeft = 22;
            card.style.paddingRight = 22;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.borderTopLeftRadius = 12;
            card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12;
            card.style.borderBottomRightRadius = 12;
            card.style.borderTopWidth = 3;
            card.style.borderBottomWidth = 3;
            card.style.borderLeftWidth = 3;
            card.style.borderRightWidth = 3;
            SetBorderColor(card, new Color(0.45f, 0.32f, 0.15f));

            bool won = go.Status == GameStatus.Won;
            var title = new Label(won ? "Victoria" : "Derrota");
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 30;
            title.style.letterSpacing = 3;
            title.style.marginBottom = 10;
            title.style.color = new StyleColor(won ? new Color(0.55f, 0.40f, 0.10f) : new Color(0.60f, 0.16f, 0.16f));
            card.Add(title);

            var paras = Chronicle.Build(s);
            Color ink = new(0.20f, 0.12f, 0.05f);

            // Crónica: texto a ancho completo, sin barra de scroll (se arrastra con el dedo).
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.marginBottom = 12;
            scroll.style.width = Length.Percent(100);
            scroll.contentContainer.style.width = Length.Percent(100);
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            EnableDragScroll(scroll);

            for (int i = 0; i < paras.Count; i++)
            {
                var p = new Label(paras[i]);
                p.style.color = new StyleColor(ink);
                p.style.fontSize = i == 0 ? 15 : 14;
                p.style.whiteSpace = WhiteSpace.Normal;
                p.style.width = Length.Percent(100);       // ocupa todo el ancho de la crónica
                p.style.unityTextAlign = TextAnchor.UpperLeft;
                p.style.marginTop = i == 0 ? 0 : 8;
                scroll.Add(p);
            }
            card.Add(scroll);

            var nueva = MakeButton("Nueva partida", () => OnNewGame?.Invoke(), Highlight);
            nueva.style.alignSelf = Align.Center;
            card.Add(nueva);

            panel.Add(card);
            return panel;
        }
    }
}
