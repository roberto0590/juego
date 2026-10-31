using System.Collections.Generic;
using System.Text;
using GodGame.Core;
using GodGame.Domain;
using GodGame.Simulation;
using GodGame.Simulation.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace GodGame.Presentation
{
    /// <summary>
    /// HUD de la escena Game (uGUI por código). Solo muestra datos y reenvía las pulsaciones a GameController:
    ///  - barra superior: Fe, Energía Divina, Materia, Conocimiento, ciclo, etapa y universo;
    ///  - panel izquierdo: planeta seleccionado (◄ ►), datos, alcance, % de éxito por vía, milagros y conquistas;
    ///  - panel derecho: crónica (últimos eventos), avisos, Fin de ciclo, Vista, Guardar/Cargar y Menú;
    ///  - panel de victoria/derrota.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private GameController _controller;

        private Text _faith;
        private Text _energy;
        private Text _matter;
        private Text _knowledge;
        private Text _cycle;
        private Text _stage;
        private Text _universe;

        private Text _planetName;
        private Text _planetInfo;
        private Text _pending;
        private Text _log;
        private Text _feedback;

        private GameObject _endPanel;
        private Text _endTitle;
        private Text _endBody;

        private readonly List<Button> _orderButtons = new List<Button>();

        public void Build(GameController controller)
        {
            _controller = controller;
            UiFactory.EnsureEventSystem();
            Canvas canvas = UiFactory.CreateCanvas("HUD");
            canvas.transform.SetParent(transform, false);
            RectTransform root = UiFactory.CreateSafeArea(canvas.transform);

            BuildTopBar(root);
            BuildPlanetPanel(root);
            BuildRightPanel(root);
            BuildEndPanel(root); // último: se dibuja encima del resto
        }

        /// <summary>Vuelca el estado de la partida en el HUD.</summary>
        public void Refresh(GameSession session, Planet selected, string universeName, int selectedIndex, int planetCount)
        {
            if (session == null) return;
            GameState state = session.State;
            God player = session.Player;
            ResourceWallet wallet = player.resources;

            _faith.text = $"{TextosES.Fe} {wallet.faith:0}";
            _energy.text = $"{TextosES.EnergiaDivina} {wallet.divineEnergy:0}";
            _matter.text = $"{TextosES.Materia} {wallet.matter:0}";
            _knowledge.text = $"{TextosES.Conocimiento} {wallet.knowledge:0}";
            _cycle.text = $"Ciclo {state.Turn + 1}";
            _stage.text = TextosES.StageName(state.ComputeStage(player));
            _universe.text = universeName;

            int pending = 0;
            foreach (ICommand command in session.Turns.Pending)
                if (command.GodId == player.id) pending++;
            _pending.text = pending == 0 ? "Sin órdenes pendientes" : $"Órdenes pendientes: {pending}";

            if (selected == null)
            {
                _planetName.text = "Sin planeta";
                _planetInfo.text = "";
                return;
            }
            _planetName.text = $"{selected.name}  ({selectedIndex + 1}/{planetCount})";
            _planetInfo.text = DescribePlanet(state, player, selected);
        }

        public void ShowFeedback(string message)
        {
            if (_feedback != null) _feedback.text = message ?? "";
        }

        public void SetLog(IReadOnlyList<string> entries)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(entries[i]);
            }
            _log.text = entries.Count == 0 ? "Aún no ha pasado ningún ciclo." : sb.ToString();
        }

        /// <summary>Activa o desactiva las órdenes (al terminar la partida).</summary>
        public void SetOrdersInteractable(bool interactable)
        {
            foreach (Button button in _orderButtons) button.interactable = interactable;
        }

        public void ShowEndPanel(string title, string body)
        {
            _endTitle.text = title;
            _endBody.text = body;
            _endPanel.SetActive(true);
        }

        public void HideEndPanel()
        {
            _endPanel.SetActive(false);
        }

        // ---------------- Construcción ----------------

        private void BuildTopBar(RectTransform root)
        {
            RectTransform bar = UiFactory.CreatePanel(root, "BarraSuperior", UiFactory.PanelColor);
            UiFactory.SetAnchors(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -80f), new Vector2(-10f, -10f));
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 6, 6);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _faith = UiFactory.CreateLabel(bar, "", 26, TextAnchor.MiddleCenter);
            _energy = UiFactory.CreateLabel(bar, "", 26, TextAnchor.MiddleCenter);
            _matter = UiFactory.CreateLabel(bar, "", 26, TextAnchor.MiddleCenter);
            _knowledge = UiFactory.CreateLabel(bar, "", 26, TextAnchor.MiddleCenter);
            _cycle = UiFactory.CreateLabel(bar, "", 26, TextAnchor.MiddleCenter);
            _stage = UiFactory.CreateLabel(bar, "", 26, TextAnchor.MiddleCenter);
            _universe = UiFactory.CreateLabel(bar, "", 26, TextAnchor.MiddleCenter);
            _universe.color = new Color(1f, 0.85f, 0.45f, 1f);
        }

        private void BuildPlanetPanel(RectTransform root)
        {
            RectTransform panel = UiFactory.CreatePanel(root, "PanelPlaneta", UiFactory.PanelColor);
            UiFactory.SetAnchors(panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 10f), new Vector2(560f, -90f));
            UiFactory.AddVerticalLayout(panel.gameObject, 8f, 12);

            RectTransform header = UiFactory.CreateRow(panel, "Seleccion", 60f, forceExpand: false);
            Button previous = UiFactory.CreateButton(header, "◄", () => _controller.SelectPrevious(), 60f, 30);
            UiFactory.SetLayout(previous.gameObject, 60f, 80f, 0f);
            _planetName = UiFactory.CreateLabel(header, "", 28, TextAnchor.MiddleCenter);
            _planetName.fontStyle = FontStyle.Bold;
            UiFactory.SetLayout(_planetName.gameObject, 60f, -1f, 1f);
            Button next = UiFactory.CreateButton(header, "►", () => _controller.SelectNext(), 60f, 30);
            UiFactory.SetLayout(next.gameObject, 60f, 80f, 0f);

            _planetInfo = UiFactory.CreateLabel(panel, "", 22, TextAnchor.UpperLeft, 300f);

            UiFactory.CreateLabel(panel, "Milagros", 22, TextAnchor.MiddleLeft, 28f).color = UiFactory.MutedTextColor;
            RectTransform miracles = UiFactory.CreateRow(panel, "Milagros", 58f);
            AddOrder(UiFactory.CreateButton(miracles, "Bendición", () => _controller.OrderMiracle(MiracleType.Bendicion), 58f, 24));
            AddOrder(UiFactory.CreateButton(miracles, "Profeta", () => _controller.OrderMiracle(MiracleType.Profeta), 58f, 24));

            UiFactory.CreateLabel(panel, "Conquista", 22, TextAnchor.MiddleLeft, 28f).color = UiFactory.MutedTextColor;
            AddOrder(UiFactory.CreateButton(panel, "Conquista cultural", () => _controller.OrderConquest(ConquestType.Cultural), 54f, 24));
            AddOrder(UiFactory.CreateButton(panel, "Conquista militar", () => _controller.OrderConquest(ConquestType.Militar), 54f, 24));
            AddOrder(UiFactory.CreateButton(panel, "Conquista tecnológica", () => _controller.OrderConquest(ConquestType.Tecnologica), 54f, 24));

            _pending = UiFactory.CreateLabel(panel, "", 22, TextAnchor.MiddleCenter, 30f);
            _pending.color = UiFactory.MutedTextColor;
            AddOrder(UiFactory.CreateButton(panel, "Cancelar órdenes", () => _controller.CancelOrders(), 50f, 22));
        }

        private void BuildRightPanel(RectTransform root)
        {
            RectTransform panel = UiFactory.CreatePanel(root, "PanelCronica", UiFactory.PanelColor);
            UiFactory.SetAnchors(panel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-560f, 10f), new Vector2(-10f, -90f));
            UiFactory.AddVerticalLayout(panel.gameObject, 8f, 12);

            UiFactory.CreateLabel(panel, "Crónica", 24, TextAnchor.MiddleLeft, 32f).color = UiFactory.MutedTextColor;
            _log = UiFactory.CreateLabel(panel, "", 21, TextAnchor.UpperLeft, 330f);
            _feedback = UiFactory.CreateLabel(panel, "", 22, TextAnchor.UpperLeft, 120f);
            _feedback.color = new Color(1f, 0.9f, 0.6f, 1f);

            AddOrder(UiFactory.CreateButton(panel, "Fin de ciclo", () => _controller.EndCycle(), 72f, 30, UiFactory.AccentColor));
            UiFactory.CreateButton(panel, "Vista sistema/planeta", () => _controller.ToggleView(), 54f, 24);
            RectTransform saveRow = UiFactory.CreateRow(panel, "Guardado", 54f);
            UiFactory.CreateButton(saveRow, "Guardar", () => _controller.SaveGame(), 54f, 24);
            UiFactory.CreateButton(saveRow, "Cargar", () => _controller.LoadGame(), 54f, 24);
            UiFactory.CreateButton(panel, "Menú", () => _controller.GoToMenu(), 54f, 24);
        }

        private void BuildEndPanel(RectTransform root)
        {
            RectTransform overlay = UiFactory.CreatePanel(root, "PanelFinal", new Color(0f, 0f, 0f, 0.7f));
            UiFactory.Stretch(overlay);

            RectTransform box = UiFactory.CreatePanel(overlay, "Caja", UiFactory.PanelColor);
            UiFactory.SetAnchors(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-450f, -230f), new Vector2(450f, 230f));
            UiFactory.AddVerticalLayout(box.gameObject, 16f, 32);

            _endTitle = UiFactory.CreateLabel(box, "", 60, TextAnchor.MiddleCenter, 90f);
            _endTitle.fontStyle = FontStyle.Bold;
            _endBody = UiFactory.CreateLabel(box, "", 28, TextAnchor.MiddleCenter, 140f);
            RectTransform row = UiFactory.CreateRow(box, "Botones", 70f);
            UiFactory.CreateButton(row, "Volver al menú", () => _controller.GoToMenu(), 70f, 28, UiFactory.AccentColor);
            UiFactory.CreateButton(row, "Cerrar", HideEndPanel, 70f, 28);

            _endPanel = overlay.gameObject;
            _endPanel.SetActive(false);
        }

        private void AddOrder(Button button) => _orderButtons.Add(button);

        // ---------------- Texto del planeta ----------------

        private static string DescribePlanet(GameState state, God player, Planet planet)
        {
            var sb = new StringBuilder();
            sb.Append("Tipo: ").Append(TextosES.PlanetTypeName(planet.type))
              .Append($" · radio {planet.radius:0.0} terrestres\n");
            sb.Append("Dueño: ").Append(planet.HasOwner ? TextosES.GodName(state, planet.ownerGodId) : "nadie");
            if (planet.id == player.homePlanetId) sb.Append("  (tu planeta asignado)");
            sb.Append('\n');

            Civilization civ = planet.civilization;
            if (civ.Exists)
            {
                sb.Append(civ.name).Append(", adora a ").Append(TextosES.GodName(state, civ.worshippedGodId)).Append('\n');
                sb.Append($"Fe local {civ.faith * 100f:0} % · estabilidad {civ.stability * 100f:0} %\n");
                sb.Append($"Población {civ.population:0.#} M · tecnología {civ.techLevel}\n");
            }
            else
            {
                sb.Append("Sin civilización\n");
            }
            sb.Append($"Defensa {planet.defense}\n");

            if (planet.ownerGodId == player.id)
            {
                sb.Append("Es tuyo: usa Bendición para afianzar la fe.");
                return sb.ToString();
            }

            bool reachable = state.CanReach(player, planet, out float distanceFactor);
            sb.Append(reachable ? $"Alcance: sí (coste ×{distanceFactor:0.00})\n" : "Alcance: no (fuera de tu escala actual)\n");
            if (!reachable) return sb.ToString();

            sb.Append("Éxito estimado: ");
            if (civ.Exists)
                sb.Append($"cultural {Chance(state, player, planet, ConquestType.Cultural)} · ");
            sb.Append($"militar {Chance(state, player, planet, ConquestType.Militar)}");
            if (civ.Exists)
                sb.Append($" · tecnológica {Chance(state, player, planet, ConquestType.Tecnologica)}");
            return sb.ToString();
        }

        private static string Chance(GameState state, God player, Planet planet, ConquestType type)
            => $"{ConquerPlanetCommand.EstimateSuccessChance(state, player, planet, type) * 100f:0} %";
    }
}
