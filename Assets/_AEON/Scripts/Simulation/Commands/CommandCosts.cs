using GodGame.Domain;

namespace GodGame.Simulation.Commands
{
    public enum MiracleType
    {
        Bendicion,   // +Fe y +estabilidad en un planeta propio
        Profeta,     // envía un profeta: +Fe hacia el dios en un planeta ajeno (prepara conquista cultural)
        Revelacion,  // +1 nivel tecnológico y +libre albedrío
        Cataclismo   // −defensa y −población en un planeta ajeno; daña el libre albedrío
    }

    /// <summary>
    /// Costes centralizados: los usan los comandos (para cobrar) y la IA (para planificar).
    /// Valores iniciales de diseño; se ajustan con Remote Config y simulaciones de balance.
    /// </summary>
    public static class CommandCosts
    {
        public static ResourceCost Conquest(ConquestType type, Planet target, UniverseRulesData rules, float distanceFactor)
        {
            ResourceCost baseCost;
            switch (type)
            {
                case ConquestType.Militar:
                    baseCost = new ResourceCost(0f, 5f, 10f + target.defense, 0f);
                    break;
                case ConquestType.Cultural:
                    baseCost = new ResourceCost(12f + target.civilization.population * 0.01f, 3f, 0f, 0f);
                    break;
                default: // Tecnologica
                    baseCost = new ResourceCost(0f, 4f, 5f, 10f + target.civilization.techLevel * 4f);
                    break;
            }
            return baseCost.Scaled(distanceFactor);
        }

        public static ResourceCost Miracle(MiracleType type, UniverseRulesData rules)
        {
            ResourceCost c;
            switch (type)
            {
                case MiracleType.Bendicion: c = new ResourceCost(5f, 8f, 0f, 0f); break;
                case MiracleType.Profeta: c = new ResourceCost(10f, 6f, 0f, 0f); break;
                case MiracleType.Revelacion: c = new ResourceCost(0f, 10f, 0f, 8f); break;
                default: c = new ResourceCost(0f, 20f, 5f, 0f); break; // Cataclismo
            }
            return c.Scaled(rules.modifiers.miracleCost);
        }
    }
}
