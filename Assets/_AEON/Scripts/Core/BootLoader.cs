using UnityEngine;
using UnityEngine.SceneManagement;

namespace GodGame.Core
{
    /// <summary>
    /// Único componente de la escena Boot: ajustes globales para móvil y salto al menú principal.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        private void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            SceneManager.LoadScene(GameConstants.MainMenuScene);
        }
    }
}
