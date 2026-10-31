using System.Collections.Generic;
using System.Linq;
using System.Text;
using GodGame.Domain;
using GodGame.Generation;
using GodGame.Save;
using GodGame.Universes.Resonancia;
using GodGame.Universes.Thalassa;
using NUnit.Framework;

namespace GodGame.Tests
{
    public class MultiverseGeneratorTests
    {
        [Test]
        public void MismaSemilla_MismoMultiverso()
        {
            var catalog = TestRules.CatalogMvp();
            string a = SaveJson.ToJson(MultiverseGenerator.Generate(1234, catalog));
            string b = SaveJson.ToJson(MultiverseGenerator.Generate(1234, catalog));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void SemillaDistinta_MultiversoDistinto()
        {
            var catalog = TestRules.CatalogMvp();
            string a = SaveJson.ToJson(MultiverseGenerator.Generate(1, catalog));
            string b = SaveJson.ToJson(MultiverseGenerator.Generate(2, catalog));
            Assert.IsFalse(a == b, "Dos semillas distintas no deberían dar el mismo multiverso");
        }

        [Test]
        public void NuncaMasDeSieteUniversos()
        {
            var catalog = TestRules.Catalog7();
            catalog.Add(TestRules.Make("extra"));
            Multiverse multiverse = MultiverseGenerator.Generate(7, catalog, universeCount: 10);
            Assert.LessOrEqual(multiverse.universes.Count, Multiverse.MaxUniverses);
            Assert.AreEqual(Multiverse.MaxUniverses, multiverse.universes.Count);
        }

        [Test]
        public void UniversoInicial_EsAleatorioEntreLosDos()
        {
            // Catálogo fijo con los dos módulos: no depende de cuántos universos descubra el registro.
            var catalog = new List<UniverseRulesData>
            {
                new ThalassaModule().CreateDefaultRules(),
                new ResonanciaModule().CreateDefaultRules()
            };
            foreach (var rules in catalog) rules.Sanitize();
            int thalassa = 0, resonancia = 0;
            const int samples = 400;
            for (int seed = 0; seed < samples; seed++)
            {
                Multiverse multiverse = MultiverseGenerator.Generate(seed, catalog);
                string rulesId = multiverse.FindUniverse(multiverse.playerStartUniverseId).rulesId;
                if (rulesId == "thalassa") thalassa++;
                else if (rulesId == "resonancia") resonancia++;
            }
            Assert.AreEqual(samples, thalassa + resonancia);
            Assert.Greater(thalassa, samples * 0.3f, "Thalassa debería salir más del 30 % de las veces");
            Assert.Greater(resonancia, samples * 0.3f, "Resonancia debería salir más del 30 % de las veces");
        }

        [Test]
        public void ElJugadorTienePlanetaAsignadoSinDueño()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                Multiverse multiverse = MultiverseGenerator.Generate(seed, TestRules.CatalogMvp());
                God player = multiverse.FindGod(multiverse.playerGodId);
                Planet home = multiverse.AllPlanets().First(p => p.id == player.homePlanetId);

                Assert.IsTrue(home.civilization.Exists, "El planeta asignado debe tener civilización");
                Assert.IsFalse(home.HasOwner, "El planeta asignado no pertenece aún al jugador");
                Assert.AreEqual(MultiverseGenerator.AncestorsGodId, home.civilization.worshippedGodId);
                Assert.LessOrEqual(0.6f, home.civilization.faith);
                Assert.LessOrEqual(home.civilization.faith, 0.8f);
                Assert.AreEqual(player.homeUniverseId, home.universeId);
                Assert.AreEqual(MultiverseGenerator.PlayerStartingFaith, player.resources.faith);
            }
        }

        [Test]
        public void PlanetaDelJugador_FueraDelSistemaDelRival()
        {
            var catalog = TestRules.CatalogMvp();
            for (int seed = 0; seed < 100; seed++)
            {
                Multiverse multiverse = MultiverseGenerator.Generate(seed, catalog);
                God player = multiverse.FindGod(multiverse.playerGodId);
                Universe start = multiverse.FindUniverse(multiverse.playerStartUniverseId);
                God rival = multiverse.FindGod(start.nativeGodId);
                Planet playerHome = multiverse.AllPlanets().First(p => p.id == player.homePlanetId);
                Planet rivalHome = multiverse.AllPlanets().First(p => p.id == rival.homePlanetId);
                Assert.AreNotEqual(rivalHome.systemId, playerHome.systemId, "Semilla " + seed);
            }
        }

        [Test]
        public void AñadirUnUniverso_NoCambiaLosExistentes()
        {
            var seven = TestRules.Catalog7();
            var six = seven.Take(6).ToList();
            Multiverse withSix = MultiverseGenerator.Generate(99, six, universeCount: 6);
            Multiverse withSeven = MultiverseGenerator.Generate(99, seven, universeCount: 7);

            Assert.AreEqual(6, withSix.universes.Count);
            foreach (Universe universe in withSix.universes)
            {
                Universe same = withSeven.universes.FirstOrDefault(u => u.rulesId == universe.rulesId);
                Assert.IsNotNull(same, "El universo " + universe.rulesId + " debe seguir existiendo");
                Assert.AreEqual(Fingerprint(universe), Fingerprint(same), "Contenido distinto en " + universe.rulesId);
            }
        }

        [Test]
        public void OrdenDelCatalogo_NoAfectaAlResultado()
        {
            var catalog = TestRules.Catalog7();
            var reversed = Enumerable.Reverse(catalog).ToList();
            string a = SaveJson.ToJson(MultiverseGenerator.Generate(5, catalog));
            string b = SaveJson.ToJson(MultiverseGenerator.Generate(5, reversed));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void DiosNativo_VieneDelModulo()
        {
            Multiverse multiverse = MultiverseGenerator.Generate(3, TestRules.CatalogMvp());
            Assert.IsNotNull(multiverse.FindGod("nerith"));
            Assert.IsNotNull(multiverse.FindGod("lyrael"));
            Assert.AreEqual("Nerith", multiverse.FindGod("nerith").name);
            Assert.AreEqual("Lyrael", multiverse.FindGod("lyrael").name);
        }

        /// <summary>
        /// Huella del contenido generado de un universo sin depender de sus ids
        /// (los ids incluyen el índice del universo, que cambia al añadir otro).
        /// </summary>
        private static string Fingerprint(Universe universe)
        {
            var sb = new StringBuilder();
            foreach (var galaxy in universe.galaxies)
            {
                sb.Append(galaxy.name).Append('|').Append(galaxy.radius).Append(';');
                foreach (var system in galaxy.systems)
                {
                    sb.Append(system.name).Append('|').Append(system.star.spectralType).Append('|').Append(system.star.massSolar).Append(';');
                    foreach (var planet in system.planets)
                    {
                        sb.Append(planet.name).Append('|').Append(planet.type).Append('|').Append(planet.terrainSeed)
                          .Append('|').Append(planet.civilization.speciesId).Append(';');
                    }
                }
            }
            return sb.ToString();
        }
    }
}
