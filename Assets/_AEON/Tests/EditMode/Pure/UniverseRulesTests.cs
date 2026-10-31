using GodGame.Domain;
using GodGame.Rules;
using GodGame.Simulation.Commands;
using GodGame.Universes.Resonancia;
using GodGame.Universes.Thalassa;
using NUnit.Framework;

namespace GodGame.Tests
{
    public class UniverseRulesTests
    {
        private const float Eps = 1e-5f;

        [Test]
        public void Thalassa_ValoresDelDoc03()
        {
            UniverseRulesData r = new ThalassaModule().CreateDefaultRules();
            Assert.AreEqual("thalassa", r.id);
            Assert.AreEqual("Thalassa", r.displayName);
            Assert.AreEqual("universe.thalassa.name", r.displayNameKey);
            Assert.AreEqual(0.6f, r.gravityMultiplier, Eps);
            Assert.AreEqual(0.7f, r.timeFlowRate, Eps);
            Assert.AreEqual(TimeModel.Lineal, r.timeModel);
            Assert.AreEqual(3, r.spatialDimensions);
            Assert.AreEqual(0.5f, r.lightSpeedFactor, Eps);
            Assert.AreEqual(EnergyType.Fluida, r.energyType);
            Assert.AreEqual(0.7f, r.matterStability, Eps);
            Assert.AreEqual(0.8f, r.mediumViscosity, Eps);
            Assert.AreEqual(0.1f, r.beliefToReality, Eps);

            AssertModifiers(r.modifiers, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.8f, 1.2f, 1.0f, 1.0f, 0.8f);

            Assert.AreEqual(2, r.galaxyCount);
            Assert.AreEqual(3, r.spiralArms);
            Assert.AreEqual(6, r.systemsPerGalaxy.min);
            Assert.AreEqual(12, r.systemsPerGalaxy.max);
            Assert.AreEqual(2, r.planetsPerSystem.min);
            Assert.AreEqual(7, r.planetsPerSystem.max);
            AssertDefaultSpectralWeights(r.spectralWeights);
            Assert.AreEqual(new[] { "leviatan_estelar", "colonia_coralina", "mareante" }, r.nativeSpeciesIds);
            Assert.AreEqual("nerith", r.nativeGodId);
            Assert.AreEqual("Nerith", r.nativeGodName);
            AssertPersonality(r.nativeGodPersonality, 0.4f, 0.6f, 0.4f, 0.9f, 0.5f);
            Assert.AreEqual("universe_thalassa", r.addressablesLabel);

            UniversePresentationDefaults p = new ThalassaModule().CreatePresentationDefaults();
            AssertColor(p.primaryColor, 0.04f, 0.16f, 0.35f);
            AssertColor(p.secondaryColor, 0.20f, 0.95f, 0.60f);
            AssertColor(p.nebulaColor, 0.02f, 0.08f, 0.20f);
            AssertColor(p.atmosphereColor, 0.25f, 0.90f, 0.80f);
            AssertColor(p.cityLightsColor, 0.30f, 1.00f, 0.70f);
            Assert.AreEqual(0.1f, p.orbitWobble, Eps);
            Assert.AreEqual(6, p.biomeStops.Length);
            AssertStop(p.biomeStops[0], 0.00f, 0.01f, 0.05f, 0.18f);
            AssertStop(p.biomeStops[1], 0.40f, 0.05f, 0.30f, 0.45f);
            AssertStop(p.biomeStops[2], 0.47f, 0.15f, 0.80f, 0.60f);
            AssertStop(p.biomeStops[3], 0.60f, 0.10f, 0.45f, 0.40f);
            AssertStop(p.biomeStops[4], 0.80f, 0.20f, 0.25f, 0.30f);
            AssertStop(p.biomeStops[5], 1.00f, 0.85f, 0.95f, 1.00f);
        }

        [Test]
        public void Resonancia_ValoresDelDoc03()
        {
            UniverseRulesData r = new ResonanciaModule().CreateDefaultRules();
            Assert.AreEqual("resonancia", r.id);
            Assert.AreEqual("Resonancia", r.displayName);
            Assert.AreEqual("universe.resonancia.name", r.displayNameKey);
            Assert.AreEqual(1.0f, r.gravityMultiplier, Eps);
            Assert.AreEqual(1.0f, r.timeFlowRate, Eps);
            Assert.AreEqual(TimeModel.Ciclico, r.timeModel);
            Assert.AreEqual(3, r.spatialDimensions);
            Assert.AreEqual(0.2f, r.lightSpeedFactor, Eps);
            Assert.AreEqual(EnergyType.Armonica, r.energyType);
            Assert.AreEqual(1.0f, r.matterStability, Eps);
            Assert.AreEqual(0.2f, r.mediumViscosity, Eps);
            Assert.AreEqual(0.2f, r.beliefToReality, Eps);

            AssertModifiers(r.modifiers, 1.2f, 1.0f, 1.1f, 1.0f, 0.9f, 0.8f, 1.6f, 1.0f, 1.0f, 0.9f);

            Assert.AreEqual(2, r.galaxyCount);
            Assert.AreEqual(5, r.spiralArms);
            Assert.AreEqual(6, r.systemsPerGalaxy.min);
            Assert.AreEqual(12, r.systemsPerGalaxy.max);
            Assert.AreEqual(2, r.planetsPerSystem.min);
            Assert.AreEqual(7, r.planetsPerSystem.max);
            AssertDefaultSpectralWeights(r.spectralWeights);
            Assert.AreEqual(new[] { "cantor_cuarzo", "coro_siliceo" }, r.nativeSpeciesIds);
            Assert.AreEqual("lyrael", r.nativeGodId);
            Assert.AreEqual("Lyrael", r.nativeGodName);
            AssertPersonality(r.nativeGodPersonality, 0.2f, 0.9f, 0.5f, 0.8f, 0.3f);
            Assert.AreEqual("universe_resonancia", r.addressablesLabel);

            UniversePresentationDefaults p = new ResonanciaModule().CreatePresentationDefaults();
            AssertColor(p.primaryColor, 0.70f, 0.55f, 0.90f);
            AssertColor(p.secondaryColor, 0.97f, 0.75f, 0.80f);
            AssertColor(p.nebulaColor, 0.35f, 0.20f, 0.50f);
            AssertColor(p.atmosphereColor, 0.85f, 0.75f, 1.00f);
            AssertColor(p.cityLightsColor, 1.00f, 0.85f, 0.95f);
            Assert.AreEqual(0.0f, p.orbitWobble, Eps);
            Assert.AreEqual(6, p.biomeStops.Length);
            AssertStop(p.biomeStops[0], 0.00f, 0.18f, 0.10f, 0.30f);
            AssertStop(p.biomeStops[1], 0.40f, 0.45f, 0.30f, 0.65f);
            AssertStop(p.biomeStops[2], 0.47f, 0.95f, 0.70f, 0.80f);
            AssertStop(p.biomeStops[3], 0.60f, 0.75f, 0.60f, 0.90f);
            AssertStop(p.biomeStops[4], 0.80f, 0.80f, 0.82f, 0.88f);
            AssertStop(p.biomeStops[5], 1.00f, 1.00f, 1.00f, 1.00f);
        }

        [Test]
        public void FactorDeViaje_Thalassa()
        {
            Assert.AreEqual(2.5456f, new ThalassaModule().CreateDefaultRules().ComputeTravelCostFactor(), 1e-3f);
        }

        [Test]
        public void FactorDeViaje_Resonancia()
        {
            Assert.AreEqual(2.6833f, new ResonanciaModule().CreateDefaultRules().ComputeTravelCostFactor(), 1e-3f);
        }

        [Test]
        public void CosteMilagro_EscalaConMiracleCost()
        {
            ResourceCost cost = CommandCosts.Miracle(MiracleType.Bendicion, new ResonanciaModule().CreateDefaultRules());
            Assert.AreEqual(4.5f, cost.faith, 1e-4f);
            Assert.AreEqual(7.2f, cost.divineEnergy, 1e-4f);
            Assert.AreEqual(0f, cost.matter, Eps);
            Assert.AreEqual(0f, cost.knowledge, Eps);
        }

        [Test]
        public void PeriodoOrbital_EscalaConGravedadYTiempo()
        {
            const float a = 5f, mass = 1f;
            float normal = OrbitMath.ComputePeriodTurns(a, mass, TestRules.Make("normal", gravity: 1f, time: 1f));
            float lowGravity = OrbitMath.ComputePeriodTurns(a, mass, TestRules.Make("baja", gravity: 0.5f, time: 1f));
            float slowTime = OrbitMath.ComputePeriodTurns(a, mass, TestRules.Make("lento", gravity: 1f, time: 0.5f));

            Assert.Greater(lowGravity, normal, "Menos gravedad ⇒ más turnos por órbita");
            Assert.Greater(slowTime, normal, "Tiempo más lento ⇒ más turnos por órbita");
        }

        [Test]
        public void Clone_EsCopiaProfunda()
        {
            UniverseRulesData original = new ThalassaModule().CreateDefaultRules();
            UniverseRulesData copy = original.Clone();

            copy.spectralWeights[0] = 99f;
            copy.nativeSpeciesIds[0] = "otra";
            copy.modifiers.faithGain = 5f;
            copy.gravityMultiplier = 2f;

            Assert.AreEqual(0.01f, original.spectralWeights[0], Eps);
            Assert.AreEqual("leviatan_estelar", original.nativeSpeciesIds[0]);
            Assert.AreEqual(1.0f, original.modifiers.faithGain, Eps);
            Assert.AreEqual(0.6f, original.gravityMultiplier, Eps);
        }

        [Test]
        public void Sanitize_RecortaValoresFueraDeRango()
        {
            var r = new UniverseRulesData
            {
                gravityMultiplier = 10f,
                timeFlowRate = 0f,
                spatialDimensions = 9,
                lightSpeedFactor = 0f,
                matterStability = 2f,
                mediumViscosity = -1f,
                beliefToReality = 3f,
                galaxyCount = 0,
                spiralArms = 12,
                systemsPerGalaxy = new IntRange(8, 3),
                planetsPerSystem = new IntRange(5, 1),
                spectralWeights = new[] { 1f, 2f, 3f },
                nativeSpeciesIds = null
            };
            r.modifiers.miracleCost = -2f;
            r.modifiers.civilizationStability = 4f;

            r.Sanitize();

            Assert.AreEqual(3f, r.gravityMultiplier, Eps);
            Assert.AreEqual(0.1f, r.timeFlowRate, Eps);
            Assert.AreEqual(5, r.spatialDimensions);
            Assert.AreEqual(0.05f, r.lightSpeedFactor, Eps);
            Assert.AreEqual(1f, r.matterStability, Eps);
            Assert.AreEqual(0f, r.mediumViscosity, Eps);
            Assert.AreEqual(1f, r.beliefToReality, Eps);
            Assert.AreEqual(1, r.galaxyCount);
            Assert.AreEqual(6, r.spiralArms);
            Assert.AreEqual(8, r.systemsPerGalaxy.min);
            Assert.AreEqual(8, r.systemsPerGalaxy.max);
            Assert.AreEqual(5, r.planetsPerSystem.min);
            Assert.AreEqual(5, r.planetsPerSystem.max);
            Assert.AreEqual(7, r.spectralWeights.Length);
            Assert.AreEqual(3f, r.spectralWeights[2], Eps);
            Assert.IsNotNull(r.nativeSpeciesIds);
            Assert.AreEqual(0f, r.modifiers.miracleCost, Eps);
            Assert.AreEqual(1f, r.modifiers.civilizationStability, Eps);
        }

        // ---------------- Utilidades ----------------

        private static void AssertModifiers(GameplayModifiers m, float faith, float energy, float matter, float knowledge,
                                            float miracle, float military, float cultural, float tech, float travel, float stability)
        {
            Assert.AreEqual(faith, m.faithGain, Eps);
            Assert.AreEqual(energy, m.divineEnergyGain, Eps);
            Assert.AreEqual(matter, m.matterGain, Eps);
            Assert.AreEqual(knowledge, m.knowledgeGain, Eps);
            Assert.AreEqual(miracle, m.miracleCost, Eps);
            Assert.AreEqual(military, m.militaryStrength, Eps);
            Assert.AreEqual(cultural, m.culturalSpread, Eps);
            Assert.AreEqual(tech, m.techProgress, Eps);
            Assert.AreEqual(travel, m.travelCost, Eps);
            Assert.AreEqual(stability, m.civilizationStability, Eps);
        }

        private static void AssertPersonality(GodPersonality p, float aggression, float piety, float curiosity, float diplomacy, float greed)
        {
            Assert.AreEqual(aggression, p.aggression, Eps);
            Assert.AreEqual(piety, p.piety, Eps);
            Assert.AreEqual(curiosity, p.curiosity, Eps);
            Assert.AreEqual(diplomacy, p.diplomacy, Eps);
            Assert.AreEqual(greed, p.greed, Eps);
        }

        private static void AssertDefaultSpectralWeights(float[] w)
        {
            float[] expected = { 0.01f, 0.03f, 0.06f, 0.12f, 0.18f, 0.25f, 0.35f };
            Assert.AreEqual(expected.Length, w.Length);
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], w[i], Eps);
        }

        private static void AssertColor(ColorRgb c, float r, float g, float b)
        {
            Assert.AreEqual(r, c.r, Eps);
            Assert.AreEqual(g, c.g, Eps);
            Assert.AreEqual(b, c.b, Eps);
        }

        private static void AssertStop(GradientStop stop, float time, float r, float g, float b)
        {
            Assert.AreEqual(time, stop.time, Eps);
            AssertColor(stop.color, r, g, b);
        }
    }
}
