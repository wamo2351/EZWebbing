// WebbingNode.cs
// A generic node component.
// Its responsibility is limited to position, drag movement, sibling order
// (Z-order), pin ownership, and keeping attached UI in sync. Actual visual
// content is delegated to an IWebbingNodePresenter implementation, so
// different layouts (memo, photo, card, etc.) can be built on top of it.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EZWebbing
{
    [RequireComponent(typeof(RectTransform))]
    public class WebbingNode : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IPointerDownHandler
    {
        public IWebbingNodeData Data { get; private set; }
        public string NodeId => Data?.NodeId;
        public WebbingGraph Graph { get; private set; }
        public RectTransform RectTransform { get; private set; }

        [Tooltip("Pins that belong to this node. If left empty, they are collected automatically from children.")]
        public List<WebbingPin> pins = new();

        [HideInInspector] public bool isNew = false;

        /// <summary>Raised whenever the node moves (useful for attached UI that needs to follow it).</summary>
        public event Action Moved;

        public IReadOnlyList<WebbingPin> Pins => pins;

        private Canvas canvas;
        private Vector2 dragOffset;
        private readonly List<WebbingNodeAttachment> attachments = new();

        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
            canvas        = GetComponentInParent<Canvas>();

            if (pins == null || pins.Count == 0)
                pins = new List<WebbingPin>(GetComponentsInChildren<WebbingPin>(true));
            foreach (var p in pins) if (p != null) p.ownerNode = this;
        }

        /// <summary>Called by WebbingGraph immediately after instantiation.</summary>
        public void Bind(WebbingGraph graph) => Graph = graph;

        /// <summary>Assigns data to this node and lets the presenter render it.</summary>
        public void Initialize(IWebbingNodeData data)
        {
            Data = data;
            var presenter = GetComponent<IWebbingNodePresenter>();
            presenter?.Present(data);
        }

        public int IndexOfPin(WebbingPin pin) => pins.IndexOf(pin);

        public WebbingPin GetPin(int index) =>
            (index >= 0 && index < pins.Count) ? pins[index] : null;

        public void RegisterAttachment(WebbingNodeAttachment a)
        {
            if (a != null && !attachments.Contains(a)) attachments.Add(a);
        }

        public void MarkAsNew() => isNew = true;
        public void ClearNew()  => isNew = false;

        // ---- Click: bring to front ----
        public void OnPointerDown(PointerEventData e) => BringToFront();

        // ---- Drag to move ----
        public void OnBeginDrag(PointerEventData e)
        {
            if (Graph != null && Graph.IsDraggingConnection) return;
            BringToFront();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                RectTransform.parent as RectTransform, e.position, GetCamera(), out Vector2 localPos);
            dragOffset = RectTransform.anchoredPosition - localPos;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Graph != null && Graph.IsDraggingConnection) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                RectTransform.parent as RectTransform, e.position, GetCamera(), out Vector2 localPos);
            RectTransform.anchoredPosition = localPos + dragOffset;
            Moved?.Invoke();
        }

        // ---- Sibling order: node, then its attached UI (node index + 1, +2, ...) ----
        // Connection lines and pins are pushed to the very top every frame by WebbingGraph.
        public void BringToFront()
        {
            transform.SetAsLastSibling();
            int idx = transform.GetSiblingIndex();
            foreach (var a in attachments)
                if (a != null && a.gameObject.activeSelf)
                    a.transform.SetSiblingIndex(++idx);
        }

        private Camera GetCamera()
        {
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }
}
