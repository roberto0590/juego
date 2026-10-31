using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace GodGame.Presentation
{
    /// <summary>
    /// Construcción de uGUI por código (D9): no hay prefabs ni escenas escritas a mano.
    /// Fuente integrada LegacyRuntime.ttf (la única fuente integrada de runtime en Unity 6).
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.05f, 0.04f, 0.12f, 0.82f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.17f, 0.40f, 1f);
        public static readonly Color AccentColor = new Color(0.60f, 0.42f, 0.10f, 1f);
        public static readonly Color TextColor = new Color(0.95f, 0.93f, 1f, 1f);
        public static readonly Color MutedTextColor = new Color(0.75f, 0.72f, 0.85f, 1f);

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>Canvas en Screen Space Overlay escalado a 1920×1080 (match 0,5).</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Crea el EventSystem si no existe, con el módulo de entrada del backend activo (D11).</summary>
        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Ocupa todo el padre, con márgenes opcionales.</summary>
        public static RectTransform Stretch(RectTransform rect, float margin = 0f)
        {
            return SetAnchors(rect, Vector2.zero, Vector2.one, new Vector2(margin, margin), new Vector2(-margin, -margin));
        }

        public static RectTransform SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>Raíz a pantalla completa ajustada al área segura.</summary>
        public static RectTransform CreateSafeArea(Transform canvas)
        {
            RectTransform rect = Stretch(CreateRect(canvas, "AreaSegura"));
            rect.gameObject.AddComponent<SafeAreaFitter>();
            return rect;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        /// <summary>Panel con VerticalLayoutGroup (los hijos usan su altura preferida y todo el ancho).</summary>
        public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing = 8f, int padding = 12)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>Fila horizontal; con forceExpand los hijos se reparten el ancho a partes iguales.</summary>
        public static RectTransform CreateRow(Transform parent, string name, float height, bool forceExpand = true, float spacing = 8f)
        {
            RectTransform rect = CreateRect(parent, name);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = forceExpand;
            layout.childForceExpandHeight = true;
            SetLayout(rect.gameObject, height);
            return rect;
        }

        /// <summary>Tamaño preferido para los layout groups (valores negativos = sin fijar).</summary>
        public static LayoutElement SetLayout(GameObject go, float preferredHeight, float preferredWidth = -1f, float flexibleWidth = -1f)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
            element.preferredWidth = preferredWidth;
            element.flexibleWidth = flexibleWidth;
            return element;
        }

        public static Text CreateLabel(Transform parent, string text, int fontSize, TextAnchor alignment = TextAnchor.MiddleLeft,
                                       float preferredHeight = -1f)
        {
            RectTransform rect = CreateRect(parent, "Texto");
            var label = rect.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = TextColor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            if (preferredHeight > 0f) SetLayout(rect.gameObject, preferredHeight);
            return label;
        }

        public static Button CreateButton(Transform parent, string text, Action onClick, float preferredHeight = 56f,
                                          int fontSize = 26, Color? color = null)
        {
            RectTransform rect = CreateRect(parent, "Boton " + text);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color ?? ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Text label = CreateLabel(rect, text, fontSize, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 4f);

            SetLayout(rect.gameObject, preferredHeight);
            return button;
        }

        public static InputField CreateInputField(Transform parent, string placeholder, float preferredHeight = 60f, int fontSize = 28)
        {
            RectTransform rect = CreateRect(parent, "Campo " + placeholder);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.10f, 0.09f, 0.20f, 1f);
            var field = rect.gameObject.AddComponent<InputField>();

            Text text = CreateLabel(rect, "", fontSize);
            text.supportRichText = false;
            Stretch(text.rectTransform, 12f);

            Text hint = CreateLabel(rect, placeholder, fontSize);
            hint.fontStyle = FontStyle.Italic;
            hint.color = new Color(1f, 1f, 1f, 0.4f);
            Stretch(hint.rectTransform, 12f);

            field.targetGraphic = image;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;

            SetLayout(rect.gameObject, preferredHeight);
            return field;
        }
    }
}
