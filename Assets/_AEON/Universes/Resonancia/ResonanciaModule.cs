using System.Collections.Generic;
using GodGame.Domain;
using GodGame.Rules;
using GodGame.Simulation;

namespace GodGame.Universes.Resonancia
{
    /// <summary>
    /// Módulo del universo Resonancia: cristal y sonido (doc 03 §1, §2 y §3.4).
    /// La luz es lenta, el sonido viaja por el vacío y toda la materia es cristalina y vibra.
    /// Origen de cada valor: ver LEEME.md de esta carpeta.
    /// </summary>
    [Preserve]
    public sealed class ResonanciaModule : IUniverseModule, IUniverseTexts
    {
        public const string Id = "resonancia";

        public string RulesId => Id;

        public string DisplayName => "Resonancia";

        public UniverseRulesData CreateDefaultRules()
        {
            return new UniverseRulesData
            {
                id = Id,
                displayName = "Resonancia",
                displayNameKey = "universe.resonancia.name",
                designNotes = "Cristal y sonido: luz lenta, planetas con frecuencia 1–5; la Afinación da +20 % de Fe (doc 03 §3.4).",

                // Leyes físicas (doc 03 §2)
                gravityMultiplier = 1.0f,
                timeFlowRate = 1.0f,
                timeModel = TimeModel.Ciclico,
                spatialDimensions = 3,
                lightSpeedFactor = 0.2f,
                energyType = EnergyType.Armonica,
                matterStability = 1.0f,
                mediumViscosity = 0.2f,
                beliefToReality = 0.2f,

                // Modificadores de gameplay (doc 03 §2)
                modifiers = new GameplayModifiers
                {
                    faithGain = 1.2f,
                    divineEnergyGain = 1.0f,
                    matterGain = 1.1f,
                    knowledgeGain = 1.0f,
                    miracleCost = 0.9f,
                    militaryStrength = 0.8f,
                    culturalSpread = 1.6f,
                    techProgress = 1.0f,
                    travelCost = 1.0f,
                    civilizationStability = 0.9f
                },

                // Generación (doc 03 §2: galaxias y brazos; el resto, valores por defecto del código de ejemplo)
                galaxyCount = 2,
                spiralArms = 5,
                systemsPerGalaxy = new IntRange(6, 12),
                planetsPerSystem = new IntRange(2, 7),
                spectralWeights = new[] { 0.01f, 0.03f, 0.06f, 0.12f, 0.18f, 0.25f, 0.35f },

                // Especies nativas (doc 03 §3.4)
                nativeSpeciesIds = new[] { "cantor_cuarzo", "coro_siliceo" },

                // Dios nativo (doc 03 §1; personalidad del generador de ejemplo)
                nativeGodId = "lyrael",
                nativeGodName = "Lyrael",
                nativeGodPersonality = new GodPersonality
                {
                    aggression = 0.2f, piety = 0.9f, curiosity = 0.5f, diplomacy = 0.8f, greed = 0.3f
                },

                addressablesLabel = "universe_resonancia"
            };
        }

        public UniversePresentationDefaults CreatePresentationDefaults()
        {
            // Paleta doc 03 §1: lila, rosa cuarzo y plata.
            return new UniversePresentationDefaults
            {
                primaryColor = new ColorRgb(0.70f, 0.55f, 0.90f),
                secondaryColor = new ColorRgb(0.97f, 0.75f, 0.80f),
                nebulaColor = new ColorRgb(0.35f, 0.20f, 0.50f),
                atmosphereColor = new ColorRgb(0.85f, 0.75f, 1.00f),
                cityLightsColor = new ColorRgb(1.00f, 0.85f, 0.95f),
                orbitWobble = 0.0f,
                biomeStops = new[]
                {
                    new GradientStop(0.00f, new ColorRgb(0.18f, 0.10f, 0.30f)), // fondo violeta
                    new GradientStop(0.40f, new ColorRgb(0.45f, 0.30f, 0.65f)), // mar lila
                    new GradientStop(0.47f, new ColorRgb(0.95f, 0.70f, 0.80f)), // orilla de cuarzo rosa
                    new GradientStop(0.60f, new ColorRgb(0.75f, 0.60f, 0.90f)), // llanuras cristalinas
                    new GradientStop(0.80f, new ColorRgb(0.80f, 0.82f, 0.88f)), // agujas de plata
                    new GradientStop(1.00f, new ColorRgb(1.00f, 1.00f, 1.00f))  // cumbres
                }
            };
        }

        public IEnumerable<UniverseRuleBase> CreateSpecialRules()
        {
            return new UniverseRuleBase[] { new AfinacionRule() };
        }

        /// <summary>Texto de la Crónica para el evento de la Afinación.</summary>
        public bool TryGetText(string key, out string text)
        {
            text = key == AfinacionRule.MessageKey ? "sus planetas afinados resuenan y generan Fe extra" : null;
            return text != null;
        }
    }
}
