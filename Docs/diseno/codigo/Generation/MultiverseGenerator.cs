using System;
using System.Collections.Generic;
using System.Linq;
using GodGame.Domain;
using Random = System.Random;

namespace GodGame.Generation
{
    /// <summary>Definición de un dios rival del lore (ver 02_trama_y_lore.md).</summary>
    public readonly struct RivalGodDefinition
    {
        public readonly string id;
        public readonly string name;
        public readonly string universeRulesId;
        public readonly GodPersonality personality;

        public RivalGodDefinition(string id, string name, string universeRulesId,
                                  float aggression, float piety, float curiosity, float diplomacy, float greed)
        {
            this.id = id; this.name = name; this.universeRulesId = universeRulesId;
            personality = new GodPersonality
            {
                aggression = aggression, piety = piety, curiosity = curiosity, diplomacy = diplomacy, greed = greed
            };
        }
    }

    /// <summary>
    /// Genera el multiverso completo (máx. 7 universos) a partir de una semilla:
    ///  - mismo seed + mismo catálogo ⇒ mismo multiverso (reproducible para QA, soporte y guardados ligeros);
    ///  - cada universo usa su propia sub-semilla derivada de (seed, rules.id), de modo que añadir
    ///    un universo nuevo en una actualización NO cambia los universos existentes;
    ///  - el universo inicial del jugador se elige aleatoriamente con la semilla.
    /// </summary>
    public static class MultiverseGenerator
    {
        public static readonly RivalGodDefinition[] DefaultRivals =
        {
            //                       id          nombre        universo     agr  pie  cur  dip  avar
            new RivalGodDefinition("velascar", "Vel'Ascar", "ascendia",   0.8f,0.3f,0.4f,0.2f,0.5f),
            new RivalGodDefinition("aeterna",  "Aeterna",   "cronalis",   0.3f,0.5f,0.8f,0.6f,0.6f),
            new RivalGodDefinition("ixxion",   "Ixxion",    "teseracto",  0.4f,0.2f,1.0f,0.3f,0.4f),
            new RivalGodDefinition("lyrael",   "Lyrael",    "resonancia", 0.2f,0.9f,0.5f,0.8f,0.3f),
            new RivalGodDefinition("pyrrhos",  "Pyrrhos",   "fulgor",     1.0f,0.4f,0.3f,0.1f,0.2f),
            new RivalGodDefinition("nerith",   "Nerith",    "thalassa",   0.4f,0.6f,0.4f,0.9f,0.5f),
            new RivalGodDefinition("somnael",  "Somnael",   "oniria",     0.5f,0.8f,0.6f,0.5f,0.2f),
        };

        public const string PlayerGodId = "player";

        /// <param name="seed">Semilla de partida (se muestra al jugador en "Detalles de partida").</param>
        /// <param name="catalog">UniverseRules disponibles (solo los descargados/desbloqueados en el MVP).</param>
        /// <param name="universeCount">Número de universos a generar (1..7).</param>
        public static Multiverse Generate(int seed, IReadOnlyList<UniverseRules> catalog,
                                          string playerGodName = "El Octavo",
                                          int universeCount = Multiverse.MaxUniverses)
        {
            if (catalog == null || catalog.Count == 0)
                throw new ArgumentException("El catálogo de UniverseRules está vacío.", nameof(catalog));

            universeCount = Math.Max(1, Math.Min(Math.Min(universeCount, Multiverse.MaxUniverses), catalog.Count));
            var rootRng = new Random(RandomExtensions.DeriveSeed(seed, "multiverse"));

            // 1) Elegir qué universos existen en esta partida y en qué orden.
            var chosen = catalog.ToList();
            rootRng.Shuffle(chosen);
            chosen = chosen.Take(universeCount).ToList();

            var multiverse = new Multiverse { seed = seed, playerGodId = PlayerGodId };

            // 2) Generar cada universo con su propia sub-semilla.
            for (int u = 0; u < chosen.Count; u++)
            {
                UniverseRules rules = chosen[u];
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
                resources = new ResourceWallet { faith = 20f, divineEnergy = 15f, matter = 10f, knowledge = 5f }
            };
            player.unlockedUniverseIds.Add(start.id);
            multiverse.gods.Add(player);

            // El planeta asignado al jugador NO le pertenece aún: debe conquistarlo (Acto I).
            God localRival = multiverse.FindGod(start.nativeGodId);
            var playerRng = new Random(RandomExtensions.DeriveSeed(seed, "player-start"));
            AssignHomePlanet(playerRng, start, player, excludePlanetId: localRival.homePlanetId, startingFaith: 0f,
                             takeOwnership: false);

            return multiverse;
        }

        private static Universe GenerateUniverse(Random rng, UniverseRules rules, int index)
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

        private static God CreateRivalGod(UniverseRules rules, Universe universe)
        {
            RivalGodDefinition def = DefaultRivals.FirstOrDefault(d => d.universeRulesId == rules.id);
            bool found = !string.IsNullOrEmpty(def.id);

            var god = new God
            {
                id = found ? def.id : "rival-" + rules.id,
                name = found ? def.name : "Dios de " + rules.id,
                homeUniverseId = universe.id,
                personality = found ? def.personality : GodPersonality.Balanced,
                resources = new ResourceWallet { faith = 40f, divineEnergy = 30f, matter = 30f, knowledge = 20f }
            };
            god.unlockedUniverseIds.Add(universe.id);
            return god;
        }

        /// <summary>Elige un planeta habitable con civilización como planeta natal de un dios.</summary>
        private static void AssignHomePlanet(Random rng, Universe universe, God god, string excludePlanetId,
                                             float startingFaith, bool takeOwnership = true)
        {
            var candidates = universe.galaxies
                .SelectMany(g => g.systems)
                .SelectMany(s => s.planets)
                .Where(p => p.id != excludePlanetId && !p.HasOwner && p.habitable && p.civilization.Exists)
                .ToList();

            Planet home;
            if (candidates.Count > 0)
            {
                home = rng.Pick(candidates);
            }
            else
            {
                // Garantía: si la generación no produjo ninguno, forzamos uno.
                home = universe.galaxies.SelectMany(g => g.systems).SelectMany(s => s.planets)
                    .First(p => p.id != excludePlanetId && !p.HasOwner);
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
        }
    }
}
