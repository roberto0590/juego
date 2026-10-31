using UnityEngine;

namespace GodGame.Generation
{
    /// <summary>
    /// Componente de prueba/vista: genera la malla del planeta al iniciar y al cambiar parámetros
    /// en el Inspector. Úsalo con el material "M_PlanetSurface" (PlanetSurface.shader) y un hijo
    /// esfera un 4–6 % mayor con "M_PlanetAtmosphere" (PlanetAtmosphere.shader).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ProceduralPlanet : MonoBehaviour
    {
        [SerializeField] private int terrainSeed = 12345;
        [SerializeField, Range(8, 192)] private int resolution = 64;
        [SerializeField] private PlanetShapeSettings shape = PlanetShapeSettings.Default;
        [SerializeField, Tooltip("Rotación propia en grados/segundo (solo visual).")] private float spinSpeed = 4f;

        private Mesh _mesh;
        private bool _dirty = true;

        public void Configure(int seed, int meshResolution, PlanetShapeSettings settings)
        {
            terrainSeed = seed;
            resolution = meshResolution;
            shape = settings;
            _dirty = true;
        }

        private void OnValidate() => _dirty = true;

        private void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                _mesh = PlanetMeshGenerator.Generate(resolution, shape.WithSeed(terrainSeed), _mesh);
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
            if (Application.isPlaying)
                transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }
    }
}
