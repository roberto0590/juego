using System;
using System.Collections.Generic;
using System.Linq;
using GodGame.Domain;
using Random = System.Random;

namespace GodGame.Simulation
{
    /// <summary>Escalas de progresión: Planeta → Sistema → Galaxia → Universo → Multiverso.</summary>
    public enum ProgressionStage
    {
        Planetaria = 0,   // solo puede actuar sobre su planeta asignado
        Sistemica = 1,    // posee su planeta natal: alcanza su sistema solar
        Galactica = 2,    // domina ≥50 % de su sistema: alcanza otros sistemas de la galaxia
        Universal = 3,    // ≥8 planetas: alcanza otras galaxias de su universo
        Multiversal = 4   // domina su universo (≥60 % de planetas civilizados): abre la Puerta del Velo
    }

    /// <summary>
    /// Estado de la simulación: envuelve el Multiverse (datos serializables) y añade índices
    /// en memoria, las reglas de cada universo (copias de UniverseRulesData) y sus reglas especiales.
    /// C# puro: se puede ejecutar en tests de modo Edit, en dotnet y en simulaciones de balance headless.
    /// </summary>
    public sealed class GameState
    {
        private static readonly IReadOnlyList<UniverseRuleBase> NoRules = new UniverseRuleBase[0];

        public Multiverse Multiverse { get; }
        public int Turn { get; internal set; }

        private readonly Dictionary<string, UniverseRulesData> _rulesById;
        private readonly Dictionary<string, List<UniverseRuleBase>> _specialRulesById =
            new Dictionary<string, List<UniverseRuleBase>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Universe> _universes = new Dictionary<string, Universe>();
        private readonly Dictionary<string, Galaxy> _galaxies = new Dictionary<string, Galaxy>();
        private readonly Dictionary<string, StarSystem> _systems = new Dictionary<string, StarSystem>();
        private readonly Dictionary<string, int> _systemOrdinals = new Dictionary<string, int>();
        private readonly Dictionary<string, Planet> _planets = new Dictionary<string, Planet>();
        // Lista en orden de generación: TODA iteración que afecte al resultado usa esta lista,
        // nunca el orden de un Dictionary (no garantizado) → determinismo.
        private readonly List<Planet> _planetList = new List<Planet>();

        /// <param name="multiverse">Datos de la partida.</param>
        /// <param name="rulesCatalog">Reglas de los universos; se guardan copias (Clone), nunca el original.</param>
        /// <param name="turn">Ciclo actual (0 en partida nueva; el guardado lo restaura).</param>
        /// <param name="specialRules">Reglas especiales de los módulos de universo (opcional).</param>
        public GameState(Multiverse multiverse, IEnumerable<UniverseRulesData> rulesCatalog, int turn = 0,
                         IEnumerable<UniverseRuleBase> specialRules = null)
        {
            Multiverse = multiverse ?? throw new ArgumentNullException(nameof(multiverse));
            if (rulesCatalog == null) throw new ArgumentNullException(nameof(rulesCatalog));
            _rulesById = rulesCatalog.ToDictionary(r => r.id, r => r.Clone(), StringComparer.Ordinal);
            if (specialRules != null)
            {
                foreach (var rule in specialRules)
                {
                    if (rule == null) continue;
                    if (!_specialRulesById.TryGetValue(rule.RulesId, out var list))
                    {
                        list = new List<UniverseRuleBase>();
                        _specialRulesById[rule.RulesId] = list;
                    }
                    list.Add(rule);
                }
            }
            Turn = turn;
            RebuildIndices();
        }

        public void RebuildIndices()
        {
            _universes.Clear(); _galaxies.Clear(); _systems.Clear(); _systemOrdinals.Clear();
            _planets.Clear(); _planetList.Clear();
            foreach (var u in Multiverse.universes)
            {
                _universes[u.id] = u;
                foreach (var g in u.galaxies)
                {
                    _galaxies[g.id] = g;
                    foreach (var s in g.systems)
                    {
                        _systems[s.id] = s;
                        _systemOrdinals[s.id] = _systemOrdinals.Count;
                        foreach (var p in s.planets) { _planets[p.id] = p; _planetList.Add(p); }
                    }
                }
            }
        }

        public IReadOnlyList<Planet> Planets => _planetList;
        public Planet GetPlanet(string id) => id != null && _planets.TryGetValue(id, out var p) ? p : null;
        public StarSystem GetSystem(string id) => id != null && _systems.TryGetValue(id, out var s) ? s : null;
        public Galaxy GetGalaxy(string id) => id != null && _galaxies.TryGetValue(id, out var g) ? g : null;
        public Universe GetUniverse(string id) => id != null && _universes.TryGetValue(id, out var u) ? u : null;
        public God GetGod(string id) => Multiverse.FindGod(id);

        /// <summary>Índice del sistema en orden de generación (0, 1, 2…); -1 si no existe.</summary>
        public int GetSystemOrdinal(string systemId)
            => systemId != null && _systemOrdinals.TryGetValue(systemId, out int ordinal) ? ordinal : -1;

        public UniverseRulesData GetRules(string universeId)
        {
            var u = GetUniverse(universeId);
            if (u == null || !_rulesById.TryGetValue(u.rulesId, out var rules))
                throw new KeyNotFoundException($"No hay reglas de universo para el universo '{universeId}'.");
            return rules;
        }

        public UniverseRulesData GetRulesForPlanet(Planet p) => GetRules(p.universeId);

        /// <summary>Reglas especiales registradas para un id de reglas, en el orden recibido (lista vacía si no hay).</summary>
        public IReadOnlyList<UniverseRuleBase> GetSpecialRules(string rulesId)
            => rulesId != null && _specialRulesById.TryGetValue(rulesId, out var list) ? list : NoRules;

        public IEnumerable<Planet> PlanetsOwnedBy(string godId) => _planetList.Where(p => p.ownerGodId == godId);

        /// <summary>
        /// Random determinista por turno: misma semilla + mismo turno + misma etiqueta ⇒ misma secuencia.
        /// No hay que guardar el estado del generador en la partida.
        /// </summary>
        public Random CreateTurnRandom(string salt)
            => new Random(RandomExtensions.DeriveSeed(Multiverse.seed, $"turn:{Turn}:{salt}"));

        public ProgressionStage ComputeStage(God god)
        {
            var owned = PlanetsOwnedBy(god.id).ToList();
            var home = GetPlanet(god.homePlanetId);
            if (home == null || home.ownerGodId != god.id) return ProgressionStage.Planetaria;

            var homeSystem = GetSystem(home.systemId);
            int ownedInHomeSystem = homeSystem.planets.Count(p => p.ownerGodId == god.id);
            if (ownedInHomeSystem * 2 < homeSystem.planets.Count) return ProgressionStage.Sistemica;
            if (owned.Count < 8) return ProgressionStage.Galactica;

            var civilized = Planets.Where(p => p.universeId == god.homeUniverseId && p.civilization.Exists).ToList();
            int ownedCivilized = civilized.Count(p => p.ownerGodId == god.id);
            bool dominatesHome = civilized.Count > 0 && ownedCivilized >= civilized.Count * 0.6f;
            return dominatesHome ? ProgressionStage.Multiversal : ProgressionStage.Universal;
        }

        /// <summary>
        /// ¿Puede el dios actuar sobre el planeta? Devuelve el factor de distancia que encarece la acción.
        /// Mismo sistema ×1 · misma galaxia ×1.5 · otra galaxia ×2.5 · otro universo ×4,
        /// todo multiplicado por las leyes físicas del universo destino (dimensiones, viscosidad, luz)
        /// y ajustado por sus reglas especiales, planeta propio a planeta propio; se queda el mínimo.
        /// En escala Planetaria solo alcanza su planeta asignado, con factor 1.
        /// </summary>
        public bool CanReach(God god, Planet target, out float distanceFactor)
        {
            distanceFactor = 1f;
            if (god == null || target == null || god.defeated) return false;
            if (!god.unlockedUniverseIds.Contains(target.universeId)) return false;

            ProgressionStage stage = ComputeStage(god);
            if (stage == ProgressionStage.Planetaria)
                return target.id == god.homePlanetId;

            var targetSystem = GetSystem(target.systemId);
            float travel = GetRulesForPlanet(target).ComputeTravelCostFactor();
            var targetRules = GetSpecialRules(GetUniverse(target.universeId).rulesId);
            float best = float.MaxValue;
            foreach (var own in PlanetsOwnedBy(god.id))
            {
                float f;
                if (own.systemId == target.systemId) f = 1f;
                else if (own.universeId != target.universeId)
                {
                    if (stage < ProgressionStage.Multiversal) continue;
                    f = 4f;
                }
                else if (GetSystem(own.systemId).galaxyId == targetSystem.galaxyId)
                {
                    if (stage < ProgressionStage.Galactica) continue;
                    f = 1.5f;
                }
                else
                {
                    if (stage < ProgressionStage.Universal) continue;
                    f = 2.5f;
                }

                f *= travel;
                for (int i = 0; i < targetRules.Count; i++)
                    f = targetRules[i].ModifyDistanceFactor(this, god, own, target, f);

                if (f < best) best = f;
            }

            if (best == float.MaxValue) return false;
            distanceFactor = best;
            return true;
        }

        public IEnumerable<Planet> ReachableTargets(God god)
        {
            foreach (var p in _planetList)
                if (p.ownerGodId != god.id && CanReach(god, p, out _))
                    yield return p;
        }
    }
}
