using System;
using System.Collections.Generic;
using System.Linq;
using GodGame.Core;
using GodGame.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GodGame.EditorTools
{
    /// <summary>
    /// Pasos 6 y 7 del configurador (plan §5): escenas Boot, MainMenu y Game en Assets/_AEON/Scenes
    /// (un único GameObject raíz con su componente; cámara, luz y UI se crean en runtime) y Build Settings.
    /// </summary>
    internal static class AeonSceneSetup
    {
        private sealed class EscenaBase
        {
            public readonly string Nombre;
            public readonly string Objeto;
            public readonly Type Componente;

            public EscenaBase(string nombre, string objeto, Type componente)
            {
                Nombre = nombre; Objeto = objeto; Componente = componente;
            }
        }

        // Orden de Build Settings: Boot (índice 0, arranque), MainMenu, Game.
        private static readonly EscenaBase[] Escenas =
        {
            new EscenaBase(GameConstants.BootScene, "AEON_Boot", typeof(BootLoader)),
            new EscenaBase(GameConstants.MainMenuScene, "AEON_MainMenu", typeof(MainMenuController)),
            new EscenaBase(GameConstants.GameScene, "AEON_Game", typeof(GameController))
        };

        internal static string RutaDe(string nombreEscena) => AeonProjectConfigurator.ScenesFolder + "/" + nombreEscena + ".unity";

        private static bool Existe(string ruta) => AssetDatabase.LoadAssetAtPath<SceneAsset>(ruta) != null;

        /// <summary>Crea las escenas que falten. Nunca toca una escena existente.</summary>
        internal static void AsegurarEscenas(bool automatico, ResumenConfiguracion resumen)
        {
            List<EscenaBase> faltan = Escenas.Where(e => !Existe(RutaDe(e.Nombre))).ToList();
            if (faltan.Count == 0) return;

            if (HayEscenasModificadas())
            {
                if (automatico)
                {
                    resumen.Aviso("Hay escenas abiertas con cambios sin guardar: no se crearon las escenas base. " +
                                  "Guárdalas y ejecuta '" + AeonProjectConfigurator.MenuPath + "'.");
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    resumen.Aviso("Creación de escenas cancelada por el usuario.");
                    return;
                }
            }

            AeonProjectConfigurator.AsegurarCarpeta(AeonProjectConfigurator.ScenesFolder, resumen);
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (EscenaBase escena in faltan)
                {
                    Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var raiz = new GameObject(escena.Objeto);
                    raiz.AddComponent(escena.Componente);

                    string ruta = RutaDe(escena.Nombre);
                    if (EditorSceneManager.SaveScene(scene, ruta)) resumen.Creado(ruta);
                    else resumen.Aviso("No se pudo guardar la escena " + ruta + ".");
                }
            }
            finally
            {
                RestaurarEscenas(setup);
            }
        }

        private static bool HayEscenasModificadas()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return true;
            return false;
        }

        /// <summary>Vuelve a abrir las escenas que había si todas tenían ruta; si no (escena sin título), abre Boot.</summary>
        private static void RestaurarEscenas(SceneSetup[] setup)
        {
            bool restaurable = setup != null && setup.Length > 0 &&
                               setup.All(s => !string.IsNullOrEmpty(s.path) && Existe(s.path));
            if (restaurable)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                return;
            }
            string boot = RutaDe(GameConstants.BootScene);
            if (Existe(boot)) EditorSceneManager.OpenScene(boot, OpenSceneMode.Single);
        }

        /// <summary>
        /// Build Settings = Boot, MainMenu, Game (habilitadas) + las demás escenas que ya hubiera, sin duplicados.
        /// Si ya están así, no se cambia nada.
        /// </summary>
        internal static void AsegurarBuildSettings(ResumenConfiguracion resumen)
        {
            var deseadas = new List<EditorBuildSettingsScene>();
            var rutas = new HashSet<string>(StringComparer.Ordinal); // solo pertenencia; el orden lo da la lista

            foreach (EscenaBase escena in Escenas)
            {
                string ruta = RutaDe(escena.Nombre);
                if (!Existe(ruta))
                {
                    resumen.Aviso("Falta la escena " + ruta + ": no se añadió a Build Settings.");
                    continue;
                }
                deseadas.Add(new EditorBuildSettingsScene(ruta, true));
                rutas.Add(ruta);
            }

            EditorBuildSettingsScene[] actuales = EditorBuildSettings.scenes ?? new EditorBuildSettingsScene[0];
            foreach (EditorBuildSettingsScene actual in actuales)
            {
                if (actual == null || string.IsNullOrEmpty(actual.path) || !rutas.Add(actual.path)) continue;
                deseadas.Add(new EditorBuildSettingsScene(actual.path, actual.enabled));
            }

            if (Iguales(actuales, deseadas)) return;
            EditorBuildSettings.scenes = deseadas.ToArray();
            resumen.Cambio("Build Settings: " + string.Join(", ", deseadas.Select(s => s.path)));
        }

        private static bool Iguales(EditorBuildSettingsScene[] actuales, List<EditorBuildSettingsScene> deseadas)
        {
            if (actuales.Length != deseadas.Count) return false;
            for (int i = 0; i < actuales.Length; i++)
            {
                if (actuales[i] == null) return false;
                if (!string.Equals(actuales[i].path, deseadas[i].path, StringComparison.Ordinal)) return false;
                if (actuales[i].enabled != deseadas[i].enabled) return false;
            }
            return true;
        }
    }
}
