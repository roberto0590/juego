using GodGame.Core;
using GodGame.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace GodGame.Presentation
{
    /// <summary>
    /// Vista de un planeta: superficie procedural (ProceduralPlanet + PlanetSurface.shader) con la rampa
    /// de biomas de su universo y una esfera de atmósfera (PlanetAtmosphere.shader).
    /// Cada planeta tiene su propia instancia de material (nivel del mar y luces de ciudad propios).
    /// </summary>
    public sealed class PlanetView : MonoBehaviour
    {
        private static readonly int BiomeRampId = Shader.PropertyToID("_BiomeRamp");
        private static readonly int SeaLevelId = Shader.PropertyToID("_SeaLevel");
        private static readonly int CityLightsId = Shader.PropertyToID("_CityLights");
        private static readonly int AtmosphereColorId = Shader.PropertyToID("_AtmosphereColor");

        private Material _surfaceMaterial;
        private Material _atmosphereMaterial;

        public string PlanetId { get; private set; }

        /// <summary>Radio en unidades de escena (la cámara lo usa para encuadrar).</summary>
        public float VisualRadius { get; private set; }

        /// <summary>Radio visual = Clamp(0,25 + 0,25·log2(1 + radio terrestre), 0,25, 1,2).</summary>
        public static float ComputeVisualRadius(float radius)
            => Mathf.Clamp(0.25f + 0.25f * Mathf.Log(1f + Mathf.Max(0f, radius), 2f), 0.25f, 1.2f);

        /// <summary>Nivel del mar por tipo de planeta (también lo recibe el shader en _SeaLevel).</summary>
        public static float SeaLevelFor(PlanetType type)
        {
            switch (type)
            {
                case PlanetType.Oceanico: return 0.62f;
                case PlanetType.Rocoso: return 0.45f;
                case PlanetType.Desertico: return 0.25f;
                case PlanetType.Helado: return 0.40f;
                case PlanetType.GiganteGaseoso: return 0f;
                case PlanetType.Volcanico: return 0.35f;
                default: return 0.5f; // Exotico
            }
        }

        public void Build(Planet planet, UniversePresentation presentation, LoadedCatalog catalog, int resolution)
        {
            PlanetId = planet.id;
            VisualRadius = ComputeVisualRadius(planet.radius);
            name = "Planeta " + planet.name;

            BuildSurface(planet, presentation, catalog, resolution);
            BuildAtmosphere(presentation, catalog);
            SetOwnerTint(Color.black);
        }

        /// <summary>Luces nocturnas de la superficie (_CityLights): indican el dueño del planeta.</summary>
        public void SetOwnerTint(Color color)
        {
            if (_surfaceMaterial != null && _surfaceMaterial.HasProperty(CityLightsId))
                _surfaceMaterial.SetColor(CityLightsId, color);
        }

        private void BuildSurface(Planet planet, UniversePresentation presentation, LoadedCatalog catalog, int resolution)
        {
            var surface = new GameObject("Superficie");
            surface.transform.SetParent(transform, false);
            ProceduralPlanet procedural = surface.AddComponent<ProceduralPlanet>(); // añade MeshFilter y MeshRenderer

            PlanetShapeSettings shape = PlanetShapeSettings.Default;
            shape.radius = VisualRadius;
            shape.seaLevel = SeaLevelFor(planet.type);
            if (planet.type == PlanetType.GiganteGaseoso) shape.elevationScale = 0f;
            procedural.Configure(planet.terrainSeed, resolution, shape);

            _surfaceMaterial = MaterialFactory.Create("Superficie " + planet.name,
                presentation.PlanetMaterial, catalog.DefaultPlanetMaterial,
                "GodGame/PlanetSurface", "Universal Render Pipeline/Lit");

            var meshRenderer = surface.GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            if (_surfaceMaterial == null) return;

            if (_surfaceMaterial.HasProperty(BiomeRampId))
            {
                _surfaceMaterial.SetTexture(BiomeRampId, BiomeRampCache.Get(presentation.RulesId, presentation.Defaults));
                _surfaceMaterial.SetFloat(SeaLevelId, shape.seaLevel);
            }
            else
            {
                // Respaldo sin el shader propio (p. ej. URP Lit): color primario del universo.
                MaterialFactory.SetBaseColor(_surfaceMaterial, presentation.Defaults.primaryColor.ToColor());
            }
            meshRenderer.sharedMaterial = _surfaceMaterial;
        }

        private void BuildAtmosphere(UniversePresentation presentation, LoadedCatalog catalog)
        {
            // Sin el shader de atmósfera no se crea la esfera: un material opaco taparía el planeta.
            _atmosphereMaterial = MaterialFactory.Create("Atmosfera " + PlanetId,
                presentation.AtmosphereMaterial, catalog.DefaultAtmosphereMaterial, "GodGame/PlanetAtmosphere");
            if (_atmosphereMaterial == null) return;

            _atmosphereMaterial.SetColor(AtmosphereColorId, presentation.Defaults.atmosphereColor.ToColor());

            MeshRenderer meshRenderer = PlanetMeshGenerator.CreateSphereObject("Atmosfera", transform);
            // La esfera unidad mide 1 de diámetro: 2,1·radio = halo un 5 % mayor que el planeta.
            meshRenderer.transform.localScale = Vector3.one * (2.1f * VisualRadius);
            meshRenderer.sharedMaterial = _atmosphereMaterial;
        }

        private void OnDestroy()
        {
            if (_surfaceMaterial != null) Destroy(_surfaceMaterial);
            if (_atmosphereMaterial != null) Destroy(_atmosphereMaterial);
        }
    }
}
