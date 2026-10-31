using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GodGame.EditorTools
{
    /// <summary>
    /// Paso 2 del configurador (plan §5): si el proyecto no tiene pipeline asignado, crea (o reutiliza)
    /// AEON_URP_Renderer.asset y AEON_URP_Movil.asset con ajustes móviles y lo asigna como pipeline por defecto.
    /// Si ya hay un pipeline asignado no toca nada.
    /// </summary>
    internal static class AeonUrpSetup
    {
        internal const string RendererPath = AeonProjectConfigurator.UrpFolder + "/AEON_URP_Renderer.asset";
        internal const string PipelinePath = AeonProjectConfigurator.UrpFolder + "/AEON_URP_Movil.asset";

        private const string AlternativaManual =
            "Alternativa manual: Assets > Create > Rendering > URP Asset (with Universal Renderer) y asígnalo en " +
            "Edit > Project Settings > Graphics > Default Render Pipeline (ver README.md).";

        internal static void Asegurar(ResumenConfiguracion resumen)
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;

            try
            {
                AeonProjectConfigurator.AsegurarCarpeta(AeonProjectConfigurator.UrpFolder, resumen);

                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if (pipeline == null)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(PipelinePath) != null)
                        throw new InvalidOperationException(PipelinePath + " existe pero no es un asset de pipeline URP");

                    var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                    if (rendererData == null)
                    {
                        if (AssetDatabase.LoadMainAssetAtPath(RendererPath) != null)
                            throw new InvalidOperationException(RendererPath + " existe pero no es un UniversalRendererData");
                        rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                        AssetDatabase.CreateAsset(rendererData, RendererPath);
                        resumen.Creado(RendererPath);
                    }

                    pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                    // Ajustes móviles: resolución nativa, sin MSAA ni HDR, sombras cortas, sin texturas de cámara.
                    pipeline.renderScale = 1f;
                    pipeline.msaaSampleCount = 1;
                    pipeline.supportsHDR = false;
                    pipeline.shadowDistance = 40f;
                    pipeline.supportsCameraDepthTexture = false;
                    pipeline.supportsCameraOpaqueTexture = false;
                    AssetDatabase.CreateAsset(pipeline, PipelinePath);
                    resumen.Creado(PipelinePath);
                }

                GraphicsSettings.defaultRenderPipeline = pipeline;
                resumen.Cambio("Graphics: Default Render Pipeline = " + PipelinePath);
            }
            catch (Exception e)
            {
                resumen.Aviso("No se pudo configurar URP automáticamente (" + e.Message + "). " + AlternativaManual);
                Debug.LogException(e);
            }
        }
    }
}
