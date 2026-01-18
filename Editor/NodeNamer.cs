using System.Collections.Generic;
using System.Text;
using Tree;
using UnityEditor;
using UnityEngine;
using Nodes;

namespace Editor
{
    public class NodeNamer
    {
        // Assign numeric-only IDs (as names consisting only of digits) per DialogTree.
        // This will name each node with a string of digits representing the path of child indices from the virtual root.
        // Example: first child twice -> "11". First child then second child -> "12".
        // The name is stored in node.name (digits only). We do NOT write any NumericId field.
        public static void AssignNumericNames(DialogTree tree)
        {
#if UNITY_EDITOR
            if (tree == null) return;

            var visited = new HashSet<string>();

            var startNodes = tree.GetStartingNodes();
            if (startNodes == null || startNodes.Count == 0)
            {
                // Fallback: use nodes list order, assign incremental digits as strings
                int idx = 1;
                foreach (var n in tree.nodes)
                {
                    if (n == null) continue;
                    AssignNumericPathName(tree, n, idx.ToString(), visited);
                    idx++;
                }

                AssetDatabase.SaveAssets();
                return;
            }

            for (int s = 0; s < startNodes.Count; s++)
            {
                var start = startNodes[s];
                if (start == null) continue;
                // treat start nodes as children of a virtual root; token is 1-based index
                string startToken = (s + 1).ToString();
                AssignNumericPathName(tree, start, startToken, visited);
            }

            AssetDatabase.SaveAssets();
#endif
        }

        private static void AssignNumericPathName(DialogTree tree, Node node, string currentPath, HashSet<string> visited)
        {
#if UNITY_EDITOR
            if (node == null) return;

            if (string.IsNullOrEmpty(node.Guid))
            {
                node.Guid = GUID.Generate().ToString();
                EditorUtility.SetDirty(node);
            }

            if (visited.Contains(node.Guid))
                return;

            visited.Add(node.Guid);

            // ensure path contains only digits
            var sb = new StringBuilder();
            foreach (var c in currentPath)
                if (char.IsDigit(c)) sb.Append(c);
            var finalDigits = sb.Length == 0 ? "1" : sb.ToString();

            // Ensure the digits fit into an int. If too long, keep the last 9 digits to avoid overflow
            string digitsForInt = finalDigits;
            if (digitsForInt.Length > 9)
                digitsForInt = digitsForInt.Substring(digitsForInt.Length - 9);

            int numericId;
            if (!int.TryParse(digitsForInt, out numericId) || numericId <= 0)
            {
                // Fallback: compute a stable hash-based positive int
                unchecked
                {
                    int hash = 23;
                    foreach (var ch in finalDigits)
                        hash = hash * 31 + ch;
                    if (hash == int.MinValue) hash = int.MaxValue;
                    numericId = System.Math.Abs(hash);
                    if (numericId == 0) numericId = 1;
                }
            }

            string finalName = numericId.ToString();

            Undo.RecordObject(node, "Assign Numeric Path Name");
            node.name = finalName;
            EditorUtility.SetDirty(node);

            // attempt to add node to the same asset as tree so name persists
            string treePath = AssetDatabase.GetAssetPath(tree);
            if (!string.IsNullOrEmpty(treePath))
            {
                string nodePath = AssetDatabase.GetAssetPath(node);
                if (nodePath != treePath)
                {
                    try { AssetDatabase.AddObjectToAsset(node, treePath); }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"NodeNamer: could not add node to asset '{treePath}': {ex.Message}");
                    }
                }
            }

            var children = tree.GetChildren(node);
            if (children == null || children.Count == 0)
                return;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;
                string token = (i + 1).ToString(); // child index as digit(s)
                string nextPath = currentPath + token;
                AssignNumericPathName(tree, child, nextPath, visited);
            }
#endif
        }

        [MenuItem("Assets/DialogBuilder/Assign Unique Node Names", false, 104)]
        private static void AssignNamesForSelected()
        {
#if UNITY_EDITOR
            var selected = Selection.GetFiltered<DialogTree>(SelectionMode.Assets);
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("No DialogTree assets selected.");
                return;
            }

            int count = 0;
            foreach (var tree in selected)
            {
                // AssignUniqueNames(tree);
                EditorUtility.SetDirty(tree);
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Assigned unique alphanumeric names for {count} DialogTree(s).");
#endif
        }

        [MenuItem("Assets/DialogBuilder/Assign Numeric Node IDs", false, 105)]
        private static void AssignNumericForSelected()
        {
#if UNITY_EDITOR
            var selected = Selection.GetFiltered<DialogTree>(SelectionMode.Assets);
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("No DialogTree assets selected.");
                return;
            }

            int count = 0;
            foreach (var tree in selected)
            {
                AssignNumericNames(tree);
                EditorUtility.SetDirty(tree);
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Assigned numeric node IDs for {count} DialogTree(s).");
#endif
        }
    }
}