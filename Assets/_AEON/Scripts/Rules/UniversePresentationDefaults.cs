using System;
using GodGame.Domain;

namespace GodGame.Rules
{
    /// <summary>Parada de la rampa de biomas: altura normalizada (0 = fondo oceánico, 1 = cumbres) y su color.</summary>
    [Serializable]
    public struct GradientStop
    {
        public float time;
        public ColorRgb color;

        public GradientStop(float time, ColorRgb color)
        {
            this.time = time; this.color = color;
        }
    }

    /// <summary>
    /// Presentación por defecto de un universo, en C# puro (colores RGB 0..1).
    /// La capa Unity la convierte a Color/Texture2D (rampa de biomas) y a parámetros de materiales.
    /// </summary>
    [Serializable]
    public class UniversePresentationDefaults
    {
        public ColorRgb primaryColor = new ColorRgb(0f, 1f, 1f);
        public ColorRgb secondaryColor = new ColorRgb(1f, 0f, 1f);
        /// <summary>Color de nebulosa; el fondo de cámara usa este color × 0,35.</summary>
        public ColorRgb nebulaColor = new ColorRgb(0.2f, 0.1f, 0.4f);
        public ColorRgb atmosphereColor = new ColorRgb(0.5f, 0.7f, 1f);
        public ColorRgb cityLightsColor = new ColorRgb(1f, 0.85f, 0.5f);
        /// <summary>Amplitud del vaivén vertical de las órbitas en la vista (0 = órbitas planas).</summary>
        public float orbitWobble;
        /// <summary>Rampa de biomas ordenada por time ascendente.</summary>
        public GradientStop[] biomeStops = Array.Empty<GradientStop>();

        /// <summary>Copia profunda (duplica el array de paradas).</summary>
        public UniversePresentationDefaults Clone()
        {
            var copy = (UniversePresentationDefaults)MemberwiseClone();
            copy.biomeStops = biomeStops != null ? (GradientStop[])biomeStops.Clone() : Array.Empty<GradientStop>();
            return copy;
        }

        /// <summary>
        /// Color de la rampa en t (0..1), interpolación lineal por tramos entre paradas.
        /// Antes de la primera parada devuelve su color; después de la última, el de la última.
        /// Sin paradas devuelve gris medio.
        /// </summary>
        public ColorRgb EvaluateBiome(float t)
        {
            if (biomeStops == null || biomeStops.Length == 0) return new ColorRgb(0.5f, 0.5f, 0.5f);
            if (t <= biomeStops[0].time) return biomeStops[0].color;

            for (int i = 1; i < biomeStops.Length; i++)
            {
                GradientStop a = biomeStops[i - 1];
                GradientStop b = biomeStops[i];
                if (t <= b.time)
                {
                    float span = b.time - a.time;
                    float k = span > 0f ? (t - a.time) / span : 1f;
                    return new ColorRgb(
                        MathUtil.Lerp(a.color.r, b.color.r, k),
                        MathUtil.Lerp(a.color.g, b.color.g, k),
                        MathUtil.Lerp(a.color.b, b.color.b, k));
                }
            }
            return biomeStops[biomeStops.Length - 1].color;
        }
    }
}
