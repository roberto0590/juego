using System;
using System.Collections.Generic;
using System.Linq;
using GodGame.Domain;
using GodGame.Simulation.Commands;

namespace GodGame.Simulation
{
    // UniverseRule se añade AL FINAL para no cambiar el valor numérico de los tipos existentes.
    public enum TurnEventType { Conquest, Miracle, Rejected, Heresy, StageReached, UniverseUnlocked, GodDefeated, Victory, UniverseRule }

    [Serializable]
    public struct TurnEvent
    {
        public TurnEventType type;
        public string godId;
        public string planetId;
        public bool success;
        public string messageKey; // clave de localización; la UI la traduce

        public TurnEvent(TurnEventType type, string godId, string planetId, bool success, string messageKey)
        {
            this.type = type; this.godId = godId; this.planetId = planetId; this.success = success; this.messageKey = messageKey;
        }
    }

    /// <summary>Resultado de un turno: la presentación lo reproduce como secuencia animada.</summary>
    public sealed class TurnReport
    {
        public int Turn { get; }
        public List<TurnEvent> Events { get; } = new List<TurnEvent>();
        public string WinnerGodId { get; internal set; }

        public TurnReport(int turn) { Turn = turn; }
        public void Add(TurnEvent e) => Events.Add(e);
    }

    /// <summary>
    /// TURNOS SIMULTÁNEOS:
    ///  1. Planificación: el jugador envía comandos (Submit); la IA los genera al resolver.
    ///  2. Resolución: todos los comandos se ordenan de forma determinista (prioridad → dios → orden de envío),
    ///     se revalidan (el estado puede haber cambiado) y se ejecutan.
    ///  3. Economía: ingreso base divino + ingresos por planeta según las leyes físicas de cada universo,
    ///     y después las reglas especiales de cada universo (en el orden de Multiverse.universes).
    ///  4. Progresión y victoria.
    /// Clase C# pura (sin MonoBehaviour) para poder testearla y simular miles de turnos headless.
    /// La capa Unity (GameSession/GameController) la crea y escucha TurnResolved.
    /// </summary>
    public sealed class TurnManager
    {
        public const float VictoryShare = 0.75f; // % de planetas civilizados del multiverso

        public event Action<TurnReport> TurnResolved;

        public GameState State { get; }
        private readonly List<ICommandSource> _sources = new List<ICommandSource>();
        private readonly List<ICommand> _pending = new List<ICommand>();
        private readonly Dictionary<string, ProgressionStage> _lastStage = new Dictionary<string, ProgressionStage>();

        public TurnManager(GameState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            foreach (var god in state.Multiverse.gods) _lastStage[god.id] = state.ComputeStage(god);
        }

        public void RegisterSource(ICommandSource source) => _sources.Add(source);

        /// <summary>Encola una orden del jugador. Devuelve false (y el motivo) si es inválida ahora mismo.</summary>
        public bool Submit(ICommand command, out string reason)
        {
            if (!command.Validate(State, out reason)) return false;
            _pending.Add(command);
            return true;
        }

        public void CancelPending(string godId) => _pending.RemoveAll(c => c.GodId == godId);

        public IReadOnlyList<ICommand> Pending => _pending;

        public TurnReport ResolveTurn()
        {
            var report = new TurnReport(State.Turn);

            // 1) Órdenes de la IA (y de otras fuentes).
            foreach (var source in _sources) source.CollectCommands(State, _pending);

            // 2) Orden determinista: imprescindible para replays, tests y PvP asíncrono.
            var ordered = _pending
                .Select((cmd, index) => (cmd, index))
                .OrderBy(x => x.cmd.Priority)
                .ThenBy(x => x.cmd.GodId, StringComparer.Ordinal)
                .ThenBy(x => x.index)
                .Select(x => x.cmd)
                .ToList();
            _pending.Clear();

            foreach (var cmd in ordered)
            {
                if (cmd.Validate(State, out string reason)) cmd.Execute(State, report);
                else report.Add(new TurnEvent(TurnEventType.Rejected, cmd.GodId, null, false, reason));
            }

            // 3) Economía, vida de las civilizaciones y reglas especiales de cada universo.
            RunEconomy(report);
            RunSpecialRules(report);

            // 4) Progresión, derrotas y victoria.
            UpdateProgression(report);
            CheckDefeatsAndVictory(report);

            State.Turn++;
            TurnResolved?.Invoke(report);
            return report;
        }

        private void RunEconomy(TurnReport report)
        {
            // Ingreso base divino (D14) por cada dios no derrotado, en el orden de la lista de dioses.
            ResourceCost baseIncome = EconomyFormulas.BaseDivineIncome;
            foreach (var god in State.Multiverse.gods)
            {
                if (!god.defeated) god.resources.Add(baseIncome);
            }

            var rng = State.CreateTurnRandom("economy");
            foreach (var planet in State.Planets)
            {
                if (!planet.HasOwner) continue;
                God owner = State.GetGod(planet.ownerGodId);
                if (owner == null) continue;

                UniverseRulesData rules = State.GetRulesForPlanet(planet);
                GameplayModifiers m = rules.modifiers;
                float time = rules.timeFlowRate;
                Civilization civ = planet.civilization;

                ResourceCost income = EconomyFormulas.PlanetIncome(planet, owner, rules);
                if (civ.Exists && civ.worshippedGodId == owner.id)
                {
                    civ.population *= 1f + 0.01f * time;
                    civ.faith = MathUtil.Clamp01(civ.faith + 0.02f * civ.stability);
                    civ.stability = MathUtil.MoveTowards(civ.stability, m.civilizationStability, 0.02f);

                    // Herejías: más probables cuanto menor es la estabilidad del universo.
                    if (rng.NextDouble() < (1f - civ.stability) * 0.05f)
                    {
                        civ.faith = MathUtil.Clamp01(civ.faith - 0.1f);
                        report.Add(new TurnEvent(TurnEventType.Heresy, owner.id, planet.id, false, "event.heresy"));
                    }
                }
                owner.resources.Add(income);
            }
        }

        private void RunSpecialRules(TurnReport report)
        {
            foreach (var universe in State.Multiverse.universes)
            {
                var rules = State.GetSpecialRules(universe.rulesId);
                for (int i = 0; i < rules.Count; i++) rules[i].OnEconomyPhase(State, universe, report);
            }
        }

        private void UpdateProgression(TurnReport report)
        {
            foreach (var god in State.Multiverse.gods)
            {
                if (god.defeated) continue;
                ProgressionStage stage = State.ComputeStage(god);
                _lastStage.TryGetValue(god.id, out var previous);
                if (stage > previous)
                    report.Add(new TurnEvent(TurnEventType.StageReached, god.id, null, true, $"stage.{stage.ToString().ToLowerInvariant()}"));
                _lastStage[god.id] = stage;

                // Puerta del Velo: al dominar su universo, el dios puede viajar a los universos vecinos (índice ±1).
                if (stage == ProgressionStage.Multiversal) UnlockNeighbourUniverses(god, report);
            }
        }

        private void UnlockNeighbourUniverses(God god, TurnReport report)
        {
            var universes = State.Multiverse.universes;
            foreach (var unlockedId in god.unlockedUniverseIds.ToList())
            {
                var u = State.GetUniverse(unlockedId);
                foreach (int idx in new[] { u.index - 1, u.index + 1 })
                {
                    if (idx < 0 || idx >= universes.Count) continue;
                    string id = universes[idx].id;
                    if (god.unlockedUniverseIds.Contains(id)) continue;
                    god.unlockedUniverseIds.Add(id);
                    report.Add(new TurnEvent(TurnEventType.UniverseUnlocked, god.id, null, true, "universe.unlocked:" + id));
                }
            }
        }

        private void CheckDefeatsAndVictory(TurnReport report)
        {
            var civilized = State.Planets.Where(p => p.civilization.Exists).ToList();
            foreach (var god in State.Multiverse.gods)
            {
                if (god.defeated) continue;
                Planet home = State.GetPlanet(god.homePlanetId);
                bool ownsNothing = !State.PlanetsOwnedBy(god.id).Any();
                if (ownsNothing && home != null && home.HasOwner && home.ownerGodId != god.id)
                {
                    god.defeated = true;
                    report.Add(new TurnEvent(TurnEventType.GodDefeated, god.id, null, false, "god.defeated"));
                    continue;
                }
                int owned = civilized.Count(p => p.ownerGodId == god.id);
                if (civilized.Count > 0 && owned >= civilized.Count * VictoryShare)
                {
                    report.WinnerGodId = god.id;
                    report.Add(new TurnEvent(TurnEventType.Victory, god.id, null, true, "victory.multiverse"));
                }
            }
        }
    }
}
