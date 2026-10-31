using System.Collections.Generic;
using GodGame.Domain;
using GodGame.Rules;
using GodGame.Simulation;

namespace GodGame.Universes.Thalassa
{
    /// <summary>
    /// Módulo del universo Thalassa: el espacio fluido (doc 03 §1, §2 y §3.6).
    /// El espacio está lleno de un éter líquido; las corrientes conectan los sistemas.
    /// Origen de cada valor: ver LEEME.md de esta carpeta.
    /// </summary>
    [Preserve]
    public sealed class ThalassaModule : IUniverseModule
    {
        public const string Id = "thalassa";

        public string RulesId => Id;

        public string DisplayName => "Thalassa";

        public UniverseRulesData CreateDefaultRules()
        {
            return new UniverseRulesData
            {
                id = Id,
                displayName = "Thalassa",
                displayNameKey = "universe.thalassa.name",
                designNotes = "Espacio fluido: éter líquido, corrientes entre sistemas que cambian cada 6 ciclos (doc 03 §3.6).",

                // Leyes físicas (doc 03 §2)
                gravityMultiplier = 0.6f,
                timeFlowRate = 0.7f,
                timeModel = TimeModel.Lineal,
                spatialDimensions = 3,
                lightSpeedFactor = 0.5f,
                energyType = EnergyType.Fluida,
                matterStability = 0.7f,
                mediumViscosity = 0.8f,
                beliefToReality = 0.1f,

                // Modificadores de gameplay (doc 03 §2)
                modifiers = new GameplayModifiers
                {
                    faithGain = 1.0f,
                    divineEnergyGain = 1.0f,
                    matterGain = 1.0f,
                    knowledgeGain = 1.0f,
                    miracleCost = 1.0f,
                    militaryStrength = 0.8f,
                    culturalSpread = 1.2f,
                    techProgress = 1.0f,
                    travelCost = 1.0f,
                    civilizationStability = 0.8f
                },

                // Generación (doc 03 §2: galaxias y brazos; el resto, valores por defecto del código de ejemplo)
                galaxyCount = 2,
                spiralArms = 3,
                systemsPerGalaxy = new IntRange(6, 12),
                planetsPerSystem = new IntRange(2, 7),
                spectralWeights = new[] { 0.01f, 0.03f, 0.06f, 0.12f, 0.18f, 0.25f, 0.35f },

                // Especies nativas (doc 03 §3.6)
                nativeSpeciesIds = new[] { "leviatan_estelar", "colonia_coralina", "mareante" },

                // Dios nativo (doc 03 §1; personalidad del generador de ejemplo)
                nativeGodId = "nerith",
                nativeGodName = "Nerith",
                nativeGodPersonality = new GodPersonality
                {
                    aggression = 0.4f, piety = 0.6f, curiosity = 0.4f, diplomacy = 0.9f, greed = 0.5f
                },

                addressablesLabel = "universe_thalassa"
            };
        }

        public UniversePresentationDefaults CreatePresentationDefaults()
        {
            // Paleta doc 03 §1: azul abisal y verde bioluminiscente.
            return new UniversePresentationDefaults
            {
                primaryColor = new ColorRgb(0.04f, 0.16f, 0.35f),
                secondaryColor = new ColorRgb(0.20f, 0.95f, 0.60f),
                nebulaColor = new ColorRgb(0.02f, 0.08f, 0.20f),
                atmosphereColor = new ColorRgb(0.25f, 0.90f, 0.80f),
                cityLightsColor = new ColorRgb(0.30f, 1.00f, 0.70f),
                orbitWobble = 0.1f, // doc 05: órbitas onduladas, y += sin(t) · 0,1
                biomeStops = new[]
                {
                    new GradientStop(0.00f, new ColorRgb(0.01f, 0.05f, 0.18f)), // abismo
                    new GradientStop(0.40f, new ColorRgb(0.05f, 0.30f, 0.45f)), // mar
                    new GradientStop(0.47f, new ColorRgb(0.15f, 0.80f, 0.60f)), // costa bioluminiscente
                    new GradientStop(0.60f, new ColorRgb(0.10f, 0.45f, 0.40f)), // arrecife
                    new GradientStop(0.80f, new ColorRgb(0.20f, 0.25f, 0.30f)), // roca
                    new GradientStop(1.00f, new ColorRgb(0.85f, 0.95f, 1.00f))  // cumbres
                }
            };
        }

        public IEnumerable<UniverseRuleBase> CreateSpecialRules()
        {
            return new UniverseRuleBase[] { new CorrientesRule() };
        }
    }
}
