using System;

namespace GodGame.Domain
{
    /// <summary>Tipo de energía dominante de un universo. Determina qué recurso es abundante y qué milagros se abaratan.</summary>
    public enum EnergyType
    {
        Gravitica,   // la energía proviene de la repulsión de masas
        Cronica,     // energía extraída de bucles temporales
        Dimensional, // energía de pliegues hiperespaciales
        Armonica,    // energía vibracional (sonido/cristal)
        Plasmatica,  // energía pura, casi sin materia estable
        Fluida,      // energía de corrientes de un éter líquido
        Onirica      // energía de la creencia colectiva
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
        // Economía
        public float faithGain;        // multiplicador de Fe generada (≥ 0)
        public float divineEnergyGain; // multiplicador de Energía Divina (≥ 0)
        public float matterGain;       // multiplicador de Materia (≥ 0)
        public float knowledgeGain;    // multiplicador de Conocimiento (≥ 0)

        // Acciones
        public float miracleCost;      // coste de milagros (≥ 0)
        public float militaryStrength; // fuerza de conquista militar (≥ 0)
        public float culturalSpread;   // eficacia de conquista cultural/religiosa (≥ 0)
        public float techProgress;     // eficacia de conquista tecnológica (≥ 0)
        public float travelCost;       // coste de alcanzar otros sistemas/universos (≥ 0)

        // Estabilidad
        public float civilizationStability; // 0..1; 1 = muy estable; bajo = rebeliones/herejías frecuentes

        public static GameplayModifiers Neutral => new GameplayModifiers
        {
            faithGain = 1f, divineEnergyGain = 1f, matterGain = 1f, knowledgeGain = 1f,
            miracleCost = 1f, militaryStrength = 1f, culturalSpread = 1f, techProgress = 1f,
            travelCost = 1f, civilizationStability = 0.8f
        };
    }

    /// <summary>
    /// Perfil de leyes físicas de un universo, en C# puro.
    /// Lo aporta cada módulo de universo (valores por defecto) y lo envuelve el ScriptableObject
    /// UniverseRules (capa Unity) como asset de diseño. La simulación trabaja siempre con una COPIA
    /// (Clone) de estos datos, nunca con el asset.
    /// </summary>
    [Serializable]
    public class UniverseRulesData
    {
        // ---------------- Identidad ----------------

        /// <summary>Id estable usado en guardados y en Addressables. No cambiar tras publicar.</summary>
        public string id = "nuevo";
        /// <summary>Nombre visible por defecto (la UI puede sustituirlo por la traducción de displayNameKey).</summary>
        public string displayName = "Nuevo universo";
        public string displayNameKey = "universe.nuevo.name"; // clave de Unity Localization
        public string designNotes = "";

        // ---------------- Leyes físicas ----------------

        /// <summary>1 = gravedad estándar. Negativo = gravedad invertida (las masas se repelen). Rango [-3, 3].</summary>
        public float gravityMultiplier = 1f;

        /// <summary>Velocidad relativa del tiempo. 2 = los ciclos avanzan el doble por turno. Rango [0,1, 4].</summary>
        public float timeFlowRate = 1f;

        public TimeModel timeModel = TimeModel.Lineal;

        /// <summary>Dimensiones espaciales. 3 = normal; 4 = adyacencias extra. Rango [2, 5].</summary>
        public int spatialDimensions = 3;

        /// <summary>Factor de velocidad de la luz. Bajo = la información llega con retraso. Rango [0,05, 10].</summary>
        public float lightSpeedFactor = 1f;

        public EnergyType energyType = EnergyType.Gravitica;

        /// <summary>0 = materia inexistente (energía pura), 1 = materia sólida estable.</summary>
        public float matterStability = 1f;

        /// <summary>Viscosidad del medio interestelar. 0 = vacío. Rango [0, 1].</summary>
        public float mediumViscosity = 0f;

        /// <summary>Cuánto la creencia altera la realidad. 0 = nada. Rango [0, 1].</summary>
        public float beliefToReality = 0f;

        // ---------------- Modificadores de gameplay ----------------

        public GameplayModifiers modifiers = GameplayModifiers.Neutral;

        // ---------------- Generación procedural ----------------

        public int galaxyCount = 2;                                  // rango [1, 6]
        public IntRange systemsPerGalaxy = new IntRange(6, 12);
        public IntRange planetsPerSystem = new IntRange(2, 7);
        public int spiralArms = 2;                                   // rango [1, 6]

        /// <summary>Pesos de aparición por tipo espectral O,B,A,F,G,K,M (no necesitan sumar 1).</summary>
        public float[] spectralWeights = { 0.01f, 0.03f, 0.06f, 0.12f, 0.18f, 0.25f, 0.35f };

        /// <summary>Ids de especies nativas que pueden aparecer (ver 04_diseno_de_personajes.md).</summary>
        public string[] nativeSpeciesIds = Array.Empty<string>();

        // ---------------- Dios nativo ----------------

        /// <summary>Id del dios rival que nace en este universo. Vacío = el generador usa "rival-" + id.</summary>
        public string nativeGodId = "";
        /// <summary>Nombre del dios nativo. Vacío = el generador usa "Dios de " + displayName.</summary>
        public string nativeGodName = "";
        public GodPersonality nativeGodPersonality = GodPersonality.Balanced;

        // ---------------- Contenido ----------------

        /// <summary>Clave Addressables del paquete de contenido de este universo (arte, audio, especies).</summary>
        public string addressablesLabel = "universe_nuevo";

        /// <summary>Gravedad efectiva (siempre positiva) usada en fórmulas orbitales.</summary>
        public float EffectiveGravity => MathUtil.Max(0.05f, MathF.Abs(gravityMultiplier));

        public bool IsGravityInverted => gravityMultiplier < 0f;

        /// <summary>
        /// Coste extra de viajar: más dimensiones = atajos (más barato);
        /// viscosidad y luz lenta = más caro.
        /// </summary>
        public float ComputeTravelCostFactor()
        {
            float dimensionFactor = 3f / spatialDimensions;            // 4D → 0.75
            float viscosityFactor = 1f + mediumViscosity;              // fluido → hasta 2x
            float lightFactor = 1f / MathF.Sqrt(lightSpeedFactor);     // luz lenta → más caro
            return modifiers.travelCost * dimensionFactor * viscosityFactor * lightFactor;
        }

        /// <summary>Copia profunda: los arrays se duplican para que modificar la copia no toque el original.</summary>
        public UniverseRulesData Clone()
        {
            var copy = (UniverseRulesData)MemberwiseClone();
            copy.spectralWeights = spectralWeights != null ? (float[])spectralWeights.Clone() : null;
            copy.nativeSpeciesIds = nativeSpeciesIds != null ? (string[])nativeSpeciesIds.Clone() : null;
            return copy;
        }

        /// <summary>
        /// Recorta cada valor a su rango válido (los mismos rangos que tenían los atributos [Range]
        /// del ScriptableObject original), fija 7 pesos espectrales y garantiza max ≥ min.
        /// </summary>
        public void Sanitize()
        {
            if (id == null) id = "";
            if (displayName == null) displayName = "";
            if (displayNameKey == null) displayNameKey = "";
            if (designNotes == null) designNotes = "";
            if (nativeGodId == null) nativeGodId = "";
            if (nativeGodName == null) nativeGodName = "";
            if (addressablesLabel == null) addressablesLabel = "";

            gravityMultiplier = MathUtil.Clamp(gravityMultiplier, -3f, 3f);
            timeFlowRate = MathUtil.Clamp(timeFlowRate, 0.1f, 4f);
            spatialDimensions = MathUtil.Clamp(spatialDimensions, 2, 5);
            lightSpeedFactor = MathUtil.Clamp(lightSpeedFactor, 0.05f, 10f);
            matterStability = MathUtil.Clamp01(matterStability);
            mediumViscosity = MathUtil.Clamp01(mediumViscosity);
            beliefToReality = MathUtil.Clamp01(beliefToReality);

            modifiers.faithGain = MathUtil.Max(0f, modifiers.faithGain);
            modifiers.divineEnergyGain = MathUtil.Max(0f, modifiers.divineEnergyGain);
            modifiers.matterGain = MathUtil.Max(0f, modifiers.matterGain);
            modifiers.knowledgeGain = MathUtil.Max(0f, modifiers.knowledgeGain);
            modifiers.miracleCost = MathUtil.Max(0f, modifiers.miracleCost);
            modifiers.militaryStrength = MathUtil.Max(0f, modifiers.militaryStrength);
            modifiers.culturalSpread = MathUtil.Max(0f, modifiers.culturalSpread);
            modifiers.techProgress = MathUtil.Max(0f, modifiers.techProgress);
            modifiers.travelCost = MathUtil.Max(0f, modifiers.travelCost);
            modifiers.civilizationStability = MathUtil.Clamp01(modifiers.civilizationStability);

            galaxyCount = MathUtil.Clamp(galaxyCount, 1, 6);
            spiralArms = MathUtil.Clamp(spiralArms, 1, 6);
            systemsPerGalaxy.min = MathUtil.Max(1, systemsPerGalaxy.min);
            systemsPerGalaxy.max = MathUtil.Max(systemsPerGalaxy.min, systemsPerGalaxy.max);
            planetsPerSystem.min = MathUtil.Max(1, planetsPerSystem.min);
            planetsPerSystem.max = MathUtil.Max(planetsPerSystem.min, planetsPerSystem.max);

            if (spectralWeights == null || spectralWeights.Length != 7)
            {
                Array.Resize(ref spectralWeights, 7);
            }
            if (nativeSpeciesIds == null) nativeSpeciesIds = Array.Empty<string>();

            nativeGodPersonality.aggression = MathUtil.Clamp01(nativeGodPersonality.aggression);
            nativeGodPersonality.piety = MathUtil.Clamp01(nativeGodPersonality.piety);
            nativeGodPersonality.curiosity = MathUtil.Clamp01(nativeGodPersonality.curiosity);
            nativeGodPersonality.diplomacy = MathUtil.Clamp01(nativeGodPersonality.diplomacy);
            nativeGodPersonality.greed = MathUtil.Clamp01(nativeGodPersonality.greed);
        }
    }
}
