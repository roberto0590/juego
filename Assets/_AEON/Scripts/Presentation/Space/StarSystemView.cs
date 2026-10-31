using System.Collections.Generic;
using GodGame.Core;
using GodGame.Domain;
using GodGame.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace GodGame.Presentation
{
    /// <summary>
    /// Vista de un sistema solar: estrella en el origen, planetas en órbita (OrbitMath) y una línea por órbita.
    /// Distancia visual = 3 + 5·sqrt(a) en la dirección real de la órbita, para que los planetas lejanos quepan en pantalla.
    /// </summary>
    public sealed class StarSystemView : MonoBehaviour
    {
        public const int PlanetResolution = 48;
        private const int OrbitSegments = 96;
        private const float OrbitWidth = 0.03f;
        private const float TurnsPerSecond = 0.02f;

        public static readonly Color PlayerTint = new Color(1f, 0.8f, 0.3f) * 2f;
        public static readonly Color RivalTint = new Color(1f, 0.25f, 0.2f) * 2f;

        private readonly List<PlanetView> _views = new List<PlanetView>();
        private readonly List<Planet> _planets = new List<Planet>();
        private GameState _state;
        private float _wobble;
        private Material _starMaterial;
        private Material _orbitMaterial;

        public Transform Star { get; private set; }
        public StarSystem SystemData { get; private set; }

        /// <summary>Radio que abarca todo el sistema (para la vista de sistema de la cámara).</summary>
        public float SystemExtent { get; private set; }

        public IReadOnlyList<PlanetView> PlanetViews => _views;

        public void Build(StarSystem system, Universe universe, UniversePresentation presentation, LoadedCatalog catalog, GameState state)
        {
            SystemData = system;
            _state = state;
            _wobble = presentation.Defaults.orbitWobble;
            name = "Sistema " + system.name + " (" + universe.id + ")";

            BuildStar(system, catalog);

            _orbitMaterial = MaterialFactory.Create("Orbita", catalog.OrbitMaterial, null,
                "Universal Render Pipeline/Unlit", "Sprites/Default");
            Color orbitColor = presentation.Defaults.secondaryColor.ToColor() * 0.6f;
            orbitColor.a = 1f;
            MaterialFactory.SetBaseColor(_orbitMaterial, orbitColor);

            float extent = 2f;
            for (int i = 0; i < system.planets.Count; i++)
            {
                Planet planet = system.planets[i];
                var go = new GameObject();
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<PlanetView>();
                view.Build(planet, presentation, catalog, PlanetResolution);
                _views.Add(view);
                _planets.Add(planet);

                CreateOrbitLine(planet);
                extent = Mathf.Max(extent, VisualDistance(planet.orbit.semiMajorAxis) + view.VisualRadius);
            }
            SystemExtent = extent;
            UpdatePlanetPositions();
        }

        public PlanetView GetPlanetView(string planetId)
        {
            for (int i = 0; i < _views.Count; i++)
                if (_views[i].PlanetId == planetId) return _views[i];
            return null;
        }

        /// <summary>Luces de ciudad según el dueño: jugador dorado, rival rojo, sin dueño apagadas.</summary>
        public void RefreshOwners(GameState state, string playerGodId)
        {
            for (int i = 0; i < _planets.Count; i++)
            {
                Planet planet = state.GetPlanet(_planets[i].id) ?? _planets[i];
                Color tint = !planet.HasOwner ? Color.black
                    : planet.ownerGodId == playerGodId ? PlayerTint
                    : RivalTint;
                _views[i].SetOwnerTint(tint);
            }
        }

        private void Update()
        {
            UpdatePlanetPositions();
        }

        private void UpdatePlanetPositions()
        {
            if (_state == null) return;
            float t = _state.Turn + Time.time * TurnsPerSecond;
            for (int i = 0; i < _planets.Count; i++)
            {
                OrbitParams orbit = _planets[i].orbit;
                Vector3 position = ToView(OrbitMath.EvaluateOrbitPosition(orbit, t), orbit.semiMajorAxis);
                position.y += Mathf.Sin(Time.time + i) * _wobble;
                _views[i].transform.localPosition = position;
            }
        }

        private void BuildStar(StarSystem system, LoadedCatalog catalog)
        {
            MeshRenderer meshRenderer = PlanetMeshGenerator.CreateSphereObject("Estrella " + system.name, transform);
            Transform star = meshRenderer.transform;
            star.localScale = Vector3.one * 1.5f;

            _starMaterial = MaterialFactory.Create("Estrella", catalog.StarMaterial, null,
                "Universal Render Pipeline/Unlit", "Unlit/Color");
            if (_starMaterial != null)
            {
                MaterialFactory.SetBaseColor(_starMaterial, system.star.color.ToColor());
                meshRenderer.sharedMaterial = _starMaterial;
            }
            Star = star;
        }

        private void CreateOrbitLine(Planet planet)
        {
            var go = new GameObject("Orbita " + planet.name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = OrbitWidth;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (_orbitMaterial != null) line.sharedMaterial = _orbitMaterial;

            var points = new Vector3[OrbitSegments];
            OrbitParams orbit = planet.orbit;
            for (int k = 0; k < OrbitSegments; k++)
            {
                float t = orbit.periodTurns * k / OrbitSegments;
                points[k] = ToView(OrbitMath.EvaluateOrbitPosition(orbit, t), orbit.semiMajorAxis);
            }
            line.positionCount = OrbitSegments;
            line.SetPositions(points);
        }

        private static float VisualDistance(float semiMajorAxis) => 3f + 5f * Mathf.Sqrt(Mathf.Max(0f, semiMajorAxis));

        private static Vector3 ToView(Vec3 orbitPosition, float semiMajorAxis)
        {
            Vector3 p = orbitPosition.ToVector3();
            if (p.sqrMagnitude < 1e-8f) return Vector3.zero;
            return p.normalized * VisualDistance(semiMajorAxis);
        }

        private void OnDestroy()
        {
            if (_starMaterial != null) Destroy(_starMaterial);
            if (_orbitMaterial != null) Destroy(_orbitMaterial);
        }
    }
}
