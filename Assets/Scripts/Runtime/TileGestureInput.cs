using System;
using Pyatnashki.Domain;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pyatnashki
{
    public sealed class TileGestureInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IDragHandler, IEndDragHandler
    {
        private readonly TileSwipeGesture gesture = new TileSwipeGesture();
        private Func<int> source, empty, version;
        private Func<bool> allowed;
        private Action move;
        private float fraction;
        private RectTransform plane, rect;
        private Vector2 start;
        private int pointerId;
        private bool tracking;

        public void Configure(Func<int> source, Func<int> empty, Func<int> version,
            Func<bool> allowed, Action move, float fraction)
        {
            this.source = source; this.empty = empty; this.version = version;
            this.allowed = allowed; this.move = move; this.fraction = fraction;
            rect = GetComponent<RectTransform>();
            plane = rect.parent as RectTransform;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (tracking || data.button != PointerEventData.InputButton.Left || !allowed()) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(plane, data.position,
                data.pressEventCamera, out start)) return;
            tracking = gesture.Begin(source(), empty(), Mathf.Min(rect.rect.width, rect.rect.height),
                fraction, version());
            pointerId = data.pointerId;
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;

        private bool Delta(PointerEventData data, out Vector2 delta)
        {
            delta = Vector2.zero;
            if (!tracking || data.pointerId != pointerId) return false;
            if (!allowed()) { gesture.Cancel(); tracking = false; return false; }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(plane, data.position,
                data.pressEventCamera, out Vector2 point)) return false;
            delta = point - start;
            return true;
        }

        public void OnDrag(PointerEventData data)
        {
            if (Delta(data, out Vector2 delta) && gesture.TryDrag(delta.x, delta.y, version())) move();
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (Delta(data, out Vector2 delta) && gesture.TryRelease(delta.x, delta.y, version())) move();
            if (data.pointerId == pointerId) tracking = false;
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (data.pointerId != pointerId) return;
            tracking = false;
            gesture.Cancel();
        }

        private void OnDisable() { tracking = false; gesture.Cancel(); }
    }
}
