# EZWebbing — Implementation Examples

EZWebbing's core (`WebbingGraph`, `WebbingNode`, `WebbingPin`,
`WebbingConnection`) has no idea what a node represents. All game-specific
behavior is added by:

1. Implementing `IWebbingNodeData` for your own data.
2. Implementing `IWebbingNodePresenter` to draw that data on your node
   prefab.
3. Assigning `WebbingGraph.ConnectionValidator` to decide which connections
   are legal.
4. Subscribing to `WebbingGraph` events (`NodeAdded`, `NodeRemoved`,
   `ConnectionEstablished`, `ConnectionFailed`, `ConnectionRemoved`) to react
   to what the player does.

This document walks through several common patterns built entirely on top
of that same API. Each pattern is a starting point, not a drop-in
component — adapt the data classes and validation logic to your project.

## Contents

1. [Mind Map (included sample)](#1-mind-map-included-sample)
2. [Detective / Deduction Board](#2-detective--deduction-board)
3. [Crafting System](#3-crafting-system)
4. [Skill Tree / Tech Tree](#4-skill-tree--tech-tree)
5. [Dialogue / Quest Graph Editor](#5-dialogue--quest-graph-editor)
6. [Read-Only Relationship Diagram](#6-read-only-relationship-diagram)
7. [Tips: Giving Pins a Role](#7-tips-giving-pins-a-role)

---

## 1. Mind Map (included sample)

**Concept:** A free-form board where any node can connect to any other node,
with no validation rules. Used for brainstorming, note-linking, and
relationship mapping.

This is the pattern shown in full in the Mind Map sample (`Samples~/MindMap`) — see
`MindMapController.cs`, `MindMapNodeData.cs`, and `MindMapNodePresenter.cs`
for a complete, working reference implementation, including Save/Load.

**Key points:**
- `ConnectionValidator` is left unassigned, so any non-duplicate,
  non-self connection is allowed.
- Nodes are created at runtime with `ScriptableObject.CreateInstance`, kept
  in a `Dictionary<string, MindMapNodeData>` for save/load lookups, and
  removed with `WebbingGraph.RemoveNode`.
- `WebbingGraph.Serialize()` / `Deserialize()` handle Save/Load directly.

---

## 2. Detective / Deduction Board

**Concept:** The player pins up clues and connects related ones. Certain
*correct* combinations of connections reveal a new "conclusion" node;
incorrect combinations are rejected with the built-in failure flash.

### Data

```csharp
public class ClueData : ScriptableObject, IWebbingNodeData
{
    public string id;
    public string title;
    public Sprite photo;
    public string NodeId => id;
}
```

### Defining valid combinations

Store the solution as data — a list of clue-id pairs that are allowed to
connect — rather than hard-coding it in the validator:

```csharp
[System.Serializable]
public struct CluePair { public string clueIdA; public string clueIdB; }

public class MysteryBoardController : MonoBehaviour
{
    public WebbingGraph graph;
    public List<CluePair> validPairs;      // Authored per case/level.
    public GameObject conclusionNodePrefabData; // Or however you spawn the reveal.

    private readonly HashSet<(string, string)> solved = new();

    private void Awake()
    {
        graph.ConnectionValidator = IsValidClueConnection;
        graph.ConnectionEstablished += OnConnectionEstablished;
    }

    private bool IsValidClueConnection(WebbingPin a, WebbingPin b)
    {
        var da = a.ownerNode.Data as ClueData;
        var db = b.ownerNode.Data as ClueData;
        if (da == null || db == null) return false;

        foreach (var pair in validPairs)
        {
            bool match = (pair.clueIdA == da.id && pair.clueIdB == db.id) ||
                         (pair.clueIdA == db.id && pair.clueIdB == da.id);
            if (match) return true;
        }
        return false; // Any pair not explicitly listed fails and flashes.
    }

    private void OnConnectionEstablished(WebbingPin a, WebbingPin b)
    {
        var da = (ClueData)a.ownerNode.Data;
        var db = (ClueData)b.ownerNode.Data;
        var key = string.CompareOrdinal(da.id, db.id) < 0 ? (da.id, db.id) : (db.id, da.id);

        if (solved.Add(key))
            RevealConclusionFor(key); // Spawn/unlock a node, play a sting, etc.
    }

    private void RevealConclusionFor((string, string) key)
    {
        // e.g. graph.AddNode(conclusionData) once all required pairs are solved.
    }
}
```

**Key points:**
- `ConnectionValidator` returning `false` automatically triggers the
  built-in Invalid-color flash (`WebbingConnection.invalidColor`) and
  raises `ConnectionFailed` — no extra code is needed for the "wrong guess"
  feedback.
- Keep the solution data (`validPairs`) separate from the validator logic
  so the same controller can drive multiple cases/levels.

---

## 3. Crafting System

**Concept:** Dragging a connection between two ingredient nodes combines
them: both ingredients are consumed and a new result node appears.

### Data

```csharp
public class IngredientData : ScriptableObject, IWebbingNodeData
{
    public string id;
    public string itemName;
    public string NodeId => id;
}
```

### Recipe lookup and combination

```csharp
[System.Serializable]
public struct Recipe { public string ingredientA; public string ingredientB; public string resultId; }

public class CraftingController : MonoBehaviour
{
    public WebbingGraph graph;
    public List<Recipe> recipes;
    public System.Func<string, IngredientData> resultLookup; // resultId -> data

    private void Awake()
    {
        graph.ConnectionValidator = HasRecipeFor;
        graph.ConnectionEstablished += OnCombine;
    }

    private bool HasRecipeFor(WebbingPin a, WebbingPin b) => FindRecipe(a, b) != null;

    private void OnCombine(WebbingPin a, WebbingPin b)
    {
        var recipe = FindRecipe(a, b);
        if (recipe == null) return;

        var nodeA = a.ownerNode;
        var nodeB = b.ownerNode;
        Vector2 midpoint = (nodeA.RectTransform.anchoredPosition +
                             nodeB.RectTransform.anchoredPosition) * 0.5f;

        // Consume the ingredients (RemoveNode also cleans up the connection
        // that was just formed between them).
        graph.RemoveNode(nodeA);
        graph.RemoveNode(nodeB);

        // Spawn the result in their place.
        var resultData = resultLookup(recipe.Value.resultId);
        graph.AddNode(resultData, midpoint);
    }

    private Recipe? FindRecipe(WebbingPin a, WebbingPin b)
    {
        var idA = ((IngredientData)a.ownerNode.Data).id;
        var idB = ((IngredientData)b.ownerNode.Data).id;
        foreach (var r in recipes)
            if ((r.ingredientA == idA && r.ingredientB == idB) ||
                (r.ingredientA == idB && r.ingredientB == idA))
                return r;
        return null;
    }
}
```

**Key points:**
- Because `RemoveNode` already detaches any connections on a node, you can
  safely remove both ingredient nodes right after the combination without
  manually clearing the connection you just formed.
- `ConnectionEstablished` fires *after* the connection exists in the graph,
  so it's safe to remove the nodes involved from inside the handler.

---

## 4. Skill Tree / Tech Tree

**Concept:** Nodes represent skills. A skill can only be unlocked by
connecting it to an already-unlocked prerequisite skill. The framework
doesn't need to know about "unlocked" state — that's tracked in your data
and enforced by the validator.

### Data

```csharp
public class SkillData : ScriptableObject, IWebbingNodeData
{
    public string id;
    public string skillName;
    public int    cost;
    public bool   isUnlocked;
    public string NodeId => id;
}
```

### Validation and unlocking

```csharp
public class SkillTreeController : MonoBehaviour
{
    public WebbingGraph graph;
    public int currentPoints;

    private void Awake()
    {
        graph.ConnectionValidator = CanUnlock;
        graph.ConnectionEstablished += OnUnlock;
    }

    private bool CanUnlock(WebbingPin from, WebbingPin to)
    {
        var prerequisite = (SkillData)from.ownerNode.Data;
        var target       = (SkillData)to.ownerNode.Data;

        if (!prerequisite.isUnlocked) return false;   // Prerequisite not met.
        if (target.isUnlocked)        return false;   // Already unlocked.
        if (currentPoints < target.cost) return false; // Can't afford it.
        return true;
    }

    private void OnUnlock(WebbingPin from, WebbingPin to)
    {
        var target = (SkillData)to.ownerNode.Data;
        target.isUnlocked = true;
        currentPoints -= target.cost;
        // Refresh the node's visuals, e.g. via its presenter:
        to.ownerNode.GetComponent<IWebbingNodePresenter>()?.Present(target);
    }
}
```

**Key points:**
- Which pin is the "prerequisite" side and which is the "target" side
  matters here — see [Section 7](#7-tips-giving-pins-a-role) for ways to
  distinguish input/output pins on a node.
- Because `ConnectionValidator` runs before the connection is created, it's
  the right place for "can afford / meets prerequisite" checks; use
  `ConnectionEstablished` for the resulting state change (spending points,
  flipping `isUnlocked`).
- A static tree is normally laid out once at design time using fixed node
  positions (pass an explicit `position` to `AddNode`) rather than the
  automatic placement used by free-form boards.

---

## 5. Dialogue / Quest Graph Editor

**Concept:** An editor-time tool (rather than in-game player interaction)
where a designer builds branching dialogue or quest flows by connecting
"line" nodes together. Order matters, and each node should have exactly one
outgoing connection per choice.

### Data

```csharp
public class DialogueLineData : ScriptableObject, IWebbingNodeData
{
    public string id;
    [TextArea] public string text;
    public string NodeId => id;
}
```

### Enforcing a single outgoing connection per choice pin

```csharp
public class DialogueGraphController : MonoBehaviour
{
    public WebbingGraph graph;

    private void Awake()
    {
        graph.ConnectionValidator = ValidateBranch;
    }

    private bool ValidateBranch(WebbingPin from, WebbingPin to)
    {
        // Each "from" pin represents one dialogue choice and may only
        // lead to a single next line.
        if (from.IsConnected) return false;

        // Prevent a line from linking back to itself indirectly by
        // disallowing a node from connecting to one of its own ancestors —
        // implement a graph walk here if you need to guard against cycles.
        return true;
    }
}
```

**Key points:**
- `WebbingPin.IsConnected` / `ConnectionCount` make simple "at most one
  connection" rules easy to enforce directly in the validator.
- For editor tooling, `Serialize()` / `Deserialize()` double as your save
  format for authored dialogue trees — write the JSON to a `TextAsset` or
  your own file format alongside the `DialogueLineData` assets referenced
  by each node.
- Because this is a design-time tool, you will typically build it as an
  `EditorWindow` hosting a `WebbingGraph` on an `IMGUI`/`UIElements`-driven
  Canvas, or as a dedicated in-editor scene — EZWebbing itself does not
  care which.

---

## 6. Read-Only Relationship Diagram

**Concept:** Not every use of EZWebbing needs player-driven dragging. You
can build a graph entirely from code — useful for org charts, dependency
diagrams, or any visualization generated from existing data.

```csharp
public class RelationshipDiagram : MonoBehaviour
{
    public WebbingGraph graph;

    public void BuildFrom(IEnumerable<PersonData> people,
                           IEnumerable<(string fromId, string toId)> relationships)
    {
        var nodesById = new Dictionary<string, WebbingNode>();
        foreach (var person in people)
            nodesById[person.id] = graph.AddNode(person);

        foreach (var (fromId, toId) in relationships)
        {
            var fromNode = nodesById[fromId];
            var toNode   = nodesById[toId];
            // Connect the nodes' first pin directly, bypassing drag input.
            graph.Connect(fromNode.GetPin(0), toNode.GetPin(0));
        }
    }
}
```

Disable dragging entirely for a read-only diagram by removing
`WebbingPin`'s drag handlers from the node prefab, or simply by not giving
the player a way to select/drag pins in your UI (for example, setting
`raycastTarget = false` on each pin's `Image`).

---

## 7. Tips: Giving Pins a Role

`WebbingPin` itself has no concept of "input" vs. "output" — any pin can
start or receive a connection. For patterns where direction or role
matters (skill tree prerequisites, dialogue choices, crafting slots), two
common approaches:

**A. Sibling index convention**

Give each node exactly the pins it needs, in a fixed order, and read them
by index:

```csharp
WebbingPin inputPin  = node.GetPin(0);
WebbingPin outputPin = node.GetPin(1);
```

This works well for nodes with a small, fixed number of pins (e.g. exactly
one input and one output).

**B. A small role component**

For more flexible layouts, add a tiny marker component to each pin prefab
variant:

```csharp
public enum PinRole { Input, Output }

public class PinRoleTag : MonoBehaviour
{
    public PinRole role;
}
```

Then check it inside your validator:

```csharp
private bool ValidateDirection(WebbingPin from, WebbingPin to)
{
    var fromRole = from.GetComponent<PinRoleTag>()?.role;
    var toRole   = to.GetComponent<PinRoleTag>()?.role;
    return fromRole == PinRole.Output && toRole == PinRole.Input;
}
```

Combine this with your pattern-specific rules (as in the Skill Tree and
Dialogue examples above) inside a single `ConnectionValidator` delegate.
