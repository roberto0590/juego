using System;
using System.Collections.Generic;
using GodGame.Domain;
using UnityEngine;
using Random = System.Random;

namespace GodGame.Generation
{
    /// <summary>
    /// Genera sistemas solares: estrella por tipo espectral (Harvard OBAFGKM) y planetas
    /// con órbitas keplerianas simplificadas. Todo depende del Random recibido → reproducible.
    /// </summary>
    public static class StarSystemGenerator
    {
        // Datos aproximados de secuencia principal por tipo espectral.
        private struct SpectralData
        {
            public float minTemp, maxTemp, minMass, maxMass;
            public Color color;
            public SpectralData(float minT, float maxT, float minM, float maxM, Color c)
            { minTemp = minT; maxTemp = maxT; minMass = minM; maxMass = maxM; color = c; }
        }

        private static readonly SpectralData[] Spectral =
        {
            new SpectralData(30000, 45000, 16f, 40f,  new Color(0.61f, 0.69f, 1.00f)), // O azul
            new SpectralData(10000, 30000, 2.1f, 16f, new Color(0.67f, 0.75f, 1.00f)), // B azul-blanco
            new SpectralData(7500, 10000, 1.4f, 2.1f, new Color(0.79f, 0.84f, 1.00f)), // A blanco
            new SpectralData(6000, 7500, 1.04f, 1.4f, new Color(0.97f, 0.97f, 1.00f)), // F blanco-amarillo
            new SpectralData(5200, 6000, 0.8f, 1.04f, new Color(1.00f, 0.96f, 0.92f)), // G amarillo (Sol)
            new SpectralData(3700, 5200, 0.45f, 0.8f, new Color(1.00f, 0.82f, 0.63f)), // K naranja
            new SpectralData(2400, 3700, 0.08f, 0.45f, new Color(1.00f, 0.80f, 0.44f)), // M rojo
        };

        public static StarSystem Generate(Random rng, UniverseRules rules, string universeId,
                                          string galaxyId, string systemId, Vector3 galacticPosition)
        {
            var system = new StarSystem
            {
                id = systemId,
                name = NameGenerator.StarName(rng),
                galaxyId = galaxyId,
                galacticPosition = galacticPosition,
                star = GenerateStar(rng, rules)
            };

            int planetCount = rng.RangeInclusive(rules.planetsPerSystem.x, rules.planetsPerSystem.y);
            float habitableZone = Mathf.Sqrt(system.star.luminosity); // UA, aproximación clásica
            float a = rng.Range(0.25f, 0.5f) * Mathf.Max(0.5f, habitableZone);

            for (int i = 0; i < planetCount; i++)
            {
                // Espaciado tipo Titius-Bode con ruido: cada órbita ~1.4–2.0 veces la anterior.
                a *= rng.Range(1.4f, 2.0f);
                var planet = GeneratePlanet(rng, rules, system, universeId, i, a, habitableZone);
                system.planets.Add(planet);
            }
            return system;
        }

        private static Star GenerateStar(Random rng, UniverseRules rules)
        {
            int typeIndex = rng.WeightedIndex(rules.spectralWeights);
            var data = Spectral[typeIndex];
            float t = (float)rng.NextDouble();
            float mass = Mathf.Lerp(data.minMass, data.maxMass, t);
            return new Star
            {
                spectralType = (SpectralType)typeIndex,
                massSolar = mass,
                temperatureK = Mathf.Lerp(data.minTemp, data.maxTemp, t),
                radiusSolar = Mathf.Pow(mass, 0.8f),     // relación masa-radio aproximada
                luminosity = Mathf.Pow(mass, 3.5f),      // relación masa-luminosidad
                color = data.color
            };
        }

        private static Planet GeneratePlanet(Random rng, UniverseRules rules, StarSystem system,
                                             string universeId, int index, float semiMajorAxis, float hz)
        {
            float distanceRatio = semiMajorAxis / Mathf.Max(0.01f, hz);
            PlanetType type = ChooseType(rng, distanceRatio, rules);
            bool habitable = distanceRatio > 0.75f && distanceRatio < 1.6f
                             && type != PlanetType.GiganteGaseoso && type != PlanetType.Volcanico;

            var planet = new Planet
            {
                id = $"{system.id}-P{index}",
                name = $"{system.name} {ToRoman(index + 1)}",
                systemId = system.id,
                universeId = universeId,
                type = type,
                radius = type == PlanetType.GiganteGaseoso ? rng.Range(6f, 12f) : rng.Range(0.4f, 2.2f),
                terrainSeed = rng.Next(),
                habitable = habitable,
                defense = rng.RangeInclusive(5, 20),
                orbit = new OrbitParams
                {
                    semiMajorAxis = semiMajorAxis,
                    eccentricity = rng.Range(0f, 0.2f),
                    inclinationDeg = rng.Range(-4f, 4f),
                    periodTurns = ComputePeriodTurns(semiMajorAxis, system.star.massSolar, rules),
                    phase = (float)rng.NextDouble()
                }
            };

            if (habitable && rules.nativeSpeciesIds.Length > 0 && rng.Chance(0.7f))
            {
                planet.civilization = new Civilization
                {
                    speciesId = rng.Pick(rules.nativeSpeciesIds),
                    name = NameGenerator.CivilizationName(rng),
                    population = rng.Range(5f, 800f),
                    techLevel = rng.RangeInclusive(0, 4),
                    faith = 0f,
                    stability = rules.modifiers.civilizationStability,
                    freeWill = rng.Range(0.3f, 0.7f)
                };
                planet.defense += planet.civilization.techLevel * 5;
            }
            return planet;
        }

        private static PlanetType ChooseType(Random rng, float distanceRatio, UniverseRules rules)
        {
            if (rules.matterStability < 0.3f && rng.Chance(0.6f)) return PlanetType.Exotico; // Fulgor
            if (distanceRatio < 0.5f) return rng.Chance(0.6f) ? PlanetType.Volcanico : PlanetType.Desertico;
            if (distanceRatio < 1.6f)
            {
                float roll = (float)rng.NextDouble();
                return roll < 0.45f ? PlanetType.Rocoso : roll < 0.8f ? PlanetType.Oceanico : PlanetType.Desertico;
            }
            if (distanceRatio < 5f) return rng.Chance(0.6f) ? PlanetType.GiganteGaseoso : PlanetType.Helado;
            return PlanetType.Helado;
        }

        /// <summary>
        /// 3ª ley de Kepler: T² = a³ / M (T en años, a en UA, M en masas solares).
        /// La gravedad del universo escala la "constante" G: T ∝ 1/sqrt(G).
        /// El tiempo del universo acelera las órbitas: más turnos = menos.
        /// 1 año = 4 turnos (escala de juego, ajustable).
        /// </summary>
        public static float ComputePeriodTurns(float a, float starMass, UniverseRules rules)
        {
            float years = Mathf.Sqrt(a * a * a / (Mathf.Max(0.05f, starMass) * rules.EffectiveGravity));
            const float TurnsPerYear = 4f;
            return Mathf.Max(1f, years * TurnsPerYear / rules.timeFlowRate);
        }

        /// <summary>
        /// Posición del planeta (en UA, plano XZ) en un instante dado, resolviendo la ecuación
        /// de Kepler M = E - e·sin(E) con Newton-Raphson. Para la vista basta con 5 iteraciones.
        /// En universos de gravedad invertida la vista puede reflejar Y para "colgar" las órbitas.
        /// </summary>
        public static Vector3 EvaluateOrbitPosition(OrbitParams orbit, float timeTurns)
        {
            float meanAnomaly = 2f * Mathf.PI * (orbit.phase + timeTurns / orbit.periodTurns);
            float e = orbit.eccentricity;
            float E = meanAnomaly;
            for (int i = 0; i < 5; i++)
            {
                E -= (E - e * Mathf.Sin(E) - meanAnomaly) / (1f - e * Mathf.Cos(E));
            }
            float x = orbit.semiMajorAxis * (Mathf.Cos(E) - e);
            float z = orbit.semiMajorAxis * Mathf.Sqrt(1f - e * e) * Mathf.Sin(E);
            return Quaternion.Euler(orbit.inclinationDeg, 0f, 0f) * new Vector3(x, 0f, z);
        }

        private static string ToRoman(int n)
        {
            string[] r = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return n >= 1 && n <= r.Length ? r[n - 1] : n.ToString();
        }
    }

    /// <summary>Generador simple de nombres por sílabas (reproducible con el mismo Random).</summary>
    public static class NameGenerator
    {
        private static readonly string[] Start = { "Ae", "Kha", "Ve", "Ny", "Ori", "Tha", "Sel", "Ix", "Lu", "Mor", "Za", "Quo" };
        private static readonly string[] Middle = { "ra", "ri", "on", "la", "ve", "th", "ce", "dra", "ns", "ly", "xa", "mi" };
        private static readonly string[] End = { "n", "s", "ar", "is", "um", "eth", "ia", "or", "ex", "os" };

        public static string StarName(Random rng)
            => rng.Pick(Start) + rng.Pick(Middle) + rng.Pick(End);

        public static string CivilizationName(Random rng)
            => "Pueblo de " + rng.Pick(Start) + rng.Pick(End);

        public static string GalaxyName(Random rng)
            => "Cúmulo " + rng.Pick(Start) + rng.Pick(Middle) + rng.Pick(Middle) + rng.Pick(End);
    }
}
