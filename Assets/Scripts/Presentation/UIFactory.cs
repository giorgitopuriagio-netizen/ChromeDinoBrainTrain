using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChromeDino.Presentation
{
    public sealed class UIFactory
    {
        public static readonly Color Ink = new Color32(37, 46, 58, 255);
        public static readonly Color Muted = new Color32(88, 102, 119, 255);
        public static readonly Color Accent = new Color32(39, 99, 190, 255);
        public static readonly Color Surface = Color.white;
        public static readonly Color Border = new Color32(218, 225, 234, 255);
        public static readonly Color Background = new Color32(245, 247, 250, 255);
        private readonly TMP_FontAsset font;
        public RectTransform Root { get; private set; }

        public UIFactory()
        {
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) throw new InvalidOperationException("The UI font is missing. Restore the project's TextMesh Pro resources.");
            var canvasObject = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Panel(canvasObject.transform, 0, 0, 0, 0, Background).rectTransform;
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            Root = Rect("Layout", canvasObject.transform, 0, 0, 1280, 900);
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.anchoredPosition = Vector2.zero;
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

        public RectTransform Card(float x, float y, float width, float height)
        {
            var edge = Panel(Root, x, y, width, height, Border).rectTransform;
            Panel(edge, 1, 1, width - 2, height - 2, Surface);
            return edge;
        }

        public TMP_Text Label(Transform parent, string text, float x, float y, float width, float height, int size = 22,
            Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool bold = false)
        {
            var rect = Rect("Text", parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.color = color ?? Ink;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            label.lineSpacing = 5;
            label.raycastTarget = false;
            return label;
        }

        public TMP_Text Stat(Transform parent, string caption, float x, float y, float width)
        {
            Label(parent, caption, x, y, width, 24, 15, Muted);
            return Label(parent, "--", x, y + 28, width, 56, 42, Ink, TextAlignmentOptions.TopLeft, true);
        }

        public Button Button(Transform parent, string text, float x, float y, float width, float height, Action clicked,
            bool primary = false, int fontSize = 21)
        {
            var rect = Rect("Button " + text, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            var edge = rect.gameObject.AddComponent<Outline>();
            edge.effectColor = Border;
            edge.effectDistance = new Vector2(1, -1);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(222, 233, 248, 255);
            colors.selectedColor = new Color32(222, 233, 248, 255);
            colors.pressedColor = new Color32(193, 214, 242, 255);
            colors.disabledColor = new Color32(196, 203, 213, 255);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => clicked());
            Label(rect, text, 12, 0, width - 24, height, fontSize, Ink, TextAlignmentOptions.Center, true);
            SetSelected(button, primary);
            return button;
        }

        public Button MenuButton(string text, string description, float y, Action clicked, bool primary = false)
        {
            var button = Button(Root, text, 672, y, 568, 88, clicked, primary, 23);
            var title = button.GetComponentInChildren<TMP_Text>();
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.rectTransform.anchoredPosition = new Vector2(24, -13);
            title.rectTransform.sizeDelta = new Vector2(520, 32);
            Label(button.transform, description, 24, 49, 520, 24, 16,
                primary ? new Color32(225, 236, 255, 255) : Muted);
            return button;
        }

        public static void SetSelected(Button button, bool selected)
        {
            button.GetComponent<Image>().color = selected ? Accent : Surface;
            button.GetComponentInChildren<TMP_Text>().color = selected ? Color.white : Ink;
            button.GetComponent<Outline>().enabled = !selected;
        }

        public TMP_Dropdown Dropdown(Transform parent, float x, float y, float width, Action<int> selected)
        {
            var root = Rect("Saved champion", parent, x, y, width, 54);
            root.gameObject.AddComponent<Image>().color = Background;
            var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
            var caption = Label(root, "", 16, 0, width - 60, 54, 18, Ink, TextAlignmentOptions.MidlineLeft);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            var left = Panel(root, width - 34, 24, 9, 2, Muted).rectTransform;
            left.localEulerAngles = new Vector3(0, 0, -45);
            var right = Panel(root, width - 28, 30, 9, 2, Muted).rectTransform;
            right.localEulerAngles = new Vector3(0, 0, 45);

            var template = Rect("Template", root, 0, 58, width, 260);
            template.gameObject.AddComponent<Image>().color = Surface;
            var shadow = template.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(25, 39, 58, 35);
            shadow.effectDistance = new Vector2(0, -4);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", template, 0, 0, width, 260);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, width, 54);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = new Vector2(0, 54);
            var item = Rect("Item", content, 0, 0, width, 54);
            var toggleImage = item.gameObject.AddComponent<Image>();
            toggleImage.color = new Color32(227, 237, 251, 255);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = toggleImage;
            toggle.graphic = Panel(item, 0, 9, 3, 36, Accent);
            var itemLabel = Label(item, "", 16, 0, width - 32, 54, 18, Ink, TextAlignmentOptions.MidlineLeft);
            itemLabel.textWrappingMode = TextWrappingModes.NoWrap;
            itemLabel.overflowMode = TextOverflowModes.Ellipsis;
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
            Label(Root, "CHROME DINO", 40, 22, 420, 40, 29, Ink, TextAlignmentOptions.TopLeft, true);
            var subtitle = Label(Root, "BRAIN TRAIN", 42, 66, 420, 22, 13, Accent);
            subtitle.characterSpacing = 4;
            Label(Root, section.ToUpperInvariant(), 700, 37, 540, 30, 15, Muted, TextAlignmentOptions.TopRight);
            Panel(Root, 40, 98, 1200, 1, Border);
        }

        public void Footer(string text)
        {
            Panel(Root, 40, 848, 1200, 1, Border);
            Label(Root, text, 40, 867, 1200, 24, 15, Muted);
        }
    }
}
