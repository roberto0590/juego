using GodGame.Domain;
using GodGame.Rules;
using GodGame.Simulation;

namespace GodGame.Universes.Thalassa
{
    /// <summary>
    /// Regla especial "Corrientes" de Thalassa (doc 03 §3.6): a favor de la corriente, el coste de viaje
    /// entre sistemas se divide entre 2. Las corrientes cambian de dirección cada 6 ciclos.
    /// "A favor" = ((Turn / 6) % 2 == 0) == (ordinal del sistema destino > ordinal del sistema de origen):
    /// en los ciclos 0–5 la corriente fluye hacia los sistemas de ordinal mayor; en 6–11, hacia los de menor; etc.
    /// </summary>
    [Preserve]
    public sealed class CorrientesRule : UniverseRuleBase
    {
        public const int CyclesPerCurrent = 6;
        public const float FavorableFactor = 0.5f;

        public override string RulesId => ThalassaModule.Id;

        public override float ModifyDistanceFactor(GameState state, God god, Planet from, Planet target, float factor)
        {
            if (from == null || target == null || from.systemId == target.systemId) return factor;
            return IsFavorable(state, from, target) ? factor * FavorableFactor : factor;
        }

        /// <summary>¿Viajar de <paramref name="from"/> a <paramref name="target"/> va a favor de la corriente en este ciclo?</summary>
        public static bool IsFavorable(GameState state, Planet from, Planet target)
        {
            bool forwardPhase = (state.Turn / CyclesPerCurrent) % 2 == 0;
            bool towardsHigher = state.GetSystemOrdinal(target.systemId) > state.GetSystemOrdinal(from.systemId);
            return forwardPhase == towardsHigher;
        }
    }
}
