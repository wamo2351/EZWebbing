// WebbingPanZoom.cs
// Handles panning, zooming, and resetting the view of the workspace
// (graphContainer). Attach this component to the graphContainer RectTransform
// you want to scale.

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EZWebbing
{
    [RequireComponent(typeof(RectTransform))]
    public class WebbingPanZoom : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        [Header("Target Graph (auto-detected if left unassigned)")]
        public WebbingGraph graph;

        [Header("Zoom Settings")]
        [Tooltip("Scale treated as 100% (the initial scale).")]
        public float baseScale = 1.0f;
        [Tooltip("Amount the scale changes per zoom step.")]
        public float zoomStep  = 0.02f;
        public float minScale  = 0.5f;
        public float maxScale  = 2.5f;

        [Header("Pan Settings")]
        public float panSensitivity = 1.0f;

        [Header("Zoom Label UI (optional)")]
        [Tooltip("Assign a Button here to trigger a zoom step. Text display is " +
                 "handled via the OnZoomChanged callback below rather than a direct " +
                 "text reference, so this component has no dependency on any " +
                 "particular text component (TextMeshPro, UI.Text, etc).")]
        public Button zoomInButton;
        public Button zoomOutButton;

        /// <summary>
        /// Raised whenever the zoom level changes, passing a formatted percentage
        /// string (e.g. "120%"). Works with any text component.
        /// Example: panZoom.OnZoomChanged += s => myLabel.text = s;
        /// </summary>
        public event Action<string> OnZoomChanged;

        private RectTransform rt;
        private Canvas        canvas;
        private bool          isPanning;
        private Vector2       lastMousePos;

        private void Awake()
        {
            rt     = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            if (graph == null)
                graph = GetComponentInParent<WebbingGraph>() ?? FindFirstObjectByType<WebbingGraph>();
        }

        private void Start()
        {
            rt.localScale = Vector3.one * baseScale;
            UpdateLabel();
            if (zoomInButton)  zoomInButton.onClick.AddListener(OnZoomInButton);
            if (zoomOutButton) zoomOutButton.onClick.AddListener(OnZoomOutButton);
        }

        private void OnDestroy()
        {
            if (zoomInButton)  zoomInButton.onClick.RemoveListener(OnZoomInButton);
            if (zoomOutButton) zoomOutButton.onClick.RemoveListener(OnZoomOutButton);
        }

        // Pan and zoom are suppressed while a connection is being dragged.
        private bool Dragging => graph != null && graph.IsDraggingConnection;

        private void OnZoomInButton()  => ApplyZoomStep(+1, ScreenCenter());
        private void OnZoomOutButton() => ApplyZoomStep(-1, ScreenCenter());
        private Vector2 ScreenCenter() => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        public void OnScroll(PointerEventData e)
        {
            if (!gameObject.activeInHierarchy || Dragging) return;
            int dir = e.scrollDelta.y > 0 ? +1 : -1;
            ApplyZoomStep(dir, e.position);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!gameObject.activeInHierarchy || Dragging) return;
            if (e.button != PointerEventData.InputButton.Left) return;
            if (e.pointerPressRaycast.gameObject != gameObject) return; // Only pan when clicking empty space.
            isPanning    = true;
            lastMousePos = e.position;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!isPanning) return;
            if (Dragging) { isPanning = false; return; }
            Vector2 delta = (e.position - lastMousePos) * panSensitivity;
            if (canvas != null) delta /= canvas.scaleFactor;
            rt.anchoredPosition += delta;
            lastMousePos = e.position;
        }

        public void OnPointerUp(PointerEventData e) => isPanning = false;

        /// <summary>Zooms around screenPoint as a fixed reference so the content under the cursor doesn't drift.</summary>
        private void ApplyZoomStep(int steps, Vector2 screenPoint)
        {
            float current = rt.localScale.x;
            float target  = Mathf.Clamp(RoundToStep(current + steps * zoomStep), minScale, maxScale);
            if (Mathf.Approximately(current, target)) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt.parent as RectTransform, screenPoint, GetCamera(), out Vector2 mouseLocal);

            float   scaleDiff = target - current;
            Vector2 shift     = (mouseLocal - rt.anchoredPosition) * (scaleDiff / current);

            rt.localScale       = Vector3.one * target;
            rt.anchoredPosition -= shift;
            UpdateLabel();
        }

        private float RoundToStep(float v) => zoomStep <= 0f ? v : Mathf.Round(v / zoomStep) * zoomStep;

        private void UpdateLabel()
        {
            string text = $"{Mathf.RoundToInt(rt.localScale.x / baseScale * 100f)}%";
            OnZoomChanged?.Invoke(text);
        }

        private Camera GetCamera()
        {
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        /// <summary>Resets the view to its centered position and base scale.</summary>
        public void ResetView()
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale       = Vector3.one * baseScale;
            UpdateLabel();
        }
    }
}
