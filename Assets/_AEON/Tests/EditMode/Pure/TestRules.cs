using System.Collections.Generic;
using System.Linq;
using GodGame.Domain;
using GodGame.Generation;
using GodGame.Rules;
using GodGame.Simulation;

namespace GodGame.Tests
{
    /// <summary>Fábricas de datos de prueba compartidas por los tests puros.</summary>
    public static class TestRules
    {
        /// <summary>Reglas pequeñas y rápidas de generar: 1 galaxia, 4–6 sistemas, 3–6 planetas, modificadores neutros.</summary>
        public static UniverseRulesData Make(string id, float gravity = 1f, float time = 1f, int dims = 3)
        {
            return new UniverseRulesData
            {
                id = id,
                displayName = id,
                displayNameKey = "universe." + id + ".name",
                gravityMultiplier = gravity,
                timeFlowRate = time,
                spatialDimensions = dims,
                modifiers = GameplayModifiers.Neutral,
                galaxyCount = 1,
                systemsPerGalaxy = new IntRange(4, 6),
                planetsPerSystem = new IntRange(3, 6),
                spiralArms = 2,
                nativeSpeciesIds = new[] { "test_species" },
                addressablesLabel = "universe_" + id
            };
        }

        /// <summary>Catálogo sintético con los 7 ids del doc 03.</summary>
        public static List<UniverseRulesData> Catalog7()
        {
            return new List<UniverseRulesData>
            {
                Make("ascendia", gravity: -1.2f),
                Make("cronalis", time: 2f),
                Make("teseracto", dims: 4),
                Make("resonancia"),
                Make("fulgor"),
                Make("thalassa", gravity: 0.6f, time: 0.7f),
                Make("oniria")
            };
        }

        /// <summary>Catálogo real: los módulos de universo presentes (Thalassa y Resonancia).</summary>
        public static List<UniverseRulesData> CatalogMvp() => UniverseModuleRegistry.CreateDefaultCatalog();

        /// <summary>Estado nuevo con las reglas especiales del registro para los universos generados.</summary>
        public static GameState NewState(int seed, IReadOnlyList<UniverseRulesData> catalog)
        {
            Multiverse multiverse = MultiverseGenerator.Generate(seed, catalog);
            var specialRules = UniverseModuleRegistry.CreateSpecialRules(multiverse.universes.Select(u => u.rulesId));
            return new GameState(multiverse, catalog, 0, specialRules);
        }

        public static God Player(GameState state) => state.GetGod(state.Multiverse.playerGodId);
    }
}
