using System.Collections.Generic;
using GodGame.Core;
using GodGame.Domain;
using GodGame.Simulation;
using GodGame.Simulation.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GodGame.Presentation
{
    /// <summary>
    /// Único componente de la escena Game. Flujo del slice:
    /// petición del menú (o semilla aleatoria) → GameSession → sistema solar del planeta asignado
    /// (StarSystemView + PlanetView) → cámara con zoom → HUD → órdenes (Submit) → Fin de ciclo (EndTurn,
    /// la IA rival actúa y se guarda automáticamente) → Guardar/Cargar → Menú.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const int MaxLogEntries = 6;

        private LoadedCatalog _catalog;
        private GameSession _session;
        private Camera _camera;
        private Light _sun;
        private CameraZoomController _zoom;
        private StarSystemView _systemView;
        private HudController _hud;
        private StarSystem _system;
        private string _universeName = "";
        private int _selectedIndex;
        private bool _gameOver;
        private readonly List<string> _log = new List<string>();

        private bool HasSession => _session != null && _systemView != null && _system != null && _system.planets.Count > 0;
        private Planet SelectedPlanet => HasSession ? _system.planets[_selectedIndex] : null;
        private string PlayerId => _session.State.Multiverse.playerGodId;

        private void Start()
        {
            _catalog = UniverseCatalogLoader.Load();
            EnsureCameraAndLight();

            _hud = gameObject.AddComponent<HudController>();
            _hud.Build(this);
            _hud.SetLog(_log);

            if (_catalog.Rules.Count == 0)
            {
                _hud.SetOrdersInteractable(false);
                _hud.ShowFeedback("No hay universos disponibles: añade un módulo en Assets/_AEON/Universes.");
                return;
            }

            string message = StartSession();
            ShowPlayerSystem();
            _hud.ShowFeedback(message);
        }

        private void LateUpdate()
        {
            // La luz direccional sale de la estrella hacia el planeta enfocado.
            if (_sun == null || _systemView == null || _systemView.Star == null || _zoom == null || _zoom.Focus == null) return;
            Vector3 direction = _zoom.Focus.position - _systemView.Star.position;
            if (direction.sqrMagnitude > 1e-6f) _sun.transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _session != null) _session.Save();
        }

        // ---------------- Acciones del HUD ----------------

        public void SelectPrevious()
        {
            if (!HasSession) return;
            int count = _system.planets.Count;
            _selectedIndex = (_selectedIndex - 1 + count) % count;
            FocusSelected();
            RefreshHud();
        }

        public void SelectNext()
        {
            if (!HasSession) return;
            _selectedIndex = (_selectedIndex + 1) % _system.planets.Count;
            FocusSelected();
            RefreshHud();
        }

        public void OrderMiracle(MiracleType type)
        {
            if (!HasSession) return;
            SubmitOrder(new MiracleCommand(PlayerId, SelectedPlanet.id, type), TextosES.MiracleName(type));
        }

        public void OrderConquest(ConquestType type)
        {
            if (!HasSession) return;
            SubmitOrder(new ConquerPlanetCommand(PlayerId, SelectedPlanet.id, type), TextosES.ConquestName(type));
        }

        public void CancelOrders()
        {
            if (!HasSession) return;
            _session.CancelPending();
            _hud.ShowFeedback("Órdenes canceladas.");
            RefreshHud();
        }

        public void EndCycle()
        {
            if (!HasSession || _gameOver) return;
            GameState state = _session.State;
            TurnReport report = _session.EndTurn();

            foreach (TurnEvent e in report.Events)
            {
                // Los rechazos internos de la IA rival no aportan nada al jugador.
                if (e.type == TurnEventType.Rejected && e.godId != PlayerId) continue;
                AddLog($"[C{report.Turn + 1}] {TextosES.Format(e, state)}");
            }
            _hud.SetLog(_log);
            _systemView.RefreshOwners(state, PlayerId);
            _hud.ShowFeedback($"Ciclo {report.Turn + 1} resuelto. Partida guardada.");

            God player = _session.Player;
            if (report.WinnerGodId != null)
            {
                bool playerWins = report.WinnerGodId == PlayerId;
                EndGame(playerWins ? "¡Victoria!" : "Derrota",
                        playerWins
                            ? "Los Siete Velos se inclinan ante ti: dominas el multiverso."
                            : TextosES.GodName(state, report.WinnerGodId) + " domina el multiverso.");
            }
            else if (player != null && player.defeated)
            {
                EndGame("Derrota", "Has perdido tu último planeta y tu planeta asignado tiene otro dios.");
            }
            RefreshHud();
        }

        public void SaveGame()
        {
            if (_session == null) return;
            _hud.ShowFeedback(_session.Save() ? "Partida guardada." : "No se pudo guardar la partida.");
        }

        public void LoadGame()
        {
            if (!GameSession.TryLoad(_catalog, out GameSession loaded, out string error))
            {
                _hud.ShowFeedback(TextosES.LoadError(error));
                return;
            }
            _session = loaded;
            _gameOver = false;
            _log.Clear();
            _hud.SetLog(_log);
            _hud.HideEndPanel();
            _hud.SetOrdersInteractable(true);
            ShowPlayerSystem();
            _hud.ShowFeedback(error == "loaded_from_backup" ? TextosES.LoadError(error) : "Partida cargada.");
        }

        public void ToggleView()
        {
            if (_zoom != null) _zoom.ToggleZoom();
        }

        public void GoToMenu()
        {
            if (_session != null) _session.Save();
            SceneManager.LoadScene(GameConstants.MainMenuScene);
        }

        // ---------------- Internos ----------------

        /// <summary>Crea la sesión según la petición del menú; sin petición, partida nueva con semilla aleatoria.</summary>
        private string StartSession()
        {
            string prefix = "";
            int seed;
            if (SessionRequest.Consume(out SessionMode mode, out int requestedSeed))
            {
                if (mode == SessionMode.Cargar)
                {
                    if (GameSession.TryLoad(_catalog, out _session, out string error))
                        return error == "loaded_from_backup" ? TextosES.LoadError(error) : "Partida cargada.";
                    prefix = TextosES.LoadError(error) + " Se inicia una partida nueva. ";
                    seed = SessionRequest.RandomSeed();
                }
                else
                {
                    seed = requestedSeed;
                }
            }
            else
            {
                seed = SessionRequest.RandomSeed();
            }

            _session = GameSession.NewGame(_catalog, seed);
            Planet home = _session.PlayerHomePlanet;
            return prefix + $"Semilla {seed}. Tu planeta asignado es {home.name}: conquístalo (empieza por Profeta y Conquista cultural).";
        }

        private void ShowPlayerSystem()
        {
            if (_systemView != null) Destroy(_systemView.gameObject);

            GameState state = _session.State;
            Planet home = _session.PlayerHomePlanet;
            _system = state.GetSystem(home.systemId);
            Universe universe = state.GetUniverse(home.universeId);
            UniversePresentation presentation = _catalog.GetPresentation(universe.rulesId);
            _universeName = presentation.DisplayName;

            Color background = presentation.Defaults.nebulaColor.ToColor() * 0.35f;
            background.a = 1f;
            _camera.backgroundColor = background;

            var go = new GameObject("Sistema");
            _systemView = go.AddComponent<StarSystemView>();
            _systemView.Build(_system, universe, presentation, _catalog, state);
            _systemView.RefreshOwners(state, PlayerId);
            _zoom.SetSystemExtent(_systemView.SystemExtent);

            _selectedIndex = Mathf.Max(0, _system.planets.IndexOf(home));
            FocusSelected();
            RefreshHud();
        }

        private void FocusSelected()
        {
            PlanetView view = _systemView.GetPlanetView(SelectedPlanet.id);
            if (view != null) _zoom.SetFocus(view.transform, view.VisualRadius);
        }

        private void SubmitOrder(ICommand command, string label)
        {
            if (_gameOver) return;
            Planet planet = SelectedPlanet;
            if (_session.Submit(command, out string reason))
                _hud.ShowFeedback($"Orden aceptada: {label} sobre {planet.name}. Se resuelve al pulsar Fin de ciclo.");
            else
                _hud.ShowFeedback($"No se puede: {TextosES.Translate(reason, _session.State)}.");
            RefreshHud();
        }

        private void EndGame(string title, string body)
        {
            _gameOver = true;
            _hud.SetOrdersInteractable(false);
            _hud.ShowEndPanel(title, body);
        }

        private void AddLog(string entry)
        {
            _log.Add(entry);
            while (_log.Count > MaxLogEntries) _log.RemoveAt(0);
        }

        private void RefreshHud()
        {
            if (!HasSession) return;
            _hud.Refresh(_session, SelectedPlanet, _universeName, _selectedIndex, _system.planets.Count);
        }

        private void EnsureCameraAndLight()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Camara principal");
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 500f;

            _zoom = _camera.GetComponent<CameraZoomController>();
            if (_zoom == null) _zoom = _camera.gameObject.AddComponent<CameraZoomController>();

            _sun = FindFirstObjectByType<Light>();
            if (_sun == null || _sun.type != LightType.Directional)
            {
                var go = new GameObject("Luz estelar");
                _sun = go.AddComponent<Light>();
                _sun.type = LightType.Directional;
            }
            _sun.intensity = 1.2f;
            _sun.shadows = LightShadows.None;
        }
    }
}
