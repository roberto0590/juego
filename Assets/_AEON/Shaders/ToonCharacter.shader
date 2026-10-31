// Shader toon para personajes (dioses, avatares, profetas, especies) en URP (Unity 6).
// - Rampa de luz en 2 tonos con borde suave controlable (cel shading).
// - Sombras reales de la luz principal (recibe y proyecta).
// - Rim light (contorno luminoso) para separar al personaje del fondo espacial oscuro.
// - Especular estilizado y emisión (runas, ojos, núcleos de energía).
// - Contorno por "casco invertido" (inverted hull) opcional, en un pase aparte.
// Todo dentro de un único CBUFFER UnityPerMaterial → compatible con SRP Batcher.
Shader "GodGame/ToonCharacter"
{
    Properties
    {
        _BaseMap ("Albedo (atlas 1024)", 2D) = "white" {}
        _BaseColor ("Tinte", Color) = (1, 1, 1, 1)
        _ShadowColor ("Color de sombra", Color) = (0.45, 0.40, 0.65, 1)
        _RampThreshold ("Umbral luz/sombra", Range(-1, 1)) = 0.1
        _RampSmooth ("Suavidad del borde", Range(0.001, 0.5)) = 0.05
        [HDR] _SpecularColor ("Color especular", Color) = (1, 1, 1, 1)
        _Glossiness ("Brillo especular", Range(1, 256)) = 48
        [HDR] _RimColor ("Color rim", Color) = (0.6, 0.8, 1.0, 1)
        _RimPower ("Potencia rim", Range(0.5, 8)) = 3
        _RimThreshold ("Umbral rim", Range(0, 1)) = 0.2
        _EmissionMap ("Emisión (máscara)", 2D) = "black" {}
        [HDR] _EmissionColor ("Color emisión", Color) = (0, 0, 0, 1)
        _OutlineColor ("Color contorno", Color) = (0.05, 0.03, 0.1, 1)
        _OutlineWidth ("Grosor contorno (0 = sin contorno)", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half _RampThreshold;
            half _RampSmooth;
            half4 _SpecularColor;
            half _Glossiness;
            half4 _RimColor;
            half _RimPower;
            half _RimThreshold;
            float4 _EmissionMap_ST;
            half4 _EmissionColor;
            half4 _OutlineColor;
            half _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        // ---------- Pase principal ----------
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light light = GetMainLight(shadowCoord);

                // Rampa toon: la sombra proyectada también "corta" la luz.
                half ndl = dot(N, light.direction);
                half lightBand = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth,
                                            ndl * light.shadowAttenuation);
                half3 diffuse = lerp(_ShadowColor.rgb, half3(1, 1, 1), lightBand) * light.color;

                // Especular estilizado (mancha de bordes duros).
                half3 H = normalize(light.direction + V);
                half specRaw = pow(saturate(dot(N, H)), _Glossiness);
                half spec = smoothstep(0.005h, 0.01h, specRaw) * lightBand;

                // Rim: más fuerte en el lado iluminado para no "brillar" en la sombra.
                half rimDot = 1.0h - saturate(dot(N, V));
                half rim = smoothstep(_RimThreshold - 0.01h, _RimThreshold + 0.01h,
                                      pow(rimDot, _RimPower) * saturate(ndl * 0.5h + 0.5h));

                half3 ambient = SampleSH(N) * 0.35h;
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;

                half3 color = albedo.rgb * (diffuse + ambient)
                            + spec * _SpecularColor.rgb
                            + rim * _RimColor.rgb
                            + emission;
                color = MixFog(color, IN.fogFactor);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // ---------- Contorno (inverted hull) ----------
        // URP dibuja pases con LightMode "SRPDefaultUnlit" además del UniversalForward.
        // Alternativa más eficiente a gran escala: un Renderer Feature de contorno por pantalla.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings  { float4 positionCS : SV_POSITION; };

            Varyings vertOutline (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float4 posCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                // Grosor constante en pantalla: se multiplica por w (perspectiva).
                float2 offset = normalize(normalCS + 1e-5) * (_OutlineWidth * 0.005) * posCS.w;
                offset.x *= _ScreenParams.y / _ScreenParams.x; // corrige el aspect ratio
                posCS.xy += offset;
                OUT.positionCS = posCS;
                return OUT;
            }

            half4 fragOutline (Varyings IN) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        // ---------- Proyección de sombras ----------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection; // los rellena URP durante el pase de sombras
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings  { float4 positionCS : SV_POSITION; };

            Varyings vertShadow (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 fragShadow (Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
