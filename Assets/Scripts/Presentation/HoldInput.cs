using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChromeDino.Presentation
{
    public sealed class HoldInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> Changed;
        public int LastPointerFrame { get; private set; } = -1;
        private bool held;
        private int pointerId;

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || held) return;
            var selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;
            held = true;
            pointerId = data.pointerId;
            LastPointerFrame = Time.frameCount;
            Changed?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && data.pointerId == pointerId) Release();
        }
        public void OnPointerExit(PointerEventData data) { if (data.pointerId == pointerId) Release(); }
        private void Release()
        {
            if (!held) return;
            held = false;
            LastPointerFrame = Time.frameCount;
            Changed?.Invoke(false);
        }
        private void OnDisable() { Release(); }
        private void OnApplicationFocus(bool value) { if (!value) Release(); }
        private void OnApplicationPause(bool value) { if (value) Release(); }
    }
}
