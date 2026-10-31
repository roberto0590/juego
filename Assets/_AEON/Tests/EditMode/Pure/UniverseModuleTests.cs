using System.Collections.Generic;
using System.Linq;
using GodGame.Domain;
using GodGame.Rules;
using GodGame.Simulation;
using GodGame.Universes.Resonancia;
using GodGame.Universes.Thalassa;
using NUnit.Framework;

namespace GodGame.Tests
{
    public class UniverseModuleTests
    {
        [Test]
        public void Registro_DescubreThalassaYResonancia()
        {
            // Subconjunto: un universo nuevo copiado de _Plantilla no debe romper este test.
            var ids = UniverseModuleRegistry.All.Select(m => m.RulesId).ToList();
            Assert.IsTrue(ids.Contains("resonancia"));
            Assert.IsTrue(ids.Contains("thalassa"));
            Assert.AreEqual(ids.OrderBy(id => id, System.StringComparer.Ordinal).ToList(), ids, "Ordenados por id");
            Assert.IsNotNull(UniverseModuleRegistry.Find("thalassa"));
            Assert.IsNotNull(UniverseModuleRegistry.Find("resonancia"));
            Assert.IsTrue(UniverseModuleRegistry.Find("inexistente") == null);

            var catalogIds = UniverseModuleRegistry.CreateDefaultCatalog().Select(r => r.id).ToList();
            Assert.AreEqual(ids, catalogIds);
        }

        [Test]
        public void Registro_TextosDeLosModulos()
        {
            Assert.IsTrue(UniverseModuleRegistry.TryGetText(AfinacionRule.MessageKey, out string text));
            Assert.IsFalse(string.IsNullOrEmpty(text));
            Assert.IsFalse(UniverseModuleRegistry.TryGetText("clave.inexistente", out _));
        }

        [Test]
        public void Registro_ReglasEspeciales_PorId()
        {
            List<UniverseRuleBase> soloThalassa = UniverseModuleRegistry.CreateSpecialRules(new[] { "thalassa" });
            Assert.AreEqual(1, soloThalassa.Count);
            Assert.IsTrue(soloThalassa[0] is CorrientesRule);
            Assert.AreEqual("thalassa", soloThalassa[0].RulesId);

            List<UniverseRuleBase> soloResonancia = UniverseModuleRegistry.CreateSpecialRules(new[] { "resonancia" });
            Assert.AreEqual(1, soloResonancia.Count);
            Assert.IsTrue(soloResonancia[0] is AfinacionRule);

            List<UniverseRuleBase> ambas = UniverseModuleRegistry.CreateSpecialRules(new[] { "thalassa", "resonancia", "otro" });
            Assert.AreEqual(2, ambas.Count);
            Assert.AreEqual("resonancia", ambas[0].RulesId); // orden de módulos (por id)
            Assert.AreEqual("thalassa", ambas[1].RulesId);

            Assert.AreEqual(0, UniverseModuleRegistry.CreateSpecialRules(new[] { "otro" }).Count);
        }

        [Test]
        public void Corrientes_AbaratanAFavorYNoEnContra()
        {
            var rule = new CorrientesRule();

            // Ciclo 0: la corriente va hacia los sistemas de ordinal mayor (S0 → S1 a favor).
            GameState turn0 = BuildTwoSystemState(ThalassaModule.Id, turn: 0, out God god0, out Planet a0, out Planet b0);
            Assert.AreEqual(0.5f, rule.ModifyDistanceFactor(turn0, god0, a0, b0, 1f), 1e-6f);
            Assert.AreEqual(1f, rule.ModifyDistanceFactor(turn0, god0, b0, a0, 1f), 1e-6f);

            // Ciclo 6: las corrientes cambian de dirección (S1 → S0 a favor).
            GameState turn6 = BuildTwoSystemState(ThalassaModule.Id, turn: 6, out God god6, out Planet a6, out Planet b6);
            Assert.AreEqual(1f, rule.ModifyDistanceFactor(turn6, god6, a6, b6, 1f), 1e-6f);
            Assert.AreEqual(0.5f, rule.ModifyDistanceFactor(turn6, god6, b6, a6, 1f), 1e-6f);

            // Mismo sistema: no aplica.
            Assert.AreEqual(1f, rule.ModifyDistanceFactor(turn0, god0, a0, a0, 1f), 1e-6f);
        }

        [Test]
        public void Afinacion_DaFeExtraConPlanetasAfinados()
        {
            const int seed = 42;
            // Buscar dos ids de planeta con la misma frecuencia (con 6 candidatos y 5 frecuencias, siempre hay).
            var candidates = Enumerable.Range(0, 6).Select(i => "U0-resonancia-G0-S0-P" + i).ToList();
            string first = null, second = null;
            for (int i = 0; i < candidates.Count && second == null; i++)
            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (AfinacionRule.Frequency(seed, candidates[i]) == AfinacionRule.Frequency(seed, candidates[j]))
                {
                    first = candidates[i]; second = candidates[j];
                    break;
                }
            }
            Assert.IsNotNull(second, "Debe existir una pareja con la misma frecuencia");

            var universe = new Universe { id = "U0-resonancia", name = "Resonancia", index = 0, rulesId = ResonanciaModule.Id };
            var galaxy = new Galaxy { id = "U0-resonancia-G0", universeId = universe.id };
            var system = new StarSystem { id = "U0-resonancia-G0-S0", galaxyId = galaxy.id };
            universe.galaxies.Add(galaxy);
            galaxy.systems.Add(system);
            system.planets.Add(OwnedPlanet(first, system.id, universe.id, "dios"));
            system.planets.Add(OwnedPlanet(second, system.id, universe.id, "dios"));

            var god = new God { id = "dios", name = "Dios", homeUniverseId = universe.id, homePlanetId = first };
            god.unlockedUniverseIds.Add(universe.id);
            var multiverse = new Multiverse { seed = seed, playerGodId = "otro" };
            multiverse.universes.Add(universe);
            multiverse.gods.Add(god);

            var rules = new ResonanciaModule().CreateDefaultRules();
            var state = new GameState(multiverse, new[] { rules }, 0, new UniverseRuleBase[] { new AfinacionRule() });

            float expected = 0f;
            foreach (var planet in system.planets)
                expected += 0.2f * EconomyFormulas.PlanetFaithIncome(planet, rules);
            Assert.Greater(expected, 0f);

            var report = new TurnReport(0);
            float before = god.resources.faith;
            new AfinacionRule().OnEconomyPhase(state, universe, report);

            Assert.AreEqual(before + expected, god.resources.faith, 1e-4f);
            Assert.AreEqual(1, report.Events.Count);
            Assert.AreEqual(TurnEventType.UniverseRule, report.Events[0].type);
            Assert.AreEqual("rule.afinacion", report.Events[0].messageKey);
            Assert.AreEqual("dios", report.Events[0].godId);
        }

        // ---------------- Utilidades ----------------

        private static Planet OwnedPlanet(string id, string systemId, string universeId, string ownerId)
        {
            return new Planet
            {
                id = id, name = id, systemId = systemId, universeId = universeId, habitable = true,
                ownerGodId = ownerId, defense = 10,
                civilization = new Civilization
                {
                    speciesId = "test_species", name = "Pueblo", population = 100f, techLevel = 1,
                    faith = 0.5f, stability = 0.8f, freeWill = 0.5f, worshippedGodId = ownerId
                }
            };
        }

        /// <summary>Universo con una galaxia y dos sistemas (S0 y S1), un planeta propio en cada uno.</summary>
        private static GameState BuildTwoSystemState(string rulesId, int turn, out God god, out Planet a, out Planet b)
        {
            string uid = "U0-" + rulesId;
            var universe = new Universe { id = uid, name = rulesId, index = 0, rulesId = rulesId };
            var galaxy = new Galaxy { id = uid + "-G0", universeId = uid };
            universe.galaxies.Add(galaxy);
            var s0 = new StarSystem { id = galaxy.id + "-S0", galaxyId = galaxy.id };
            var s1 = new StarSystem { id = galaxy.id + "-S1", galaxyId = galaxy.id };
            galaxy.systems.Add(s0);
            galaxy.systems.Add(s1);
            a = OwnedPlanet(s0.id + "-P0", s0.id, uid, "dios");
            b = OwnedPlanet(s1.id + "-P0", s1.id, uid, "dios");
            s0.planets.Add(a);
            s1.planets.Add(b);

            god = new God { id = "dios", name = "Dios", homeUniverseId = uid, homePlanetId = a.id };
            god.unlockedUniverseIds.Add(uid);
            var multiverse = new Multiverse { seed = 1, playerGodId = "otro" };
            multiverse.universes.Add(universe);
            multiverse.gods.Add(god);

            UniverseRulesData rules = TestRules.Make(rulesId);
            return new GameState(multiverse, new[] { rules }, turn);
        }
    }
}
