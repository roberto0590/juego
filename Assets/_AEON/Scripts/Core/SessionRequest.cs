using UnityEngine;

namespace GodGame.Core
{
    /// <summary>Qué debe hacer la escena de juego al arrancar.</summary>
    public enum SessionMode
    {
        NuevaPartida,
        Cargar
    }

    /// <summary>
    /// Petición del menú principal a la escena Game (estado estático que sobrevive al cambio de escena).
    /// La escena Game la consume una sola vez; sin petición arranca una partida nueva con semilla aleatoria.
    /// </summary>
    public static class SessionRequest
    {
        // Solo para semillas de partidas nuevas (fuera de la simulación): no afecta al determinismo.
        private static readonly System.Random SeedRng = new System.Random();
        private static bool _pending;

        public static SessionMode Mode { get; private set; }
        public static int Seed { get; private set; }
        public static bool HasPending => _pending;

        public static void SetNewGame(int seed)
        {
            Mode = SessionMode.NuevaPartida;
            Seed = seed;
            _pending = true;
        }

        public static void SetLoad()
        {
            Mode = SessionMode.Cargar;
            Seed = 0;
            _pending = true;
        }

        /// <summary>Devuelve la petición pendiente (si la hay) y la borra.</summary>
        public static bool Consume(out SessionMode mode, out int seed)
        {
            mode = Mode;
            seed = Seed;
            bool had = _pending;
            Clear();
            return had;
        }

        /// <summary>Semilla aleatoria no negativa para una partida nueva.</summary>
        public static int RandomSeed() => SeedRng.Next(0, int.MaxValue);

        // Con "Enter Play Mode Options" sin recarga de dominio los estáticos sobreviven: se limpian al entrar en Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear()
        {
            _pending = false;
            Mode = SessionMode.NuevaPartida;
            Seed = 0;
        }
    }
}
