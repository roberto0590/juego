using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GodGame.Presentation
{
    /// <summary>Parámetros de forma del terreno. Blittable para poder pasarlo a un job Burst.</summary>
    [Serializable]
    public struct PlanetShapeSettings
    {
        [Tooltip("Radio base en unidades de Unity.")] public float radius;
        [Tooltip("Altura máxima del relieve como fracción del radio (0.02–0.1 se ve bien en móvil).")]
        public float elevationScale;
        [Tooltip("Frecuencia base del ruido.")] public float frequency;
        [Range(1, 8)] public int octaves;
        [Tooltip("Multiplicador de frecuencia por octava (≈2).")] public float lacunarity;
        [Tooltip("Multiplicador de amplitud por octava (≈0.5).")] public float persistence;
        [Tooltip("Nivel del mar (0..1). Por debajo, el terreno se aplana como océano.")]
        [Range(0f, 1f)] public float seaLevel;
        [Tooltip("Desplazamiento del ruido; se deriva de Planet.terrainSeed.")] public float3 noiseOffset;

        public static PlanetShapeSettings Default => new PlanetShapeSettings
        {
            radius = 1f, elevationScale = 0.06f, frequency = 1.2f, octaves = 5,
            lacunarity = 2f, persistence = 0.5f, seaLevel = 0.45f, noiseOffset = float3.zero
        };

        /// <summary>Convierte la semilla del planeta en un offset de ruido (cada planeta, un relieve distinto).</summary>
        public PlanetShapeSettings WithSeed(int terrainSeed)
        {
            var rng = new Unity.Mathematics.Random((uint)terrainSeed | 1u); // la semilla no puede ser 0
            var copy = this;
            copy.noiseOffset = rng.NextFloat3(-1000f, 1000f);
            return copy;
        }
    }

    /// <summary>
    /// Malla de planeta como ESFERA CUBO-NORMALIZADA:
    ///   6 caras de un cubo subdivididas en una rejilla res×res, cada vértice normalizado → esfera.
    ///   Ventajas frente a UV-sphere: densidad de vértices casi uniforme (sin polos pinchados)
    ///   y fácil de dividir en "chunks" por cara para LOD.
    /// El relieve se calcula con fBm (suma de octavas de ruido simplex 3D) en un job Burst paralelo.
    ///
    /// Salida extra: UV0 = (elevación normalizada 0..1, latitud 0..1). El shader de superficie
    /// usa UV0.x para escoger bioma en una textura de gradiente (ver PlanetSurface.shader) y UV0.y
    /// para casquetes polares. Así no hacen falta texturas grandes por planeta.
    /// </summary>
    public static class PlanetMeshGenerator
    {
        private const int UnitSphereResolution = 24;
        private static Mesh _unitSphere;

        /// <summary>
        /// Esfera lisa de 1 de diámetro (como la primitiva de Unity), compartida por estrellas y atmósferas.
        /// Sustituye a GameObject.CreatePrimitive, que añade un SphereCollider y puede fallar en IL2CPP
        /// si el stripping del motor elimina esa clase. Normales analíticas: sin costuras entre caras.
        /// </summary>
        public static Mesh UnitSphere
        {
            get
            {
                if (_unitSphere != null) return _unitSphere;
                PlanetShapeSettings shape = PlanetShapeSettings.Default;
                shape.radius = 0.5f;
                shape.elevationScale = 0f;
                shape.octaves = 1;
                Mesh mesh = Generate(UnitSphereResolution, shape);
                mesh.name = "EsferaUnidad";
                Vector3[] vertices = mesh.vertices;
                var normals = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) normals[i] = vertices[i].normalized;
                mesh.normals = normals;
                _unitSphere = mesh;
                return mesh;
            }
        }

        /// <summary>GameObject con MeshFilter (UnitSphere) y MeshRenderer, sin collider ni sombras.</summary>
        public static MeshRenderer CreateSphereObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = UnitSphere;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return meshRenderer;
        }

        /// <param name="resolution">Vértices por arista de cada cara. 24 (lejos) / 64 (medio) / 128 (cerca).</param>
        public static Mesh Generate(int resolution, PlanetShapeSettings settings, Mesh reuse = null)
        {
            resolution = Mathf.Clamp(resolution, 2, 255);
            int vertsPerFace = resolution * resolution;
            int vertexCount = vertsPerFace * 6;

            var positions = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var uvs = new NativeArray<float2>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            var job = new CubeSphereJob
            {
                resolution = resolution,
                settings = settings,
                positions = positions,
                uvs = uvs
            };
            // 64 vértices por lote de trabajo: buen equilibrio en CPUs móviles de 4–8 núcleos.
            job.Schedule(vertexCount, 64).Complete();

            int[] triangles = BuildTriangles(resolution);

            Mesh mesh = reuse != null ? reuse : new Mesh { name = "ProceduralPlanet" };
            mesh.Clear();
            mesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            // Nota: las aristas entre caras duplican vértices → leves costuras de normales.
            // Con estilo low-poly/toon apenas se notan; para eliminarlas, calcular la normal
            // analíticamente en el job (gradiente del ruido) en lugar de RecalculateNormals.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            positions.Dispose();
            uvs.Dispose();
            return mesh;
        }

        /// <summary>Índices de triángulos: idénticos para cualquier planeta de la misma resolución (cacheables).</summary>
        private static int[] BuildTriangles(int res)
        {
            int quadsPerFace = (res - 1) * (res - 1);
            var tris = new int[quadsPerFace * 6 * 6];
            int t = 0;
            for (int face = 0; face < 6; face++)
            {
                int offset = face * res * res;
                for (int y = 0; y < res - 1; y++)
                for (int x = 0; x < res - 1; x++)
                {
                    int i = offset + x + y * res;
                    tris[t++] = i;
                    tris[t++] = i + res + 1;
                    tris[t++] = i + res;
                    tris[t++] = i;
                    tris[t++] = i + 1;
                    tris[t++] = i + res + 1;
                }
            }
            return tris;
        }

        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        private struct CubeSphereJob : IJobParallelFor
        {
            public int resolution;
            public PlanetShapeSettings settings;
            [WriteOnly] public NativeArray<float3> positions;
            [WriteOnly] public NativeArray<float2> uvs;

            public void Execute(int index)
            {
                int perFace = resolution * resolution;
                int face = index / perFace;
                int local = index - face * perFace;
                int x = local % resolution;
                int y = local / resolution;

                GetFaceAxes(face, out float3 up, out float3 axisA, out float3 axisB);
                float2 percent = new float2(x, y) / (resolution - 1);
                float3 pointOnCube = up + (percent.x - 0.5f) * 2f * axisA + (percent.y - 0.5f) * 2f * axisB;
                float3 pointOnSphere = math.normalize(pointOnCube);

                float n = Fbm(pointOnSphere * settings.frequency + settings.noiseOffset); // ≈ 0..1
                // Océanos: todo lo que queda bajo el nivel del mar se aplana.
                float land = math.max(0f, n - settings.seaLevel) / math.max(1e-4f, 1f - settings.seaLevel);
                float elevation = settings.radius * (1f + land * settings.elevationScale);

                positions[index] = pointOnSphere * elevation;
                float latitude = math.abs(pointOnSphere.y); // 0 ecuador, 1 polos
                uvs[index] = new float2(n, latitude);
            }

            /// <summary>fBm: suma de octavas de ruido simplex con frecuencia creciente y amplitud decreciente.</summary>
            private float Fbm(float3 p)
            {
                float sum = 0f, amplitude = 1f, frequency = 1f, norm = 0f;
                for (int o = 0; o < settings.octaves; o++)
                {
                    sum += noise.snoise(p * frequency) * amplitude; // snoise ∈ [-1,1]
                    norm += amplitude;
                    amplitude *= settings.persistence;
                    frequency *= settings.lacunarity;
                }
                return sum / norm * 0.5f + 0.5f;
            }

            /// <summary>Ejes de cada cara del cubo: up = normal de la cara, A y B = ejes de la rejilla.</summary>
            private static void GetFaceAxes(int face, out float3 up, out float3 axisA, out float3 axisB)
            {
                switch (face)
                {
                    case 0: up = new float3(0, 1, 0); break;
                    case 1: up = new float3(0, -1, 0); break;
                    case 2: up = new float3(-1, 0, 0); break;
                    case 3: up = new float3(1, 0, 0); break;
                    case 4: up = new float3(0, 0, 1); break;
                    default: up = new float3(0, 0, -1); break;
                }
                axisA = new float3(up.y, up.z, up.x);
                axisB = math.cross(up, axisA);
            }
        }
    }
}
