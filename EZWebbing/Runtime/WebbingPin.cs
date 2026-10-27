// WebbingPin.cs
// A connection socket on a node.
// A pin can hold multiple simultaneous connections; ConnectionCount tracks how many.
// Dragging from one pin and dropping onto another establishes a connection.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EZWebbing
{
    [RequireComponent(typeof(Image))]
    public class WebbingPin : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler
    {
        [HideInInspector] public WebbingNode ownerNode;

        [Header("Pin Colors")]
        public Color normalColor    = new Color(0.85f, 0.15f, 0.15f, 1f);
        public Color hoverColor     = new Color(1.00f, 0.40f, 0.40f, 1f);
        public Color connectedColor = new Color(0.60f, 0.05f, 0.05f, 1f);

        /// <summary>Number of connections currently held by this pin.</summary>
        public int ConnectionCount { get; private set; }
        public bool IsConnected => ConnectionCount > 0;

        private Image image;
        private WebbingGraph Graph => ownerNode != null ? ownerNode.Graph : null;

        private void Awake()
        {
            image = GetComponent<Image>();
            image.color = normalColor;
        }

        public Vector3 GetAnchorWorldPos() => transform.position;

        // ---- Called by WebbingGraph when a connection is added/removed ----
        public void AddConnectionRef()    { ConnectionCount++;                       RefreshColor(); }
        public void RemoveConnectionRef() { ConnectionCount = Mathf.Max(0, ConnectionCount - 1); RefreshColor(); }
        public void ResetConnectionRef()  { ConnectionCount = 0;                     RefreshColor(); }
        private void RefreshColor()       => image.color = IsConnected ? connectedColor : normalColor;

        // ---- Clicking brings the owning node to the front ----
        public void OnPointerDown(PointerEventData e)
        {
            if (ownerNode != null) ownerNode.BringToFront();
        }

        // ---- Dragging draws a connection line ----
        public void OnBeginDrag(PointerEventData e)
        {
            if (Graph == null) return;
            image.raycastTarget = false; // Allow raycasts to reach the drop-target pin underneath.
            Graph.BeginConnection(this, e);
        }

        public void OnDrag(PointerEventData e) => Graph?.UpdateConnection(e);

        public void OnEndDrag(PointerEventData e)
        {
            image.raycastTarget = true;
            RefreshColor();
            Graph?.EndConnection(e);
        }

        // ---- Dropping completes the connection ----
        public void OnDrop(PointerEventData e) => Graph?.TryCompleteConnection(this, e);

        // ---- Hover ----
        public void OnPointerEnter(PointerEventData e) => image.color = hoverColor;
        public void OnPointerExit(PointerEventData e)  => RefreshColor();
    }
}
