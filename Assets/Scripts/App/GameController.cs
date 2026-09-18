// ============================================================================
// GameController — puente entre el motor puro (ElViaje.Game) y la capa Unity.
// Mantiene el GameState actual, aplica acciones vía Engine.ApplyMove y avisa a
// la UI cuando el estado cambia. No dibuja nada: eso lo hará la vista (UI Toolkit).
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using ElViaje.Game;

namespace ElViaje.App
{
    public class GameController : MonoBehaviour
    {
        /// <summary>Estado actual de la partida (null hasta llamar a NewGame).</summary>
        public GameState State { get; private set; }

        /// <summary>Se dispara con el nuevo estado tras cada cambio (nueva partida o acción aplicada).</summary>
        public event Action<GameState> StateChanged;

        public bool HasGame => State != null;

        // -------------------------------------------------------------------
        // Ciclo de partida
        // -------------------------------------------------------------------
        public void NewGame(uint seed, Difficulty difficulty = Difficulty.Medio, Starter starter = Starter.Heroe)
        {
            State = Engine.CreateInitialState(new NewGameOptions
            {
                Seed = seed,
                Difficulty = difficulty,
                Starter = starter,
            });
            StateChanged?.Invoke(State);
        }

        /// <summary>Nueva partida con semilla aleatoria (capa de UI).</summary>
        public void NewGame(Difficulty difficulty = Difficulty.Medio, Starter starter = Starter.Heroe)
            => NewGame(Rng.MakeSeed(), difficulty, starter);

        /// <summary>Carga una partida existente (desde guardado) y notifica a la UI.</summary>
        public void LoadGame(GameState s)
        {
            if (s == null) return;
            State = s;
            StateChanged?.Invoke(State);
        }

        /// <summary>
        /// Aplica una acción. Devuelve true si el estado avanzó (acción válida).
        /// Engine.ApplyMove devuelve el mismo objeto si la acción no era válida.
        /// </summary>
        public bool Dispatch(GameAction action)
        {
            if (State == null || action == null) return false;
            var next = Engine.ApplyMove(State, action);
            bool changed = !ReferenceEquals(next, State);
            State = next;
            if (changed) StateChanged?.Invoke(State);
            return changed;
        }

        /// <summary>
        /// Avanza el turno del Mundo si toca (el "oponente" determinista). Devuelve
        /// true si hizo una jugada. Útil para llamar en bucle desde la UI.
        /// </summary>
        public bool AdvanceWorld()
        {
            if (State == null) return false;
            var move = Engine.GetAIMove(State);
            if (move == null) return false;
            return Dispatch(move);
        }

        // -------------------------------------------------------------------
        // Consultas para la vista (pasan al motor)
        // -------------------------------------------------------------------
        public List<GameAction> LegalMoves()
            => State != null ? Engine.GetLegalMoves(State) : new List<GameAction>();

        public List<Placement> Placements(string cardId)
            => State != null ? Engine.GetPlacements(State, cardId) : new List<Placement>();

        public List<(int x, int y)> StepTargets()
            => State != null ? Engine.GetStepTargets(State) : new List<(int, int)>();

        public int PartyPower() => State != null ? Engine.GetPartyPower(State) : 0;

        public GameOverInfo GameOver()
            => State != null ? Engine.CheckGameOver(State) : new GameOverInfo { Over = false, Status = GameStatus.Playing };
    }
}
