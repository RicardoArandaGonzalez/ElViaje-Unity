// ============================================================================
// RNG determinista (mulberry32). Port fiel de src/game/rng.ts.
// La semilla vive en el estado (uint) y cada uso devuelve valor + nueva semilla.
// Se usa aritmética uint 'unchecked' para replicar Math.imul / >>> de JS.
// ============================================================================
using System;
using System.Collections.Generic;

namespace ElViaje.Game
{
    public static class Rng
    {
        public static (double value, uint seed) NextFloat(uint seed)
        {
            unchecked
            {
                uint t = seed + 0x6D2B79F5u;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                uint mixed = t ^ (t >> 14);
                return (mixed / 4294967296.0, t);
            }
        }

        public static (int value, uint seed) NextInt(uint seed, int min, int max)
        {
            var (value, s) = NextFloat(seed);
            return (min + (int)Math.Floor(value * (max - min + 1)), s);
        }

        public static (int value, uint seed) RollDie(uint seed) => NextInt(seed, 1, 6);

        /// <summary>Baraja una copia (Fisher–Yates determinista).</summary>
        public static (List<T> result, uint seed) Shuffle<T>(IReadOnlyList<T> arr, uint seed)
        {
            var result = new List<T>(arr);
            uint s = seed;
            for (int i = result.Count - 1; i > 0; i--)
            {
                var r = NextInt(s, 0, i);
                s = r.seed;
                int j = r.value;
                (result[i], result[j]) = (result[j], result[i]);
            }
            return (result, s);
        }

        /// <summary>Semilla a partir del reloj (capa de UI; no usar en la lógica pura).</summary>
        public static uint MakeSeed()
        {
            unchecked { return (uint)(Environment.TickCount ^ Guid.NewGuid().GetHashCode()); }
        }
    }
}
