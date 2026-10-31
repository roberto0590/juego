using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GodGame.Domain;
using GodGame.Simulation;

namespace GodGame.Rules
{
    /// <summary>
    /// Registro de módulos de universo descubiertos por reflexión (D5). Un único mecanismo válido
    /// en el Editor, en el player (IL2CPP, con [Preserve] + link.xml), en los tests EditMode y en dotnet:
    ///  1) recorre AppDomain.CurrentDomain.GetAssemblies();
    ///  2) considera el ensamblado que define IUniverseModule y los que lo referencian por nombre;
    ///  3) instancia los tipos no abstractos con constructor público vacío que implementan IUniverseModule;
    ///  4) ordena por RulesId (ordinal) para que el resultado no dependa del orden de carga.
    /// Añadir un universo = añadir una carpeta de módulo; ni el núcleo ni el Editor cambian.
    /// </summary>
    public static class UniverseModuleRegistry
    {
        private static readonly object Sync = new object();
        private static List<IUniverseModule> _discovered;                       // null = aún no descubierto
        private static readonly List<IUniverseModule> Manual = new List<IUniverseModule>();
        private static List<IUniverseModule> _all;                              // caché combinada y ordenada

        /// <summary>Todos los módulos (descubiertos + registrados a mano), ordenados por RulesId ordinal.</summary>
        public static IReadOnlyList<IUniverseModule> All
        {
            get
            {
                lock (Sync)
                {
                    EnsureBuilt();
                    return _all;
                }
            }
        }

        /// <summary>Módulo con ese id de reglas, o null.</summary>
        public static IUniverseModule Find(string rulesId)
        {
            if (rulesId == null) return null;
            var all = All;
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].RulesId, rulesId, StringComparison.Ordinal)) return all[i];
            return null;
        }

        /// <summary>
        /// Registra un módulo a mano (útil si un módulo no tiene constructor vacío o en pruebas).
        /// Lanza InvalidOperationException si ya existe un módulo con el mismo RulesId.
        /// </summary>
        public static void Register(IUniverseModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            lock (Sync)
            {
                EnsureBuilt();
                IUniverseModule existing = _all.FirstOrDefault(m => string.Equals(m.RulesId, module.RulesId, StringComparison.Ordinal));
                if (existing != null)
                    throw new InvalidOperationException(
                        $"Ya existe un módulo de universo con el id '{module.RulesId}' ({existing.GetType().FullName}).");
                Manual.Add(module);
                _all = null;
                EnsureBuilt();
            }
        }

        /// <summary>Olvida los módulos registrados a mano y la caché; el siguiente acceso vuelve a descubrir.</summary>
        public static void ResetForTests()
        {
            lock (Sync)
            {
                Manual.Clear();
                _discovered = null;
                _all = null;
            }
        }

        /// <summary>Reglas por defecto de cada módulo (instancias nuevas, saneadas), en el orden de All.</summary>
        public static List<UniverseRulesData> CreateDefaultCatalog()
        {
            var catalog = new List<UniverseRulesData>();
            foreach (var module in All)
            {
                UniverseRulesData rules = module.CreateDefaultRules();
                if (rules == null) continue;
                rules.Sanitize();
                catalog.Add(rules);
            }
            return catalog;
        }

        /// <summary>
        /// Texto de una clave propia de algún módulo (los que implementan IUniverseTexts), en el orden de All.
        /// Devuelve false si ningún módulo la conoce.
        /// </summary>
        public static bool TryGetText(string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(key)) return false;
            foreach (var module in All)
            {
                if (module is IUniverseTexts texts && texts.TryGetText(key, out text) && text != null) return true;
            }
            text = null;
            return false;
        }

        /// <summary>
        /// Reglas especiales de los módulos cuyos ids estén en <paramref name="rulesIds"/>
        /// (null = todos), en el orden de All. Ids sin módulo se ignoran.
        /// </summary>
        public static List<UniverseRuleBase> CreateSpecialRules(IEnumerable<string> rulesIds)
        {
            var wanted = rulesIds != null ? new HashSet<string>(rulesIds, StringComparer.Ordinal) : null; // solo pertenencia
            var result = new List<UniverseRuleBase>();
            foreach (var module in All)
            {
                if (wanted != null && !wanted.Contains(module.RulesId)) continue;
                IEnumerable<UniverseRuleBase> rules = module.CreateSpecialRules();
                if (rules == null) continue;
                foreach (var rule in rules)
                    if (rule != null) result.Add(rule);
            }
            return result;
        }

        // ---------------- Descubrimiento ----------------

        private static void EnsureBuilt()
        {
            if (_all != null) return;
            if (_discovered == null) _discovered = Discover();

            var combined = new List<IUniverseModule>(_discovered.Count + Manual.Count);
            combined.AddRange(_discovered);
            foreach (var module in Manual)
            {
                if (combined.Any(m => string.Equals(m.RulesId, module.RulesId, StringComparison.Ordinal))) continue;
                combined.Add(module);
            }
            combined.Sort((a, b) => string.CompareOrdinal(a.RulesId, b.RulesId));
            _all = combined;
        }

        private static List<IUniverseModule> Discover()
        {
            Type contract = typeof(IUniverseModule);
            Assembly rulesAssembly = contract.Assembly;
            string rulesAssemblyName = rulesAssembly.GetName().Name;

            var modules = new List<IUniverseModule>();
            var byId = new Dictionary<string, Type>(StringComparer.Ordinal); // solo para detectar duplicados

            // Orden de ensamblados por nombre: los errores y el resultado no dependen del orden de carga.
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .OrderBy(a => a.GetName().Name, StringComparer.Ordinal);

            foreach (Assembly assembly in assemblies)
            {
                if (assembly != rulesAssembly && !ReferencesAssembly(assembly, rulesAssemblyName)) continue;

                foreach (Type type in GetLoadableTypes(assembly).OrderBy(t => t.FullName, StringComparer.Ordinal))
                {
                    if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;
                    if (!contract.IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    var module = (IUniverseModule)Activator.CreateInstance(type);
                    string id = module.RulesId;
                    if (string.IsNullOrEmpty(id))
                        throw new InvalidOperationException($"El módulo de universo {type.FullName} tiene un RulesId vacío.");
                    if (byId.TryGetValue(id, out Type other))
                        throw new InvalidOperationException(
                            $"Hay dos módulos de universo con el id '{id}': {other.FullName} y {type.FullName}.");
                    byId[id] = type;
                    modules.Add(module);
                }
            }

            modules.Sort((a, b) => string.CompareOrdinal(a.RulesId, b.RulesId));
            return modules;
        }

        private static bool ReferencesAssembly(Assembly assembly, string assemblyName)
        {
            try
            {
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                    if (string.Equals(reference.Name, assemblyName, StringComparison.Ordinal)) return true;
            }
            catch (Exception)
            {
                // Ensamblados del sistema que no permiten inspección: se ignoran.
            }
            return false;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }
}
