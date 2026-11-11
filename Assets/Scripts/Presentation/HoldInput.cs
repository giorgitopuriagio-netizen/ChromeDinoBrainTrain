using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChromeDino.Presentation
{
    public sealed class HoldInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> Changed;
        public int LastPointerFrame { get; private set; } = -1;
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            LastPointerFrame = Time.frameCount;
            Changed?.Invoke(true);
        }
        public void OnPointerUp(PointerEventData data) { Release(); }
        public void OnPointerExit(PointerEventData data) { Release(); }
        private void Release() { LastPointerFrame = Time.frameCount; Changed?.Invoke(false); }
        private void OnDisable() { Changed?.Invoke(false); }
    }
}
