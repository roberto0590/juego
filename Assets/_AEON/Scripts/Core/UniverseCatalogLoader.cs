using System;
using System.Collections.Generic;
using GodGame.Domain;
using GodGame.Rules;
using GodGame.Rules.Assets;
using UnityEngine;

namespace GodGame.Core
{
    /// <summary>Catálogo listo para jugar: reglas puras (ordenadas por id) + presentación por id de reglas + materiales compartidos.</summary>
    public sealed class LoadedCatalog
    {
        // Diccionario solo para búsquedas; el orden de juego lo da la lista Rules.
        private readonly Dictionary<string, UniversePresentation> _presentations;

        public LoadedCatalog(List<UniverseRulesData> rules, Dictionary<string, UniversePresentation> presentations, bool fromAsset)
        {
            Rules = rules ?? new List<UniverseRulesData>();
            _presentations = presentations ?? new Dictionary<string, UniversePresentation>(StringComparer.Ordinal);
            FromAsset = fromAsset;
        }

        /// <summary>Reglas de los universos disponibles, ordenadas por id (ordinal).</summary>
        public List<UniverseRulesData> Rules { get; }

        /// <summary>true si viene de Resources/AEON/UniverseCatalog; false si se construyó con los módulos (D13).</summary>
        public bool FromAsset { get; }

        public Material StarMaterial { get; internal set; }
        public Material OrbitMaterial { get; internal set; }
        public Material DefaultPlanetMaterial { get; internal set; }
        public Material DefaultAtmosphereMaterial { get; internal set; }

        public bool HasRules(string rulesId)
            => rulesId != null && Rules.Exists(r => string.Equals(r.id, rulesId, StringComparison.Ordinal));

        /// <summary>Presentación del universo; si no hay, la del módulo (o una neutra) sin materiales.</summary>
        public UniversePresentation GetPresentation(string rulesId)
        {
            string key = rulesId ?? "";
            if (_presentations.TryGetValue(key, out UniversePresentation presentation)) return presentation;
            presentation = new UniversePresentation(key, key, UniverseCatalogLoader.DefaultsFromModule(key), null, null);
            _presentations[key] = presentation;
            return presentation;
        }
    }

    /// <summary>
    /// Carga el catálogo de universos (D13): Resources.Load&lt;UniverseCatalog&gt;("AEON/UniverseCatalog").
    /// Si no existe o está vacío, usa los valores por defecto de los módulos (UniverseModuleRegistry)
    /// para que el juego funcione aunque no se haya ejecutado el configurador.
    /// </summary>
    public static class UniverseCatalogLoader
    {
        public static LoadedCatalog Load()
        {
            UniverseCatalog asset = Resources.Load<UniverseCatalog>(GameConstants.ResourcesCatalogPath);
            List<UniverseRulesData> rules = asset != null ? asset.BuildRuntimeRules() : null;

            LoadedCatalog result;
            if (rules != null && rules.Count > 0)
            {
                result = FromAsset(asset, rules);
            }
            else
            {
                Debug.LogWarning(asset == null
                    ? "[AEON] No se encontró Resources/" + GameConstants.ResourcesCatalogPath + ": se usan los valores por defecto de los módulos de universo. Ejecuta 'AEON/Configurar proyecto' en el Editor."
                    : "[AEON] El catálogo de universos está vacío: se usan los valores por defecto de los módulos de universo. Ejecuta 'AEON/Configurar proyecto' en el Editor.");
                result = FromModules();
            }

            if (asset != null)
            {
                result.StarMaterial = asset.starMaterial;
                result.OrbitMaterial = asset.orbitMaterial;
                result.DefaultPlanetMaterial = asset.defaultPlanetMaterial;
                result.DefaultAtmosphereMaterial = asset.defaultAtmosphereMaterial;
            }

            if (result.Rules.Count == 0)
                Debug.LogError("[AEON] No hay ningún universo disponible: falta al menos un módulo en Assets/_AEON/Universes.");

            return result;
        }

        /// <summary>Presentación por defecto del módulo con ese id, o una neutra si no hay módulo.</summary>
        public static UniversePresentationDefaults DefaultsFromModule(string rulesId)
        {
            IUniverseModule module = UniverseModuleRegistry.Find(rulesId);
            UniversePresentationDefaults defaults = module != null ? module.CreatePresentationDefaults() : null;
            return defaults ?? new UniversePresentationDefaults();
        }

        private static LoadedCatalog FromAsset(UniverseCatalog asset, List<UniverseRulesData> rules)
        {
            var presentations = new Dictionary<string, UniversePresentation>(StringComparer.Ordinal);
            foreach (UniverseRules ur in asset.universes)
            {
                if (ur == null || ur.Data == null) continue;
                string id = ur.Id;
                if (string.IsNullOrEmpty(id) || presentations.ContainsKey(id)) continue;

                UniversePresentationDefaults defaults = ur.Presentation != null ? ur.Presentation.Clone() : DefaultsFromModule(id);
                string displayName = !string.IsNullOrEmpty(ur.Data.displayName) ? ur.Data.displayName : ur.name;
                presentations[id] = new UniversePresentation(id, displayName, defaults, ur.planetMaterial, ur.atmosphereMaterial);
            }
            return new LoadedCatalog(rules, presentations, fromAsset: true);
        }

        private static LoadedCatalog FromModules()
        {
            List<UniverseRulesData> rules = UniverseModuleRegistry.CreateDefaultCatalog();
            rules.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            var presentations = new Dictionary<string, UniversePresentation>(StringComparer.Ordinal);
            foreach (IUniverseModule module in UniverseModuleRegistry.All)
            {
                string displayName = !string.IsNullOrEmpty(module.DisplayName) ? module.DisplayName : module.RulesId;
                presentations[module.RulesId] = new UniversePresentation(
                    module.RulesId, displayName, module.CreatePresentationDefaults(), null, null);
            }
            return new LoadedCatalog(rules, presentations, fromAsset: false);
        }
    }
}
