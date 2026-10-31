using System;
using System.IO;
using GodGame.Domain;
using GodGame.Save;
using GodGame.Simulation;
using NUnit.Framework;

namespace GodGame.Tests
{
    public class SaveSystemTests
    {
        private string _tempDirectory;

        [TearDown]
        public void TearDown()
        {
            if (_tempDirectory != null && Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, true);
            _tempDirectory = null;
        }

        [Test]
        public void IdaYVuelta_ConservaElEstado()
        {
            GameState state = StateAtTurn(12);
            string json = SaveSystem.Serialize(SaveSystem.Capture(state));

            Assert.IsTrue(SaveSystem.TryDeserialize(json, out SaveData data, out string error), error);
            Assert.AreEqual(12, data.turn);
            Assert.AreEqual(state.Multiverse.seed, data.seed);
            Assert.AreEqual(SaveJson.ToJson(state.Multiverse), SaveJson.ToJson(data.multiverse));
        }

        [Test]
        public void ArchivoManipulado_FallaElChecksum()
        {
            string json = SaveSystem.Serialize(SaveSystem.Capture(StateAtTurn(1)));
            SaveEnvelope envelope = SaveJson.FromJson<SaveEnvelope>(json);
            envelope.payload = envelope.payload.Replace("\"turn\":1", "\"turn\":999");
            string tampered = SaveJson.ToJson(envelope);

            Assert.IsFalse(SaveSystem.TryDeserialize(tampered, out _, out string error));
            Assert.AreEqual("checksum_mismatch", error);
        }

        [Test]
        public void VersionFutura_EsRechazada()
        {
            string json = SaveSystem.Serialize(SaveSystem.Capture(StateAtTurn(1)));
            SaveEnvelope envelope = SaveJson.FromJson<SaveEnvelope>(json);
            envelope.version = SaveSystem.CurrentVersion + 1;

            Assert.IsFalse(SaveSystem.TryDeserialize(SaveJson.ToJson(envelope), out _, out string error));
            Assert.AreEqual("newer_version", error);
        }

        [Test]
        public void GuardarYCargarArchivo_EnCarpetaTemporal()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "aeon-tests-" + Guid.NewGuid().ToString("N"));
            GameState state = StateAtTurn(3);
            string path = SaveSystem.SlotPath(_tempDirectory, 0);

            Assert.IsFalse(SaveSystem.HasSave(_tempDirectory, 0));
            Assert.IsFalse(SaveSystem.TryLoadFromFile(_tempDirectory, 0, out _, out string missing));
            Assert.AreEqual("not_found", missing);

            SaveSystem.SaveToFile(_tempDirectory, 0, SaveSystem.Capture(state), "test");
            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".bak"));

            SaveSystem.SaveToFile(_tempDirectory, 0, SaveSystem.Capture(state), "test");
            Assert.IsTrue(File.Exists(path + ".bak"), "El segundo guardado debe dejar copia .bak");
            Assert.IsTrue(SaveSystem.HasSave(_tempDirectory, 0));

            Assert.IsTrue(SaveSystem.TryLoadFromFile(_tempDirectory, 0, out SaveData data, out string error), error);
            Assert.IsTrue(error == null, "Carga directa, sin recurrir a la copia: " + error);
            Assert.AreEqual(3, data.turn);
            Assert.AreEqual(SaveJson.ToJson(state.Multiverse), SaveJson.ToJson(data.multiverse));

            SaveSystem.Delete(_tempDirectory, 0);
            Assert.IsFalse(SaveSystem.HasSave(_tempDirectory, 0));
        }

        [Test]
        public void Restore_ReconstruyeIndices()
        {
            var catalog = TestRules.CatalogMvp();
            GameState state = StateAtTurn(2);
            God player = TestRules.Player(state);
            string json = SaveSystem.Serialize(SaveSystem.Capture(state));

            Assert.IsTrue(SaveSystem.TryDeserialize(json, out SaveData data, out string error), error);
            GameState restored = SaveSystem.Restore(data, catalog);

            Assert.AreEqual(2, restored.Turn);
            Planet home = restored.GetPlanet(player.homePlanetId);
            Assert.IsNotNull(home);
            Assert.AreEqual(player.homePlanetId, home.id);
            Assert.IsNotNull(restored.GetSystem(home.systemId));
            Assert.AreEqual(state.Planets.Count, restored.Planets.Count);
        }

        private static GameState StateAtTurn(int turn)
        {
            GameState state = TestRules.NewState(2024, TestRules.CatalogMvp());
            var turns = new TurnManager(state);
            for (int i = 0; i < turn; i++) turns.ResolveTurn();
            return state;
        }
    }
}
