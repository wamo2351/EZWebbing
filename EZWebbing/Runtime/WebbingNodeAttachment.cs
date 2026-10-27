// WebbingNodeAttachment.cs
// Generic base class for UI that follows a node (hints, labels, badges, etc.).
//
// Usage:
//   - Instantiate directly under graphContainer.
//   - Call Attach(node, offset) to make it follow the node and keep its
//     sibling order (Z-order) one step above the owning node.
//   - Any game-specific display conditions should be implemented by
//     overriding Update() in a subclass.

using UnityEngine;

namespace EZWebbing
{
    [RequireComponent(typeof(RectTransform))]
    public class WebbingNodeAttachment : MonoBehaviour
    {
        [Tooltip("The node this attachment follows.")]
        public WebbingNode owner;

        [Tooltip("Local-space offset from the node's anchoredPosition.")]
        public Vector2 offset = new Vector2(80f, 60f);

        protected RectTransform rt;

        protected virtual void Awake() => rt = GetComponent<RectTransform>();

        /// <summary>Attaches this UI to a node and registers itself with that node.</summary>
        public virtual void Attach(WebbingNode node, Vector2 offset)
        {
            owner       = node;
            this.offset = offset;
            owner?.RegisterAttachment(this);
        }

        protected virtual void LateUpdate()
        {
            if (owner == null) return;
            rt.anchoredPosition = owner.RectTransform.anchoredPosition + offset;
            MaintainSiblingOrder();
        }

        /// <summary>Keeps this object directly above its owning node in sibling order (index + 1).</summary>
        protected void MaintainSiblingOrder()
        {
            if (owner == null) return;
            int ownerIndex = owner.transform.GetSiblingIndex();
            if (transform.GetSiblingIndex() != ownerIndex + 1)
                transform.SetSiblingIndex(ownerIndex + 1);
        }
    }
}
