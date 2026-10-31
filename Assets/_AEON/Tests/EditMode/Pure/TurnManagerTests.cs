using System.Linq;
using GodGame.AI;
using GodGame.Domain;
using GodGame.Generation;
using GodGame.Save;
using GodGame.Simulation;
using GodGame.Simulation.Commands;
using NUnit.Framework;

namespace GodGame.Tests
{
    public class TurnManagerTests
    {
        [Test]
        public void AlInicio_ElJugadorSoloAlcanzaSuPlanetaAsignado()
        {
            GameState state = TestRules.NewState(11, TestRules.CatalogMvp());
            God player = TestRules.Player(state);

            var reachable = state.ReachableTargets(player).ToList();
            Assert.AreEqual(1, reachable.Count);
            Assert.AreEqual(player.homePlanetId, reachable[0].id);
            Assert.AreEqual(ProgressionStage.Planetaria, state.ComputeStage(player));
        }

        [Test]
        public void ConquistaCultural_ConvierteEnVariosCiclos()
        {
            GameState state = TestRules.NewState(21, TestRules.CatalogMvp());
            var turns = new TurnManager(state);
            God player = TestRules.Player(state);
            Planet home = state.GetPlanet(player.homePlanetId);

            int cycles = 0;
            while (home.ownerGodId != player.id && cycles < 40)
            {
                turns.Submit(new ConquerPlanetCommand(player.id, home.id, ConquestType.Cultural), out _);
                turns.ResolveTurn();
                cycles++;
            }

            Assert.AreEqual(player.id, home.ownerGodId, "El jugador debería convertir su planeta asignado");
            Assert.LessOrEqual(2, cycles, "La conversión no debe ser instantánea");
            Assert.LessOrEqual(cycles, 20, "La conversión no debe eternizarse");
        }

        [Test]
        public void Resolucion_EsDeterminista()
        {
            Assert.AreEqual(Simulate(77, 50), Simulate(77, 50));
        }

        [Test]
        public void ComandoSinRecursos_EsRechazado()
        {
            GameState state = TestRules.NewState(31, TestRules.CatalogMvp());
            var turns = new TurnManager(state);
            God player = TestRules.Player(state);
            player.resources = new ResourceWallet();

            bool accepted = turns.Submit(new ConquerPlanetCommand(player.id, player.homePlanetId, ConquestType.Cultural), out string reason);
            Assert.IsFalse(accepted);
            Assert.AreEqual("resources.insufficient", reason);
        }

        [Test]
        public void Milagro_SeResuelveAntesQueConquista()
        {
            GameState state = TestRules.NewState(41, TestRules.CatalogMvp());
            var turns = new TurnManager(state);
            God player = TestRules.Player(state);
            player.resources = new ResourceWallet { faith = 200f, divineEnergy = 200f, matter = 200f, knowledge = 200f };

            // Se envía primero la conquista (prioridad 10) y luego el milagro (prioridad 0).
            Assert.IsTrue(turns.Submit(new ConquerPlanetCommand(player.id, player.homePlanetId, ConquestType.Cultural), out _));
            Assert.IsTrue(turns.Submit(new MiracleCommand(player.id, player.homePlanetId, MiracleType.Profeta), out _));

            TurnReport report = turns.ResolveTurn();
            int miracle = report.Events.FindIndex(e => e.type == TurnEventType.Miracle);
            int conquest = report.Events.FindIndex(e => e.type == TurnEventType.Conquest);
            Assert.LessOrEqual(0, miracle);
            Assert.LessOrEqual(0, conquest);
            Assert.Greater(conquest, miracle, "El milagro (prioridad 0) se resuelve antes que la conquista (10)");
        }

        [Test]
        public void TurnManager_AplicaComandoYCobra()
        {
            GameState state = TestRules.NewState(51, TestRules.CatalogMvp());
            var turns = new TurnManager(state);
            God player = TestRules.Player(state);
            Planet home = state.GetPlanet(player.homePlanetId);

            // Planeta propio, sin población: su Fe generada es 0 y el saldo solo depende del coste y del ingreso base.
            home.ownerGodId = player.id;
            home.civilization.worshippedGodId = player.id;
            home.civilization.faith = 0.5f;
            home.civilization.population = 0f;
            float faithBefore = player.resources.faith;
            float civFaithBefore = home.civilization.faith;
            UniverseRulesData rules = state.GetRulesForPlanet(home);

            Assert.IsTrue(turns.Submit(new MiracleCommand(player.id, home.id, MiracleType.Bendicion), out string reason), reason);
            turns.ResolveTurn();

            float expected = faithBefore - 5f * rules.modifiers.miracleCost + EconomyFormulas.BaseDivineIncome.faith;
            Assert.AreEqual(expected, player.resources.faith, 1e-4f);
            Assert.Greater(home.civilization.faith, civFaithBefore, "La Bendición sube la fe del planeta");
        }

        [Test]
        public void JugadorSinPlanetas_RecibeIngresoBase()
        {
            GameState state = TestRules.NewState(61, TestRules.CatalogMvp());
            var turns = new TurnManager(state);
            God player = TestRules.Player(state);
            Assert.AreEqual(0, state.PlanetsOwnedBy(player.id).Count());

            float faith = player.resources.faith, energy = player.resources.divineEnergy;
            float matter = player.resources.matter, knowledge = player.resources.knowledge;
            turns.ResolveTurn();

            ResourceCost income = EconomyFormulas.BaseDivineIncome;
            Assert.AreEqual(faith + income.faith, player.resources.faith, 1e-4f);
            Assert.AreEqual(energy + income.divineEnergy, player.resources.divineEnergy, 1e-4f);
            Assert.AreEqual(matter + income.matter, player.resources.matter, 1e-4f);
            Assert.AreEqual(knowledge + income.knowledge, player.resources.knowledge, 1e-4f);
        }

        [Test]
        public void ReglaEspecial_SeInvocaEnLaEconomia()
        {
            var catalog = TestRules.CatalogMvp();
            Multiverse multiverse = MultiverseGenerator.Generate(71, catalog);
            string startRulesId = multiverse.FindUniverse(multiverse.playerStartUniverseId).rulesId;
            var counter = new CountingRule(startRulesId);

            var state = new GameState(multiverse, catalog, 0, new UniverseRuleBase[] { counter });
            var turns = new TurnManager(state);
            turns.ResolveTurn();
            turns.ResolveTurn();

            Assert.AreEqual(2, counter.Calls);
            Assert.AreEqual(multiverse.playerStartUniverseId, counter.LastUniverseId);
        }

        // ---------------- Utilidades ----------------

        private static string Simulate(int seed, int cycles)
        {
            GameState state = TestRules.NewState(seed, TestRules.CatalogMvp());
            var turns = new TurnManager(state);
            foreach (God god in state.Multiverse.gods)
                if (!god.isPlayer) turns.RegisterSource(new GodUtilityAI(god.id));
            for (int i = 0; i < cycles; i++) turns.ResolveTurn();
            return SaveJson.ToJson(state.Multiverse) + "|turn=" + state.Turn;
        }

        /// <summary>Regla falsa que cuenta las llamadas a la fase de economía.</summary>
        private sealed class CountingRule : UniverseRuleBase
        {
            private readonly string _rulesId;
            public int Calls;
            public string LastUniverseId;

            public CountingRule(string rulesId) { _rulesId = rulesId; }

            public override string RulesId => _rulesId;

            public override void OnEconomyPhase(GameState state, Universe universe, TurnReport report)
            {
                Calls++;
                LastUniverseId = universe.id;
            }
        }
    }
}
