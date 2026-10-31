using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GodGame.Domain;
using GodGame.Simulation;
using UnityEngine;

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
    /// Guardado local en JSON (JsonUtility) con escritura atómica y copia de seguridad.
    /// - Ruta: Application.persistentDataPath/saves/slot_N.json (iOS la incluye en el backup de iCloud).
    /// - Escritura atómica: se escribe .tmp y luego se reemplaza → nunca queda un archivo a medias.
    /// - Cloud Save (Unity Gaming Services): subir el mismo string del sobre (ver 06_arquitectura_tecnica.md).
    /// Si el JSON crece demasiado (multiversos grandes), sustituir JsonUtility por MessagePack-CSharp
    /// manteniendo esta misma API.
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 2;

        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");
        private static string SlotPath(int slot) => Path.Combine(SaveDirectory, $"slot_{slot}.json");

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

        public static string Serialize(SaveData data)
        {
            string payload = JsonUtility.ToJson(data);
            var envelope = new SaveEnvelope
            {
                version = CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                appVersion = Application.version,
                checksum = Sha256(payload),
                payload = payload
            };
            return JsonUtility.ToJson(envelope);
        }

        public static void Save(int slot, SaveData data)
        {
            Directory.CreateDirectory(SaveDirectory);
            string path = SlotPath(slot);
            string tmp = path + ".tmp";
            string bak = path + ".bak";

            File.WriteAllText(tmp, Serialize(data), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(tmp, path, bak);
            else File.Move(tmp, path);
        }

        /// <summary>Carga un slot. Si el archivo principal está corrupto, intenta con la copia .bak.</summary>
        public static bool TryLoad(int slot, out SaveData data, out string error)
        {
            string path = SlotPath(slot);
            if (TryLoadFile(path, out data, out error)) return true;
            string bak = path + ".bak";
            if (File.Exists(bak) && TryLoadFile(bak, out data, out string bakError))
            {
                error = "loaded_from_backup";
                return true;
            }
            return false;
        }

        public static bool TryDeserialize(string json, out SaveData data, out string error)
        {
            data = null;
            SaveEnvelope envelope;
            try { envelope = JsonUtility.FromJson<SaveEnvelope>(json); }
            catch (Exception e) { error = "invalid_json: " + e.Message; return false; }

            if (envelope == null || string.IsNullOrEmpty(envelope.payload)) { error = "empty"; return false; }
            if (envelope.checksum != Sha256(envelope.payload)) { error = "checksum_mismatch"; return false; }
            if (envelope.version > CurrentVersion) { error = "newer_version"; return false; } // pedir actualizar la app

            string payload = Migrate(envelope.payload, envelope.version);
            data = JsonUtility.FromJson<SaveData>(payload);
            error = null;
            return data?.multiverse != null;
        }

        /// <summary>Reconstruye el GameState jugable a partir de los datos y el catálogo de leyes.</summary>
        public static GameState Restore(SaveData data, IEnumerable<UniverseRules> rulesCatalog)
            => new GameState(data.multiverse, rulesCatalog, data.turn);

        public static void Delete(int slot)
        {
            foreach (var p in new[] { SlotPath(slot), SlotPath(slot) + ".bak" })
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
                        // v1 no tenía storyFlags ni difficulty. JsonUtility rellena los campos ausentes con
                        // los valores por defecto de la clase, así que basta con deserializar y volver a serializar.
                        var d = JsonUtility.FromJson<SaveData>(payload);
                        if (string.IsNullOrEmpty(d.difficulty)) d.difficulty = "normal";
                        payload = JsonUtility.ToJson(d);
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
