using System;
using System.Collections.Generic;
using UnityEngine;

// Modelo de dominio puro: clases [Serializable] con campos públicos para que
// JsonUtility (o MessagePack) las serialice sin código extra.
// Reglas:
//  - Las referencias entre entidades se guardan como IDs (string), nunca como referencias
//    de objeto, para evitar ciclos al serializar.
//  - Nada de MonoBehaviour aquí: la presentación (vistas) lee este modelo, no al revés.
namespace GodGame.Domain
{
    public enum SpectralType { O, B, A, F, G, K, M }

    public enum PlanetType { Rocoso, Oceanico, Desertico, Helado, GiganteGaseoso, Volcanico, Exotico }

    public enum ConquestType { Militar, Cultural, Tecnologica }

    /// <summary>Raíz del estado del juego: hasta 7 universos.</summary>
    [Serializable]
    public class Multiverse
    {
        public const int MaxUniverses = 7;

        public int seed;
        public string playerGodId;
        public string playerStartUniverseId;
        public List<Universe> universes = new List<Universe>();
        public List<God> gods = new List<God>();

        public God FindGod(string godId) => gods.Find(g => g.id == godId);

        public Universe FindUniverse(string universeId) => universes.Find(u => u.id == universeId);

        public IEnumerable<Planet> AllPlanets()
        {
            foreach (var u in universes)
            foreach (var g in u.galaxies)
            foreach (var s in g.systems)
            foreach (var p in s.planets)
                yield return p;
        }
    }

    [Serializable]
    public class Universe
    {
        public string id;
        public string name;
        public int index;              // 0..6, orden en el multiverso
        public string rulesId;         // referencia a UniverseRules.id (el asset no se serializa)
        public string nativeGodId;     // dios rival que "nació" en este universo
        public List<Galaxy> galaxies = new List<Galaxy>();
    }

    [Serializable]
    public class Galaxy
    {
        public string id;
        public string name;
        public string universeId;
        public float radius;
        public List<StarSystem> systems = new List<StarSystem>();
    }

    [Serializable]
    public class StarSystem
    {
        public string id;
        public string name;
        public string galaxyId;
        public Vector3 galacticPosition;
        public Star star = new Star();
        public List<Planet> planets = new List<Planet>();
    }

    [Serializable]
    public class Star
    {
        public SpectralType spectralType;
        public float massSolar;     // masas solares
        public float radiusSolar;   // radios solares
        public float luminosity;    // luminosidades solares
        public float temperatureK;
        public Color color;
    }

    /// <summary>Parámetros orbitales keplerianos simplificados (ver StarSystemGenerator).</summary>
    [Serializable]
    public class OrbitParams
    {
        public float semiMajorAxis;   // UA
        public float eccentricity;    // 0..0.3 para mantener legibilidad
        public float inclinationDeg;
        public float periodTurns;     // turnos de juego por órbita completa
        public float phase;           // 0..1, posición inicial
    }

    [Serializable]
    public class Planet
    {
        public string id;
        public string name;
        public string systemId;
        public string universeId;     // desnormalizado para búsquedas rápidas
        public PlanetType type;
        public OrbitParams orbit = new OrbitParams();
        public float radius;          // radios terrestres
        public int terrainSeed;       // semilla para PlanetMeshGenerator
        public bool habitable;

        public string ownerGodId;     // vacío = sin dueño
        public int defense;           // resistencia a conquista militar
        public Civilization civilization = new Civilization();

        public bool HasOwner => !string.IsNullOrEmpty(ownerGodId);
    }

    /// <summary>
    /// Civilización mortal de un planeta. JsonUtility no admite null en clases anidadas,
    /// por eso "sin civilización" se representa con speciesId vacío.
    /// </summary>
    [Serializable]
    public class Civilization
    {
        public string speciesId;
        public string name;
        public float population;        // millones
        public int techLevel;           // 0 (tribal) .. 10 (trascendente)
        [Range(0f, 1f)] public float faith;      // devoción hacia worshippedGodId
        [Range(0f, 1f)] public float stability;
        [Range(0f, 1f)] public float freeWill;   // usado por el final "Trascendencia"
        public string worshippedGodId;

        public bool Exists => !string.IsNullOrEmpty(speciesId);
    }

    /// <summary>Pesos de personalidad de un dios (0..1). Los usa GodUtilityAI.</summary>
    [Serializable]
    public struct GodPersonality
    {
        [Range(0f, 1f)] public float aggression; // prefiere conquista militar
        [Range(0f, 1f)] public float piety;      // prefiere fe, milagros y conquista cultural
        [Range(0f, 1f)] public float curiosity;  // prefiere conocimiento y conquista tecnológica
        [Range(0f, 1f)] public float diplomacy;  // prefiere pactos (fase posterior)
        [Range(0f, 1f)] public float greed;      // acumula recursos antes de actuar

        public static GodPersonality Balanced => new GodPersonality
        {
            aggression = 0.5f, piety = 0.5f, curiosity = 0.5f, diplomacy = 0.5f, greed = 0.5f
        };
    }

    [Serializable]
    public struct ResourceCost
    {
        public float faith, divineEnergy, matter, knowledge;

        public ResourceCost(float faith, float divineEnergy, float matter, float knowledge)
        {
            this.faith = faith; this.divineEnergy = divineEnergy; this.matter = matter; this.knowledge = knowledge;
        }

        public ResourceCost Scaled(float k) => new ResourceCost(faith * k, divineEnergy * k, matter * k, knowledge * k);
    }

    /// <summary>Los 4 recursos del juego: Fe, Energía Divina, Materia, Conocimiento.</summary>
    [Serializable]
    public class ResourceWallet
    {
        public float faith;
        public float divineEnergy;
        public float matter;
        public float knowledge;

        public bool CanAfford(ResourceCost c)
            => faith >= c.faith && divineEnergy >= c.divineEnergy && matter >= c.matter && knowledge >= c.knowledge;

        public bool TrySpend(ResourceCost c)
        {
            if (!CanAfford(c)) return false;
            faith -= c.faith; divineEnergy -= c.divineEnergy; matter -= c.matter; knowledge -= c.knowledge;
            return true;
        }

        public void Add(ResourceCost c)
        {
            faith += c.faith; divineEnergy += c.divineEnergy; matter += c.matter; knowledge += c.knowledge;
        }
    }

    [Serializable]
    public class God
    {
        public string id;
        public string name;
        public string homeUniverseId;
        public string homePlanetId;    // el planeta asignado que debe conquistar primero
        public bool isPlayer;
        public bool defeated;
        public GodPersonality personality = GodPersonality.Balanced;
        public ResourceWallet resources = new ResourceWallet();
        public List<string> unlockedUniverseIds = new List<string>(); // universos a los que puede viajar
    }
}
