using System;
using System.Collections.Generic;
using System.Linq;
using GodGame.AI;
using GodGame.Domain;
using GodGame.Generation;
using GodGame.Rules;
using GodGame.Save;
using GodGame.Simulation;
using GodGame.Simulation.Commands;
using UnityEngine;

namespace GodGame.Core
{
    /// <summary>
    /// Composición de una partida: GameState + TurnManager + IA de cada dios rival + reglas especiales
    /// de los módulos presentes en el multiverso + guardado en Application.persistentDataPath/saves.
    /// La presentación solo habla con esta clase (y lee State); nunca construye la simulación por su cuenta.
    /// </summary>
    public sealed class GameSession
    {
        public const string Difficulty = "normal";

        /// <summary>Prefijo del error de carga cuando la partida usa un universo que ya no está en el build.</summary>
        public const string MissingUniverseErrorPrefix = "missing_universe:";

        public LoadedCatalog Catalog { get; }
        public GameState State { get; }
        public TurnManager Turns { get; }

        public int Seed => State.Multiverse.seed;
        public God Player => State.GetGod(State.Multiverse.playerGodId);

        /// <summary>Planeta asignado al jugador (el que debe conquistar primero).</summary>
        public Planet PlayerHomePlanet
        {
            get
            {
                God player = Player;
                return player != null ? State.GetPlanet(player.homePlanetId) : null;
            }
        }

        private GameSession(LoadedCatalog catalog, GameState state)
        {
            Catalog = catalog;
            State = state;
            Turns = new TurnManager(state);
            // Una IA por dios no jugador, en el orden de la lista de dioses (determinista).
            foreach (God god in state.Multiverse.gods)
            {
                if (!god.isPlayer) Turns.RegisterSource(new GodUtilityAI(god.id));
            }
        }

        /// <summary>Partida nueva: semilla → multiverso → estado con las reglas especiales de sus universos.</summary>
        public static GameSession NewGame(LoadedCatalog catalog, int seed)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Multiverse multiverse = MultiverseGenerator.Generate(seed, catalog.Rules, GameConstants.PlayerGodName);
            var state = new GameState(multiverse, catalog.Rules, 0, CreateSpecialRules(multiverse));
            return new GameSession(catalog, state);
        }

        /// <summary>¿Hay partida guardada (o su copia .bak) en la ranura del slice?</summary>
        public static bool HasSave() => SaveSystem.HasSave(SavePaths.Directory, GameConstants.SaveSlot);

        /// <summary>
        /// Carga la ranura del slice. Devuelve true si hay partida; en ese caso <paramref name="error"/> puede ser
        /// "loaded_from_backup". Errores: los de SaveSystem ('not_found', 'checksum_mismatch', 'newer_version',
        /// 'invalid_json: ...', 'empty', 'io: ...'), 'missing_universe:&lt;id&gt;' o 'restore: ...'.
        /// </summary>
        public static bool TryLoad(LoadedCatalog catalog, out GameSession session, out string error)
        {
            session = null;
            if (catalog == null) { error = "empty"; return false; }
            try
            {
                if (!SaveSystem.TryLoadFromFile(SavePaths.Directory, GameConstants.SaveSlot, out SaveData data, out error))
                    return false;

                foreach (Universe universe in data.multiverse.universes)
                {
                    if (!catalog.HasRules(universe.rulesId))
                    {
                        error = MissingUniverseErrorPrefix + universe.rulesId;
                        return false;
                    }
                }

                GameState state = SaveSystem.Restore(data, catalog.Rules, CreateSpecialRules(data.multiverse));
                session = new GameSession(catalog, state);
                return true;
            }
            catch (Exception e)
            {
                session = null;
                error = "restore: " + e.Message;
                return false;
            }
        }

        /// <summary>Encola una orden del jugador para este ciclo (validación inmediata).</summary>
        public bool Submit(ICommand command, out string reason) => Turns.Submit(command, out reason);

        /// <summary>Cancela las órdenes pendientes del jugador.</summary>
        public void CancelPending()
        {
            God player = Player;
            if (player != null) Turns.CancelPending(player.id);
        }

        /// <summary>Resuelve el ciclo (la IA rival actúa aquí) y guarda automáticamente.</summary>
        public TurnReport EndTurn()
        {
            TurnReport report = Turns.ResolveTurn();
            Save();
            return report;
        }

        /// <summary>Guarda en la ranura del slice. Devuelve false (y avisa en consola) si falla la escritura.</summary>
        public bool Save()
        {
            try
            {
                SaveData data = SaveSystem.Capture(State, null, Difficulty);
                SaveSystem.SaveToFile(SavePaths.Directory, GameConstants.SaveSlot, data, Application.version);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AEON] No se pudo guardar la partida: " + e.Message);
                return false;
            }
        }

        private static List<UniverseRuleBase> CreateSpecialRules(Multiverse multiverse)
            => UniverseModuleRegistry.CreateSpecialRules(multiverse.universes.Select(u => u.rulesId));
    }
}
