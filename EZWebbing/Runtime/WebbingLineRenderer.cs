// WebbingLineRenderer.cs
// Lightweight component that draws a straight line between two points on a Canvas.
// Inherits from Graphic and builds a quad strip in OnPopulateMesh.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EZWebbing
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class WebbingLineRenderer : Graphic
    {
        [Header("Start / End Points (local coordinates)")]
        public Vector2 startPoint;
        public Vector2 endPoint;

        [Header("Line Width")]
        public float lineWidth = 3f;

        [Header("Segment Count (1 is sufficient for a straight line)")]
        public int segments = 1;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (segments < 1) segments = 1;

            var points = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points.Add(Vector2.Lerp(startPoint, endPoint, t));
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p0   = points[i];
                Vector2 p1   = points[i + 1];
                Vector2 dir  = (p1 - p0).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x) * (lineWidth * 0.5f);

                int baseIdx = i * 4;
                vh.AddVert(p0 - perp, color, Vector2.zero);
                vh.AddVert(p0 + perp, color, Vector2.zero);
                vh.AddVert(p1 + perp, color, Vector2.zero);
                vh.AddVert(p1 - perp, color, Vector2.zero);
                vh.AddTriangle(baseIdx, baseIdx + 1, baseIdx + 2);
                vh.AddTriangle(baseIdx, baseIdx + 2, baseIdx + 3);
            }
        }

        public void SetPoints(Vector2 start, Vector2 end)
        {
            startPoint = start;
            endPoint   = end;
            SetVerticesDirty();
        }

        public void SetLineColor(Color c)
        {
            color = c;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false; // The line itself should not block drops onto pins.
        }
    }
}
