// ============================================================================
// GameApp — arranque de la capa Unity. Va en el mismo GameObject que el
// UI Document. Crea la partida, enlaza GameController con BoardView y traduce
// los clics de la vista en acciones del motor.
// ============================================================================
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
        string selectedCardId;

        void Start()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("GameApp: el UI Document no tiene rootVisualElement. ¿Asignaste el Panel Settings?");
                return;
            }

            controller = GetComponent<GameController>();
            if (controller == null) controller = gameObject.AddComponent<GameController>();

            view = new BoardView(root, controller);
            WireCallbacks();

            controller.StateChanged += OnStateChanged;

            if (controller.HasGame) OnStateChanged(controller.State);
            else view.OpenStart(); // arranca en la pantalla de inicio
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
            view.OnDraw = () => controller.Dispatch(GameAction.Draw());
            view.OnRoll = () => controller.Dispatch(GameAction.Roll());
            view.OnEndMove = () => controller.Dispatch(GameAction.EndMove());
            view.OnInvokeGeneral = () => { selectedCardId = null; controller.Dispatch(GameAction.InvokeGeneral()); };
            view.OnDiscard = id => controller.Dispatch(GameAction.Discard(id));
            view.OnCombatSelect = (r, c) => controller.Dispatch(GameAction.CombatSelect(r, c));
            view.OnCombatRetreat = () => controller.Dispatch(GameAction.CombatRetreat());
        }

        void OnStateChanged(GameState s)
        {
            view.SelectedCardId = selectedCardId;
            view.Render(s);

            // El Turno del Mundo es determinista: se resuelve solo.
            if (s.Status == GameStatus.Playing && s.Phase == Phase.World)
                controller.AdvanceWorld();
        }
    }
}
