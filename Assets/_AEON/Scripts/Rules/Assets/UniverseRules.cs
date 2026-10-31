using GodGame.Domain;
using GodGame.Rules;
using UnityEngine;

namespace GodGame.Rules.Assets
{
    /// <summary>
    /// Asset de diseño de un universo (D3): envuelve los datos puros (UniverseRulesData) y su presentación
    /// por defecto, y añade los materiales propios. La simulación nunca usa este asset directamente:
    /// recibe una copia saneada con ToRuntimeData().
    /// El configurador (menú AEON/Configurar proyecto) crea un UR_&lt;Nombre&gt;.asset por módulo con Initialize().
    /// </summary>
    [CreateAssetMenu(menuName = "AEON/Reglas de universo", fileName = "UR_NuevoUniverso")]
    public sealed class UniverseRules : ScriptableObject
    {
        [SerializeField] private UniverseRulesData data = new UniverseRulesData();
        [SerializeField] private UniversePresentationDefaults presentation = new UniversePresentationDefaults();

        [Header("Materiales (opcionales: si faltan se usan los del catálogo o Shader.Find)")]
        [Tooltip("Material de superficie (shader GodGame/PlanetSurface). Se instancia una copia por planeta.")]
        public Material planetMaterial;
        [Tooltip("Material de atmósfera (shader GodGame/PlanetAtmosphere). Se instancia una copia por planeta.")]
        public Material atmosphereMaterial;

        /// <summary>Id estable de las reglas (UniverseRulesData.id).</summary>
        public string Id => data != null ? data.id : null;

        /// <summary>Datos del asset (solo lectura: para modificar, editar el asset en el Inspector).</summary>
        public UniverseRulesData Data => data;

        /// <summary>Paleta, rampa de biomas y parámetros visuales del universo.</summary>
        public UniversePresentationDefaults Presentation => presentation;

        /// <summary>Copia profunda y saneada para la simulación: modificarla no cambia el asset.</summary>
        public UniverseRulesData ToRuntimeData()
        {
            UniverseRulesData copy = (data ?? new UniverseRulesData()).Clone();
            copy.Sanitize();
            return copy;
        }

        /// <summary>Rellena el asset con los valores por defecto de un módulo (lo usa el configurador de Editor).</summary>
        public void Initialize(UniverseRulesData rules, UniversePresentationDefaults presentationDefaults)
        {
            data = rules != null ? rules.Clone() : new UniverseRulesData();
            data.Sanitize();
            presentation = presentationDefaults != null ? presentationDefaults.Clone() : new UniversePresentationDefaults();
        }

        private void OnValidate()
        {
            data?.Sanitize();
        }
    }
}
