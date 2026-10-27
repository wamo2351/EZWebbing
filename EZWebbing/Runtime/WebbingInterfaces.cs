// WebbingInterfaces.cs
// Abstractions that keep the EZWebbing core completely independent of what
// a node's content actually represents.

namespace EZWebbing
{
    /// <summary>
    /// The minimum contract for data held by a node.
    /// Whether implemented by a ScriptableObject or a plain class, any type
    /// implementing this interface can be fed into WebbingGraph.
    /// </summary>
    public interface IWebbingNodeData
    {
        /// <summary>Unique id used for save/load, duplicate checks, and lookups.</summary>
        string NodeId { get; }
    }

    /// <summary>
    /// Implemented on the node prefab to define how a node's data is rendered.
    /// WebbingNode.Initialize() calls Present() automatically.
    /// Different presenters can implement entirely different layouts (memo,
    /// photo, card, etc.) for the same core node behavior.
    /// </summary>
    public interface IWebbingNodePresenter
    {
        void Present(IWebbingNodeData data);
    }
}
