using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace HowToFish.Mobile
{
    /// <summary>
    /// Adds invisible touch hitboxes to the game's EXISTING InventorySlot objects.
    /// It never creates or redraws hotbar slots. When the game unlocks another slot,
    /// the newly-active original slot is discovered automatically and becomes touchable.
    /// </summary>
    public sealed class OriginalHotbarTouchAdapter : MonoBehaviour
    {
        private readonly Dictionary<GameObject, GameObject> _overlays = new Dictionary<GameObject, GameObject>();
        private Type _inventorySlotType;

        private IEnumerator Start()
        {
            while (true)
            {
                Refresh();
                yield return new WaitForSecondsRealtime(0.35f);
            }
        }

        private void Refresh()
        {
            _inventorySlotType ??= Type.GetType("InventorySlot, Assembly-CSharp");
            if (_inventorySlotType == null) return;

            var components = Resources.FindObjectsOfTypeAll(_inventorySlotType)
                .OfType<Component>()
                .Where(c => c != null && c.gameObject.scene.IsValid() && c.gameObject.activeInHierarchy)
                .Where(c => c.transform is RectTransform)
                .ToList();

            var holderSlots = components.Where(HasHotbarAncestor).ToList();
            if (holderSlots.Count > 0) components = holderSlots;

            var slots = components
                .Where(IsVisibleUiSlot)
                .OrderBy(c => ((RectTransform)c.transform).position.x)
                .ToList();

            for (int i = 0; i < slots.Count && i < 9; i++)
            {
                var slotObject = slots[i].gameObject;
                if (_overlays.ContainsKey(slotObject)) continue;
                _overlays[slotObject] = AttachHitbox(slotObject, i + 1);
            }

            var gone = _overlays.Keys.Where(k => k == null || !k.activeInHierarchy).ToArray();
            foreach (var key in gone)
            {
                if (key != null && _overlays.TryGetValue(key, out var overlay) && overlay != null)
                    Destroy(overlay);
                _overlays.Remove(key);
            }
        }

        private static bool HasHotbarAncestor(Component component)
        {
            for (var t = component.transform; t != null; t = t.parent)
            {
                var n = t.name;
                if (string.Equals(n, "InventorySlotHolder", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n, "InventoryHolder", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsVisibleUiSlot(Component component)
        {
            var rect = component.transform as RectTransform;
            if (rect == null || rect.rect.width < 8f || rect.rect.height < 8f) return false;
            var canvas = component.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            var group = component.GetComponentInParent<CanvasGroup>();
            return group == null || group.alpha > 0.01f;
        }

        private static GameObject AttachHitbox(GameObject slot, int visibleIndex)
        {
            var overlay = new GameObject($"MobileTouch_{visibleIndex}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(slot.transform, false);
            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var image = overlay.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;

            var button = overlay.AddComponent<OnScreenButton>();
            button.controlPath = $"<Keyboard>/{visibleIndex}";
            return overlay;
        }
    }
}
