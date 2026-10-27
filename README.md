# EZWebbing

**EZWebbing** is a general-purpose visual graph UI framework for Unity UGUI.
It lets players (or your own tools) connect nodes by dragging lines between
pins, and gives you the building blocks — nodes, pins, connections, pan/zoom,
and save/load — needed to build detective boards, crafting systems, skill
trees, mind maps, and similar node-based interfaces.

The framework itself is completely data-agnostic: it has no knowledge of what
a "node" represents. You provide your own data type and visual layout, and
EZWebbing handles placement, dragging, connecting, validating, and
serializing the graph.

## Features

- Drag-to-connect pins with **Pending / Valid / Invalid** visual states
- Pluggable connection validation via a single delegate
  (`WebbingGraph.ConnectionValidator`)
- Automatic, overlap-avoiding placement of new nodes
- Pan and zoom of the workspace, with cursor-anchored zooming
- Click-to-delete connections (optional, via `WebbingConnectionInteractor`)
- Save and load the entire graph (node positions + connections) as JSON
- Fully event-driven integration (`NodeAdded`, `NodeRemoved`,
  `ConnectionEstablished`, `ConnectionFailed`, `ConnectionRemoved`)
- No dependency on any specific node data type — implement `IWebbingNodeData`
  and `IWebbingNodePresenter` to plug in your own content and visuals

## Requirements

- Unity 2022.3 LTS or later
- Unity UI (uGUI)
- TextMeshPro (`com.unity.textmeshpro`) — **only** needed for the bundled
  Mind Map sample, whose presenter displays labels with `TextMeshProUGUI`.
  The core runtime does not depend on it. The sample is not compiled unless
  you import it, so projects without TextMeshPro are unaffected.

## Installation

### Option A — Unity Package Manager (Git URL)

1. Open **Window > Package Manager**.
2. Click **+ > Add package from git URL...**
3. Enter:

   ```
   https://github.com/<owner>/EZWebbing.git?path=/EZWebbing
   ```

   To pin a version, append a tag, e.g. `...?path=/EZWebbing#v1.0.0`.

### Option B — Copy into your project

Copy the `EZWebbing/` folder (the one containing `package.json`) into your
project's `Packages/` directory (or `Assets/`). Keep the `.meta` files so
that script and asset references stay intact.

### Importing the sample

Open **Window > Package Manager > EZWebbing > Samples** and click **Import**
on *Mind Map*. It is copied into your `Assets/` folder, where you can open
and modify it freely.

## Quick Start

1. Create a UI **Canvas** and add an empty `RectTransform` inside it to act
   as your workspace (this will be your `graphContainer`).
2. Add a `WebbingGraph` component anywhere in the scene and assign:
   - `graphContainer` → the RectTransform from step 1
   - `nodePrefab` → a prefab with a `WebbingNode` component
   - `connectionPrefab` → a prefab with a `WebbingConnection` component
3. (Optional) Add a `WebbingPanZoom` component to `graphContainer` to enable
   mouse-wheel zoom and drag-to-pan.
4. Implement `IWebbingNodeData` on your own data type (a `ScriptableObject`
   or a plain class) and `IWebbingNodePresenter` on a component in your node
   prefab to define how that data is displayed.
5. Call `graph.AddNode(myData)` to add nodes at runtime. Drag from one pin to
   another to connect them.
6. Call `graph.Serialize()` / `graph.Deserialize(json, resolver)` to save and
   restore the graph.

## Documentation

| Document | Contents |
|---|---|
| [API Reference](EZWebbing/Documentation~/API-Reference.md) | Every public type, member, and event |
| [Implementation Examples](EZWebbing/Documentation~/Implementation-Examples.md) | Patterns for mind maps, deduction boards, crafting, skill trees, dialogue graphs, and more |
| [Changelog](EZWebbing/CHANGELOG.md) | Version history |

## Repository Layout

```
EZWebbing/            Unity package root (package.json lives here)
├─ Runtime/           Core runtime scripts (namespace: EZWebbing)
├─ Editor/            Custom inspector (editor-only assembly)
├─ Arts/              Sprites used by the sample (pins, corkboard, paper)
├─ Samples~/MindMap/  Sample scene, prefabs, and scripts (import via Package Manager)
└─ Documentation~/    API reference and implementation examples
```

All core runtime scripts live under the `EZWebbing` namespace and have no
dependency on the sample content.

## Samples

| Sample | Description |
|---|---|
| **Mind Map** | A free-form board that allows any connection, with free node placement and Save/Load support. Located in `EZWebbing/Samples~/MindMap`; import it from the Package Manager. |

## Contributing

Bug reports, feature requests, and pull requests are welcome. Please see
[CONTRIBUTING.md](CONTRIBUTING.md) before you start.

## License

Released under the [MIT License](LICENSE). The license covers all contents of
this repository, including the source code, documentation, and the image
files in `EZWebbing/Arts/`.
