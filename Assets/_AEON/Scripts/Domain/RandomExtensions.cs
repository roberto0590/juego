using System;
using System.Collections.Generic;

namespace GodGame.Domain
{
    /// <summary>
    /// Utilidades sobre System.Random. Se usa System.Random (y no el Random de Unity)
    /// porque permite tener varias instancias independientes con semilla,
    /// necesario para que la generación y la IA sean reproducibles.
    /// </summary>
    public static class RandomExtensions
    {
        public static float Range(this Random rng, float min, float max)
            => min + (float)rng.NextDouble() * (max - min);

        /// <summary>Entero en [min, max] (ambos incluidos).</summary>
        public static int RangeInclusive(this Random rng, int min, int max)
            => rng.Next(min, max + 1);

        public static bool Chance(this Random rng, float probability)
            => rng.NextDouble() < probability;

        /// <summary>Distribución normal (Box-Muller).</summary>
        public static float Gaussian(this Random rng, float mean = 0f, float stdDev = 1f)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * (float)z;
        }

        /// <summary>Índice elegido según pesos relativos.</summary>
        public static int WeightedIndex(this Random rng, IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0f, weights[i]);
            if (total <= 0f) return rng.Next(weights.Count);

            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= Math.Max(0f, weights[i]);
                if (roll <= 0f) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>Fisher-Yates in-place.</summary>
        public static void Shuffle<T>(this Random rng, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static T Pick<T>(this Random rng, IReadOnlyList<T> list) => list[rng.Next(list.Count)];

        /// <summary>
        /// Deriva una sub-semilla estable a partir de una semilla y una etiqueta.
        /// Permite regenerar un planeta concreto sin regenerar todo el multiverso.
        /// (FNV-1a: no usar string.GetHashCode, que no es estable entre plataformas/ejecuciones.)
        /// </summary>
        public static int DeriveSeed(int seed, string label)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }
}
