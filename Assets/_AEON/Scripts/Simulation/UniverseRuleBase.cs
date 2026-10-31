using GodGame.Domain;

namespace GodGame.Simulation
{
    /// <summary>
    /// Regla especial de un universo (doc 03 §4.4): estrategia registrada por id de reglas,
    /// para que la economía y el alcance base no se llenen de "if (universo == ...)".
    /// Cada módulo de universo aporta sus subclases; GameState las recibe y las aplica:
    ///  - ModifyDistanceFactor: dentro de GameState.CanReach, por cada planeta propio de origen;
    ///  - OnEconomyPhase: en TurnManager, justo después de la economía base.
    /// Es una clase base (no una interfaz con métodos por defecto) para ser compatible con C# 9 e IL2CPP.
    /// </summary>
    public abstract class UniverseRuleBase
    {
        /// <summary>Id de las reglas de universo (UniverseRulesData.id) a las que se aplica.</summary>
        public abstract string RulesId { get; }

        /// <summary>Se llama una vez por ciclo y por universo con este rulesId, tras la economía base.</summary>
        public virtual void OnEconomyPhase(GameState state, Universe universe, TurnReport report) { }

        /// <summary>
        /// Ajusta el factor de distancia de una acción desde el planeta propio <paramref name="from"/>
        /// hasta <paramref name="target"/> (planeta del universo de esta regla). Devuelve el factor nuevo.
        /// </summary>
        public virtual float ModifyDistanceFactor(GameState state, God god, Planet from, Planet target, float factor)
            => factor;
    }
}
