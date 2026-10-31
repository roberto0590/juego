using System.Collections.Generic;
using GodGame.Domain;
using GodGame.Rules;
using GodGame.Simulation;

namespace GodGame.Universes.Resonancia
{
    /// <summary>
    /// Regla especial "Afinación" de Resonancia (doc 03 §3.4): dos o más planetas propios con la misma
    /// frecuencia (1 a 5) dentro de un mismo sistema reciben +20 % de Fe.
    /// Implementación: en cada sistema del universo se agrupan los planetas con dueño cuya civilización
    /// adora a ese dueño, por (dueño, frecuencia). Cada grupo de 2 o más da a su dueño un extra de
    /// 0,2 × EconomyFormulas.PlanetFaithIncome por planeta. Se emite un TurnEvent "rule.afinacion"
    /// por dios beneficiado y ciclo.
    /// </summary>
    [Preserve]
    public sealed class AfinacionRule : UniverseRuleBase
    {
        public const int FrequencyCount = 5;
        public const float BonusPerPlanet = 0.2f;
        public const string MessageKey = "rule.afinacion";

        public override string RulesId => ResonanciaModule.Id;

        /// <summary>Frecuencia (1..5) del planeta, estable para una semilla de partida.</summary>
        public static int Frequency(int seed, string planetId)
            => 1 + (int)((uint)RandomExtensions.DeriveSeed(seed, "freq:" + planetId) % FrequencyCount);

        private sealed class Group
        {
            public string ownerId;
            public int frequency;
            public readonly List<Planet> planets = new List<Planet>();
        }

        public override void OnEconomyPhase(GameState state, Universe universe, TurnReport report)
        {
            UniverseRulesData rules = state.GetRules(universe.id);
            int seed = state.Multiverse.seed;

            // Orden de beneficiados = orden de aparición (determinista); el diccionario solo acumula.
            var beneficiaries = new List<string>();
            var extraByGod = new Dictionary<string, float>();

            foreach (var galaxy in universe.galaxies)
            foreach (var system in galaxy.systems)
            {
                var groups = new List<Group>();
                foreach (var planet in system.planets)
                {
                    Civilization civ = planet.civilization;
                    if (!planet.HasOwner || civ == null || !civ.Exists || civ.worshippedGodId != planet.ownerGodId) continue;

                    int frequency = Frequency(seed, planet.id);
                    Group group = groups.Find(g => g.ownerId == planet.ownerGodId && g.frequency == frequency);
                    if (group == null)
                    {
                        group = new Group { ownerId = planet.ownerGodId, frequency = frequency };
                        groups.Add(group);
                    }
                    group.planets.Add(planet);
                }

                foreach (var group in groups)
                {
                    if (group.planets.Count < 2) continue;
                    float extra = 0f;
                    foreach (var planet in group.planets)
                        extra += BonusPerPlanet * EconomyFormulas.PlanetFaithIncome(planet, rules);
                    if (extra <= 0f) continue;

                    if (!extraByGod.ContainsKey(group.ownerId))
                    {
                        extraByGod[group.ownerId] = 0f;
                        beneficiaries.Add(group.ownerId);
                    }
                    extraByGod[group.ownerId] += extra;
                }
            }

            foreach (string godId in beneficiaries)
            {
                God god = state.GetGod(godId);
                if (god == null) continue;
                god.resources.faith += extraByGod[godId];
                report.Add(new TurnEvent(TurnEventType.UniverseRule, godId, null, true, MessageKey));
            }
        }
    }
}
