using System;
using System.Collections.Generic;
using GodGame.Domain;
using UnityEngine;
using Random = System.Random;

namespace GodGame.Generation
{
    /// <summary>
    /// Galaxias espirales con brazos logarítmicos: r = a · e^(b·θ).
    /// Se generan dos cosas distintas:
    ///  1) Pocas posiciones "jugables" (sistemas solares con datos) → Generate().
    ///  2) Miles de puntos decorativos (polvo/estrellas de fondo) → GenerateSpiralPoints(),
    ///     que la vista dibuja con un ParticleSystem o con GPU instancing, sin datos de juego.
    /// </summary>
    public static class GalaxyGenerator
    {
        public static Galaxy Generate(Random rng, UniverseRules rules, string universeId, string galaxyId)
        {
            var galaxy = new Galaxy
            {
                id = galaxyId,
                name = NameGenerator.GalaxyName(rng),
                universeId = universeId,
                radius = rng.Range(80f, 140f)
            };

            int systemCount = rng.RangeInclusive(rules.systemsPerGalaxy.x, rules.systemsPerGalaxy.y);
            var positions = GenerateSpiralPoints(rng, systemCount, rules.spiralArms, galaxy.radius,
                                                 pitch: 0.25f, armSpread: 0.25f, minSeparation: galaxy.radius * 0.12f);

            for (int i = 0; i < positions.Count; i++)
            {
                string systemId = $"{galaxyId}-S{i}";
                galaxy.systems.Add(StarSystemGenerator.Generate(rng, rules, universeId, galaxyId, systemId, positions[i]));
            }
            return galaxy;
        }

        /// <summary>
        /// Puntos distribuidos en brazos espirales logarítmicos.
        /// </summary>
        /// <param name="pitch">b de la espiral: más alto = brazos más abiertos.</param>
        /// <param name="armSpread">dispersión gaussiana perpendicular al brazo (fracción del radio).</param>
        /// <param name="minSeparation">distancia mínima entre puntos (0 = sin restricción; usar solo para pocos puntos).</param>
        public static List<Vector3> GenerateSpiralPoints(Random rng, int count, int arms, float radius,
                                                         float pitch = 0.25f, float armSpread = 0.2f,
                                                         float minSeparation = 0f, float thickness = 0.04f)
        {
            arms = Mathf.Max(1, arms);
            var points = new List<Vector3>(count);
            const float a = 1f;
            // θ máximo tal que a·e^(bθ) = radius
            float thetaMax = Mathf.Log(radius / a) / pitch;
            int attempts = 0, maxAttempts = count * 30;

            while (points.Count < count && attempts++ < maxAttempts)
            {
                int arm = rng.Next(arms);
                // Sesgo hacia el centro: más densidad en el bulbo.
                float t = Mathf.Pow((float)rng.NextDouble(), 0.7f);
                float theta = t * thetaMax;
                float r = a * Mathf.Exp(pitch * theta);
                float angle = theta + arm * (2f * Mathf.PI / arms);

                Vector3 p = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                // Dispersión: mayor lejos del centro para brazos difusos.
                float spread = armSpread * radius * (0.3f + 0.7f * t);
                p.x += rng.Gaussian(0f, spread * 0.5f);
                p.z += rng.Gaussian(0f, spread * 0.5f);
                p.y = rng.Gaussian(0f, thickness * radius * (1f - t * 0.7f));

                if (minSeparation > 0f && TooClose(points, p, minSeparation)) continue;
                points.Add(p);
            }
            return points;
        }

        private static bool TooClose(List<Vector3> points, Vector3 p, float minDist)
        {
            float sq = minDist * minDist;
            for (int i = 0; i < points.Count; i++)
                if ((points[i] - p).sqrMagnitude < sq) return true;
            return false;
        }
    }
}
