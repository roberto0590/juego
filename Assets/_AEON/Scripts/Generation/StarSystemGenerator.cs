using System;
using GodGame.Domain;
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
            public ColorRgb color;
            public SpectralData(float minT, float maxT, float minM, float maxM, ColorRgb c)
            { minTemp = minT; maxTemp = maxT; minMass = minM; maxMass = maxM; color = c; }
        }

        private static readonly SpectralData[] Spectral =
        {
            new SpectralData(30000, 45000, 16f, 40f,  new ColorRgb(0.61f, 0.69f, 1.00f)), // O azul
            new SpectralData(10000, 30000, 2.1f, 16f, new ColorRgb(0.67f, 0.75f, 1.00f)), // B azul-blanco
            new SpectralData(7500, 10000, 1.4f, 2.1f, new ColorRgb(0.79f, 0.84f, 1.00f)), // A blanco
            new SpectralData(6000, 7500, 1.04f, 1.4f, new ColorRgb(0.97f, 0.97f, 1.00f)), // F blanco-amarillo
            new SpectralData(5200, 6000, 0.8f, 1.04f, new ColorRgb(1.00f, 0.96f, 0.92f)), // G amarillo (Sol)
            new SpectralData(3700, 5200, 0.45f, 0.8f, new ColorRgb(1.00f, 0.82f, 0.63f)), // K naranja
            new SpectralData(2400, 3700, 0.08f, 0.45f, new ColorRgb(1.00f, 0.80f, 0.44f)), // M rojo
        };

        public static StarSystem Generate(Random rng, UniverseRulesData rules, string universeId,
                                          string galaxyId, string systemId, Vec3 galacticPosition)
        {
            var system = new StarSystem
            {
                id = systemId,
                name = NameGenerator.StarName(rng),
                galaxyId = galaxyId,
                galacticPosition = galacticPosition,
                star = GenerateStar(rng, rules)
            };

            int planetCount = rng.RangeInclusive(rules.planetsPerSystem.min, rules.planetsPerSystem.max);
            float habitableZone = MathF.Sqrt(system.star.luminosity); // UA, aproximación clásica
            float a = rng.Range(0.25f, 0.5f) * MathUtil.Max(0.5f, habitableZone);

            for (int i = 0; i < planetCount; i++)
            {
                // Espaciado tipo Titius-Bode con ruido: cada órbita ~1.4–2.0 veces la anterior.
                a *= rng.Range(1.4f, 2.0f);
                var planet = GeneratePlanet(rng, rules, system, universeId, i, a, habitableZone);
                system.planets.Add(planet);
            }
            return system;
        }

        private static Star GenerateStar(Random rng, UniverseRulesData rules)
        {
            int typeIndex = rng.WeightedIndex(rules.spectralWeights);
            var data = Spectral[typeIndex];
            float t = (float)rng.NextDouble();
            float mass = MathUtil.Lerp(data.minMass, data.maxMass, t);
            return new Star
            {
                spectralType = (SpectralType)typeIndex,
                massSolar = mass,
                temperatureK = MathUtil.Lerp(data.minTemp, data.maxTemp, t),
                radiusSolar = MathF.Pow(mass, 0.8f),     // relación masa-radio aproximada
                luminosity = MathF.Pow(mass, 3.5f),      // relación masa-luminosidad
                color = data.color
            };
        }

        private static Planet GeneratePlanet(Random rng, UniverseRulesData rules, StarSystem system,
                                             string universeId, int index, float semiMajorAxis, float hz)
        {
            float distanceRatio = semiMajorAxis / MathUtil.Max(0.01f, hz);
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

        private static PlanetType ChooseType(Random rng, float distanceRatio, UniverseRulesData rules)
        {
            if (rules.matterStability < 0.3f && rng.Chance(0.6f)) return PlanetType.Exotico; // materia inestable
            if (distanceRatio < 0.5f) return rng.Chance(0.6f) ? PlanetType.Volcanico : PlanetType.Desertico;
            if (distanceRatio < 1.6f)
            {
                float roll = (float)rng.NextDouble();
                return roll < 0.45f ? PlanetType.Rocoso : roll < 0.8f ? PlanetType.Oceanico : PlanetType.Desertico;
            }
            if (distanceRatio < 5f) return rng.Chance(0.6f) ? PlanetType.GiganteGaseoso : PlanetType.Helado;
            return PlanetType.Helado;
        }

        /// <summary>Periodo orbital en turnos. Delega en OrbitMath (3ª ley de Kepler escalada por las leyes del universo).</summary>
        public static float ComputePeriodTurns(float a, float starMass, UniverseRulesData rules)
            => OrbitMath.ComputePeriodTurns(a, starMass, rules);

        /// <summary>Posición orbital en un instante. Reenvío a OrbitMath.EvaluateOrbitPosition.</summary>
        public static Vec3 EvaluateOrbitPosition(OrbitParams orbit, float timeTurns)
            => OrbitMath.EvaluateOrbitPosition(orbit, timeTurns);

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
