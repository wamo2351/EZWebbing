// WebbingGraph.cs
// Core manager of the EZWebbing framework.
//
// Design notes:
//   - Contains no game-specific logic (no stage data, flags, dialogs, or
//     board open/close state). It only manages nodes, connections, and layout.
//   - Whether a connection is allowed is decided by the ConnectionValidator
//     delegate, which external code can assign.
//   - The full connection lifecycle is exposed through C# events so that
//     game-specific behavior can be implemented by subscribing to them.
//   - Individual connections can be removed with RemoveConnection.
//   - Serialize / Deserialize provide simple save/load support.
//
// Game-specific logic should live in an adapter that subscribes to these
// events/hooks rather than being added to this class directly.
//
// Sibling (Z-order) policy, back to front:
//   Nodes and their attachments -> connection lines -> pins.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EZWebbing
{
    public class WebbingGraph : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Prefab that has a WebbingNode component (and optionally an IWebbingNodePresenter).")]
        public GameObject nodePrefab;
        [Tooltip("Prefab that has a WebbingConnection component.")]
        public GameObject connectionPrefab;

        [Header("UI References")]
        [Tooltip("Parent RectTransform that holds nodes, lines, and pins.")]
        public RectTransform graphContainer;

        [Header("Initial Scale (only applied if graphContainer has no WebbingPanZoom)")]
        public float initialScale = 1.0f;

        [Header("Automatic Node Placement")]
        public float nodeMinDistance = 220f;
        public float nodeSpawnRadius = 350f;
        public int   placementRetry  = 30;

        [Header("Rejection Flash Duration (seconds)")]
        public float flashDuration = 0.4f;

        // ───────── Hooks / Events ─────────

        /// <summary>
        /// Final validation hook for whether a connection between two pins is
        /// allowed. Self-connections and duplicate connections are already
        /// rejected by the time this is called. If null, any connection that
        /// isn't a self-connection or duplicate is allowed.
        /// </summary>
        public Func<WebbingPin, WebbingPin, bool> ConnectionValidator;

        /// <summary>Raised when a connection is successfully established (from, to).</summary>
        public event Action<WebbingPin, WebbingPin> ConnectionEstablished;
        /// <summary>Raised when a connection attempt is rejected (from, to).</summary>
        public event Action<WebbingPin, WebbingPin> ConnectionFailed;
        /// <summary>Raised when an existing connection is removed.</summary>
        public event Action<WebbingConnection>      ConnectionRemoved;
        /// <summary>Raised when a node is added.</summary>
        public event Action<WebbingNode>            NodeAdded;
        /// <summary>Raised when a node is removed.</summary>
        public event Action<WebbingNode>            NodeRemoved;

        // ───────── State ─────────
        public bool IsDraggingConnection { get; private set; }

        private readonly List<WebbingNode>       nodes       = new();
        private readonly List<WebbingConnection> connections = new();
        public IReadOnlyList<WebbingNode>       Nodes       => nodes;
        public IReadOnlyList<WebbingConnection> Connections => connections;

        private WebbingConnection pendingLine;
        private WebbingPin        pendingPin;
        private Canvas            canvas;

        // ───────── Unity Lifecycle ─────────
        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        }

        private void Start()
        {
            if (graphContainer != null && graphContainer.GetComponent<WebbingPanZoom>() == null)
                graphContainer.localScale = Vector3.one * initialScale;
        }

        private void Update()
        {
            foreach (var c in connections) if (c != null) c.RefreshPosition();
            PushLinesAndPinsToTop();
        }

        // ───────── Node Management ─────────

        /// <summary>Instantiates a node from data. If position is omitted, it is placed automatically.</summary>
        public WebbingNode AddNode(IWebbingNodeData data, Vector2? position = null)
        {
            if (data == null || nodePrefab == null || graphContainer == null) return null;

            var go   = Instantiate(nodePrefab, graphContainer);
            var node = go.GetComponent<WebbingNode>();
            if (node == null)
            {
                Debug.LogError("[EZWebbing] nodePrefab does not have a WebbingNode component.");
                Destroy(go);
                return null;
            }

            node.Bind(this);
            node.Initialize(data);

            Vector2 pos = position ?? FindPlacementPosition(GetPlacedPositions());
            node.RectTransform.anchoredPosition = pos;

            nodes.Add(node);
            NodeAdded?.Invoke(node);
            return node;
        }

        /// <summary>Removes a node and automatically disconnects any connections attached to it.</summary>
        public void RemoveNode(WebbingNode node)
        {
            if (node == null) return;
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                var c = connections[i];
                if (c.FromPin?.ownerNode == node || c.ToPin?.ownerNode == node)
                    RemoveConnection(c);
            }
            nodes.Remove(node);
            NodeRemoved?.Invoke(node);
            if (node != null) Destroy(node.gameObject);
        }

        public WebbingNode FindNode(string nodeId)
        {
            foreach (var n in nodes) if (n != null && n.NodeId == nodeId) return n;
            return null;
        }

        // ───────── Connection Dragging (called from WebbingPin) ─────────

        public void BeginConnection(WebbingPin pin, PointerEventData e)
        {
            if (connectionPrefab == null) return;
            IsDraggingConnection = true;
            pendingPin           = pin;
            var go = Instantiate(connectionPrefab, graphContainer);
            pendingLine = go.GetComponent<WebbingConnection>();
        }

        public void UpdateConnection(PointerEventData e)
        {
            if (!IsDraggingConnection || pendingLine == null) return;
            Vector2 start = WorldToGraph(pendingPin.GetAnchorWorldPos());
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                graphContainer, e.position, EventCamera(), out Vector2 cursor);
            pendingLine.UpdatePending(start, cursor);
        }

        /// <summary>Attempts to complete a connection on the target pin. Validity is checked by CanConnect.</summary>
        public void TryCompleteConnection(WebbingPin targetPin, PointerEventData e)
        {
            if (!IsDraggingConnection || pendingLine == null) return;

            var from = pendingPin;
            var to   = targetPin;

            if (!CanConnect(from, to))
            {
                ConnectionFailed?.Invoke(from, to);
                StartCoroutine(FlashAndDestroy(pendingLine));
                CleanupPending();
                return;
            }

            pendingLine.Complete(from, to);
            from.AddConnectionRef();
            to.AddConnectionRef();
            connections.Add(pendingLine);
            CleanupPending();

            ConnectionEstablished?.Invoke(from, to);
        }

        public void EndConnection(PointerEventData e)
        {
            if (!IsDraggingConnection) return;
            if (pendingLine != null) Destroy(pendingLine.gameObject);
            CleanupPending();
        }

        private void CleanupPending()
        {
            IsDraggingConnection = false;
            pendingLine          = null;
            pendingPin           = null;
        }

        // ───────── Connection Validation ─────────
        private bool CanConnect(WebbingPin a, WebbingPin b)
        {
            if (a == null || b == null) return false;
            if (a == b) return false;                                   // Same pin.
            if (a.ownerNode == b.ownerNode) return false;               // Self-connection.
            if (Exists(a, b)) return false;                             // Duplicate connection.
            if (ConnectionValidator != null) return ConnectionValidator(a, b);
            return true;
        }

        private bool Exists(WebbingPin a, WebbingPin b)
        {
            foreach (var c in connections)
                if ((c.FromPin == a && c.ToPin == b) || (c.FromPin == b && c.ToPin == a))
                    return true;
            return false;
        }

        // ───────── Programmatic Connect / Remove (used for loading and testing) ─────────

        /// <summary>Connects two pins directly from code (e.g. when loading saved data).
        /// Set validate = true to also run the normal connection rules.</summary>
        public WebbingConnection Connect(WebbingPin a, WebbingPin b, bool validate = false)
        {
            if (validate)
            {
                if (!CanConnect(a, b)) return null;
            }
            else if (a == null || b == null || a == b || a.ownerNode == b.ownerNode || Exists(a, b))
            {
                return null;
            }

            var go   = Instantiate(connectionPrefab, graphContainer);
            var conn = go.GetComponent<WebbingConnection>();
            conn.Complete(a, b);
            a.AddConnectionRef();
            b.AddConnectionRef();
            connections.Add(conn);
            ConnectionEstablished?.Invoke(a, b);
            return conn;
        }

        /// <summary>Removes a single connection.</summary>
        public void RemoveConnection(WebbingConnection c)
        {
            if (c == null) return;
            connections.Remove(c);
            c.FromPin?.RemoveConnectionRef();
            c.ToPin?.RemoveConnectionRef();
            ConnectionRemoved?.Invoke(c);
            if (c != null) Destroy(c.gameObject);
        }

        /// <summary>Removes every connection.</summary>
        public void ClearConnections()
        {
            for (int i = connections.Count - 1; i >= 0; i--)
                RemoveConnection(connections[i]);
        }

        // ───────── Sibling (Z-order) Policy ─────────
        private void PushLinesAndPinsToTop()
        {
            if (graphContainer == null) return;
            foreach (var c in connections) if (c != null) c.transform.SetAsLastSibling();
            if (pendingLine != null) pendingLine.transform.SetAsLastSibling();
            foreach (var pin in graphContainer.GetComponentsInChildren<WebbingPin>(true))
                pin.transform.SetAsLastSibling();
        }

        // ───────── Automatic Placement ─────────
        private Vector2 FindPlacementPosition(List<Vector2> existing)
        {
            for (int i = 0; i < placementRetry; i++)
            {
                Vector2 candidate = UnityEngine.Random.insideUnitCircle * nodeSpawnRadius;
                bool valid = true;
                foreach (var pos in existing)
                    if (Vector2.Distance(candidate, pos) < nodeMinDistance) { valid = false; break; }
                if (valid) return candidate;
            }
            return SpiralFallback(existing.Count);
        }

        private List<Vector2> GetPlacedPositions()
        {
            var result = new List<Vector2>();
            foreach (var n in nodes) if (n != null) result.Add(n.RectTransform.anchoredPosition);
            return result;
        }

        private Vector2 SpiralFallback(int index)
        {
            float angle  = index * 137.5f * Mathf.Deg2Rad;
            float radius = nodeMinDistance * 0.6f * Mathf.Sqrt(index + 1);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        // ───────── Rejection Flash ─────────
        private IEnumerator FlashAndDestroy(WebbingConnection line)
        {
            if (line == null) yield break;
            line.SetInvalid();
            yield return new WaitForSecondsRealtime(flashDuration);
            if (line != null) Destroy(line.gameObject);
        }

        // ───────── Coordinate Conversion ─────────
        private Camera EventCamera() =>
            (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;

        private Vector2 WorldToGraph(Vector3 worldPos)
        {
            var cam = EventCamera();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                graphContainer,
                RectTransformUtility.WorldToScreenPoint(cam, worldPos),
                cam,
                out Vector2 local);
            return local;
        }

        // ───────── Serialization ─────────

        /// <summary>Writes the current node layout and connections to a JSON string.</summary>
        public string Serialize()
        {
            var state = new WebbingGraphState();

            foreach (var n in nodes)
            {
                if (n == null || n.Data == null) continue;
                state.nodes.Add(new WebbingNodeState
                {
                    nodeId = n.NodeId,
                    x = n.RectTransform.anchoredPosition.x,
                    y = n.RectTransform.anchoredPosition.y
                });
            }

            foreach (var c in connections)
            {
                if (!c.IsComplete) continue;
                state.connections.Add(new WebbingConnectionState
                {
                    fromNodeId   = c.FromPin.ownerNode.NodeId,
                    fromPinIndex = c.FromPin.ownerNode.IndexOfPin(c.FromPin),
                    toNodeId     = c.ToPin.ownerNode.NodeId,
                    toPinIndex   = c.ToPin.ownerNode.IndexOfPin(c.ToPin)
                });
            }

            return JsonUtility.ToJson(state);
        }

        /// <summary>
        /// Restores node layout and connections from a JSON string produced by Serialize().
        /// resolver looks up an IWebbingNodeData instance for a given nodeId; the caller
        /// (game-specific code) is responsible for providing it.
        /// </summary>
        public void Deserialize(string json, Func<string, IWebbingNodeData> resolver)
        {
            if (string.IsNullOrEmpty(json) || resolver == null) return;

            // Clear the current graph first.
            ClearConnections();
            for (int i = nodes.Count - 1; i >= 0; i--) RemoveNode(nodes[i]);

            var state = JsonUtility.FromJson<WebbingGraphState>(json);
            if (state == null) return;

            var map = new Dictionary<string, WebbingNode>();
            foreach (var ns in state.nodes)
            {
                var data = resolver(ns.nodeId);
                if (data == null)
                {
                    Debug.LogWarning($"[EZWebbing] resolver could not resolve node id '{ns.nodeId}'.");
                    continue;
                }
                var node = AddNode(data, new Vector2(ns.x, ns.y));
                if (node != null) map[ns.nodeId] = node;
            }

            foreach (var cs in state.connections)
            {
                if (!map.TryGetValue(cs.fromNodeId, out var fromNode)) continue;
                if (!map.TryGetValue(cs.toNodeId,   out var toNode))   continue;
                var a = fromNode.GetPin(cs.fromPinIndex);
                var b = toNode.GetPin(cs.toPinIndex);
                if (a != null && b != null) Connect(a, b);
            }
        }
    }
}
