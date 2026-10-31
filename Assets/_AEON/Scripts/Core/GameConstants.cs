namespace GodGame.Core
{
    /// <summary>Constantes compartidas por Core, Presentation y el configurador de Editor.</summary>
    public static class GameConstants
    {
        // Nombres de escena (Assets/_AEON/Scenes/*.unity, creadas por el configurador y añadidas a Build Settings).
        public const string BootScene = "Boot";
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";

        /// <summary>Ruta del catálogo dentro de Resources (Assets/_AEON/Resources/AEON/UniverseCatalog.asset).</summary>
        public const string ResourcesCatalogPath = "AEON/UniverseCatalog";

        /// <summary>Nombre del dios del jugador.</summary>
        public const string PlayerGodName = "El Octavo";

        /// <summary>Ranura de guardado del slice (una sola partida).</summary>
        public const int SaveSlot = 0;
    }
}
