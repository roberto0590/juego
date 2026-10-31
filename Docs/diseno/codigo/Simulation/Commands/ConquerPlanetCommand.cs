using GodGame.Domain;
using UnityEngine;

namespace GodGame.Simulation.Commands
{
    /// <summary>
    /// Conquista de un planeta por una de las 3 vías:
    ///  - Militar: avatares/ejércitos divinos contra la defensa del planeta. Rápida, baja la estabilidad.
    ///  - Cultural/religiosa: profetas y templos reducen la fe hacia el dios actual; al bajar del umbral,
    ///    el planeta se convierte. Lenta (varios turnos), sin daño.
    ///  - Tecnológica: regalar conocimiento ("elevación"); la civilización adopta al dios. Sube el libre albedrío.
    /// </summary>
    public sealed class ConquerPlanetCommand : ICommand
    {
        public const float CulturalConversionThreshold = 0.2f;

        public string GodId { get; }
        public string PlanetId { get; }
        public ConquestType Type { get; }
        public int Priority => 10;

        public ConquerPlanetCommand(string godId, string planetId, ConquestType type)
        {
            GodId = godId; PlanetId = planetId; Type = type;
        }

        public bool Validate(GameState state, out string reason)
        {
            God god = state.GetGod(GodId);
            Planet planet = state.GetPlanet(PlanetId);
            if (god == null || god.defeated) { reason = "god.invalid"; return false; }
            if (planet == null) { reason = "planet.invalid"; return false; }
            if (planet.ownerGodId == GodId) { reason = "planet.already_owned"; return false; }
            if (Type != ConquestType.Militar && !planet.civilization.Exists)
            { reason = "planet.no_civilization"; return false; }
            if (!state.CanReach(god, planet, out float distance)) { reason = "planet.unreachable"; return false; }

            var cost = CommandCosts.Conquest(Type, planet, state.GetRulesForPlanet(planet), distance);
            if (!god.resources.CanAfford(cost)) { reason = "resources.insufficient"; return false; }

            reason = null;
            return true;
        }

        /// <summary>Estimación de éxito sin tirar dados (la usa la IA y la UI para mostrar "% de éxito").</summary>
        public static float EstimateSuccessChance(GameState state, God god, Planet planet, ConquestType type)
        {
            var mods = state.GetRulesForPlanet(planet).modifiers;
            switch (type)
            {
                case ConquestType.Militar:
                    float attack = MilitaryPower(state, god) * mods.militaryStrength;
                    // Probabilidad suave: 50 % cuando ataque = defensa.
                    return Mathf.Clamp01(0.5f + (attack - planet.defense) / (2f * Mathf.Max(1f, planet.defense)));
                case ConquestType.Cultural:
                    float spread = CulturalSpread(planet, mods);
                    float currentFaith = planet.civilization.worshippedGodId == god.id ? 0f : planet.civilization.faith;
                    return currentFaith - spread <= CulturalConversionThreshold ? 0.9f : 0.3f; // 0.3 = progreso parcial
                default:
                    return Mathf.Clamp01(0.4f * mods.techProgress + 0.06f * planet.civilization.techLevel);
            }
        }

        public void Execute(GameState state, TurnReport report)
        {
            God god = state.GetGod(GodId);
            Planet planet = state.GetPlanet(PlanetId);
            UniverseRules rules = state.GetRulesForPlanet(planet);
            state.CanReach(god, planet, out float distance);
            god.resources.TrySpend(CommandCosts.Conquest(Type, planet, rules, distance));

            var rng = state.CreateTurnRandom($"conquer:{GodId}:{PlanetId}");
            bool success;
            Civilization civ = planet.civilization;

            switch (Type)
            {
                case ConquestType.Militar:
                {
                    float attack = MilitaryPower(state, god) * rules.modifiers.militaryStrength * rng.Range(0.8f, 1.2f);
                    success = attack > planet.defense;
                    if (success)
                    {
                        TransferOwnership(planet, god.id, faith: 0.2f);
                        if (civ.Exists)
                        {
                            civ.stability = Mathf.Clamp01(civ.stability - 0.3f);
                            civ.freeWill = Mathf.Clamp01(civ.freeWill - 0.1f);
                            civ.population *= 0.9f;
                        }
                        planet.defense = Mathf.Max(5, planet.defense / 2);
                    }
                    else
                    {
                        planet.defense = Mathf.Max(1, planet.defense - 3); // el asedio desgasta
                    }
                    break;
                }
                case ConquestType.Cultural:
                {
                    if (civ.worshippedGodId != god.id)
                    {
                        civ.faith = Mathf.Clamp01(civ.faith - CulturalSpread(planet, rules.modifiers) * rng.Range(0.8f, 1.2f));
                    }
                    success = civ.faith <= CulturalConversionThreshold;
                    if (success) TransferOwnership(planet, god.id, faith: 0.45f);
                    break;
                }
                default: // Tecnológica
                {
                    civ.techLevel = Mathf.Min(10, civ.techLevel + 1);
                    success = rng.NextDouble() < EstimateSuccessChance(state, god, planet, Type);
                    if (success)
                    {
                        TransferOwnership(planet, god.id, faith: 0.3f);
                        civ.freeWill = Mathf.Clamp01(civ.freeWill + 0.1f);
                    }
                    break;
                }
            }

            report.Add(new TurnEvent(TurnEventType.Conquest, GodId, PlanetId, success,
                $"conquest.{Type.ToString().ToLowerInvariant()}.{(success ? "success" : "fail")}"));
        }

        private static float MilitaryPower(GameState state, God god)
        {
            int owned = 0;
            foreach (var _ in state.PlanetsOwnedBy(god.id)) owned++;
            return 15f + owned * 3f + god.resources.matter * 0.05f;
        }

        private static float CulturalSpread(Planet planet, GameplayModifiers mods)
            => 0.25f * mods.culturalSpread * (1f - planet.civilization.stability * 0.5f);

        private static void TransferOwnership(Planet planet, string godId, float faith)
        {
            planet.ownerGodId = godId;
            if (planet.civilization.Exists)
            {
                planet.civilization.worshippedGodId = godId;
                planet.civilization.faith = faith;
            }
        }
    }
}
