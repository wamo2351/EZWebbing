// WebbingConnection.cs
// Represents a single connection line between two pins.
// Supports three visual states: Pending (being dragged), Valid (established),
// and Invalid (rejected, used for a brief failure flash).
// Screen-space and camera-space Canvas render modes are both supported.

using UnityEngine;

namespace EZWebbing
{
    [RequireComponent(typeof(WebbingLineRenderer))]
    public class WebbingConnection : MonoBehaviour
    {
        public WebbingPin FromPin { get; private set; }
        public WebbingPin ToPin   { get; private set; }
        public bool IsComplete => FromPin != null && ToPin != null;

        [Header("Connection Colors")]
        [Tooltip("Color used while the connection is being dragged (pending).")]
        public Color pendingColor = new Color(1.0f, 0.3f, 0.3f, 0.7f);
        [Tooltip("Color used once the connection is successfully established.")]
        public Color validColor   = new Color(0.85f, 0.10f, 0.10f, 1.0f);
        [Tooltip("Color used briefly when a connection attempt is rejected.")]
        public Color invalidColor = new Color(1.0f, 0.55f, 0.20f, 1.0f);

        [Header("Line End Inset")]
        [Tooltip("Distance in pixels to stop the line short of each pin's center, " +
                 "so the pin graphic isn't hidden behind the line. Use 0 to draw " +
                 "all the way to the pin center.")]
        public float lineEndInset = 8f;

        private WebbingLineRenderer lineRenderer;
        private Canvas canvas;

        private void Awake()
        {
            lineRenderer = GetComponent<WebbingLineRenderer>();
            if (lineRenderer == null)
                lineRenderer = gameObject.AddComponent<WebbingLineRenderer>();
            canvas = GetComponentInParent<Canvas>();
        }

        /// <summary>Updates the temporary line while a connection is being dragged.</summary>
        public void UpdatePending(Vector2 start, Vector2 cursor)
        {
            lineRenderer.SetLineColor(pendingColor);
            lineRenderer.SetPoints(start, cursor);
        }

        /// <summary>Finalizes the connection between two pins.</summary>
        public void Complete(WebbingPin from, WebbingPin to)
        {
            FromPin = from;
            ToPin   = to;
            lineRenderer.SetLineColor(validColor);
            RefreshPosition();
        }

        /// <summary>Switches to the rejection color, used for the failure flash effect.</summary>
        public void SetInvalid() => lineRenderer.SetLineColor(invalidColor);

        /// <summary>Repositions the line to follow its connected nodes. Called every frame by WebbingGraph.</summary>
        public void RefreshPosition()
        {
            if (!IsComplete) return;
            var graphRT = transform.parent as RectTransform;
            var fromPos = WorldToGraph(FromPin.GetAnchorWorldPos(), graphRT);
            var toPos   = WorldToGraph(ToPin.GetAnchorWorldPos(),   graphRT);

            // When lineEndInset > 0, pull both ends in toward the pin centers.
            if (lineEndInset > 0f)
            {
                Vector2 dir = (toPos - fromPos);
                float length = dir.magnitude;
                if (length > lineEndInset * 2f)
                {
                    Vector2 unit = dir / length;
                    fromPos += unit * lineEndInset;
                    toPos   -= unit * lineEndInset;
                }
            }

            lineRenderer.SetPoints(fromPos, toPos);
        }

        private Camera Cam =>
            (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;

        private Vector2 WorldToGraph(Vector3 worldPos, RectTransform graphRT)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                graphRT,
                RectTransformUtility.WorldToScreenPoint(Cam, worldPos),
                Cam,
                out Vector2 local);
            return local;
        }
    }
}
