using System;
using System.Collections.Generic;
using System.Linq;
using GodGame.Domain;
using GodGame.Simulation;
using GodGame.Simulation.Commands;
using Random = System.Random;

namespace GodGame.AI
{
    /// <summary>
    /// UTILITY AI para dioses rivales.
    /// Cada turno: 1) genera acciones candidatas, 2) puntúa cada una multiplicando "consideraciones"
    /// normalizadas (0..1) pasadas por curvas de respuesta, 3) aplica los pesos de personalidad,
    /// 4) elige entre las mejores con algo de azar determinista (para no ser predecible).
    ///
    /// Por qué Utility AI y no árboles de comportamiento para la estrategia: en un 4X hay muchas
    /// opciones comparables a la vez (¿qué planeta? ¿qué vía?); puntuarlas escala mejor que ramas fijas.
    /// Los árboles de comportamiento se reservan para secuencias guionizadas (eventos de historia).
    /// </summary>
    public sealed class GodUtilityAI : ICommandSource
    {
        private readonly string _godId;

        /// <summary>Acciones máximas por turno (sube con la dificultad).</summary>
        public int MaxActionsPerTurn = 2;

        /// <summary>Cuántas de las mejores opciones entran en el sorteo final. 1 = IA totalmente determinista y "óptima".</summary>
        public int TopCandidates = 3;

        /// <summary>Multiplicador global de agresividad/eficacia según dificultad (0.7 fácil, 1 normal, 1.3 difícil).</summary>
        public float Difficulty = 1f;

        public GodUtilityAI(string godId) { _godId = godId; }

        private readonly struct Option
        {
            public readonly ICommand command; // null = "esperar y acumular"
            public readonly float score;
            public readonly string planetId;
            public Option(ICommand command, float score, string planetId)
            { this.command = command; this.score = score; this.planetId = planetId; }
        }

        public void CollectCommands(GameState state, List<ICommand> output)
        {
            God god = state.GetGod(_godId);
            if (god == null || god.defeated || god.isPlayer) return;

            Random rng = state.CreateTurnRandom("ai:" + _godId);
            var options = new List<Option>();
            EvaluateConquests(state, god, options);
            EvaluateMiracles(state, god, options);
            options.Add(new Option(null, SaveScore(god), null));

            // Presupuesto simulado para no planificar acciones que no podrá pagar.
            var budget = new ResourceWallet
            {
                faith = god.resources.faith, divineEnergy = god.resources.divineEnergy,
                matter = god.resources.matter, knowledge = god.resources.knowledge
            };
            var usedPlanets = new HashSet<string>(); // solo pertenencia: no influye en el orden

            for (int action = 0; action < MaxActionsPerTurn && options.Count > 0; action++)
            {
                var top = options.OrderByDescending(o => o.score).Take(Math.Max(1, TopCandidates)).ToList();
                int pick = rng.WeightedIndex(top.Select(o => o.score * o.score).ToList()); // cuadrado: favorece al mejor
                Option chosen = top[pick];
                if (chosen.command == null) break; // decidió ahorrar

                options.Remove(chosen);
                if (usedPlanets.Contains(chosen.planetId)) { action--; continue; }

                ResourceCost cost = CostOf(state, god, chosen.command);
                if (!budget.TrySpend(cost)) { action--; continue; }

                usedPlanets.Add(chosen.planetId);
                output.Add(chosen.command);
            }
        }

        // ---------------- Consideraciones ----------------

        private void EvaluateConquests(GameState state, God god, List<Option> options)
        {
            GodPersonality p = god.personality;
            foreach (Planet target in state.ReachableTargets(god))
            {
                state.CanReach(god, target, out float distance);
                UniverseRulesData rules = state.GetRulesForPlanet(target);
                float value = PlanetValue(target);
                float threat = target.HasOwner ? 1f : 0.6f; // arrebatar planetas a rivales también los debilita

                foreach (ConquestType type in (ConquestType[])Enum.GetValues(typeof(ConquestType)))
                {
                    if (type != ConquestType.Militar && !target.civilization.Exists) continue;

                    ResourceCost cost = CommandCosts.Conquest(type, target, rules, distance);
                    float affordability = Affordability(god.resources, cost);
                    if (affordability <= 0f) continue;

                    float success = ConquerPlanetCommand.EstimateSuccessChance(state, god, target, type);
                    float personality = type == ConquestType.Militar ? p.aggression
                                      : type == ConquestType.Cultural ? p.piety
                                      : p.curiosity;

                    float score = Combine(
                        Logistic(value, 0.4f, 8f),
                        Quadratic(success),
                        affordability,
                        Linear(1f / distance),
                        threat);
                    score *= MathUtil.Lerp(0.3f, 1.2f, personality) * Difficulty;
                    score *= 1f - p.greed * 0.4f;

                    options.Add(new Option(new ConquerPlanetCommand(god.id, target.id, type), score, target.id));
                }
            }
        }

        private void EvaluateMiracles(GameState state, God god, List<Option> options)
        {
            GodPersonality p = god.personality;

            foreach (Planet own in state.PlanetsOwnedBy(god.id))
            {
                if (!own.civilization.Exists) continue;
                var rules = state.GetRulesForPlanet(own);

                // Bendición: urgente si la fe propia es baja (riesgo de herejía o conversión rival).
                float need = 1f - own.civilization.faith;
                AddMiracle(state, god, own, MiracleType.Bendicion, rules,
                           Combine(Quadratic(need), PlanetValue(own)) * MathUtil.Lerp(0.4f, 1.1f, p.piety), options);

                // Revelación: invertir en conocimiento si la civilización está atrasada.
                float backward = 1f - own.civilization.techLevel / 10f;
                AddMiracle(state, god, own, MiracleType.Revelacion, rules,
                           Combine(backward, PlanetValue(own)) * MathUtil.Lerp(0.3f, 1.0f, p.curiosity), options);
            }

            foreach (Planet target in state.ReachableTargets(god))
            {
                if (!target.civilization.Exists) continue;
                var rules = state.GetRulesForPlanet(target);

                // Profeta: prepara conquistas culturales.
                AddMiracle(state, god, target, MiracleType.Profeta, rules,
                           Combine(PlanetValue(target), target.civilization.faith) * MathUtil.Lerp(0.3f, 1.0f, p.piety), options);

                // Cataclismo: solo contra planetas de rivales y si la diplomacia es baja.
                if (target.HasOwner)
                {
                    float hostility = p.aggression * (1f - p.diplomacy);
                    AddMiracle(state, god, target, MiracleType.Cataclismo, rules,
                               Combine(PlanetValue(target), hostility) * 0.8f, options);
                }
            }
        }

        private void AddMiracle(GameState state, God god, Planet planet, MiracleType type,
                                UniverseRulesData rules, float score, List<Option> options)
        {
            var cost = CommandCosts.Miracle(type, rules);
            float affordability = Affordability(god.resources, cost);
            if (affordability <= 0f) return;
            var cmd = new MiracleCommand(god.id, planet.id, type);
            if (!cmd.Validate(state, out _)) return;
            options.Add(new Option(cmd, score * affordability * Difficulty, planet.id));
        }

        /// <summary>Utilidad de "no hacer nada": la avaricia y la pobreza la elevan.</summary>
        private static float SaveScore(God god)
        {
            float wealth = god.resources.faith + god.resources.divineEnergy + god.resources.matter + god.resources.knowledge;
            float poverty = 1f - MathUtil.Clamp01(wealth / 200f);
            return 0.1f + 0.35f * god.personality.greed + 0.25f * poverty;
        }

        private static ResourceCost CostOf(GameState state, God god, ICommand cmd)
        {
            switch (cmd)
            {
                case ConquerPlanetCommand c:
                {
                    var planet = state.GetPlanet(c.PlanetId);
                    state.CanReach(god, planet, out float d);
                    return CommandCosts.Conquest(c.Type, planet, state.GetRulesForPlanet(planet), d);
                }
                case MiracleCommand m:
                    return CommandCosts.Miracle(m.Type, state.GetRulesForPlanet(state.GetPlanet(m.PlanetId)));
                default:
                    return default;
            }
        }

        // ---------------- Curvas de respuesta y utilidades ----------------

        /// <summary>Valor de un planeta (0..1): población (log), tecnología y habitabilidad.</summary>
        private static float PlanetValue(Planet p)
        {
            if (!p.civilization.Exists) return p.habitable ? 0.3f : 0.1f;
            float pop = MathUtil.Clamp01(MathF.Log10(1f + p.civilization.population) / 3f); // 1000 M → 1
            float tech = p.civilization.techLevel / 10f;
            return MathUtil.Clamp01(0.6f * pop + 0.4f * tech);
        }

        /// <summary>1 si sobra para pagarlo 3 veces; 0 si no alcanza.</summary>
        private static float Affordability(ResourceWallet w, ResourceCost c)
        {
            if (!w.CanAfford(c)) return 0f;
            float ratio = 1f;
            if (c.faith > 0) ratio = MathUtil.Min(ratio, w.faith / (c.faith * 3f));
            if (c.divineEnergy > 0) ratio = MathUtil.Min(ratio, w.divineEnergy / (c.divineEnergy * 3f));
            if (c.matter > 0) ratio = MathUtil.Min(ratio, w.matter / (c.matter * 3f));
            if (c.knowledge > 0) ratio = MathUtil.Min(ratio, w.knowledge / (c.knowledge * 3f));
            return MathUtil.Clamp(ratio, 0.2f, 1f);
        }

        private static float Linear(float x) => MathUtil.Clamp01(x);
        private static float Quadratic(float x) => MathUtil.Clamp01(x) * MathUtil.Clamp01(x);
        private static float Logistic(float x, float midpoint, float steepness)
            => 1f / (1f + MathF.Exp(-steepness * (x - midpoint)));

        /// <summary>
        /// Producto de consideraciones con "factor de compensación" (Dave Mark): evita que tener
        /// muchas consideraciones hunda la puntuación solo por multiplicar números menores que 1.
        /// </summary>
        private static float Combine(params float[] considerations)
        {
            float score = 1f;
            float modFactor = 1f - 1f / considerations.Length;
            foreach (float c in considerations)
            {
                float v = MathUtil.Clamp01(c);
                float makeUp = (1f - v) * modFactor;
                score *= v + makeUp * v;
            }
            return score;
        }
    }
}
