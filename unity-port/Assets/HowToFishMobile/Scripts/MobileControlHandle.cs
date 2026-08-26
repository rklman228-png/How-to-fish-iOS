using UnityEngine;
using UnityEngine.EventSystems;

namespace HowToFish.Mobile
{
    public sealed class MobileControlHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
    {
        [SerializeField] private string controlId;
        private RectTransform _rect;
        private Canvas _canvas;
        private Vector2 _dragOffset;

        public string ControlId => controlId;

        public void Initialize(string id, Canvas canvas)
        {
            controlId = id;
            _canvas = canvas;
            _rect = transform as RectTransform;
        }

        public void ApplySavedLayout()
        {
            if (_rect == null) _rect = transform as RectTransform;
            var p = MobileLayoutStore.Get(controlId);
            _rect.anchorMin = p.normalizedPosition;
            _rect.anchorMax = p.normalizedPosition;
            _rect.anchoredPosition = Vector2.zero;
            _rect.localScale = Vector3.one * p.scale;
        }

        public void SetScale(float value)
        {
            value = Mathf.Clamp(value, 0.55f, 1.80f);
            var p = MobileLayoutStore.Get(controlId);
            p.scale = value;
            if (_rect != null) _rect.localScale = Vector3.one * value;
            MobileLayoutStore.Save();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!MobileLayoutEditor.IsEditing) return;
            MobileLayoutEditor.Select(this);
            eventData.Use();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!MobileLayoutEditor.IsEditing || _rect == null) return;
            MobileLayoutEditor.Select(this);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var local);
            _dragOffset = _rect.anchoredPosition - local;
            eventData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!MobileLayoutEditor.IsEditing || _rect == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var local)) return;

            _rect.anchoredPosition = local + _dragOffset;
            var parent = _rect.parent as RectTransform;
            var anchor = new Vector2(
                Mathf.InverseLerp(parent.rect.xMin, parent.rect.xMax, _rect.anchoredPosition.x),
                Mathf.InverseLerp(parent.rect.yMin, parent.rect.yMax, _rect.anchoredPosition.y));
            anchor.x = Mathf.Clamp01(anchor.x);
            anchor.y = Mathf.Clamp01(anchor.y);

            _rect.anchorMin = anchor;
            _rect.anchorMax = anchor;
            _rect.anchoredPosition = Vector2.zero;
            MobileLayoutStore.Get(controlId).normalizedPosition = anchor;
            MobileLayoutStore.Save();
            eventData.Use();
        }
    }

    public static class MobileLayoutEditor
    {
        public static bool IsEditing { get; private set; }
        public static MobileControlHandle Selected { get; private set; }
        public static event System.Action<MobileControlHandle> SelectionChanged;
        public static event System.Action<bool> EditingChanged;

        public static void SetEditing(bool value)
        {
            IsEditing = value;
            if (!value) Selected = null;
            EditingChanged?.Invoke(value);
            SelectionChanged?.Invoke(Selected);
        }

        public static void Select(MobileControlHandle handle)
        {
            if (!IsEditing) return;
            Selected = handle;
            SelectionChanged?.Invoke(handle);
        }
    }
}
