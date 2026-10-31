// Atmósfera de planeta para URP (Unity 6). Se aplica a una esfera un 4–6 % mayor que el planeta.
// Técnica: halo por Fresnel (rim) + atenuación por el lado nocturno según la luz principal.
// Coste: 1 pase transparente aditivo, sin texturas → muy barato en móvil.
Shader "GodGame/PlanetAtmosphere"
{
    Properties
    {
        [HDR] _AtmosphereColor ("Color de atmósfera", Color) = (0.35, 0.65, 1.0, 1.0)
        [HDR] _SunsetColor ("Color del terminador (atardecer)", Color) = (1.0, 0.45, 0.2, 1.0)
        _FresnelPower ("Potencia Fresnel", Range(0.5, 8)) = 3.0
        _Intensity ("Intensidad", Range(0, 10)) = 1.6
        _SunInfluence ("Influencia del sol (0 = brilla igual de noche)", Range(0, 1)) = 0.85
        _TerminatorWidth ("Ancho del terminador", Range(0.01, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One   // aditivo con alfa: suma luz, nunca oscurece
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Compatibilidad SRP Batcher: todas las propiedades del material dentro de UnityPerMaterial.
            CBUFFER_START(UnityPerMaterial)
                half4 _AtmosphereColor;
                half4 _SunsetColor;
                half _FresnelPower;
                half _Intensity;
                half _SunInfluence;
                half _TerminatorWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // Fresnel: 0 en el centro del disco, 1 en el borde → halo.
                half fresnel = pow(1.0h - saturate(dot(N, V)), _FresnelPower);

                // Lado día/noche respecto a la luz principal (la estrella del sistema).
                Light mainLight = GetMainLight();
                half ndl = dot(N, mainLight.direction);            // -1 noche, 1 mediodía
                half dayMask = saturate(ndl * 0.5h + 0.5h);
                half lightFactor = lerp(1.0h, dayMask, _SunInfluence);

                // Banda cálida en el terminador (amanecer/atardecer).
                half terminator = 1.0h - saturate(abs(ndl) / _TerminatorWidth);
                half3 color = lerp(_AtmosphereColor.rgb, _SunsetColor.rgb, terminator * 0.7h);

                half alpha = saturate(fresnel * _Intensity * lightFactor);
                return half4(color * mainLight.color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
