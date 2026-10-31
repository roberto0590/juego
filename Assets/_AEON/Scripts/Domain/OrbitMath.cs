using System;

namespace GodGame.Domain
{
    /// <summary>
    /// Mecánica orbital simplificada, en C# puro. La usan la generación (periodo) y la vista (posición).
    /// </summary>
    public static class OrbitMath
    {
        /// <summary>1 año de juego = 4 turnos (escala ajustable).</summary>
        public const float TurnsPerYear = 4f;

        private const float DegToRad = MathF.PI / 180f;

        /// <summary>
        /// 3ª ley de Kepler: T² = a³ / M (T en años, a en UA, M en masas solares).
        /// La gravedad del universo escala la "constante" G: T ∝ 1/sqrt(G).
        /// El tiempo del universo acelera las órbitas: más velocidad del tiempo = menos turnos.
        /// </summary>
        public static float ComputePeriodTurns(float a, float starMass, UniverseRulesData rules)
        {
            float years = MathF.Sqrt(a * a * a / (MathUtil.Max(0.05f, starMass) * rules.EffectiveGravity));
            return MathUtil.Max(1f, years * TurnsPerYear / rules.timeFlowRate);
        }

        /// <summary>
        /// Posición del planeta (en UA, plano XZ inclinado) en un instante dado, resolviendo la ecuación
        /// de Kepler M = E - e·sin(E) con Newton-Raphson. Para la vista basta con 5 iteraciones.
        /// La inclinación es una rotación alrededor del eje X (equivale a Quaternion.Euler(i, 0, 0)):
        /// y = -z·sin(i), z = z·cos(i).
        /// En universos de gravedad invertida la vista puede reflejar Y para "colgar" las órbitas.
        /// </summary>
        public static Vec3 EvaluateOrbitPosition(OrbitParams orbit, float timeTurns)
        {
            float meanAnomaly = 2f * MathF.PI * (orbit.phase + timeTurns / orbit.periodTurns);
            float e = orbit.eccentricity;
            float E = meanAnomaly;
            for (int i = 0; i < 5; i++)
            {
                E -= (E - e * MathF.Sin(E) - meanAnomaly) / (1f - e * MathF.Cos(E));
            }
            float x = orbit.semiMajorAxis * (MathF.Cos(E) - e);
            float z = orbit.semiMajorAxis * MathF.Sqrt(1f - e * e) * MathF.Sin(E);

            float inc = orbit.inclinationDeg * DegToRad;
            return new Vec3(x, -z * MathF.Sin(inc), z * MathF.Cos(inc));
        }
    }
}
