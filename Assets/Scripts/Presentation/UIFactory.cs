using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChromeDino.Presentation
{
    public sealed class UIFactory
    {
        public static readonly Color Ink = new Color32(65, 68, 72, 255);
        public static readonly Color Muted = new Color32(107, 112, 119, 255);
        public static readonly Color Green = new Color32(60, 108, 160, 255);
        public static readonly Color Cream = new Color32(236, 239, 243, 255);
        private readonly Font font;
        public RectTransform Root { get; private set; }

        public UIFactory()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Root = Rect("Layout", canvasObject.transform, 0, 0, 1280, 900);
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.anchoredPosition = Vector2.zero;
            Panel(Root, 0, 0, 1280, 900, new Color32(247, 248, 250, 255));
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.anchorMin = result.anchorMax = new Vector2(0, 1);
            result.pivot = new Vector2(0, 1);
            result.anchoredPosition = new Vector2(x, -y);
            result.sizeDelta = new Vector2(width, height);
            return result;
        }

        public Image Panel(Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Rect("Panel", parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public Text Label(Transform parent, string text, float x, float y, float width, float height, int size = 22,
            Color? color = null, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var rect = Rect("Text", parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color ?? Ink;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        public Button Button(Transform parent, string text, float x, float y, float width, float height, Action clicked,
            bool primary = false, int fontSize = 22)
        {
            var rect = Rect("Button " + text, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = primary ? Green : Cream;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.88f, 0.94f, 0.85f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.68f);
            colors.disabledColor = new Color(0.78f, 0.78f, 0.72f, 0.55f);
            button.colors = colors;
            button.onClick.AddListener(() => clicked());
            Label(rect, text, 10, 0, width - 20, height, fontSize, primary ? Color.white : Ink, TextAnchor.MiddleCenter);
            return button;
        }

        public Dropdown Dropdown(Transform parent, float x, float y, float width, Action<int> selected)
        {
            var root = Rect("Selection", parent, x, y, width, 50);
            root.gameObject.AddComponent<Image>().color = Cream;
            var dropdown = root.gameObject.AddComponent<Dropdown>();
            var caption = Label(root, "", 14, 0, width - 54, 50, 19, Ink, TextAnchor.MiddleLeft);
            Label(root, "▼", width - 35, 0, 30, 50, 16, Ink, TextAnchor.MiddleCenter);

            var template = Rect("Template", root, 0, 54, width, 230);
            template.gameObject.AddComponent<Image>().color = Cream;
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", template, 0, 0, width, 230);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, width, 48);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = new Vector2(0, 48);
            var item = Rect("Item", content, 0, 0, width, 48);
            var toggleImage = item.gameObject.AddComponent<Image>();
            toggleImage.color = new Color32(218, 228, 240, 255);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = toggleImage;
            toggle.graphic = Panel(item, 6, 17, 12, 12, Green);
            var itemLabel = Label(item, "", 28, 0, width - 35, 48, 18, Ink, TextAnchor.MiddleLeft);
            scroll.viewport = viewport;
            scroll.content = content;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.onValueChanged.AddListener(value => selected(value));
            template.gameObject.SetActive(false);
            return dropdown;
        }

        public void Header(string section)
        {
            Label(Root, "CHROME DINO", 60, 25, 420, 48, 34);
            Label(Root, section, 550, 34, 650, 42, 24, Muted);
        }
    }
}
