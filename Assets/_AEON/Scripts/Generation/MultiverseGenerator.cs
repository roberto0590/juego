using System;
using System.Collections.Generic;
using System.Linq;
using GodGame.Domain;
using Random = System.Random;

namespace GodGame.Generation
{
    /// <summary>
    /// Genera el multiverso completo (máx. 7 universos) a partir de una semilla:
    ///  - mismo seed + mismo catálogo ⇒ mismo multiverso (reproducible para QA, soporte y guardados ligeros);
    ///  - el catálogo se ordena por id antes de barajar: el orden en que llegan los módulos no cambia el resultado;
    ///  - cada universo usa su propia sub-semilla derivada de (seed, rules.id), de modo que añadir
    ///    un universo nuevo en una actualización NO cambia los universos existentes;
    ///  - el dios nativo de cada universo viene de sus datos (nativeGod*), no de una tabla en el núcleo;
    ///  - el universo inicial del jugador se elige aleatoriamente con la semilla.
    /// </summary>
    public static class MultiverseGenerator
    {
        public const string PlayerGodId = "player";

        /// <summary>Culto con el que nace el planeta asignado del jugador (aún no adora a ningún dios del juego).</summary>
        public const string AncestorsGodId = "ancestros";

        /// <summary>Fe inicial del jugador (D14: con 20 no alcanzaba para actuar en los primeros ciclos).</summary>
        public const float PlayerStartingFaith = 30f;

        /// <param name="seed">Semilla de partida (se muestra al jugador en "Detalles de partida").</param>
        /// <param name="catalog">Reglas de los universos disponibles (los módulos presentes en el build).</param>
        /// <param name="universeCount">Número de universos a generar (1..7).</param>
        public static Multiverse Generate(int seed, IReadOnlyList<UniverseRulesData> catalog,
                                          string playerGodName = "El Octavo",
                                          int universeCount = Multiverse.MaxUniverses)
        {
            if (catalog == null || catalog.Count == 0)
                throw new ArgumentException("El catálogo de reglas de universo está vacío.", nameof(catalog));

            universeCount = Math.Max(1, Math.Min(Math.Min(universeCount, Multiverse.MaxUniverses), catalog.Count));
            var rootRng = new Random(RandomExtensions.DeriveSeed(seed, "multiverse"));

            // 1) Elegir qué universos existen en esta partida y en qué orden.
            //    Orden canónico por id (ordinal) antes de barajar: el orden de descubrimiento no influye.
            var chosen = catalog.OrderBy(r => r.id, StringComparer.Ordinal).ToList();
            rootRng.Shuffle(chosen);
            chosen = chosen.Take(universeCount).ToList();

            var multiverse = new Multiverse { seed = seed, playerGodId = PlayerGodId };

            // 2) Generar cada universo con su propia sub-semilla.
            for (int u = 0; u < chosen.Count; u++)
            {
                UniverseRulesData rules = chosen[u];
                var uRng = new Random(RandomExtensions.DeriveSeed(seed, "universe:" + rules.id));
                Universe universe = GenerateUniverse(uRng, rules, u);
                multiverse.universes.Add(universe);

                God rival = CreateRivalGod(rules, universe);
                universe.nativeGodId = rival.id;
                multiverse.gods.Add(rival);
                AssignHomePlanet(uRng, universe, rival, excludePlanetId: null, startingFaith: 0.6f);
            }

            // 3) Asignación ALEATORIA del universo inicial del jugador.
            Universe start = rootRng.Pick(multiverse.universes);
            multiverse.playerStartUniverseId = start.id;

            var player = new God
            {
                id = PlayerGodId,
                name = playerGodName,
                isPlayer = true,
                homeUniverseId = start.id,
                personality = GodPersonality.Balanced,
                resources = new ResourceWallet { faith = PlayerStartingFaith, divineEnergy = 15f, matter = 10f, knowledge = 5f }
            };
            player.unlockedUniverseIds.Add(start.id);
            multiverse.gods.Add(player);

            // El planeta asignado al jugador NO le pertenece aún: debe conquistarlo (Acto I).
            // Su civilización adora a sus ancestros con una fe de 0,6–0,8, así la conversión cultural lleva varios ciclos.
            // Fuera del sistema natal del rival: con escala Sistémica desde el ciclo 0 lo alcanzaría
            // y podría derrotar al jugador antes de que este conquiste nada.
            God localRival = multiverse.FindGod(start.nativeGodId);
            string rivalSystemId = start.galaxies.SelectMany(g => g.systems).SelectMany(s => s.planets)
                .First(p => p.id == localRival.homePlanetId).systemId;
            var playerRng = new Random(RandomExtensions.DeriveSeed(seed, "player-start"));
            Planet playerHome = AssignHomePlanet(playerRng, start, player, excludePlanetId: localRival.homePlanetId,
                                                 startingFaith: 0f, takeOwnership: false, excludeSystemId: rivalSystemId);
            playerHome.civilization.worshippedGodId = AncestorsGodId;
            playerHome.civilization.faith = playerRng.Range(0.6f, 0.8f);

            return multiverse;
        }

        private static Universe GenerateUniverse(Random rng, UniverseRulesData rules, int index)
        {
            var universe = new Universe
            {
                id = $"U{index}-{rules.id}",
                name = rules.displayNameKey,
                index = index,
                rulesId = rules.id
            };
            for (int g = 0; g < rules.galaxyCount; g++)
            {
                universe.galaxies.Add(GalaxyGenerator.Generate(rng, rules, universe.id, $"{universe.id}-G{g}"));
            }
            return universe;
        }

        private static God CreateRivalGod(UniverseRulesData rules, Universe universe)
        {
            string displayName = string.IsNullOrEmpty(rules.displayName) ? rules.id : rules.displayName;
            var god = new God
            {
                id = string.IsNullOrEmpty(rules.nativeGodId) ? "rival-" + rules.id : rules.nativeGodId,
                name = string.IsNullOrEmpty(rules.nativeGodName) ? "Dios de " + displayName : rules.nativeGodName,
                homeUniverseId = universe.id,
                personality = rules.nativeGodPersonality,
                resources = new ResourceWallet { faith = 40f, divineEnergy = 30f, matter = 30f, knowledge = 20f }
            };
            god.unlockedUniverseIds.Add(universe.id);
            return god;
        }

        /// <summary>
        /// Elige un planeta habitable con civilización como planeta natal de un dios.
        /// Con <paramref name="excludeSystemId"/>, prefiere planetas de otros sistemas (si no hay, admite ese sistema).
        /// </summary>
        private static Planet AssignHomePlanet(Random rng, Universe universe, God god, string excludePlanetId,
                                               float startingFaith, bool takeOwnership = true,
                                               string excludeSystemId = null)
        {
            var allPlanets = universe.galaxies.SelectMany(g => g.systems).SelectMany(s => s.planets).ToList();
            var candidates = allPlanets
                .Where(p => p.id != excludePlanetId && !p.HasOwner && p.habitable && p.civilization.Exists)
                .ToList();
            if (excludeSystemId != null)
            {
                var outside = candidates.Where(p => p.systemId != excludeSystemId).ToList();
                if (outside.Count > 0) candidates = outside;
            }

            Planet home;
            if (candidates.Count > 0)
            {
                home = rng.Pick(candidates);
            }
            else
            {
                // Garantía: si la generación no produjo ninguno, forzamos uno.
                home = allPlanets.FirstOrDefault(p => p.id != excludePlanetId && !p.HasOwner && p.systemId != excludeSystemId)
                       ?? allPlanets.First(p => p.id != excludePlanetId && !p.HasOwner);
                home.habitable = true;
                home.type = PlanetType.Rocoso;
                home.civilization = new Civilization
                {
                    speciesId = "generic", name = NameGenerator.CivilizationName(rng),
                    population = 50f, techLevel = 1, stability = 0.8f, freeWill = 0.5f
                };
            }

            god.homePlanetId = home.id;
            if (takeOwnership)
            {
                home.ownerGodId = god.id;
                home.civilization.worshippedGodId = god.id;
                home.civilization.faith = startingFaith;
            }
            return home;
        }
    }
}
