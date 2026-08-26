using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HowToFish.Mobile
{
    [DefaultExecutionOrder(-5000)]
    public sealed class MobileControlsBootstrap : MonoBehaviour
    {
        private readonly struct ActionSpec
        {
            public readonly string Id;
            public readonly string Path;
            public readonly string Glyph;
            public readonly Vector2 Size;

            public ActionSpec(string id, string path, string glyph, Vector2 size)
            {
                Id = id;
                Path = path;
                Glyph = glyph;
                Size = size;
            }
        }

        private static readonly ActionSpec[] Actions =
        {
            new ActionSpec("primary",   "<Gamepad>/rightTrigger",   "L", new Vector2(132,132)),
            new ActionSpec("secondary", "<Gamepad>/leftTrigger",    "R", new Vector2(108,108)),
            new ActionSpec("interact",  "<Gamepad>/buttonWest",     "✋", new Vector2(100,100)),
            new ActionSpec("drop",      "<Gamepad>/buttonNorth",    "",  new Vector2(100,100)),
            new ActionSpec("jump",      "<Gamepad>/buttonSouth",    "↑", new Vector2(96,96)),
            new ActionSpec("sprint",    "<Gamepad>/leftStickPress", "⇈", new Vector2(96,96)),
            new ActionSpec("crouch",    "<Gamepad>/buttonEast",     "",  new Vector2(96,96)),
            new ActionSpec("inspect",   "<Gamepad>/dpad/up",        "◉", new Vector2(82,82)),
            new ActionSpec("reload",    "<Keyboard>/r",             "↻", new Vector2(82,82)),
            new ActionSpec("bait",      "<Gamepad>/rightStickPress","J", new Vector2(82,82)),
            new ActionSpec("journal",   "<Gamepad>/select",         "▣", new Vector2(82,82)),
            new ActionSpec("ptt",       "<Gamepad>/dpad/left",      "●", new Vector2(82,82)),
            new ActionSpec("pause",     "<Gamepad>/start",          "Ⅱ", new Vector2(82,82)),
            new ActionSpec("skin-prev", "<Keyboard>/z",             "‹", new Vector2(72,72)),
            new ActionSpec("skin-next", "<Keyboard>/c",             "›", new Vector2(72,72)),
        };

        public static MobileControlsBootstrap Instance { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform HudRoot { get; private set; }
        public IReadOnlyDictionary<string, MobileControlHandle> Handles => _handles;

        private readonly Dictionary<string, MobileControlHandle> _handles = new Dictionary<string, MobileControlHandle>();
        private readonly List<Behaviour> _onScreenControls = new List<Behaviour>();
        private Image _slotStyleSource;
        private Coroutine _rebuildRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_IOS || UNITY_ANDROID || UNITY_EDITOR
            if (FindFirstObjectByType<MobileControlsBootstrap>() != null) return;
            var go = new GameObject("HowToFish.MobileControls");
            DontDestroyOnLoad(go);
            go.AddComponent<MobileControlsBootstrap>();
#endif
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            MobileLayoutEditor.EditingChanged += OnEditingChanged;
            BuildCanvas();
            gameObject.AddComponent<OriginalHotbarTouchAdapter>();
            gameObject.AddComponent<MobileSettingsInjector>();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            MobileLayoutEditor.EditingChanged -= OnEditingChanged;
            if (Instance == this) Instance = null;
        }

        private void Start() => ScheduleRebuild();
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ScheduleRebuild();

        private void ScheduleRebuild()
        {
            if (_rebuildRoutine != null) StopCoroutine(_rebuildRoutine);
            _rebuildRoutine = StartCoroutine(RebuildWhenUiExists());
        }

        private IEnumerator RebuildWhenUiExists()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(0.20f);
            _slotStyleSource = FindOriginalSlotImage();
            if (HudRoot == null) BuildCanvas();
            if (_handles.Count == 0) BuildControls();
            ApplyStyleToAllButtons();
        }

        private void BuildCanvas()
        {
            if (Canvas != null) return;
            var canvasGo = new GameObject("MobileControlsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas = canvasGo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 32000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            var hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(canvasGo.transform, false);
            HudRoot = (RectTransform)hudGo.transform;
            HudRoot.anchorMin = Vector2.zero;
            HudRoot.anchorMax = Vector2.one;
            HudRoot.offsetMin = Vector2.zero;
            HudRoot.offsetMax = Vector2.zero;
        }

        private void BuildControls()
        {
            BuildMoveStick();
            BuildLookZone();
            foreach (var spec in Actions) BuildActionButton(spec);
        }

        private void BuildMoveStick()
        {
            var baseGo = CreateControlRoot("move", new Vector2(220, 220), true);
            var bg = baseGo.gameObject.AddComponent<Image>();
            bg.color = new Color(0.93f, 0.90f, 0.80f, 0.15f);
            bg.raycastTarget = false;

            var handleGo = new GameObject("Stick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGo.transform.SetParent(baseGo, false);
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(.5f, .5f);
            handleRect.sizeDelta = new Vector2(96, 96);
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = new Color(0.93f, 0.90f, 0.80f, 0.70f);
            handleImage.raycastTarget = true;

            var stick = handleGo.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 70f;
            _onScreenControls.Add(stick);
        }

        private void BuildLookZone()
        {
            var root = CreateControlRoot("look", new Vector2(780, 760), false);
            var image = root.gameObject.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.001f);
            image.raycastTarget = true;

            var stick = root.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/rightStick";
            stick.movementRange = 110f;
            TryEnableDynamicOrigin(stick);
            _onScreenControls.Add(stick);
        }

        private RectTransform CreateControlRoot(string id, Vector2 size, bool visibleInEdit)
        {
            var go = new GameObject($"Mobile_{id}", typeof(RectTransform));
            go.transform.SetParent(HudRoot, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;

            var handle = go.AddComponent<MobileControlHandle>();
            handle.Initialize(id, Canvas);
            handle.ApplySavedLayout();
            _handles[id] = handle;

            if (visibleInEdit)
            {
                var edit = go.AddComponent<MobileEditOutline>();
                edit.Initialize(handle);
            }
            return rect;
        }

        private void BuildActionButton(ActionSpec spec)
        {
            var rect = CreateControlRoot(spec.Id, spec.Size, true);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = true;
            ApplyOriginalSlotStyle(image);

            var onScreen = rect.gameObject.AddComponent<OnScreenButton>();
            onScreen.controlPath = spec.Path;
            _onScreenControls.Add(onScreen);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(rect, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = new Vector2(.18f, .18f);
            iconRect.anchorMax = new Vector2(.82f, .82f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.color = new Color(.07f, .065f, .055f, .94f);

            if (spec.Id == "drop") icon.sprite = ProceduralMobileIcons.CreateDropIcon();
            else if (spec.Id == "crouch") icon.sprite = ProceduralMobileIcons.CreateCrouchIcon();
            else
            {
                icon.enabled = false;
                AddGlyph(rect, spec.Glyph);
            }
        }

        private static void AddGlyph(RectTransform parent, string glyph)
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = glyph;
            text.fontSize = 42;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.07f, .065f, .055f, .94f);
            text.raycastTarget = false;
        }

        private void ApplyStyleToAllButtons()
        {
            foreach (var image in HudRoot.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject.name.StartsWith("Mobile_", StringComparison.Ordinal) &&
                    image.gameObject.name != "Mobile_move" && image.gameObject.name != "Mobile_look")
                    ApplyOriginalSlotStyle(image);
            }
        }

        private void ApplyOriginalSlotStyle(Image target)
        {
            if (_slotStyleSource == null) _slotStyleSource = FindOriginalSlotImage();
            if (_slotStyleSource != null)
            {
                target.sprite = _slotStyleSource.sprite;
                target.material = _slotStyleSource.material;
                target.type = _slotStyleSource.type;
                target.fillCenter = _slotStyleSource.fillCenter;
                target.pixelsPerUnitMultiplier = _slotStyleSource.pixelsPerUnitMultiplier;
                target.color = _slotStyleSource.color;
                target.preserveAspect = false;
            }
            else target.color = new Color(0.90f, 0.87f, 0.76f, .78f);
        }

        private static Image FindOriginalSlotImage()
        {
            var images = Resources.FindObjectsOfTypeAll<Image>();
            Image fallback = null;
            var slotType = Type.GetType("InventorySlot, Assembly-CSharp");
            foreach (var image in images)
            {
                if (image == null || !image.gameObject.scene.IsValid() || image.sprite == null) continue;
                if (string.Equals(image.gameObject.name, "InventorySlot", StringComparison.OrdinalIgnoreCase)) return image;
                if (slotType != null && image.GetComponentInParent(slotType) != null) fallback ??= image;
            }
            return fallback;
        }

        private static void TryEnableDynamicOrigin(OnScreenStick stick)
        {
            try
            {
                var type = typeof(OnScreenStick);
                var prop = type.GetProperty("behaviour");
                if (prop != null && prop.PropertyType.IsEnum)
                {
                    var value = Enum.Parse(prop.PropertyType, "ExactPositionWithDynamicOrigin");
                    prop.SetValue(stick, value);
                }
                type.GetProperty("dynamicOriginRange")?.SetValue(stick, 150f);
            }
            catch { }
        }

        private void OnEditingChanged(bool editing)
        {
            for (int i = 0; i < _onScreenControls.Count; i++)
                if (_onScreenControls[i] != null) _onScreenControls[i].enabled = !editing;

            foreach (var outline in HudRoot.GetComponentsInChildren<MobileEditOutline>(true))
                outline.SetVisible(editing);
        }
    }

    internal sealed class MobileEditOutline : MonoBehaviour
    {
        private Outline _outline;

        public void Initialize(MobileControlHandle handle)
        {
            _outline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(1f, .82f, .15f, .95f);
            _outline.effectDistance = new Vector2(3, -3);
            _outline.enabled = false;
        }

        public void SetVisible(bool value) => _outline.enabled = value;
    }
}
