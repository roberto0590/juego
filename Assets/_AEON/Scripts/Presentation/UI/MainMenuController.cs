using System;
using System.Globalization;
using GodGame.Core;
using GodGame.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GodGame.Presentation
{
    /// <summary>
    /// Único componente de la escena MainMenu: crea cámara (si falta), fondo y menú uGUI por código.
    /// Nueva partida con semilla (editable o aleatoria) o Continuar la partida guardada.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0.03f, 0.02f, 0.08f, 1f);

        private InputField _seedField;
        private Text _status;

        private void Start()
        {
            EnsureCamera();
            LoadedCatalog catalog = UniverseCatalogLoader.Load();
            BuildUi(catalog);
        }

        private static void EnsureCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var go = new GameObject("Camara principal");
                go.tag = "MainCamera";
                mainCamera = go.AddComponent<Camera>();
            }
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = BackgroundColor;
        }

        private void BuildUi(LoadedCatalog catalog)
        {
            UiFactory.EnsureEventSystem();
            Canvas canvas = UiFactory.CreateCanvas("MenuPrincipal");
            canvas.transform.SetParent(transform, false);
            RectTransform root = UiFactory.CreateSafeArea(canvas.transform);

            RectTransform panel = UiFactory.CreatePanel(root, "Panel", UiFactory.PanelColor);
            UiFactory.SetAnchors(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(-520f, -470f), new Vector2(520f, 470f));
            UiFactory.AddVerticalLayout(panel.gameObject, 14f, 32);

            Text title = UiFactory.CreateLabel(panel, "ÆON: Guerra de los Siete Velos", 60, TextAnchor.MiddleCenter, 90f);
            title.fontStyle = FontStyle.Bold;
            Text subtitle = UiFactory.CreateLabel(panel,
                "Eres un dios recién nacido. Conquista tu planeta, después tu sistema, tu universo y los Siete Velos.",
                26, TextAnchor.MiddleCenter, 80f);
            subtitle.color = UiFactory.MutedTextColor;

            UiFactory.CreateLabel(panel, "Universos disponibles (tu universo inicial se sortea con la semilla):", 26,
                                  TextAnchor.MiddleLeft, 40f);
            foreach (UniverseRulesData rules in catalog.Rules)
            {
                UniversePresentation presentation = catalog.GetPresentation(rules.id);
                Text item = UiFactory.CreateLabel(panel, "•  " + presentation.DisplayName, 28, TextAnchor.MiddleLeft, 40f);
                item.color = presentation.Defaults.secondaryColor.ToColor();
            }
            if (catalog.Rules.Count == 0)
                UiFactory.CreateLabel(panel, "No hay universos: añade un módulo en Assets/_AEON/Universes.", 26, TextAnchor.MiddleLeft, 40f);

            RectTransform seedRow = UiFactory.CreateRow(panel, "FilaSemilla", 64f, forceExpand: false);
            Text seedLabel = UiFactory.CreateLabel(seedRow, "Semilla", 28, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(seedLabel.gameObject, 60f, 160f, 0f);
            _seedField = UiFactory.CreateInputField(seedRow, "Semilla", 60f);
            UiFactory.SetLayout(_seedField.gameObject, 60f, -1f, 1f);
            _seedField.contentType = InputField.ContentType.IntegerNumber;
            _seedField.text = (Environment.TickCount & 0x7FFFFFFF).ToString(CultureInfo.InvariantCulture);

            RectTransform buttons = UiFactory.CreateRow(panel, "FilaBotones", 72f);
            UiFactory.CreateButton(buttons, "Semilla aleatoria", OnRandomSeed, 72f, 28);
            Button newGame = UiFactory.CreateButton(buttons, "Nueva partida", OnNewGame, 72f, 28, UiFactory.AccentColor);
            newGame.interactable = catalog.Rules.Count > 0;

            Button continueButton = UiFactory.CreateButton(panel, "Continuar", OnContinue, 72f, 28);
            continueButton.interactable = catalog.Rules.Count > 0 && GameSession.HasSave();

            _status = UiFactory.CreateLabel(panel, "", 24, TextAnchor.MiddleCenter, 60f);
            _status.color = UiFactory.MutedTextColor;
        }

        private void OnRandomSeed()
        {
            _seedField.text = SessionRequest.RandomSeed().ToString(CultureInfo.InvariantCulture);
        }

        private void OnNewGame()
        {
            SessionRequest.SetNewGame(ParseSeed(_seedField.text));
            _status.text = "Creando el multiverso…";
            SceneManager.LoadScene(GameConstants.GameScene);
        }

        private void OnContinue()
        {
            SessionRequest.SetLoad();
            _status.text = "Cargando partida…";
            SceneManager.LoadScene(GameConstants.GameScene);
        }

        /// <summary>Número entero → semilla; cualquier otro texto se convierte con el hash estable del núcleo.</summary>
        private static int ParseSeed(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return SessionRequest.RandomSeed();
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)) return seed;
            return RandomExtensions.DeriveSeed(0, text.Trim()) & 0x7FFFFFFF;
        }
    }
}
