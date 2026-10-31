using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GodGame.Domain;
using GodGame.Simulation;

namespace GodGame.Save
{
    /// <summary>Contenido de la partida. Solo datos: el estado completo del multiverso + metadatos.</summary>
    [Serializable]
    public class SaveData
    {
        public int seed;
        public int turn;
        public string difficulty = "normal";
        public Multiverse multiverse;
        public List<string> storyFlags = new List<string>(); // decisiones de trama (finales múltiples)
    }

    /// <summary>
    /// Sobre exterior: versión + suma de verificación + carga útil.
    /// La versión permite migrar partidas antiguas tras una actualización del juego.
    /// El checksum detecta archivos corruptos (cortes de batería durante la escritura); no es anti-trampas.
    /// </summary>
    [Serializable]
    public class SaveEnvelope
    {
        public int version;
        public string savedAtUtc;
        public string appVersion;
        public string checksum;
        public string payload; // JSON de SaveData
    }

    /// <summary>
    /// Guardado local en JSON (Newtonsoft, ver SaveJson) con escritura atómica y copia de seguridad.
    /// - El directorio lo decide quien llama (en Unity: Application.persistentDataPath/saves, ver Core/SavePaths);
    ///   así esta clase es C# puro y se prueba en dotnet con una carpeta temporal.
    /// - Archivo: &lt;directorio&gt;/slot_N.json; copia anterior en slot_N.json.bak.
    /// - Escritura atómica: se escribe .tmp y luego se reemplaza → nunca queda un archivo a medias.
    /// - Cloud Save (Unity Gaming Services): subir el mismo string del sobre (ver 06_arquitectura_tecnica.md).
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 2;

        public static string SlotPath(string directory, int slot) => Path.Combine(directory, $"slot_{slot}.json");

        public static SaveData Capture(GameState state, IEnumerable<string> storyFlags = null, string difficulty = "normal")
        {
            var data = new SaveData
            {
                seed = state.Multiverse.seed,
                turn = state.Turn,
                difficulty = difficulty,
                multiverse = state.Multiverse
            };
            if (storyFlags != null) data.storyFlags.AddRange(storyFlags);
            return data;
        }

        public static string Serialize(SaveData data, string appVersion = "dev")
        {
            string payload = SaveJson.ToJson(data);
            var envelope = new SaveEnvelope
            {
                version = CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                appVersion = appVersion,
                checksum = Sha256(payload),
                payload = payload
            };
            return SaveJson.ToJson(envelope);
        }

        public static void SaveToFile(string directory, int slot, SaveData data, string appVersion = "dev")
        {
            Directory.CreateDirectory(directory);
            string path = SlotPath(directory, slot);
            string tmp = path + ".tmp";
            string bak = path + ".bak";

            File.WriteAllText(tmp, Serialize(data, appVersion), Encoding.UTF8);
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, bak);
                }
                catch (Exception e) when (e is PlatformNotSupportedException || e is IOException)
                {
                    // Algunas plataformas/sistemas de archivos no admiten File.Replace: equivalente no atómico.
                    File.Copy(path, bak, true);
                    File.Delete(path);
                    File.Move(tmp, path);
                }
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        /// <summary>Carga un slot. Si el archivo principal está corrupto, intenta con la copia .bak.</summary>
        public static bool TryLoadFromFile(string directory, int slot, out SaveData data, out string error)
        {
            string path = SlotPath(directory, slot);
            if (TryLoadFile(path, out data, out error)) return true;
            string bak = path + ".bak";
            if (File.Exists(bak) && TryLoadFile(bak, out SaveData bakData, out _))
            {
                data = bakData;
                error = "loaded_from_backup";
                return true;
            }
            return false;
        }

        public static bool HasSave(string directory, int slot)
        {
            if (string.IsNullOrEmpty(directory)) return false;
            string path = SlotPath(directory, slot);
            return File.Exists(path) || File.Exists(path + ".bak");
        }

        public static bool TryDeserialize(string json, out SaveData data, out string error)
        {
            data = null;
            SaveEnvelope envelope;
            try { envelope = SaveJson.FromJson<SaveEnvelope>(json); }
            catch (Exception e) { error = "invalid_json: " + e.Message; return false; }

            if (envelope == null || string.IsNullOrEmpty(envelope.payload)) { error = "empty"; return false; }
            if (envelope.checksum != Sha256(envelope.payload)) { error = "checksum_mismatch"; return false; }
            if (envelope.version > CurrentVersion) { error = "newer_version"; return false; } // pedir actualizar la app

            string payload = Migrate(envelope.payload, envelope.version);
            data = SaveJson.FromJson<SaveData>(payload);
            error = null;
            return data?.multiverse != null;
        }

        /// <summary>Reconstruye el GameState jugable a partir de los datos, el catálogo de leyes y las reglas especiales.</summary>
        public static GameState Restore(SaveData data, IEnumerable<UniverseRulesData> rulesCatalog,
                                        IEnumerable<UniverseRuleBase> specialRules = null)
            => new GameState(data.multiverse, rulesCatalog, data.turn, specialRules);

        public static void Delete(string directory, int slot)
        {
            string path = SlotPath(directory, slot);
            foreach (var p in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }

        private static bool TryLoadFile(string path, out SaveData data, out string error)
        {
            data = null;
            if (!File.Exists(path)) { error = "not_found"; return false; }
            try { return TryDeserialize(File.ReadAllText(path, Encoding.UTF8), out data, out error); }
            catch (IOException e) { error = "io: " + e.Message; return false; }
        }

        /// <summary>
        /// Migraciones encadenadas v1→v2→…→actual. Cada paso transforma el JSON de la carga útil.
        /// Nunca borrar pasos antiguos: un jugador puede volver tras meses sin actualizar.
        /// </summary>
        private static string Migrate(string payload, int fromVersion)
        {
            int v = fromVersion;
            while (v < CurrentVersion)
            {
                switch (v)
                {
                    case 1:
                        // v1 no tenía storyFlags ni difficulty. Al deserializar, los campos ausentes quedan con
                        // los valores por defecto de la clase, así que basta con deserializar y volver a serializar.
                        var d = SaveJson.FromJson<SaveData>(payload);
                        if (string.IsNullOrEmpty(d.difficulty)) d.difficulty = "normal";
                        if (d.storyFlags == null) d.storyFlags = new List<string>();
                        payload = SaveJson.ToJson(d);
                        break;
                }
                v++;
            }
            return payload;
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
