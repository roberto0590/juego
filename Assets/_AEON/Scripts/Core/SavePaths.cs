using System.IO;
using UnityEngine;

namespace GodGame.Core
{
    /// <summary>Carpeta de guardado en el dispositivo (D7). SaveSystem es puro y recibe esta ruta por parámetro.</summary>
    public static class SavePaths
    {
        /// <summary>Application.persistentDataPath/saves (iOS: Documents de la app; Android: almacenamiento interno).</summary>
        public static string Directory => Path.Combine(Application.persistentDataPath, "saves");
    }
}
