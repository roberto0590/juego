using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace GodGame.Presentation
{
    /// <summary>
    /// Cámara orbital con zoom continuo planeta → sistema (D10).
    ///  - Zoom01 = 0: encuadra el planeta enfocado; Zoom01 = 1: todo el sistema (pivote en el origen = estrella).
    ///  - Rueda del ratón o pellizco: zoom. Arrastre (ratón o un dedo): gira yaw/pitch (pitch 10–80°).
    ///  - Entrada con Input System si está activo (ENABLE_INPUT_SYSTEM) y, si no, con UnityEngine.Input (D11).
    /// Los arrastres que empiezan sobre la UI no giran la cámara.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraZoomController : MonoBehaviour
    {
        private const float MinPitch = 10f;
        private const float MaxPitch = 80f;
        private const float DragDegreesPerPixel = 0.25f;
        private const float ScrollStep = 0.08f;
        private const float PinchSpeed = 1.5f;
        private const float SmoothTime = 0.25f;

        private Transform _focus;
        private float _focusRadius = 0.5f;
        private float _systemExtent = 10f;
        private float _yaw = 30f;
        private float _pitch = 35f;
        private float _zoom = 1f;
        private float _targetZoom = 1f;
        private float _zoomVelocity;
        private float _focusBlend = 1f;
        private float _focusBlendVelocity;
        private Vector3 _previousFocusPoint;
        private bool _dragging;
#if !ENABLE_INPUT_SYSTEM
        private Vector3 _lastMousePosition;
#endif

        /// <summary>0 = planeta enfocado, 1 = sistema completo.</summary>
        public float Zoom01 => _zoom;

        public Transform Focus => _focus;

        public void SetFocus(Transform focus, float radius)
        {
            _previousFocusPoint = CurrentFocusPoint();
            _focus = focus;
            _focusRadius = Mathf.Max(0.05f, radius);
            _focusBlend = 0f;
        }

        public void SetSystemExtent(float extent)
        {
            _systemExtent = Mathf.Max(1f, extent);
        }

        /// <summary>Alterna entre vista de planeta y vista de sistema.</summary>
        public void ToggleZoom()
        {
            _targetZoom = _targetZoom < 0.5f ? 1f : 0f;
        }

        private void LateUpdate()
        {
            ReadInput(out float zoomDelta, out Vector2 drag);
            _targetZoom = Mathf.Clamp01(_targetZoom + zoomDelta);
            _yaw += drag.x * DragDegreesPerPixel;
            _pitch = Mathf.Clamp(_pitch - drag.y * DragDegreesPerPixel, MinPitch, MaxPitch);

            _zoom = Mathf.SmoothDamp(_zoom, _targetZoom, ref _zoomVelocity, SmoothTime);
            _focusBlend = Mathf.SmoothDamp(_focusBlend, 1f, ref _focusBlendVelocity, SmoothTime);

            Vector3 focusPoint = Vector3.Lerp(_previousFocusPoint, CurrentFocusPoint(), _focusBlend);
            Vector3 pivot = Vector3.Lerp(focusPoint, Vector3.zero, _zoom);
            float planetDistance = Mathf.Max(_focusRadius * 4f, 1.5f);
            float systemDistance = Mathf.Max(_systemExtent * 1.8f, 6f);
            float distance = Mathf.Lerp(planetDistance, systemDistance, _zoom);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = pivot - rotation * Vector3.forward * distance;
            transform.rotation = rotation;
        }

        private Vector3 CurrentFocusPoint() => _focus != null ? _focus.position : Vector3.zero;

        private void ReadInput(out float zoomDelta, out Vector2 drag)
        {
            zoomDelta = 0f;
            drag = Vector2.zero;
            float screenScale = Mathf.Max(1f, Screen.height);

#if ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                TouchControl first = null;
                TouchControl second = null;
                int active = 0;
                foreach (TouchControl touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    if (active == 0) first = touch;
                    else if (active == 1) second = touch;
                    active++;
                }

                if (active >= 2)
                {
                    _dragging = false;
                    zoomDelta = PinchDelta(first.position.ReadValue(), first.delta.ReadValue(),
                                           second.position.ReadValue(), second.delta.ReadValue()) / screenScale;
                    return;
                }
                if (active == 1)
                {
                    HandleDrag(first.press.wasPressedThisFrame, true, first.delta.ReadValue(), ref drag);
                    return;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) zoomDelta = -Mathf.Sign(scroll) * ScrollStep;
                HandleDrag(mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed, mouse.delta.ReadValue(), ref drag);
            }
#else
            if (Input.touchCount >= 2)
            {
                _dragging = false;
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);
                zoomDelta = PinchDelta(a.position, a.deltaPosition, b.position, b.deltaPosition) / screenScale;
                return;
            }
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                bool held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                HandleDrag(touch.phase == TouchPhase.Began, held, touch.deltaPosition, ref drag);
                return;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f) zoomDelta = -Mathf.Sign(scroll) * ScrollStep;
            Vector3 mousePosition = Input.mousePosition;
            Vector2 mouseDelta = Input.GetMouseButtonDown(0) ? Vector2.zero : (Vector2)(mousePosition - _lastMousePosition);
            _lastMousePosition = mousePosition;
            HandleDrag(Input.GetMouseButtonDown(0), Input.GetMouseButton(0), mouseDelta, ref drag);
#endif
        }

        /// <summary>Separar los dedos acerca (zoom hacia el planeta); juntarlos aleja.</summary>
        private static float PinchDelta(Vector2 positionA, Vector2 deltaA, Vector2 positionB, Vector2 deltaB)
        {
            float now = (positionA - positionB).magnitude;
            float before = ((positionA - deltaA) - (positionB - deltaB)).magnitude;
            return (before - now) * PinchSpeed;
        }

        private void HandleDrag(bool began, bool held, Vector2 delta, ref Vector2 drag)
        {
            if (began) _dragging = !IsPointerOverUi();
            if (!held)
            {
                _dragging = false;
                return;
            }
            if (_dragging && !began) drag = delta;
        }

        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }
    }
}
