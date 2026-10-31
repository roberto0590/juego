namespace GodGame.Rules
{
    /// <summary>
    /// Interfaz opcional de un módulo de universo: textos en español de sus propias claves
    /// (p. ej. el messageKey de los TurnEvent de su regla especial). La UI los consulta con
    /// UniverseModuleRegistry.TryGetText cuando la clave no es del núcleo, así un universo nuevo
    /// no obliga a editar Assets/_AEON/Scripts.
    /// </summary>
    public interface IUniverseTexts
    {
        /// <summary>Devuelve true y el texto si la clave pertenece a este módulo.</summary>
        bool TryGetText(string key, out string text);
    }
}
