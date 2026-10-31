using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GodGame.Core;
using GodGame.Domain;
using GodGame.Rules;
using GodGame.Rules.Assets;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace GodGame.EditorTools
{
    /// <summary>Lo que ha hecho una ejecución del configurador: assets creados, ajustes cambiados y avisos.</summary>
    internal sealed class ResumenConfiguracion
    {
        public readonly List<string> Creados = new List<string>();
        public readonly List<string> Cambios = new List<string>();
        public readonly List<string> Avisos = new List<string>();

        public void Creado(string ruta) => Creados.Add(ruta);

        public void Cambio(string texto) => Cambios.Add(texto);

        public void Aviso(string texto)
        {
            Avisos.Add(texto);
            Debug.LogWarning("[AEON] " + texto);
        }
    }

    /// <summary>
    /// Configurador idempotente del proyecto (plan §5). Crea solo lo que falta y nunca sobrescribe un asset
    /// existente: carpetas, URP móvil, UR_*.asset y materiales de cada módulo de universo, materiales compartidos,
    /// catálogo en Resources, escenas Boot/MainMenu/Game, Build Settings y PlayerSettings móviles.
    /// Se ejecuta solo una vez al abrir el proyecto (bandera en Library/AEON) y siempre desde el menú
    /// "AEON/Configurar proyecto". Los universos se recorren con UniverseModuleRegistry: este código no
    /// conoce ningún universo concreto.
    /// </summary>
    [InitializeOnLoad]
    public static class AeonProjectConfigurator
    {
        /// <summary>Versión de la configuración. Subirla hace que el auto-arranque vuelva a ejecutarse una vez.</summary>
        public const int CONFIG_VERSION = 1;

        public const string MenuPath = "AEON/Configurar proyecto";

        internal const string RootFolder = "Assets/_AEON";
        internal const string ResourcesRoot = RootFolder + "/Resources";
        internal const string CatalogPath = ResourcesRoot + "/" + GameConstants.ResourcesCatalogPath + ".asset";
        internal const string MaterialsFolder = RootFolder + "/Art/Materials";
        internal const string ScenesFolder = RootFolder + "/Scenes";
        internal const string UrpFolder = RootFolder + "/Settings/URP";
        internal const string UniversesFolder = RootFolder + "/Universes";

        internal const string PlanetShaderName = "GodGame/PlanetSurface";
        internal const string AtmosphereShaderName = "GodGame/PlanetAtmosphere";
        internal const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        private const float DefaultSeaLevel = 0.45f;
        private const string ProductName = "ÆON: Guerra de los Siete Velos";
        private const string CompanyName = "AEON Studio";
        private const string ApplicationId = "com.aeonstudio.sietevelos";

        private static bool _ejecutando;

        static AeonProjectConfigurator()
        {
            // En modo batch (CI, Test Runner por línea de comandos) no se autoejecuta:
            // para eso existe ConfigurarDesdeLineaDeComandos (-executeMethod).
            if (Application.isBatchMode) return;
            if (File.Exists(RutaBandera)) return;
            EditorApplication.delayCall += IntentarAutoEjecutar;
        }

        /// <summary>R/Library/AEON/configurado-v&lt;CONFIG_VERSION&gt;.flag (Library no se versiona: cada clon se configura una vez).</summary>
        private static string RutaBandera
            => Path.Combine(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "AEON"),
                "configurado-v" + CONFIG_VERSION + ".flag");

        private static void IntentarAutoEjecutar()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += IntentarAutoEjecutar;
                return;
            }
            if (File.Exists(RutaBandera)) return;
            Ejecutar(automatico: true);
        }

        [MenuItem(MenuPath, false, 0)]
        public static void ConfigurarDesdeMenu()
        {
            Ejecutar(automatico: false);
        }

        /// <summary>
        /// Para CI: Unity -batchmode -quit -projectPath &lt;R&gt; -executeMethod GodGame.EditorTools.AeonProjectConfigurator.ConfigurarDesdeLineaDeComandos
        /// Sale con código 1 si algún paso deja avisos.
        /// </summary>
        public static void ConfigurarDesdeLineaDeComandos()
        {
            bool ok = Ejecutar(automatico: true);
            if (Application.isBatchMode && !ok) EditorApplication.Exit(1);
        }

        /// <summary>Ejecuta todos los pasos. Devuelve true si terminó sin avisos (y entonces escribe la bandera).</summary>
        public static bool Ejecutar(bool automatico)
        {
            if (_ejecutando) return false;
            _ejecutando = true;
            var resumen = new ResumenConfiguracion();
            try
            {
                var reglas = new List<UniverseRules>();
                var compartidos = new MaterialesCompartidos();

                Paso(resumen, "carpetas", () => AsegurarCarpetas(resumen));
                Paso(resumen, "URP", () => AeonUrpSetup.Asegurar(resumen));
                Paso(resumen, "módulos de universo", () => reglas = AsegurarModulos(resumen));
                Paso(resumen, "materiales compartidos", () => compartidos = AsegurarMaterialesCompartidos(resumen));
                Paso(resumen, "catálogo de universos", () => AsegurarCatalogo(reglas, compartidos, resumen));
                Paso(resumen, "escenas", () => AeonSceneSetup.AsegurarEscenas(automatico, resumen));
                Paso(resumen, "Build Settings", () => AeonSceneSetup.AsegurarBuildSettings(resumen));
                Paso(resumen, "PlayerSettings", () => AsegurarPlayerSettings(resumen));
                Paso(resumen, "guardar assets", () =>
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                });

                bool ok = resumen.Avisos.Count == 0;
                if (ok) Paso(resumen, "bandera", EscribirBandera);
                ok = resumen.Avisos.Count == 0;

                LogResumen(resumen, automatico, ok);
                return ok;
            }
            finally
            {
                _ejecutando = false;
            }
        }

        private static void Paso(ResumenConfiguracion resumen, string nombre, Action accion)
        {
            try
            {
                accion();
            }
            catch (Exception e)
            {
                resumen.Aviso($"Falló el paso '{nombre}': {e.Message}");
                Debug.LogException(e);
            }
        }

        // ---------------- 1. Carpetas ----------------

        private static void AsegurarCarpetas(ResumenConfiguracion resumen)
        {
            AsegurarCarpeta(CarpetaDe(CatalogPath), resumen);
            AsegurarCarpeta(MaterialsFolder, resumen);
            AsegurarCarpeta(ScenesFolder, resumen);
            AsegurarCarpeta(UrpFolder, resumen);
            foreach (IUniverseModule module in UniverseModuleRegistry.All)
                AsegurarCarpeta(CarpetaDatosDelModulo(module), resumen);
        }

        /// <summary>Crea la carpeta (y sus padres) con AssetDatabase.CreateFolder si no existe.</summary>
        internal static void AsegurarCarpeta(string ruta, ResumenConfiguracion resumen)
        {
            ruta = ruta.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(ruta)) return;
            int corte = ruta.LastIndexOf('/');
            if (corte <= 0) throw new ArgumentException("Ruta de carpeta no válida: " + ruta);
            string padre = ruta.Substring(0, corte);
            AsegurarCarpeta(padre, resumen);
            AssetDatabase.CreateFolder(padre, ruta.Substring(corte + 1));
            resumen?.Creado(ruta);
        }

        private static string CarpetaDe(string rutaAsset)
        {
            int corte = rutaAsset.LastIndexOf('/');
            return corte > 0 ? rutaAsset.Substring(0, corte) : rutaAsset;
        }

        /// <summary>
        /// Carpeta Data del módulo: junto a su asmdef (CompilationPipeline), o Assets/_AEON/Universes/&lt;DisplayName&gt;/Data
        /// si el ensamblado no tiene asmdef.
        /// </summary>
        private static string CarpetaDatosDelModulo(IUniverseModule module)
        {
            string assemblyName = module.GetType().Assembly.GetName().Name;
            string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            string carpeta = !string.IsNullOrEmpty(asmdef)
                ? Path.GetDirectoryName(asmdef).Replace('\\', '/')
                : UniversesFolder + "/" + NombreDeArchivo(NombreVisible(module));
            return carpeta + "/Data";
        }

        private static string NombreVisible(IUniverseModule module)
            => !string.IsNullOrEmpty(module.DisplayName) ? module.DisplayName : module.RulesId;

        /// <summary>Quita los caracteres no válidos en nombres de archivo.</summary>
        private static string NombreDeArchivo(string texto)
        {
            var sb = new StringBuilder(texto ?? "");
            foreach (char c in Path.GetInvalidFileNameChars()) sb.Replace(c, '_');
            sb.Replace('/', '_');
            return sb.Length > 0 ? sb.ToString() : "_";
        }

        // ---------------- 3. Módulos de universo ----------------

        private static List<UniverseRules> AsegurarModulos(ResumenConfiguracion resumen)
        {
            var result = new List<UniverseRules>();
            IReadOnlyList<IUniverseModule> modulos = UniverseModuleRegistry.All;
            if (modulos.Count == 0)
                resumen.Aviso("No se encontró ningún módulo de universo (IUniverseModule) en " + UniversesFolder + ".");

            foreach (IUniverseModule module in modulos)
            {
                try
                {
                    UniverseRules reglas = AsegurarModulo(module, resumen);
                    if (reglas != null) result.Add(reglas);
                }
                catch (Exception e)
                {
                    resumen.Aviso($"No se pudo preparar el universo '{module.RulesId}': {e.Message}");
                    Debug.LogException(e);
                }
            }
            return result;
        }

        private static UniverseRules AsegurarModulo(IUniverseModule module, ResumenConfiguracion resumen)
        {
            string carpeta = CarpetaDatosDelModulo(module);
            AsegurarCarpeta(carpeta, resumen);
            string id = NombreDeArchivo(module.RulesId);
            UniversePresentationDefaults presentacion = module.CreatePresentationDefaults() ?? new UniversePresentationDefaults();

            Material planeta = AsegurarMaterial(carpeta + "/M_Planeta_" + id + ".mat", PlanetShaderName,
                m => SetFloatSiExiste(m, "_SeaLevel", DefaultSeaLevel), resumen);
            Material atmosfera = AsegurarMaterial(carpeta + "/M_Atmosfera_" + id + ".mat", AtmosphereShaderName,
                m => SetColorSiExiste(m, "_AtmosphereColor", ToColor(presentacion.atmosphereColor)), resumen);

            string ruta = carpeta + "/UR_" + NombreDeArchivo(NombreVisible(module)) + ".asset";
            UniverseRules reglas = AssetDatabase.LoadAssetAtPath<UniverseRules>(ruta);
            bool nuevo = false;
            if (reglas == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ruta) != null)
                {
                    resumen.Aviso($"{ruta} existe pero no es un UniverseRules: no se toca.");
                    return null;
                }
                reglas = ScriptableObject.CreateInstance<UniverseRules>();
                reglas.Initialize(module.CreateDefaultRules(), presentacion);
                nuevo = true;
            }

            bool cambiado = false;
            if (reglas.planetMaterial == null && planeta != null) { reglas.planetMaterial = planeta; cambiado = true; }
            if (reglas.atmosphereMaterial == null && atmosfera != null) { reglas.atmosphereMaterial = atmosfera; cambiado = true; }

            if (nuevo)
            {
                AssetDatabase.CreateAsset(reglas, ruta);
                resumen.Creado(ruta);
            }
            else if (cambiado)
            {
                EditorUtility.SetDirty(reglas);
                resumen.Cambio(ruta + ": materiales asignados");
            }
            return reglas;
        }

        // ---------------- 4. Materiales compartidos ----------------

        private sealed class MaterialesCompartidos
        {
            public Material Estrella;
            public Material Orbita;
            public Material Planeta;
            public Material Atmosfera;
        }

        private static MaterialesCompartidos AsegurarMaterialesCompartidos(ResumenConfiguracion resumen)
        {
            AsegurarCarpeta(MaterialsFolder, resumen);
            return new MaterialesCompartidos
            {
                Estrella = AsegurarMaterial(MaterialsFolder + "/M_Estrella.mat", UnlitShaderName, null, resumen),
                Orbita = AsegurarMaterial(MaterialsFolder + "/M_Orbita.mat", UnlitShaderName, null, resumen),
                Planeta = AsegurarMaterial(MaterialsFolder + "/M_PlanetaBase.mat", PlanetShaderName,
                    m => SetFloatSiExiste(m, "_SeaLevel", DefaultSeaLevel), resumen),
                Atmosfera = AsegurarMaterial(MaterialsFolder + "/M_AtmosferaBase.mat", AtmosphereShaderName, null, resumen)
            };
        }

        /// <summary>Devuelve el material de esa ruta; si no existe lo crea con el shader indicado (null si el shader no está).</summary>
        private static Material AsegurarMaterial(string ruta, string shaderName, Action<Material> configurar, ResumenConfiguracion resumen)
        {
            Material existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (existente != null) return existente;
            if (AssetDatabase.LoadMainAssetAtPath(ruta) != null)
            {
                resumen.Aviso($"{ruta} existe pero no es un Material: no se toca.");
                return null;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                resumen.Aviso($"No se encontró el shader '{shaderName}': no se creó {ruta}. Comprueba que URP y Assets/_AEON/Shaders se importaron y vuelve a ejecutar '{MenuPath}'.");
                return null;
            }

            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(ruta) };
            configurar?.Invoke(material);
            AssetDatabase.CreateAsset(material, ruta);
            resumen.Creado(ruta);
            return material;
        }

        private static void SetFloatSiExiste(Material material, string propiedad, float valor)
        {
            if (material.HasProperty(propiedad)) material.SetFloat(propiedad, valor);
        }

        private static void SetColorSiExiste(Material material, string propiedad, Color valor)
        {
            if (material.HasProperty(propiedad)) material.SetColor(propiedad, valor);
        }

        private static Color ToColor(ColorRgb c) => new Color(c.r, c.g, c.b, 1f);

        // ---------------- 5. Catálogo ----------------

        private static void AsegurarCatalogo(List<UniverseRules> reglas, MaterialesCompartidos compartidos, ResumenConfiguracion resumen)
        {
            AsegurarCarpeta(CarpetaDe(CatalogPath), resumen);
            UniverseCatalog catalogo = AssetDatabase.LoadAssetAtPath<UniverseCatalog>(CatalogPath);
            bool nuevo = false;
            if (catalogo == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(CatalogPath) != null)
                {
                    resumen.Aviso($"{CatalogPath} existe pero no es un UniverseCatalog: no se toca.");
                    return;
                }
                catalogo = ScriptableObject.CreateInstance<UniverseCatalog>();
                nuevo = true;
            }
            if (catalogo.universes == null) catalogo.universes = new List<UniverseRules>();

            bool cambiado = false;
            foreach (UniverseRules ur in reglas.Where(r => r != null && !string.IsNullOrEmpty(r.Id))
                                               .OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                if (catalogo.universes.Contains(ur) || catalogo.Find(ur.Id) != null) continue;
                catalogo.universes.Add(ur);
                cambiado = true;
                if (!nuevo) resumen.Cambio("Catálogo: añadido '" + ur.Id + "'");
            }

            if (compartidos != null)
            {
                if (catalogo.starMaterial == null && compartidos.Estrella != null) { catalogo.starMaterial = compartidos.Estrella; cambiado = true; }
                if (catalogo.orbitMaterial == null && compartidos.Orbita != null) { catalogo.orbitMaterial = compartidos.Orbita; cambiado = true; }
                if (catalogo.defaultPlanetMaterial == null && compartidos.Planeta != null) { catalogo.defaultPlanetMaterial = compartidos.Planeta; cambiado = true; }
                if (catalogo.defaultAtmosphereMaterial == null && compartidos.Atmosfera != null) { catalogo.defaultAtmosphereMaterial = compartidos.Atmosfera; cambiado = true; }
            }

            if (nuevo)
            {
                AssetDatabase.CreateAsset(catalogo, CatalogPath);
                resumen.Creado(CatalogPath);
            }
            else if (cambiado)
            {
                EditorUtility.SetDirty(catalogo);
            }
        }

        // ---------------- 8. PlayerSettings ----------------

        /// <summary>Ajustes móviles. Cada uno se cambia solo si sigue en el valor por defecto de Unity.</summary>
        private static void AsegurarPlayerSettings(ResumenConfiguracion resumen)
        {
            string carpetaProyecto = Path.GetFileName(Directory.GetParent(Application.dataPath).FullName);
            if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == carpetaProyecto)
            {
                PlayerSettings.productName = ProductName;
                resumen.Cambio("PlayerSettings.productName = " + ProductName);
            }
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
            {
                PlayerSettings.companyName = CompanyName;
                resumen.Cambio("PlayerSettings.companyName = " + CompanyName);
            }

            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                string actual = PlayerSettings.GetApplicationIdentifier(target);
                if (string.IsNullOrEmpty(actual) || actual.Contains("DefaultCompany"))
                {
                    PlayerSettings.SetApplicationIdentifier(target, ApplicationId);
                    resumen.Cambio($"Identificador de aplicación ({target.TargetName}) = {ApplicationId}");
                }
            }

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                resumen.Cambio("PlayerSettings.colorSpace = Linear");
            }

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.Mono2x)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                resumen.Cambio("Android: scripting backend = IL2CPP");
            }

            AndroidArchitecture arquitecturas = PlayerSettings.Android.targetArchitectures;
            if (arquitecturas == AndroidArchitecture.ARMv7 || arquitecturas == (AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64))
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                resumen.Cambio("Android: arquitecturas = ARM64");
            }

            bool orientacionPorDefecto =
                PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait ||
                (PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation &&
                 PlayerSettings.allowedAutorotateToPortrait && PlayerSettings.allowedAutorotateToPortraitUpsideDown);
            if (orientacionPorDefecto)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                resumen.Cambio("Orientación: solo horizontal (auto-rotación izquierda/derecha)");
            }
        }

        // ---------------- 9. Bandera y resumen ----------------

        private static void EscribirBandera()
        {
            string ruta = RutaBandera;
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(ruta, "AEON configurado (CONFIG_VERSION " + CONFIG_VERSION + ") " + DateTime.UtcNow.ToString("o"));
        }

        private static void LogResumen(ResumenConfiguracion resumen, bool automatico, bool ok)
        {
            var sb = new StringBuilder();
            sb.Append("[AEON] Configuración ").Append(automatico ? "automática" : "manual").Append(" terminada: ")
              .Append(resumen.Creados.Count).Append(" creados, ")
              .Append(resumen.Cambios.Count).Append(" ajustes, ")
              .Append(resumen.Avisos.Count).Append(" avisos.");
            foreach (string c in resumen.Creados) sb.Append("\n  + ").Append(c);
            foreach (string c in resumen.Cambios) sb.Append("\n  * ").Append(c);
            foreach (string a in resumen.Avisos) sb.Append("\n  ! ").Append(a);

            if (ok)
            {
                if (resumen.Creados.Count == 0 && resumen.Cambios.Count == 0)
                    sb.Append("\nEl proyecto ya estaba configurado: no se ha cambiado nada.");
                Debug.Log(sb.ToString());
            }
            else
            {
                sb.Append("\nNo se escribió la bandera de Library/AEON: corrige los avisos y ejecuta '").Append(MenuPath)
                  .Append("' (el arranque automático lo volverá a intentar al abrir el proyecto).");
                Debug.LogWarning(sb.ToString());
            }
        }
    }
}
