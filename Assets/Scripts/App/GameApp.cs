// ============================================================================
// GameApp — arranque de la capa Unity. Va en el mismo GameObject que el
// UI Document. Crea la partida, enlaza GameController con BoardView y traduce
// los clics de la vista en acciones del motor. Además gestiona una capa de
// efectos (fxRoot) independiente del re-dibujado de la vista, para animaciones.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.UIElements;
using ElViaje.Game;

namespace ElViaje.App
{
    [RequireComponent(typeof(UIDocument))]
    public class GameApp : MonoBehaviour
    {
        [Header("Partida")]
        public Difficulty difficulty = Difficulty.Medio;
        public Starter starter = Starter.Heroe;
        [Tooltip("0 = semilla aleatoria")]
        public uint seed = 0;

        GameController controller;
        BoardView view;
        VisualElement fxRoot;
        string selectedCardId;
        bool rolling;

        AudioSource musicMain, musicBoss;
        bool muted;

        void Start()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("GameApp: el UI Document no tiene rootVisualElement. ¿Asignaste el Panel Settings?");
                return;
            }

            // Dos capas: el tablero y una capa de efectos por encima.
            root.Clear();
            root.style.flexGrow = 1;
            var boardRoot = new VisualElement();
            boardRoot.style.flexGrow = 1;
            root.Add(boardRoot);

            fxRoot = new VisualElement();
            fxRoot.style.position = Position.Absolute;
            fxRoot.style.left = 0;
            fxRoot.style.right = 0;
            fxRoot.style.top = 0;
            fxRoot.style.bottom = 0;
            fxRoot.pickingMode = PickingMode.Ignore;
            fxRoot.style.display = DisplayStyle.None; // solo visible durante animaciones
            root.Add(fxRoot);

            controller = GetComponent<GameController>();
            if (controller == null) controller = gameObject.AddComponent<GameController>();

            SetupAudio();

            view = new BoardView(boardRoot, controller);
            WireCallbacks();

            controller.StateChanged += OnStateChanged;

            if (controller.HasGame) OnStateChanged(controller.State);
            else { view.OpenStart(); ApplyMusic(null); }
        }

        void OnDisable()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
        }

        void WireCallbacks()
        {
            view.OnNewGame = () => { selectedCardId = null; view.OpenStart(); };

            view.OnStartGame = (diff, st) =>
            {
                selectedCardId = null;
                if (seed == 0) controller.NewGame(diff, st);
                else controller.NewGame(seed, diff, st);
            };

            view.OnSelectCard = id =>
            {
                selectedCardId = id;
                view.SelectedCardId = id;
                if (controller.State != null) view.Render(controller.State);
            };

            view.OnPlace = (x, y) =>
            {
                var orientation = Orientation.None;
                foreach (var p in controller.Placements(selectedCardId))
                    if (p.X == x && p.Y == y) { orientation = p.Orientation; break; }
                controller.Dispatch(GameAction.PlayCard(selectedCardId, x, y, orientation));
                selectedCardId = null;
            };

            view.OnStep = (x, y) => controller.Dispatch(GameAction.Step(x, y));

            // Robar: animación de la carta volando del mazo a la mano.
            view.OnDraw = () =>
            {
                var s = controller.State;
                if (s == null) return;
                string top = s.Deck.Count > 0 ? s.Deck[0] : null;
                int before = s.Hand.Count;
                if (controller.Dispatch(GameAction.Draw()) && top != null && controller.State.Hand.Count > before)
                    PlayCardFly(top);
            };

            // Tirar: animación del dado girando y luego se resuelve.
            view.OnRoll = () =>
            {
                if (rolling) return;
                rolling = true;
                PlayDieRoll(
                    resolve: () => { controller.Dispatch(GameAction.Roll()); return controller.State?.LastRoll ?? 1; },
                    onFinished: () => rolling = false);
            };

            view.OnEndMove = () => controller.Dispatch(GameAction.EndMove());
            view.OnInvokeGeneral = () => { selectedCardId = null; controller.Dispatch(GameAction.InvokeGeneral()); };
            view.OnDiscard = id => controller.Dispatch(GameAction.Discard(id));
            view.OnCombatSelect = (r, c) => controller.Dispatch(GameAction.CombatSelect(r, c));
            view.OnCombatRetreat = () => controller.Dispatch(GameAction.CombatRetreat());

            view.Muted = muted;
            view.OnToggleMute = () =>
            {
                muted = !muted;
                view.Muted = muted;
                ApplyMusic(controller.State);
                view.Refresh();
            };
        }

        void OnStateChanged(GameState s)
        {
            view.SelectedCardId = selectedCardId;
            view.Render(s);

            ApplyMusic(s);

            if (s.Status != GameStatus.Playing) return;

            // El Turno del Mundo es determinista: se resuelve solo.
            if (s.Phase == Phase.World)
                controller.AdvanceWorld();
            // Con un General en mano (Posesión) no se roba: se salta la fase.
            else if (s.Phase == Phase.Draw && Engine.IsPossessed(s))
                controller.Dispatch(GameAction.Draw());
        }

        // -------------------------------------------------------------------
        // Música: principal en el juego, melodía de jefe en combate/final.
        // -------------------------------------------------------------------
        void SetupAudio()
        {
            musicMain = gameObject.AddComponent<AudioSource>();
            musicMain.clip = Resources.Load<AudioClip>("Audio/music-main");
            musicMain.loop = true;
            musicMain.volume = 0.35f;
            musicMain.playOnAwake = false;

            musicBoss = gameObject.AddComponent<AudioSource>();
            musicBoss.clip = Resources.Load<AudioClip>("Audio/music-boss");
            musicBoss.loop = true;
            musicBoss.volume = 0.4f;
            musicBoss.playOnAwake = false;
        }

        void ApplyMusic(GameState s)
        {
            if (musicMain == null || musicBoss == null) return;
            if (muted)
            {
                if (musicMain.isPlaying) musicMain.Pause();
                if (musicBoss.isPlaying) musicBoss.Pause();
                return;
            }
            bool boss = s != null && s.Status == GameStatus.Playing
                && (s.Phase == Phase.Combat || s.Phase == Phase.Final);
            if (boss)
            {
                if (musicMain.isPlaying) musicMain.Pause();
                if (!musicBoss.isPlaying) musicBoss.Play();
            }
            else
            {
                if (musicBoss.isPlaying) musicBoss.Pause();
                if (!musicMain.isPlaying) musicMain.Play();
            }
        }

        // -------------------------------------------------------------------
        // Animaciones (en fxRoot, independientes del re-render de la vista)
        // -------------------------------------------------------------------
        void FxAdd(VisualElement e)
        {
            fxRoot.style.display = DisplayStyle.Flex;
            fxRoot.Add(e);
        }

        void FxRemove(VisualElement e)
        {
            fxRoot.Remove(e);
            if (fxRoot.childCount == 0) fxRoot.style.display = DisplayStyle.None;
        }

        static void Tween(VisualElement e, float durationMs, Action<float> apply, Action onDone = null)
        {
            float startMs = Time.realtimeSinceStartup * 1000f;
            IVisualElementScheduledItem item = null;
            item = e.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup * 1000f - startMs) / durationMs);
                apply(t);
                if (t >= 1f)
                {
                    item.Pause();
                    onDone?.Invoke();
                }
            }).Every(16);
        }

        // Dado dibujado por código (cara con puntos) que gira cambiando de cara
        // y se asienta en el resultado. resolve() aplica la tirada y devuelve el valor.
        void PlayDieRoll(Func<int> resolve, Action onFinished)
        {
            var die = new VisualElement();
            die.pickingMode = PickingMode.Ignore;
            die.style.position = Position.Absolute;
            die.style.width = 96;
            die.style.height = 96;
            die.style.left = Length.Percent(50);
            die.style.top = Length.Percent(42);
            die.style.marginLeft = -48;
            die.style.marginTop = -48;
            die.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            FxAdd(die);

            var rng = new System.Random();
            const float spin = 700f, hold = 480f;
            float startMs = Time.realtimeSinceStartup * 1000f;
            bool resolved = false;
            IVisualElementScheduledItem item = null;
            item = die.schedule.Execute(() =>
            {
                float el = Time.realtimeSinceStartup * 1000f - startMs;
                if (el < spin)
                {
                    RenderDieFace(die, rng.Next(1, 7));
                    float t = el / spin;
                    die.style.rotate = new Rotate(new Angle(540f * t));
                    float sc = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
                    die.style.scale = new Scale(new Vector3(sc, sc, 1f));
                }
                else if (!resolved)
                {
                    resolved = true;
                    int face = Mathf.Clamp(resolve(), 1, 6);
                    die.style.rotate = new Rotate(new Angle(0f));
                    die.style.scale = new Scale(new Vector3(1.12f, 1.12f, 1f));
                    RenderDieFace(die, face);
                }
                else if (el >= spin + hold)
                {
                    item.Pause();
                    FxRemove(die);
                    onFinished?.Invoke();
                }
            }).Every(55);
        }

        // Puntos de cada cara (posición en rejilla 3x3, col/fila 0..2).
        static readonly int[][][] PIPS =
        {
            new[] { new[] { 1, 1 } },                                              // 1
            new[] { new[] { 0, 0 }, new[] { 2, 2 } },                              // 2
            new[] { new[] { 0, 0 }, new[] { 1, 1 }, new[] { 2, 2 } },              // 3
            new[] { new[] { 0, 0 }, new[] { 2, 0 }, new[] { 0, 2 }, new[] { 2, 2 } }, // 4
            new[] { new[] { 0, 0 }, new[] { 2, 0 }, new[] { 1, 1 }, new[] { 0, 2 }, new[] { 2, 2 } }, // 5
            new[] { new[] { 0, 0 }, new[] { 2, 0 }, new[] { 0, 1 }, new[] { 2, 1 }, new[] { 0, 2 }, new[] { 2, 2 } }, // 6
        };

        static void RenderDieFace(VisualElement die, int value)
        {
            die.Clear();
            die.style.backgroundColor = new StyleColor(new Color(0.96f, 0.95f, 0.90f));
            die.style.borderTopLeftRadius = 16;
            die.style.borderTopRightRadius = 16;
            die.style.borderBottomLeftRadius = 16;
            die.style.borderBottomRightRadius = 16;
            die.style.borderTopWidth = 3;
            die.style.borderBottomWidth = 3;
            die.style.borderLeftWidth = 3;
            die.style.borderRightWidth = 3;
            var edge = new Color(0.25f, 0.18f, 0.12f);
            die.style.borderTopColor = new StyleColor(edge);
            die.style.borderBottomColor = new StyleColor(edge);
            die.style.borderLeftColor = new StyleColor(edge);
            die.style.borderRightColor = new StyleColor(edge);

            float[] pct = { 24f, 50f, 76f };
            foreach (var p in PIPS[Mathf.Clamp(value, 1, 6) - 1])
            {
                var pip = new VisualElement();
                pip.style.position = Position.Absolute;
                pip.style.width = 16;
                pip.style.height = 16;
                pip.style.marginLeft = -8;
                pip.style.marginTop = -8;
                pip.style.left = Length.Percent(pct[p[0]]);
                pip.style.top = Length.Percent(pct[p[1]]);
                pip.style.backgroundColor = new StyleColor(new Color(0.15f, 0.11f, 0.08f));
                pip.style.borderTopLeftRadius = 8;
                pip.style.borderTopRightRadius = 8;
                pip.style.borderBottomLeftRadius = 8;
                pip.style.borderBottomRightRadius = 8;
                die.Add(pip);
            }
        }

        // Robo: la carta sale del mazo (columna derecha), flota al centro y baja a la mano.
        void PlayCardFly(string cardId)
        {
            var def = Cards.GetCard(cardId);
            var card = new VisualElement();
            card.pickingMode = PickingMode.Ignore;
            card.style.position = Position.Absolute;
            card.style.width = 84;
            card.style.height = 116;
            card.style.marginLeft = -42;
            card.style.marginTop = -58;
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            if (!BoardView.ApplyCardArt(card, def.Kind, def.Region, def.Connections))
                card.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            FxAdd(card);

            // Trayectoria en dos tramos: mazo → centro → mano.
            Vector2 deck = new(90f, 52f);
            Vector2 center = new(50f, 44f);
            Vector2 hand = new(50f, 88f);
            Tween(card, 900, t =>
            {
                Vector2 pos;
                float sc;
                if (t < 0.5f)
                {
                    float u = t / 0.5f; u = 1f - (1f - u) * (1f - u);
                    pos = Vector2.Lerp(deck, center, u);
                    sc = Mathf.Lerp(0.7f, 1.2f, u);
                }
                else
                {
                    float u = (t - 0.5f) / 0.5f; u *= u;
                    pos = Vector2.Lerp(center, hand, u);
                    sc = Mathf.Lerp(1.2f, 0.85f, u);
                }
                card.style.left = Length.Percent(pos.x);
                card.style.top = Length.Percent(pos.y);
                card.style.scale = new Scale(new Vector3(sc, sc, 1f));
            }, () => FxRemove(card));
        }
    }
}
