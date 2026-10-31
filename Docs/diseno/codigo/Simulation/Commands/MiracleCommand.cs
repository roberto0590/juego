using GodGame.Domain;
using UnityEngine;

namespace GodGame.Simulation.Commands
{
    /// <summary>
    /// Milagro sobre un planeta. Los efectos se escalan con las leyes del universo:
    /// p. ej. en Oniria (beliefToReality alto) la Bendición y el Profeta son más potentes.
    /// La presentación (VFX, cámara, audio) escucha el TurnEvent resultante; el comando no sabe nada de ella.
    /// </summary>
    public sealed class MiracleCommand : ICommand
    {
        public string GodId { get; }
        public string PlanetId { get; }
        public MiracleType Type { get; }
        public int Priority => 0;

        public MiracleCommand(string godId, string planetId, MiracleType type)
        {
            GodId = godId; PlanetId = planetId; Type = type;
        }

        public bool Validate(GameState state, out string reason)
        {
            God god = state.GetGod(GodId);
            Planet planet = state.GetPlanet(PlanetId);
            if (god == null || god.defeated) { reason = "god.invalid"; return false; }
            if (planet == null) { reason = "planet.invalid"; return false; }

            bool ownPlanet = planet.ownerGodId == GodId;
            bool needsOwn = Type == MiracleType.Bendicion || Type == MiracleType.Revelacion;
            if (needsOwn && !ownPlanet) { reason = "miracle.requires_own_planet"; return false; }
            if (!needsOwn && ownPlanet) { reason = "miracle.requires_foreign_planet"; return false; }
            if (!planet.civilization.Exists && Type != MiracleType.Cataclismo)
            { reason = "planet.no_civilization"; return false; }
            if (!ownPlanet && !state.CanReach(god, planet, out _)) { reason = "planet.unreachable"; return false; }

            var cost = CommandCosts.Miracle(Type, state.GetRulesForPlanet(planet));
            if (!god.resources.CanAfford(cost)) { reason = "resources.insufficient"; return false; }

            reason = null;
            return true;
        }

        public void Execute(GameState state, TurnReport report)
        {
            God god = state.GetGod(GodId);
            Planet planet = state.GetPlanet(PlanetId);
            UniverseRules rules = state.GetRulesForPlanet(planet);
            god.resources.TrySpend(CommandCosts.Miracle(Type, rules));

            Civilization civ = planet.civilization;
            float beliefBoost = 1f + rules.beliefToReality; // Oniria: hasta ×2

            switch (Type)
            {
                case MiracleType.Bendicion:
                    civ.faith = Mathf.Clamp01(civ.faith + 0.15f * beliefBoost);
                    civ.stability = Mathf.Clamp01(civ.stability + 0.1f);
                    break;
                case MiracleType.Profeta:
                    // Si adoran a otro dios, su fe disminuye: prepara la conquista cultural.
                    if (civ.worshippedGodId != GodId)
                        civ.faith = Mathf.Clamp01(civ.faith - 0.12f * beliefBoost);
                    break;
                case MiracleType.Revelacion:
                    civ.techLevel = Mathf.Min(10, civ.techLevel + 1);
                    civ.freeWill = Mathf.Clamp01(civ.freeWill + 0.05f);
                    break;
                case MiracleType.Cataclismo:
                    planet.defense = Mathf.Max(1, planet.defense - 8);
                    if (civ.Exists)
                    {
                        civ.population *= 0.85f;
                        civ.freeWill = Mathf.Clamp01(civ.freeWill - 0.1f); // el miedo no es fe libre
                        civ.stability = Mathf.Clamp01(civ.stability - 0.15f);
                    }
                    break;
            }

            report.Add(new TurnEvent(TurnEventType.Miracle, GodId, PlanetId, true,
                $"miracle.{Type.ToString().ToLowerInvariant()}"));
        }
    }
}
