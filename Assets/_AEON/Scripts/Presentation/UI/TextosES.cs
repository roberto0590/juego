using System;
using System.Collections.Generic;
using GodGame.Core;
using GodGame.Domain;
using GodGame.Generation;
using GodGame.Rules;
using GodGame.Simulation;
using GodGame.Simulation.Commands;

namespace GodGame.Presentation
{
    /// <summary>
    /// Textos en español de la UI. La simulación solo produce claves (messageKey / motivos de rechazo);
    /// aquí se traducen. Una clave desconocida se muestra tal cual para detectarla en pruebas.
    /// (Sustituible por Unity Localization en una fase posterior.)
    /// </summary>
    public static class TextosES
    {
        public const string UniverseUnlockedPrefix = "universe.unlocked:";

        public const string Fe = "Fe";
        public const string EnergiaDivina = "Energía Divina";
        public const string Materia = "Materia";
        public const string Conocimiento = "Conocimiento";

        private static readonly Dictionary<string, string> Textos = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Conquistas
            { "conquest.militar.success", "conquista militar lograda" },
            { "conquest.militar.fail", "la conquista militar fracasa" },
            { "conquest.cultural.success", "conversión cultural completada: el pueblo le adora" },
            { "conquest.cultural.fail", "la fe local se debilita (conversión en curso)" },
            { "conquest.tecnologica.success", "elevación tecnológica: el pueblo adopta a su nuevo dios" },
            { "conquest.tecnologica.fail", "regala conocimiento, pero el pueblo aún duda" },

            // Milagros
            { "miracle.bendicion", "obra una Bendición" },
            { "miracle.profeta", "envía un Profeta" },
            { "miracle.revelacion", "concede una Revelación" },
            { "miracle.cataclismo", "desata un Cataclismo" },

            // Eventos
            { "event.heresy", "una herejía reduce la fe" },
            { "stage.planetaria", "vuelve a la escala Planetaria" },
            { "stage.sistemica", "alcanza la escala Sistémica: ya llega a todo su sistema solar" },
            { "stage.galactica", "alcanza la escala Galáctica: ya llega a otros sistemas" },
            { "stage.universal", "alcanza la escala Universal: ya llega a otras galaxias" },
            { "stage.multiversal", "alcanza la escala Multiversal: la Puerta del Velo se abre" },
            { "god.defeated", "ha sido derrotado" },
            { "victory.multiverse", "domina el multiverso" },
            // Las claves de las reglas especiales las aporta cada módulo (IUniverseTexts).

            // Rechazos
            { "god.invalid", "dios no válido" },
            { "planet.invalid", "planeta no válido" },
            { "planet.already_owned", "el planeta ya es tuyo" },
            { "planet.no_civilization", "el planeta no tiene civilización" },
            { "planet.unreachable", "el planeta está fuera de tu alcance" },
            { "resources.insufficient", "recursos insuficientes" },
            { "miracle.requires_own_planet", "este milagro requiere un planeta propio" },
            { "miracle.requires_foreign_planet", "este milagro requiere un planeta ajeno" },

            // Errores de carga (SaveSystem / GameSession)
            { "not_found", "No hay ninguna partida guardada." },
            { "checksum_mismatch", "El archivo de guardado está dañado." },
            { "newer_version", "La partida es de una versión más nueva del juego: actualiza la app." },
            { "loaded_from_backup", "El guardado principal estaba dañado: se cargó la copia de seguridad." },
            { "empty", "El archivo de guardado está vacío." }
        };

        /// <summary>Traduce una clave. Con estado, resuelve también las claves con parámetros (universo desbloqueado).</summary>
        public static string Translate(string key, GameState state = null)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.StartsWith(UniverseUnlockedPrefix, StringComparison.Ordinal))
            {
                string universeId = key.Substring(UniverseUnlockedPrefix.Length);
                return "se abre la Puerta del Velo hacia " + UniverseName(state, universeId);
            }
            if (Textos.TryGetValue(key, out string text)) return text;
            return UniverseModuleRegistry.TryGetText(key, out string moduleText) ? moduleText : key;
        }

        /// <summary>Línea de registro: "Dios: texto (planeta)". Los rechazos se marcan como tales.</summary>
        public static string Format(TurnEvent e, GameState state)
        {
            string who = GodName(state, e.godId);
            string text = Translate(e.messageKey, state);
            if (e.type == TurnEventType.Rejected) text = "orden rechazada, " + text;

            Planet planet = state != null ? state.GetPlanet(e.planetId) : null;
            return planet != null ? $"{who}: {text} ({planet.name})" : $"{who}: {text}";
        }

        /// <summary>Mensaje de error de carga en español.</summary>
        public static string LoadError(string error)
        {
            if (string.IsNullOrEmpty(error)) return "";
            if (error.StartsWith(GameSession.MissingUniverseErrorPrefix, StringComparison.Ordinal))
                return "La partida usa un universo que no está en esta versión ("
                       + error.Substring(GameSession.MissingUniverseErrorPrefix.Length) + ").";
            if (error.StartsWith("invalid_json:", StringComparison.Ordinal)) return "El archivo de guardado no es válido.";
            if (error.StartsWith("io:", StringComparison.Ordinal)) return "No se pudo leer el archivo de guardado.";
            if (error.StartsWith("restore:", StringComparison.Ordinal)) return "No se pudo reconstruir la partida guardada.";
            return Translate(error);
        }

        public static string GodName(GameState state, string godId)
        {
            if (string.IsNullOrEmpty(godId)) return "Nadie";
            if (godId == MultiverseGenerator.AncestorsGodId) return "los Ancestros";
            God god = state != null ? state.GetGod(godId) : null;
            return god != null ? god.name : godId;
        }

        /// <summary>Nombre visible de un universo de la partida (displayName de sus reglas).</summary>
        public static string UniverseName(GameState state, string universeId)
        {
            Universe universe = state != null ? state.GetUniverse(universeId) : null;
            if (universe == null) return universeId;
            UniverseRulesData rules = state.GetRules(universeId);
            return !string.IsNullOrEmpty(rules.displayName) ? rules.displayName : universe.rulesId;
        }

        public static string PlanetTypeName(PlanetType type)
        {
            switch (type)
            {
                case PlanetType.Rocoso: return "Rocoso";
                case PlanetType.Oceanico: return "Oceánico";
                case PlanetType.Desertico: return "Desértico";
                case PlanetType.Helado: return "Helado";
                case PlanetType.GiganteGaseoso: return "Gigante gaseoso";
                case PlanetType.Volcanico: return "Volcánico";
                default: return "Exótico";
            }
        }

        public static string StageName(ProgressionStage stage)
        {
            switch (stage)
            {
                case ProgressionStage.Planetaria: return "Escala Planetaria";
                case ProgressionStage.Sistemica: return "Escala Sistémica";
                case ProgressionStage.Galactica: return "Escala Galáctica";
                case ProgressionStage.Universal: return "Escala Universal";
                default: return "Escala Multiversal";
            }
        }

        public static string MiracleName(MiracleType type)
        {
            switch (type)
            {
                case MiracleType.Bendicion: return "Bendición";
                case MiracleType.Profeta: return "Profeta";
                case MiracleType.Revelacion: return "Revelación";
                default: return "Cataclismo";
            }
        }

        public static string ConquestName(ConquestType type)
        {
            switch (type)
            {
                case ConquestType.Militar: return "Conquista militar";
                case ConquestType.Cultural: return "Conquista cultural";
                default: return "Conquista tecnológica";
            }
        }
    }
}
