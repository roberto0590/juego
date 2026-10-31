using System.Collections.Generic;

namespace GodGame.Simulation.Commands
{
    /// <summary>
    /// Patrón Command: cada orden de un dios (jugador o IA) es un objeto que se valida y ejecuta
    /// en la fase de resolución del turno. Ventajas:
    ///  - turnos simultáneos: todas las órdenes se recogen y se resuelven en un orden determinista;
    ///  - repetición/replays y PvP asíncrono: basta con serializar la lista de comandos por turno;
    ///  - tests: se puede construir un estado, aplicar comandos y comprobar el resultado.
    /// </summary>
    public interface ICommand
    {
        string GodId { get; }

        /// <summary>Menor = se resuelve antes. Milagros (0) antes que conquistas (10).</summary>
        int Priority { get; }

        /// <summary>Se llama al enviar (para feedback en UI) y otra vez justo antes de ejecutar.</summary>
        bool Validate(GameState state, out string reason);

        void Execute(GameState state, TurnReport report);
    }

    /// <summary>Fuente de comandos para un turno (IA de dioses rivales, red en PvP asíncrono, replays).</summary>
    public interface ICommandSource
    {
        void CollectCommands(GameState state, List<ICommand> output);
    }
}
