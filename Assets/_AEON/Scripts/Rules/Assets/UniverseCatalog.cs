using System;
using System.Collections.Generic;
using GodGame.Domain;
using UnityEngine;

namespace GodGame.Rules.Assets
{
    /// <summary>
    /// Catálogo de universos del build (D13). Vive en Assets/_AEON/Resources/AEON/UniverseCatalog.asset
    /// y referencia los UR_*.asset (y estos sus materiales), así todo entra en el build.
    /// Si falta, Core/UniverseCatalogLoader construye el catálogo con los módulos de universo.
    /// </summary>
    [CreateAssetMenu(menuName = "AEON/Catálogo de universos", fileName = "UniverseCatalog")]
    public sealed class UniverseCatalog : ScriptableObject
    {
        [Tooltip("Reglas de cada universo disponible. El orden no importa: se ordena por id.")]
        public List<UniverseRules> universes = new List<UniverseRules>();

        [Header("Materiales compartidos")]
        [Tooltip("Estrellas (URP Unlit).")] public Material starMaterial;
        [Tooltip("Líneas de órbita (URP Unlit).")] public Material orbitMaterial;
        [Tooltip("Superficie por defecto si un universo no tiene material propio.")] public Material defaultPlanetMaterial;
        [Tooltip("Atmósfera por defecto si un universo no tiene material propio.")] public Material defaultAtmosphereMaterial;

        /// <summary>Asset de reglas con ese id, o null.</summary>
        public UniverseRules Find(string id)
        {
            if (universes == null || id == null) return null;
            foreach (UniverseRules rules in universes)
            {
                if (rules != null && string.Equals(rules.Id, id, StringComparison.Ordinal)) return rules;
            }
            return null;
        }

        /// <summary>
        /// Copias saneadas de las reglas para la simulación, ordenadas por id (ordinal), sin entradas nulas.
        /// Si dos assets comparten id se conserva el primero y se avisa (GameState no admite ids repetidos).
        /// </summary>
        public List<UniverseRulesData> BuildRuntimeRules()
        {
            var result = new List<UniverseRulesData>();
            if (universes == null) return result;

            foreach (UniverseRules rules in universes)
            {
                if (rules == null || rules.Data == null || string.IsNullOrEmpty(rules.Id)) continue;
                string id = rules.Id;
                if (result.Exists(r => string.Equals(r.id, id, StringComparison.Ordinal)))
                {
                    Debug.LogWarning($"[AEON] El catálogo tiene dos reglas con el id '{id}'; se ignora '{rules.name}'.");
                    continue;
                }
                result.Add(rules.ToRuntimeData());
            }

            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }
    }
}
