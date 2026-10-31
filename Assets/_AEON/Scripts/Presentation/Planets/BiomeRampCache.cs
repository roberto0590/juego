using System;
using System.Collections.Generic;
using GodGame.Rules;
using UnityEngine;

namespace GodGame.Presentation
{
    /// <summary>
    /// Rampa de biomas en runtime (D12): hornea una Texture2D 256×1 (RGBA32, sin mipmaps, Clamp, Bilinear)
    /// por universo a partir de las paradas del módulo y la reutiliza para todos sus planetas (_BiomeRamp).
    /// </summary>
    public static class BiomeRampCache
    {
        public const int Width = 256;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public static Texture2D Get(string rulesId, UniversePresentationDefaults defaults)
        {
            string key = rulesId ?? "";
            if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            var texture = new Texture2D(Width, 1, TextureFormat.RGBA32, false)
            {
                name = "RampaBiomas_" + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[Width];
            for (int i = 0; i < Width; i++)
            {
                float t = i / (float)(Width - 1);
                Color color = defaults != null ? defaults.EvaluateBiome(t).ToColor() : Color.gray;
                pixels[i] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true); // sin mipmaps; libera la copia en CPU

            Cache[key] = texture;
            return texture;
        }
    }
}
