// Superficie de planeta estilizada para URP (Unity 6).
// Usa los UV que genera PlanetMeshGenerator:  UV0.x = elevación (0..1),  UV0.y = latitud (0 ecuador .. 1 polo).
// El color del bioma sale de una textura-gradiente 256×1 (océano profundo → costa → llanura → bosque → roca → nieve),
// una por universo (paleta propia) → cada planeta cuesta 1 textura minúscula en lugar de un atlas de 2048.
// Iluminación: Lambert en bandas (toon) + casquetes polares + brillo especular solo en océanos.
Shader "GodGame/PlanetSurface"
{
    Properties
    {
        _BiomeRamp ("Gradiente de biomas (256x1, Clamp)", 2D) = "white" {}
        _SeaLevel ("Nivel del mar", Range(0, 1)) = 0.45
        _PolarCap ("Inicio casquete polar (latitud)", Range(0, 1)) = 0.82
        _PolarColor ("Color polar", Color) = (0.92, 0.96, 1, 1)
        _Bands ("Bandas de luz (toon)", Range(1, 8)) = 4
        _ShadowColor ("Color de sombra", Color) = (0.12, 0.10, 0.25, 1)
        _OceanGloss ("Brillo del océano", Range(0, 1)) = 0.6
        [HDR] _CityLights ("Luces nocturnas (planeta civilizado)", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BiomeRamp_ST;
            half _SeaLevel;
            half _PolarCap;
            half4 _PolarColor;
            half _Bands;
            half4 _ShadowColor;
            half _OceanGloss;
            half4 _CityLights;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "PlanetForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BiomeRamp); SAMPLER(sampler_BiomeRamp);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings  { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                Light light = GetMainLight();

                half elevation = IN.uv.x;
                half latitude = IN.uv.y;
                half3 albedo = SAMPLE_TEXTURE2D(_BiomeRamp, sampler_BiomeRamp, float2(elevation, 0.5)).rgb;

                // Casquetes polares: suben con la elevación (montañas nevadas a menor latitud).
                half polar = smoothstep(_PolarCap, _PolarCap + 0.05h, latitude + elevation * 0.15h);
                albedo = lerp(albedo, _PolarColor.rgb, polar);

                // Lambert cuantizado en bandas → look toon/low-poly.
                half ndl = saturate(dot(N, light.direction));
                half banded = floor(ndl * _Bands) / _Bands;
                half3 lit = lerp(_ShadowColor.rgb, light.color, banded);

                // Especular solo bajo el nivel del mar.
                half isOcean = step(elevation, _SeaLevel) * (1.0h - polar);
                half3 H = normalize(light.direction + V);
                half spec = pow(saturate(dot(N, H)), 64.0h) * _OceanGloss * isOcean;

                // Luces de ciudades en el lado nocturno (solo en tierra).
                half night = saturate(-dot(N, light.direction) * 4.0h);
                half3 city = _CityLights.rgb * night * (1.0h - isOcean);

                half3 color = albedo * lit + spec * light.color + city + albedo * SampleSH(N) * 0.3h;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
