using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>
    /// Small deterministic RNG (SplitMix64) whose whole state is one ulong stored in the GameState,
    /// so saves and replays continue the exact same sequence.
    /// </summary>
    public class Rng
    {
        readonly GameState _state;

        public Rng(GameState state) { _state = state; }

        public ulong NextULong()
        {
            ulong z = (_state.RngState += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>[0, 1)</summary>
        public double Next() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        public double Range(double min, double max) => min + (max - min) * Next();

        /// <summary>[min, max] inclusive.</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(Next() * (max - min + 1));
        }

        public bool Chance(double p) => Next() < p;

        public double Normal(double mean = 0, double sd = 1)
        {
            double u1 = 1.0 - Next();
            double u2 = Next();
            double n = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + sd * n;
        }

        public T Pick<T>(IList<T> list) => list.Count == 0 ? default : list[Range(0, list.Count - 1)];

        public T PickWeighted<T>(IList<T> items, Func<T, double> weight)
        {
            double total = 0;
            for (int i = 0; i < items.Count; i++) total += Math.Max(0, weight(items[i]));
            if (total <= 0) return items.Count > 0 ? items[Range(0, items.Count - 1)] : default;
            double r = Next() * total;
            for (int i = 0; i < items.Count; i++)
            {
                r -= Math.Max(0, weight(items[i]));
                if (r <= 0) return items[i];
            }
            return items[items.Count - 1];
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
