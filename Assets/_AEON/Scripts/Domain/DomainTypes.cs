using System;

namespace GodGame.Domain
{
    /// <summary>
    /// Vector 3D puro (sustituye al Vector3 de Unity en el modelo). La presentación lo convierte
    /// a Vector3 con una extensión; el núcleo nunca depende del motor.
    /// </summary>
    [Serializable]
    public struct Vec3
    {
        public float x;
        public float y;
        public float z;

        public Vec3(float x, float y, float z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        public static Vec3 Zero => new Vec3(0f, 0f, 0f);

        /// <summary>Longitud al cuadrado (evita la raíz en comparaciones de distancia).</summary>
        public float SqrMagnitude => x * x + y * y + z * z;

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vec3 operator *(Vec3 a, float k) => new Vec3(a.x * k, a.y * k, a.z * k);
        public static Vec3 operator *(float k, Vec3 a) => new Vec3(a.x * k, a.y * k, a.z * k);

        public override string ToString() => $"({x}, {y}, {z})";
    }

    /// <summary>Color RGB lineal 0..1 (sustituye al Color de Unity en el modelo).</summary>
    [Serializable]
    public struct ColorRgb
    {
        public float r;
        public float g;
        public float b;

        public ColorRgb(float r, float g, float b)
        {
            this.r = r; this.g = g; this.b = b;
        }

        public override string ToString() => $"({r}, {g}, {b})";
    }

    /// <summary>Rango entero [min, max], ambos incluidos (sustituye a Vector2Int en los datos de generación).</summary>
    [Serializable]
    public struct IntRange
    {
        public int min;
        public int max;

        public IntRange(int min, int max)
        {
            this.min = min; this.max = max;
        }

        public override string ToString() => $"[{min}, {max}]";
    }
}
