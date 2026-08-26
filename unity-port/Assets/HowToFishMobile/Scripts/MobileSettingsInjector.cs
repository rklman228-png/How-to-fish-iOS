using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HowToFish.Mobile
{
    public sealed class MobileSettingsInjector : MonoBehaviour
    {
        private GameObject _mobilePanel;
        private Button _mobileTabButton;
        private Slider _sizeSlider;
        private TextMeshProUGUI _selectedLabel;
        private TextMeshProUGUI _editLabel;

        private IEnumerator Start()
        {
            MobileLayoutEditor.SelectionChanged += OnSelectionChanged;
            while (true)
            {
                TryInject();
                yield return new WaitForSecondsRealtime(0.75f);
            }
        }

        private void OnDestroy()
        {
            MobileLayoutEditor.SelectionChanged -= OnSelectionChanged;
        }

        private void TryInject()
        {
            if (_mobileTabButton != null && _mobilePanel != null) return;

            var gameplayButtonGo = FindSceneObject("GameplayButton");
            var gameplayDisplay = FindSceneObject("GameplayDisplay (To toggle)");
            if (gameplayButtonGo == null || gameplayDisplay == null) return;

            var parent = gameplayButtonGo.transform.parent;
            if (parent.Find("MobileControlsButton") != null)
            {
                _mobileTabButton = parent.Find("MobileControlsButton").GetComponent<Button>();
                _mobilePanel = FindSceneObject("MobileControlsDisplay (To toggle)");
                return;
            }

            var clone = Instantiate(gameplayButtonGo, parent);
            clone.name = "MobileControlsButton";
            clone.transform.SetSiblingIndex(gameplayButtonGo.transform.GetSiblingIndex() + 1);
            PositionIfNoLayout(clone.GetComponent<RectTransform>(), gameplayButtonGo.GetComponent<RectTransform>());

            foreach (var c in clone.GetComponents<MonoBehaviour>())
            {
                if (c == null) continue;
                if (string.Equals(c.GetType().Name, "UIButton", StringComparison.Ordinal)) c.enabled = false;
            }

            _mobileTabButton = clone.GetComponent<Button>() ?? clone.AddComponent<Button>();
            _mobileTabButton.onClick.RemoveAllListeners();
            _mobileTabButton.onClick.AddListener(OpenMobileSettings);
            SetFirstText(clone, "Mobile");

            _mobilePanel = CreatePanel(gameplayDisplay);
            HookOtherTabs();
        }

        private GameObject CreatePanel(GameObject gameplayDisplay)
        {
            var parent = gameplayDisplay.transform.parent;
            var go = new GameObject("MobileControlsDisplay (To toggle)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            var sourceRect = gameplayDisplay.GetComponent<RectTransform>();
            CopyRect(sourceRect, rect);

            var bg = go.GetComponent<Image>();
            var sourceImage = gameplayDisplay.GetComponent<Image>();
            if (sourceImage != null)
            {
                bg.sprite = sourceImage.sprite;
                bg.material = sourceImage.material;
                bg.type = sourceImage.type;
                bg.color = sourceImage.color;
            }
            else bg.color = new Color(0.06f, 0.055f, 0.045f, 0.88f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(go.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(.08f, .10f);
            content.anchorMax = new Vector2(.92f, .90f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 18;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateText(content, "MOBILE CONTROLS", 40, FontStyles.Bold, 64);
            CreateText(content, "Drag controls on screen to move them. Select a control, then change its size.", 25, FontStyles.Normal, 72);

            var edit = CreateButton(content, "Edit Layout", out _editLabel);
            edit.onClick.AddListener(() =>
            {
                bool next = !MobileLayoutEditor.IsEditing;
                MobileLayoutEditor.SetEditing(next);
                _editLabel.text = next ? "Done Editing" : "Edit Layout";
            });

            _selectedLabel = CreateText(content, "Selected: none", 25, FontStyles.Bold, 48);

            _sizeSlider = CreateSlider(content, .55f, 1.80f, 1f);
            _sizeSlider.interactable = false;
            _sizeSlider.onValueChanged.AddListener(value =>
            {
                if (MobileLayoutEditor.Selected != null)
                    MobileLayoutEditor.Selected.SetScale(value);
            });

            var reset = CreateButton(content, "Reset Layout", out _);
            reset.onClick.AddListener(() =>
            {
                MobileLayoutStore.ResetToDefaults();
                var runtime = MobileControlsBootstrap.Instance;
                if (runtime != null)
                    foreach (var handle in runtime.Handles.Values) handle.ApplySavedLayout();
                OnSelectionChanged(MobileLayoutEditor.Selected);
            });

            go.SetActive(false);
            return go;
        }

        private void OpenMobileSettings()
        {
            HideDisplay("GameplayDisplay (To toggle)");
            HideDisplay("GraphicsDisplay (To toggle)");
            HideDisplay("AudioDisplay (To toggle)");
            HideDisplay("ServerSettingsScreen (To toggle)");
            if (_mobilePanel != null) _mobilePanel.SetActive(true);
        }

        private void HookOtherTabs()
        {
            string[] names = { "GameplayButton", "GraphicsButton", "AudioButton", "ServerSettingsButton" };
            foreach (string name in names)
            {
                var go = FindSceneObject(name);
                var button = go != null ? go.GetComponent<Button>() : null;
                if (button == null) continue;
                button.onClick.AddListener(CloseMobileSettings);
            }
        }

        private void CloseMobileSettings()
        {
            if (_mobilePanel != null) _mobilePanel.SetActive(false);
            if (MobileLayoutEditor.IsEditing) MobileLayoutEditor.SetEditing(false);
            if (_editLabel != null) _editLabel.text = "Edit Layout";
        }

        private void OnSelectionChanged(MobileControlHandle handle)
        {
            if (_selectedLabel != null)
                _selectedLabel.text = handle == null ? "Selected: none" : $"Selected: {handle.ControlId}";
            if (_sizeSlider != null)
            {
                _sizeSlider.interactable = handle != null;
                if (handle != null) _sizeSlider.SetValueWithoutNotify(MobileLayoutStore.Get(handle.ControlId).scale);
            }
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t != null && t.gameObject.scene.IsValid() && string.Equals(t.name, name, StringComparison.Ordinal))
                    return t.gameObject;
            return null;
        }

        private static void HideDisplay(string name)
        {
            var go = FindSceneObject(name);
            if (go != null) go.SetActive(false);
        }

        private static void SetFirstText(GameObject go, string value)
        {
            var tmp = go.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
            if (tmp != null) { tmp.text = value; return; }
            var text = go.GetComponentsInChildren<Text>(true).FirstOrDefault();
            if (text != null) text.text = value;
        }

        private static void PositionIfNoLayout(RectTransform clone, RectTransform source)
        {
            if (clone == null || source == null || clone.parent == null) return;
            if (clone.parent.GetComponent<LayoutGroup>() != null) return;
            clone.anchorMin = source.anchorMin;
            clone.anchorMax = source.anchorMax;
            clone.pivot = source.pivot;
            clone.sizeDelta = source.sizeDelta;
            clone.anchoredPosition = source.anchoredPosition + new Vector2(source.rect.width + 14f, 0f);
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            if (source == null || target == null) return;
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string value, float fontSize, FontStyles style, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = value;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return tmp;
        }

        private static Button CreateButton(RectTransform parent, string label, out TextMeshProUGUI text)
        {
            var go = new GameObject(label.Replace(" ", "") + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(.86f, .83f, .72f, .95f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            go.GetComponent<LayoutElement>().preferredHeight = 68;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.08f, .075f, .06f, 1f);
            text.raycastTarget = false;
            return button;
        }

        private static Slider CreateSlider(RectTransform parent, float min, float max, float value)
        {
            var root = new GameObject("SizeSlider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<LayoutElement>().preferredHeight = 60;
            var slider = root.GetComponent<Slider>();
            slider.minValue = min; slider.maxValue = max; slider.value = value;

            var bg = CreateSliderImage(root.transform, "Background", new Color(.18f, .17f, .14f, .75f));
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = new Vector2(0, .35f); bgRt.anchorMax = new Vector2(1, .65f); bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            var fa = (RectTransform)fillArea.transform;
            fa.anchorMin = new Vector2(0, .35f); fa.anchorMax = new Vector2(1, .65f); fa.offsetMin = fa.offsetMax = Vector2.zero;
            var fill = CreateSliderImage(fillArea.transform, "Fill", new Color(.92f, .86f, .55f, 1f));
            var fr = (RectTransform)fill.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            var ha = (RectTransform)handleArea.transform;
            ha.anchorMin = Vector2.zero; ha.anchorMax = Vector2.one; ha.offsetMin = new Vector2(20, 0); ha.offsetMax = new Vector2(-20, 0);
            var handle = CreateSliderImage(handleArea.transform, "Handle", new Color(.92f, .89f, .80f, 1f));
            var hr = (RectTransform)handle.transform;
            hr.sizeDelta = new Vector2(42, 42);

            slider.fillRect = fr;
            slider.handleRect = hr;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Image CreateSliderImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
