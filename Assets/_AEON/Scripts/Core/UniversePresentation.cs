using GodGame.Rules;
using UnityEngine;

namespace GodGame.Core
{
    /// <summary>
    /// Presentación de un universo en runtime: paleta y rampa (datos puros del módulo o del asset)
    /// más los materiales propios del asset UR_*. Los materiales pueden ser null: las vistas
    /// recurren entonces a los del catálogo o a Shader.Find.
    /// </summary>
    public sealed class UniversePresentation
    {
        public UniversePresentation(string rulesId, string displayName, UniversePresentationDefaults defaults,
                                    Material planetMaterial, Material atmosphereMaterial)
        {
            RulesId = rulesId;
            DisplayName = displayName;
            Defaults = defaults ?? new UniversePresentationDefaults();
            PlanetMaterial = planetMaterial;
            AtmosphereMaterial = atmosphereMaterial;
        }

        public string RulesId { get; }
        public string DisplayName { get; }
        public UniversePresentationDefaults Defaults { get; }
        public Material PlanetMaterial { get; }
        public Material AtmosphereMaterial { get; }
    }
}
