using System;
using UnityEngine;

namespace GodGame.Domain
{
    /// <summary>Tipo de energía dominante de un universo. Determina qué recurso es abundante y qué milagros se abaratan.</summary>
    public enum EnergyType
    {
        Gravitica,   // Ascendia: la energía proviene de la repulsión de masas
        Cronica,     // Cronalis: energía extraída de bucles temporales
        Dimensional, // Teseracto: energía de pliegues hiperespaciales
        Armonica,    // Resonancia: energía vibracional (sonido/cristal)
        Plasmatica,  // Fulgor: energía pura, casi sin materia estable
        Fluida,      // Thalassa: energía de corrientes del éter líquido
        Onirica      // Oniria: energía de la creencia colectiva
    }

    /// <summary>Cómo fluye el tiempo en el universo.</summary>
    public enum TimeModel
    {
        Lineal,      // pasado → futuro, normal
        Ramificado,  // permite "ecos": deshacer/anticipar acciones con coste
        Ciclico      // los eventos se repiten cada N turnos
    }

    /// <summary>
    /// Modificadores de gameplay derivados de las leyes físicas.
    /// Todos son multiplicadores (1 = neutro). La simulación los lee; nunca los escribe.
    /// </summary>
    [Serializable]
    public struct GameplayModifiers
    {
        [Header("Economía")]
        [Min(0f)] public float faithGain;        // multiplicador de Fe generada
        [Min(0f)] public float divineEnergyGain; // multiplicador de Energía Divina
        [Min(0f)] public float matterGain;       // multiplicador de Materia
        [Min(0f)] public float knowledgeGain;    // multiplicador de Conocimiento

        [Header("Acciones")]
        [Min(0f)] public float miracleCost;      // coste de milagros
        [Min(0f)] public float militaryStrength; // fuerza de conquista militar
        [Min(0f)] public float culturalSpread;   // eficacia de conquista cultural/religiosa
        [Min(0f)] public float techProgress;     // eficacia de conquista tecnológica
        [Min(0f)] public float travelCost;       // coste de alcanzar otros sistemas/universos

        [Header("Estabilidad")]
        [Range(0f, 1f)] public float civilizationStability; // 1 = muy estable; bajo = rebeliones/herejías frecuentes

        public static GameplayModifiers Neutral => new GameplayModifiers
        {
            faithGain = 1f, divineEnergyGain = 1f, matterGain = 1f, knowledgeGain = 1f,
            miracleCost = 1f, militaryStrength = 1f, culturalSpread = 1f, techProgress = 1f,
            travelCost = 1f, civilizationStability = 0.8f
        };
    }

    /// <summary>
    /// Perfil de leyes físicas de un universo. Es un asset de datos (ScriptableObject):
    /// los diseñadores crean uno por universo en Assets/_Game/Data/Universes/.
    /// La generación procedural y la simulación lo consultan, pero no lo modifican en runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "GodGame/Universe Rules", fileName = "UR_NuevoUniverso")]
    public class UniverseRules : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Id estable usado en guardados y en Addressables (p. ej. 'ascendia'). No cambiar tras publicar.")]
        public string id = "nuevo";
        public string displayNameKey = "universe.nuevo.name"; // clave de Unity Localization
        [TextArea] public string designNotes;

        [Header("Leyes físicas")]
        [Tooltip("1 = gravedad estándar. Negativo = gravedad invertida (las masas se repelen).")]
        [Range(-3f, 3f)] public float gravityMultiplier = 1f;

        [Tooltip("Velocidad relativa del tiempo. 2 = los ciclos (órbitas, crecimiento) avanzan el doble por turno.")]
        [Range(0.1f, 4f)] public float timeFlowRate = 1f;

        public TimeModel timeModel = TimeModel.Lineal;

        [Tooltip("Dimensiones espaciales. 3 = normal; 4 = adyacencias extra y proyecciones visuales.")]
        [Range(2, 5)] public int spatialDimensions = 3;

        [Tooltip("Factor de velocidad de la luz. Bajo = la información (visión, diplomacia) llega con retraso.")]
        [Range(0.05f, 10f)] public float lightSpeedFactor = 1f;

        public EnergyType energyType = EnergyType.Gravitica;

        [Tooltip("0 = materia inexistente (energía pura), 1 = materia sólida estable.")]
        [Range(0f, 1f)] public float matterStability = 1f;

        [Tooltip("Viscosidad del medio interestelar (Thalassa). 0 = vacío.")]
        [Range(0f, 1f)] public float mediumViscosity = 0f;

        [Tooltip("Cuánto la creencia altera la realidad (Oniria). 0 = nada.")]
        [Range(0f, 1f)] public float beliefToReality = 0f;

        [Header("Modificadores de gameplay")]
        public GameplayModifiers modifiers = GameplayModifiers.Neutral;

        [Header("Generación procedural")]
        [Range(1, 6)] public int galaxyCount = 2;
        public Vector2Int systemsPerGalaxy = new Vector2Int(6, 12);
        public Vector2Int planetsPerSystem = new Vector2Int(2, 7);
        [Range(1, 6)] public int spiralArms = 2;
        [Tooltip("Pesos de aparición por tipo espectral O,B,A,F,G,K,M (no necesitan sumar 1).")]
        public float[] spectralWeights = { 0.01f, 0.03f, 0.06f, 0.12f, 0.18f, 0.25f, 0.35f };

        [Tooltip("Ids de especies nativas que pueden aparecer (ver 04_diseno_de_personajes.md).")]
        public string[] nativeSpeciesIds = Array.Empty<string>();

        [Header("Presentación")]
        public Color primaryColor = Color.cyan;
        public Color secondaryColor = Color.magenta;
        public Color nebulaColor = new Color(0.2f, 0.1f, 0.4f);
        [Tooltip("Clave Addressables del paquete de contenido de este universo (arte, audio, especies).")]
        public string addressablesLabel = "universe_nuevo";

        /// <summary>Gravedad efectiva (siempre positiva) usada en fórmulas orbitales.</summary>
        public float EffectiveGravity => Mathf.Max(0.05f, Mathf.Abs(gravityMultiplier));

        public bool IsGravityInverted => gravityMultiplier < 0f;

        /// <summary>
        /// Coste extra de viajar: más dimensiones = atajos (más barato);
        /// viscosidad y luz lenta = más caro.
        /// </summary>
        public float ComputeTravelCostFactor()
        {
            float dimensionFactor = 3f / spatialDimensions;            // 4D → 0.75
            float viscosityFactor = 1f + mediumViscosity;              // fluido → hasta 2x
            float lightFactor = 1f / Mathf.Sqrt(lightSpeedFactor);     // luz lenta → más caro
            return modifiers.travelCost * dimensionFactor * viscosityFactor * lightFactor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (spectralWeights == null || spectralWeights.Length != 7)
            {
                Array.Resize(ref spectralWeights, 7);
            }
            systemsPerGalaxy.y = Mathf.Max(systemsPerGalaxy.x, systemsPerGalaxy.y);
            planetsPerSystem.y = Mathf.Max(planetsPerSystem.x, planetsPerSystem.y);
        }
#endif
    }
}
