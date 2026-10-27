// WebbingGraphEditor.cs
// Custom inspector for WebbingGraph.
// - Warns about common setup mistakes (missing references, missing components).
// - While playing, shows live node/connection counts and provides buttons to
//   print the serialized graph and to clear all connections.

using UnityEditor;
using UnityEngine;

namespace EZWebbing.EditorTools
{
    [CustomEditor(typeof(WebbingGraph))]
    public class WebbingGraphEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var g = (WebbingGraph)target;

            EditorGUILayout.Space(6);

            // ---- Setup validation ----
            if (g.graphContainer == null)
                EditorGUILayout.HelpBox("graphContainer is not assigned.", MessageType.Warning);

            if (g.nodePrefab == null)
                EditorGUILayout.HelpBox("nodePrefab is not assigned.", MessageType.Warning);
            else if (g.nodePrefab.GetComponent<WebbingNode>() == null)
                EditorGUILayout.HelpBox("nodePrefab does not have a WebbingNode component.", MessageType.Error);

            if (g.connectionPrefab == null)
                EditorGUILayout.HelpBox("connectionPrefab is not assigned.", MessageType.Warning);
            else if (g.connectionPrefab.GetComponent<WebbingConnection>() == null)
                EditorGUILayout.HelpBox("connectionPrefab does not have a WebbingConnection component.", MessageType.Error);

            // ---- Runtime tools ----
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Runtime Tools", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (Application.isPlaying)
                    EditorGUILayout.LabelField($"Nodes: {g.Nodes.Count}    Connections: {g.Connections.Count}");

                if (GUILayout.Button("Serialize to Console"))
                    Debug.Log("[EZWebbing] " + g.Serialize());

                if (GUILayout.Button("Clear Connections"))
                    g.ClearConnections();
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Runtime tools are only available in Play Mode.", MessageType.None);
        }
    }
}
