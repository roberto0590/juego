using System;
using GodGame.Domain;

namespace GodGame.Simulation
{
    /// <summary>
    /// Fórmulas de economía por ciclo (GDD §5), centralizadas para que las usen TurnManager,
    /// las reglas especiales de los universos y la UI:
    ///   Fe           = sqrt(población) · fe · 0,5 · mod.faithGain · timeFlowRate
    ///   Energía      = 1 · mod.divineEnergyGain + Fe · 0,3 · mod.divineEnergyGain
    ///   Materia      = 2 · mod.matterGain · matterStability
    ///   Conocimiento = techLevel · 0,5 · mod.knowledgeGain · timeFlowRate
    /// La Fe, la Energía derivada de la Fe y el Conocimiento solo llegan si la civilización adora al dueño.
    /// </summary>
    public static class EconomyFormulas
    {
        /// <summary>
        /// Ingreso base divino por dios no derrotado y ciclo (D14): Fe 8, Energía 4, Materia 3, Conocimiento 2.
        /// Sin él, un dios sin planetas (escala Planetaria) se bloquea tras el primer ciclo. Valor provisional de balance.
        /// </summary>
        public static ResourceCost BaseDivineIncome => new ResourceCost(8f, 4f, 3f, 2f);

        /// <summary>Fe que genera la civilización del planeta (0 si no existe civilización).</summary>
        public static float PlanetFaithIncome(Planet planet, UniverseRulesData rules)
        {
            Civilization civ = planet.civilization;
            if (civ == null || !civ.Exists) return 0f;
            return MathF.Sqrt(civ.population) * civ.faith * 0.5f * rules.modifiers.faithGain * rules.timeFlowRate;
        }

        /// <summary>Ingreso total del planeta para su dueño en un ciclo.</summary>
        public static ResourceCost PlanetIncome(Planet planet, God owner, UniverseRulesData rules)
        {
            GameplayModifiers m = rules.modifiers;
            var income = new ResourceCost(0f, 1f * m.divineEnergyGain, 2f * m.matterGain * rules.matterStability, 0f);

            Civilization civ = planet.civilization;
            if (civ != null && civ.Exists && owner != null && civ.worshippedGodId == owner.id)
            {
                float faith = PlanetFaithIncome(planet, rules);
                income.faith += faith;
                income.divineEnergy += faith * 0.3f * m.divineEnergyGain; // la fe se transmuta en poder
                income.knowledge += civ.techLevel * 0.5f * m.knowledgeGain * rules.timeFlowRate;
            }
            return income;
        }
    }
}
