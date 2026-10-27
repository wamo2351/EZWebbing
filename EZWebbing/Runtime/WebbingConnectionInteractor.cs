// WebbingConnectionInteractor.cs
// Optional component that lets the user click a connection line to delete it.
//
// The line itself (WebbingLineRenderer) has raycastTarget = false, so this
// component places an invisible hit area along the line every frame and
// listens for clicks there. By default, right-click deletes the connection,
// which calls WebbingGraph.RemoveConnection.
//
// Requirement: the RectTransform of the WebbingConnection this is attached to
// must use anchor = center (0.5, 0.5), pivot = center, anchoredPosition = 0,
// sizeDelta = 0, so its local coordinate space matches the parent graphContainer.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EZWebbing
{
    [RequireComponent(typeof(WebbingConnection))]
    public class WebbingConnectionInteractor : MonoBehaviour
    {
        public enum MouseButton { Left, Right, Middle }

        [Header("Delete Trigger")]
        public MouseButton deleteButton = MouseButton.Right;

        [Tooltip("Thickness of the clickable hit area along the line, in pixels.")]
        public float hitThickness = 16f;

        [Tooltip("Distance in pixels to shrink the hit area in from each end of the line.\n" +
                 "Prevents the hit area from overlapping the pins' own hit areas.\n" +
                 "Set equal to lineEndInset to align exactly with the pin edges.")]
        public float hitAreaInset = 8f;

        private WebbingConnection connection;
        private WebbingGraph      graph;
        private RectTransform     hitArea;
        private Canvas            canvas;

        private void Awake()
        {
            connection = GetComponent<WebbingConnection>();
            graph      = GetComponentInParent<WebbingGraph>();
            canvas     = GetComponentInParent<Canvas>();
            CreateHitArea();
        }

        private void CreateHitArea()
        {
            var go = new GameObject("HitArea", typeof(RectTransform), typeof(Image));
            hitArea = go.GetComponent<RectTransform>();
            hitArea.SetParent(transform, false);
            hitArea.anchorMin = hitArea.anchorMax = new Vector2(0.5f, 0.5f);
            hitArea.pivot     = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>();
            img.color         = new Color(0, 0, 0, 0); // Invisible; raycasting still works at alpha 0.
            img.raycastTarget = true;

            go.AddComponent<HitForwarder>().target = this;
        }

        private void LateUpdate()
        {
            if (connection == null || !connection.IsComplete || hitArea == null)
            {
                if (hitArea != null) hitArea.gameObject.SetActive(false);
                return;
            }

            var parent = transform.parent as RectTransform;
            Vector2 a = ToLocal(connection.FromPin.GetAnchorWorldPos(), parent);
            Vector2 b = ToLocal(connection.ToPin.GetAnchorWorldPos(),   parent);
            Vector2 d = b - a;
            float length = d.magnitude;

            // If shrinking both ends by hitAreaInset leaves no length, hide the hit area.
            float insetTotal = hitAreaInset * 2f;
            if (length <= insetTotal)
            {
                hitArea.gameObject.SetActive(false);
                return;
            }

            hitArea.gameObject.SetActive(true);

            // Position the hit area using the endpoints shrunk inward by hitAreaInset.
            Vector2 unit   = d / length;
            Vector2 start  = a + unit * hitAreaInset;
            Vector2 end    = b - unit * hitAreaInset;
            float trimmed  = length - insetTotal;

            hitArea.anchoredPosition = (start + end) * 0.5f;
            hitArea.sizeDelta        = new Vector2(trimmed, hitThickness);
            hitArea.localRotation    = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        private Camera Cam =>
            (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;

        private Vector2 ToLocal(Vector3 world, RectTransform rt)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, RectTransformUtility.WorldToScreenPoint(Cam, world), Cam, out var local);
            return local;
        }

        private void HandleClick(PointerEventData e)
        {
            bool match = deleteButton switch
            {
                MouseButton.Right  => e.button == PointerEventData.InputButton.Right,
                MouseButton.Middle => e.button == PointerEventData.InputButton.Middle,
                _                  => e.button == PointerEventData.InputButton.Left
            };
            if (match && graph != null) graph.RemoveConnection(connection);
        }

        // Forwards clicks from the child HitArea object to the parent interactor.
        private class HitForwarder : MonoBehaviour, IPointerClickHandler
        {
            public WebbingConnectionInteractor target;
            public void OnPointerClick(PointerEventData e) => target?.HandleClick(e);
        }
    }
}
