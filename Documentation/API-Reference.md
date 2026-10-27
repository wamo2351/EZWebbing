# EZWebbing — API Reference

Namespace: `EZWebbing`

This document describes every public type, member, and event that ships
with the EZWebbing runtime. Scripts referenced below live under
`EZWebbing/Runtime/`.

---

## Table of Contents

1. [IWebbingNodeData](#iwebbingnodedata)
2. [IWebbingNodePresenter](#iwebbingnodepresenter)
3. [WebbingGraph](#webbinggraph)
4. [WebbingNode](#webbingnode)
5. [WebbingPin](#webbingpin)
6. [WebbingConnection](#webbingconnection)
7. [WebbingConnectionInteractor](#webbingconnectioninteractor)
8. [WebbingPanZoom](#webbingpanzoom)
9. [WebbingLineRenderer](#webbinglinerenderer)
10. [WebbingNodeAttachment](#webbingnodeattachment)
11. [Serialization Types](#serialization-types)

---

## IWebbingNodeData

```csharp
public interface IWebbingNodeData
{
    string NodeId { get; }
}
```

The minimum contract for the data a node holds. Implement this on a
`ScriptableObject` or a plain class to plug your own content into
EZWebbing. `NodeId` must be unique per node — it is used for save/load,
duplicate detection, and lookups.

## IWebbingNodePresenter

```csharp
public interface IWebbingNodePresenter
{
    void Present(IWebbingNodeData data);
}
```

Implement this on a component attached to your node prefab to define how a
node's data is rendered. `Present()` is called automatically by
`WebbingNode.Initialize()` whenever a node is created or loaded. Different
presenters can implement completely different visual layouts (memo card,
photo card, skill icon, etc.) on top of the same core node behavior.

---

## WebbingGraph

`MonoBehaviour`. The central manager of a graph: owns all nodes and
connections, handles connection dragging, placement, and serialization.
Contains no game-specific logic — all game-specific behavior should be
implemented by subscribing to its events or assigning `ConnectionValidator`.

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `nodePrefab` | `GameObject` | Prefab instantiated by `AddNode`. Must have a `WebbingNode` component. |
| `connectionPrefab` | `GameObject` | Prefab instantiated for each connection. Must have a `WebbingConnection` component. |
| `graphContainer` | `RectTransform` | Parent that holds all nodes, connection lines, and pins. |
| `initialScale` | `float` | Initial scale applied to `graphContainer` if it has no `WebbingPanZoom`. |
| `nodeMinDistance` | `float` | Minimum distance enforced between automatically placed nodes. |
| `nodeSpawnRadius` | `float` | Radius within which automatic placement searches for a free position. |
| `placementRetry` | `int` | Number of random placement attempts before falling back to a spiral layout. |
| `flashDuration` | `float` | Duration, in seconds, of the rejection-color flash on a failed connection attempt. |

### Properties

| Member | Type | Description |
|---|---|---|
| `IsDraggingConnection` | `bool` | `true` while the user is dragging a new connection from a pin. |
| `Nodes` | `IReadOnlyList<WebbingNode>` | All nodes currently in the graph. |
| `Connections` | `IReadOnlyList<WebbingConnection>` | All connections currently in the graph. |

### Delegate

```csharp
public Func<WebbingPin, WebbingPin, bool> ConnectionValidator;
```

Assign this to perform custom connection validation. Self-connections and
exact duplicate connections are always rejected before this delegate is
consulted. If left `null`, any connection that is not a self-connection or
duplicate is allowed.

### Events

| Event | Signature | Raised when |
|---|---|---|
| `ConnectionEstablished` | `Action<WebbingPin, WebbingPin>` | A connection between two pins succeeds. |
| `ConnectionFailed` | `Action<WebbingPin, WebbingPin>` | A connection attempt is rejected. |
| `ConnectionRemoved` | `Action<WebbingConnection>` | A connection is removed (by the user, by code, or as a side effect of node removal). |
| `NodeAdded` | `Action<WebbingNode>` | A node is added via `AddNode`. |
| `NodeRemoved` | `Action<WebbingNode>` | A node is removed via `RemoveNode`. |

### Methods

```csharp
WebbingNode AddNode(IWebbingNodeData data, Vector2? position = null)
```
Instantiates `nodePrefab`, initializes it with `data`, and adds it to the
graph. If `position` is omitted, an automatic, overlap-avoiding position is
chosen. Raises `NodeAdded`.

```csharp
void RemoveNode(WebbingNode node)
```
Removes a node and destroys its `GameObject`. Any connections attached to
it are removed automatically first. Raises `NodeRemoved` (and
`ConnectionRemoved` for each detached connection).

```csharp
WebbingNode FindNode(string nodeId)
```
Returns the node whose `NodeId` matches, or `null` if none is found.

```csharp
void BeginConnection(WebbingPin pin, PointerEventData e)
void UpdateConnection(PointerEventData e)
void TryCompleteConnection(WebbingPin targetPin, PointerEventData e)
void EndConnection(PointerEventData e)
```
Called internally by `WebbingPin` during a drag-to-connect gesture. You do
not normally need to call these directly.

```csharp
WebbingConnection Connect(WebbingPin a, WebbingPin b, bool validate = false)
```
Creates a connection directly from code (for example, when restoring saved
data). If `validate` is `true`, the connection also passes through
`ConnectionValidator` and the duplicate/self-connection checks; if `false`,
only the duplicate/self-connection checks are applied. Returns `null` if the
connection could not be created.

```csharp
void RemoveConnection(WebbingConnection c)
void ClearConnections()
```
Removes a single connection, or every connection in the graph. Both raise
`ConnectionRemoved` for each connection removed.

```csharp
string Serialize()
```
Returns a JSON string containing every node's `NodeId` and position, and
every connection's endpoints (identified by node id and pin index).

```csharp
void Deserialize(string json, Func<string, IWebbingNodeData> resolver)
```
Clears the current graph and rebuilds it from `json` (as produced by
`Serialize()`). `resolver` is called once per saved node to obtain the
`IWebbingNodeData` instance for a given `NodeId` — your game is responsible
for looking up or recreating that data (for example, from a save file or an
in-memory registry). Nodes whose id cannot be resolved are skipped with a
warning.

---

## WebbingNode

`MonoBehaviour`, requires `RectTransform`. Represents a single node.
Handles position, drag-to-move, sibling order (Z-order), and pin ownership.
Visual content is delegated to an `IWebbingNodePresenter` on the same
`GameObject`.

### Properties

| Member | Type | Description |
|---|---|---|
| `Data` | `IWebbingNodeData` | The data assigned via `Initialize`. |
| `NodeId` | `string` | Shortcut for `Data?.NodeId`. |
| `Graph` | `WebbingGraph` | The owning graph, set by `Bind`. |
| `RectTransform` | `RectTransform` | Cached reference to this node's `RectTransform`. |
| `Pins` | `IReadOnlyList<WebbingPin>` | Pins owned by this node. |
| `pins` | `List<WebbingPin>` | Inspector-assignable pin list; auto-collected from children if left empty. |
| `isNew` | `bool` | Free-form flag your game can use (e.g. to highlight newly created nodes). |

### Event

```csharp
public event Action Moved;
```
Raised every time the node's position changes via dragging.

### Methods

```csharp
void Bind(WebbingGraph graph)
void Initialize(IWebbingNodeData data)
```
Called by `WebbingGraph.AddNode`; you do not normally call these directly.

```csharp
int IndexOfPin(WebbingPin pin)
WebbingPin GetPin(int index)
```
Look up a pin's index (used by serialization) or retrieve a pin by index.

```csharp
void RegisterAttachment(WebbingNodeAttachment a)
```
Called by `WebbingNodeAttachment.Attach` to register follower UI.

```csharp
void MarkAsNew()
void ClearNew()
```
Convenience setters for the `isNew` flag.

```csharp
void BringToFront()
```
Moves this node (and its registered attachments) to the front of the
sibling order. Called automatically on pointer-down and drag-begin.

---

## WebbingPin

`MonoBehaviour`, requires `Image`. A connection socket on a node. Supports
multiple simultaneous connections.

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `normalColor` | `Color` | Color when idle and unconnected. |
| `hoverColor` | `Color` | Color while the pointer hovers over the pin. |
| `connectedColor` | `Color` | Color while the pin has at least one connection. |

### Properties

| Member | Type | Description |
|---|---|---|
| `ownerNode` | `WebbingNode` | The node this pin belongs to. |
| `ConnectionCount` | `int` | Number of connections currently attached to this pin. |
| `IsConnected` | `bool` | `true` if `ConnectionCount > 0`. |

### Methods

```csharp
Vector3 GetAnchorWorldPos()
```
Returns the pin's current world position, used by `WebbingConnection` to
position connection lines.

```csharp
void AddConnectionRef()
void RemoveConnectionRef()
void ResetConnectionRef()
```
Reference-count bookkeeping called by `WebbingGraph`. Not normally called
directly by user code.

`WebbingPin` also implements the standard Unity `IPointerDownHandler`,
`IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IDropHandler`,
`IPointerEnterHandler`, and `IPointerExitHandler` interfaces to drive the
drag-to-connect gesture; these are invoked automatically by Unity's event
system and do not need to be called manually.

---

## WebbingConnection

`MonoBehaviour`, requires `WebbingLineRenderer`. Represents one connection
line between two pins.

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `pendingColor` | `Color` | Color while the connection is being dragged. |
| `validColor` | `Color` | Color once the connection is established. |
| `invalidColor` | `Color` | Color briefly shown when a connection attempt is rejected. |
| `lineEndInset` | `float` | Pixels to stop the line short of each pin's center. |

### Properties

| Member | Type | Description |
|---|---|---|
| `FromPin` | `WebbingPin` | The connection's origin pin. |
| `ToPin` | `WebbingPin` | The connection's destination pin. |
| `IsComplete` | `bool` | `true` once both `FromPin` and `ToPin` are set. |

### Methods

```csharp
void UpdatePending(Vector2 start, Vector2 cursor)
void Complete(WebbingPin from, WebbingPin to)
void SetInvalid()
void RefreshPosition()
```
Called internally by `WebbingGraph`. `RefreshPosition()` is called once per
frame for every active connection to keep lines attached to moving nodes.

---

## WebbingConnectionInteractor

`MonoBehaviour`, requires `WebbingConnection`. Optional component (add it
to your connection prefab) that lets the user click a connection to delete
it, since the line itself does not receive raycasts.

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `deleteButton` | `MouseButton` (`Left`/`Right`/`Middle`) | Which mouse button deletes the connection. Default: `Right`. |
| `hitThickness` | `float` | Thickness, in pixels, of the clickable area along the line. |
| `hitAreaInset` | `float` | Pixels to shrink the hit area in from each end, to avoid overlapping pin hit areas. |

> **Setup requirement:** the `RectTransform` this component is attached to
> must use anchor = center, pivot = center, `anchoredPosition` = 0, and
> `sizeDelta` = 0, so its local space matches `graphContainer`.

---

## WebbingPanZoom

`MonoBehaviour`, requires `RectTransform`. Attach to `graphContainer` to
enable panning (drag on empty space) and zooming (mouse wheel or buttons).

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `graph` | `WebbingGraph` | Target graph; auto-detected if left unassigned. |
| `baseScale` | `float` | Scale treated as 100%. |
| `zoomStep` | `float` | Scale change per zoom step. |
| `minScale` / `maxScale` | `float` | Zoom clamp range. |
| `panSensitivity` | `float` | Multiplier applied to pan drag delta. |
| `zoomInButton` / `zoomOutButton` | `Button` | Optional buttons wired to a single zoom step. |

### Event

```csharp
public event Action<string> OnZoomChanged;
```
Raised whenever the zoom level changes, passing a formatted percentage
string (e.g. `"120%"`). Subscribe to update any text label — the component
has no direct dependency on TextMeshPro or `UnityEngine.UI.Text`.

### Method

```csharp
void ResetView()
```
Resets pan position to the origin and scale to `baseScale`.

---

## WebbingLineRenderer

`Graphic` (Canvas renderer), requires `CanvasRenderer`. Lightweight
component that draws a straight line between two local-space points. Used
internally by `WebbingConnection`; can also be used standalone for other UI
line-drawing needs.

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `startPoint` / `endPoint` | `Vector2` | Local-space endpoints of the line. |
| `lineWidth` | `float` | Line thickness in pixels. |
| `segments` | `int` | Number of segments (1 is sufficient for a straight line). |

### Methods

```csharp
void SetPoints(Vector2 start, Vector2 end)
void SetLineColor(Color c)
```

---

## WebbingNodeAttachment

`MonoBehaviour`, requires `RectTransform`. Base class for UI that should
follow a node (tooltips, badges, hint labels, etc.) and stay one sibling
above it in Z-order. Intended to be subclassed for game-specific display
logic.

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `owner` | `WebbingNode` | The node this attachment follows. |
| `offset` | `Vector2` | Local-space offset from the node's position. |

### Method

```csharp
virtual void Attach(WebbingNode node, Vector2 offset)
```
Attaches this object to `node` and registers itself so `WebbingNode`
includes it when bringing itself to the front.

---

## Serialization Types

Plain, `[Serializable]` data-transfer objects used by
`WebbingGraph.Serialize()` / `Deserialize()`. All fields are public so
`JsonUtility` can (de)serialize them directly.

```csharp
[Serializable]
public class WebbingNodeState
{
    public string nodeId;
    public float  x;
    public float  y;
}

[Serializable]
public class WebbingConnectionState
{
    public string fromNodeId;
    public int    fromPinIndex;
    public string toNodeId;
    public int    toPinIndex;
}

[Serializable]
public class WebbingGraphState
{
    public List<WebbingNodeState>       nodes;
    public List<WebbingConnectionState> connections;
}
```

You will not normally need to construct these directly; they are exposed so
you can inspect or post-process the JSON produced by `Serialize()` if
needed.
