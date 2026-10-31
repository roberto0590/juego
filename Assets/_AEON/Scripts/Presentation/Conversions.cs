using GodGame.Domain;
using UnityEngine;

namespace GodGame.Presentation
{
    /// <summary>Conversión de los tipos puros del dominio a los del motor (solo en la capa de presentación).</summary>
    public static class Conversions
    {
        public static Vector3 ToVector3(this Vec3 v) => new Vector3(v.x, v.y, v.z);

        public static Color ToColor(this ColorRgb c, float a = 1f) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>
    /// Creación de materiales en runtime: copia del material de plantilla (asset) o, si no hay,
    /// material nuevo con el primer shader que exista (Shader.Find). Devuelve null si no hay ninguno.
    /// </summary>
    internal static class MaterialFactory
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static Material Create(string name, Material primary, Material secondary, params string[] shaderNames)
        {
            // Comparación explícita con null: respeta el "null" de Unity para referencias perdidas.
            Material template = primary != null ? primary : secondary;
            if (template != null) return new Material(template) { name = name };

            foreach (string shaderName in shaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null) return new Material(shader) { name = name };
            }
            return null;
        }

        /// <summary>Color principal del material (URP usa _BaseColor; los shaders integrados, _Color).</summary>
        public static void SetBaseColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            else if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }
    }
}
