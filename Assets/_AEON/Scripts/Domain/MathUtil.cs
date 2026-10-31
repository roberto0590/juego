using System;

namespace GodGame.Domain
{
    /// <summary>
    /// Sustituto puro del Mathf de Unity para las capas sin motor (Domain, Generation, Simulation, AI).
    /// Se apoya en System.MathF y reproduce la semántica de Mathf en las funciones que usa el juego.
    /// </summary>
    public static class MathUtil
    {
        public const float PI = MathF.PI;

        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Interpolación lineal con t recortado a [0, 1] (igual que Mathf.Lerp).</summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        /// <summary>Acerca current a target como máximo maxDelta (igual que Mathf.MoveTowards).</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta) return target;
            return current + MathF.Sign(target - current) * maxDelta;
        }

        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
    }
}
