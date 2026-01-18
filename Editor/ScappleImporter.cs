using System.IO;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEditor;
using Tree;
using Nodes;
using Nodes.Decorator;
using UnityEngine;

namespace Editor
{
    public class ScappleImporter
    {
        public static DialogTree ImportScapToDialogTree(string scapPath, string assetPath)
        {
            string[] characterNames = {"HILDE", "PAUL", "KIM", "GOTTLOB"};
            // Load and parse the .scap XML
            var doc = XDocument.Load(scapPath);
            var notes = new List<XElement>();
            var noteIdToNode = new Dictionary<string, Node>();
            var referenced = new HashSet<string>();

            // Find Notes
            var notesElem = doc.Root.Element("Notes");
            if (notesElem == null)
                throw new System.Exception("No <Notes> element found in .scap file");
            foreach (var noteElem in notesElem.Elements("Note"))
            {
                var id = noteElem.Attribute("ID")?.Value;
                if (id != null)
                {
                    notes.Add(noteElem);
                    noteIdToNode[id] = null; // Initialize with null
                }
            }

            // Create DialogTree asset
            var dialogTree = ScriptableObject.CreateInstance<DialogTree>();
            dialogTree.nodes = new List<Node>();
            dialogTree.StartNodes = new List<DialogOptionNode>();
            AssetDatabase.CreateAsset(dialogTree, assetPath);

            // --- Build hierarchy: parent/child relationships ---
            var childToParents = new Dictionary<string, List<string>>();
            var parentToChildren = new Dictionary<string, List<string>>();
            foreach (var noteElem in notes)
            {
                var id = noteElem.Attribute("ID")?.Value;
                var pointsToAttr = noteElem.Attribute("PointsToNoteIDs")?.Value;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(pointsToAttr))
                {
                    var targets = pointsToAttr.Split(',');
                    foreach (var targetIdRaw in targets)
                    {
                        var targetId = targetIdRaw.Trim();
                        if (string.IsNullOrEmpty(targetId) || targetId == id)
                            continue;
                        if (!parentToChildren.ContainsKey(id)) parentToChildren[id] = new List<string>();
                        parentToChildren[id].Add(targetId);
                        if (!childToParents.ContainsKey(targetId)) childToParents[targetId] = new List<string>();
                        childToParents[targetId].Add(id);
                    }
                }
            }
            // --- Identify root nodes (no parents) ---
            var rootIds = new List<string>();
            foreach (var noteElem in notes)
            {
                var id = noteElem.Attribute("ID")?.Value;
                if (!string.IsNullOrEmpty(id) && !childToParents.ContainsKey(id))
                    rootIds.Add(id);
            }
            // --- Assign depth (level) to each node using BFS ---
            var nodeDepth = new Dictionary<string, int>();
            var levelToNodes = new Dictionary<int, List<string>>();
            var queue = new Queue<(string, int)>();
            foreach (var rootId in rootIds)
            {
                queue.Enqueue((rootId, 0));
            }
            while (queue.Count > 0)
            {
                var (id, depth) = queue.Dequeue();
                if (nodeDepth.ContainsKey(id)) continue;
                nodeDepth[id] = depth;
                if (!levelToNodes.ContainsKey(depth)) levelToNodes[depth] = new List<string>();
                levelToNodes[depth].Add(id);
                if (parentToChildren.TryGetValue(id, out var children))
                {
                    foreach (var child in children)
                        queue.Enqueue((child, depth + 1));
                }
            }
            // --- Assign X/Y positions based on hierarchy ---
            var nodePositions = new Dictionary<string, Vector2>();
            float verticalSpacing = 100f;
            float horizontalSpacing = 150f;
            foreach (var kvp in levelToNodes)
            {
                int level = kvp.Key;
                var idsAtLevel = kvp.Value;
                for (int i = 0; i < idsAtLevel.Count; i++)
                {
                    float x = i * horizontalSpacing;
                    float y = -level * verticalSpacing;
                    nodePositions[idsAtLevel[i]] = new Vector2(x, y);
                }
            }
            // --- First pass: create all nodes using Scapple file positions ---
            foreach (var noteElem in notes)
            {
                var id = noteElem.Attribute("ID")?.Value;
                var posAttr = noteElem.Attribute("Position")?.Value;
                var stringElem = noteElem.Element("String");
                var text = stringElem?.Value ?? "";
                var lines = text.Split('\n');
                string dialogLine = lines.Length > 1 ? string.Join("\n", lines, 1, lines.Length - 1) : text;
                string firstLine = lines.Length > 0 ? lines[0].Trim() : "";
                // Determine node type: NpcDialogOption if first line contains any character name, otherwise PlayerDialogOption
                Node node;
                bool isNpc = false;
                foreach (var name in characterNames)
                {
                    if (!string.IsNullOrEmpty(firstLine) && firstLine.IndexOf(name, System.StringComparison.CurrentCulture) >= 0)
                    {
                        isNpc = true;
                        break;
                    }
                }
                if (isNpc)
                {
                    node = dialogTree.CreateNode(typeof(NpcDialogOption));
                }
                else
                {
                    node = dialogTree.CreateNode(typeof(PlayerDialogOption));
                }
                node.DialogLine = dialogLine.Trim();
                // Set position from Scapple file
                float x = 0, y = 0;
                if (!string.IsNullOrEmpty(posAttr))
                {
                    var parts = posAttr.Split(',');
                    if (parts.Length == 2 && float.TryParse(parts[0], out x) && float.TryParse(parts[1], out y))
                    {
                        node.Position = new UnityEngine.Vector2(x, y);
                    }
                    else
                    {
                        node.Position = Vector2.zero;
                    }
                }
                else
                {
                    node.Position = Vector2.zero;
                }
                // Ensure Children is empty after creation
                if (node is CompositeNode compositeNode)
                {
                    compositeNode.Children.Clear();
                    Debug.Log($"[ScappleImporter] Node {id} (instanceID={node.GetInstanceID()}) Children count after clear: {compositeNode.Children.Count}");
                }

                noteIdToNode[id] = node;
            }

            // --- Clear all Children lists before connecting ---
            foreach (var node in noteIdToNode.Values)
            {
                if (node is CompositeNode compositeNode)
                    compositeNode.Children.Clear();
            }

            // Second pass: connect nodes using PointsToNoteIDs
            foreach (var noteElem in notes)
            {
                var id = noteElem.Attribute("ID")?.Value?.Trim();
                if (string.IsNullOrEmpty(id) || !noteIdToNode.ContainsKey(id))
                    continue;
                var node = noteIdToNode[id];
                var connectedElem = noteElem.Element("PointsToNoteIDs");
                if (connectedElem != null)
                {
                    var targets = connectedElem.Value.Split(',');
                    foreach (var targetIdRaw in targets)
                    {
                        var targetId = targetIdRaw.Trim();
                        if (string.IsNullOrEmpty(targetId) || targetId == id)
                            continue; // skip empty or self-connection
                        if (noteIdToNode.TryGetValue(targetId, out var childNode))
                        {
                            if (node is DialogOptionNode dialogOptionNode && childNode is DialogOptionNode childDialogOption)
                            {
                                if (!dialogOptionNode.Children.Contains(childDialogOption))
                                {
                                    dialogOptionNode.Children.Add(childDialogOption);
                                    referenced.Add(targetId);
                                    Debug.Log($"Connecting node {id} -> {targetId}");
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(targetId))
                        {
                            Debug.LogWarning($"ScappleImporter: Target node ID '{targetId}' not found for connection from '{id}'.");
                        }
                    }
                }
            }
            // --- Diagnostic: Print Children for every node after connecting ---
            foreach (var kvp in noteIdToNode)
            {
                var node = kvp.Value;
                if (node is DialogOptionNode dialogOptionNode)
                {
                    var childIds = string.Join(", ", dialogOptionNode.Children.ConvertAll(c => c.GetInstanceID().ToString()));
                    Debug.Log($"[ScappleImporter] Node {kvp.Key} (instanceID={node.GetInstanceID()}) has children: [{childIds}]");
                }
            }

            // Set StartNodes (roots: not referenced as children)
            foreach (var noteElem in notes)
            {
                var id = noteElem.Attribute("ID")?.Value;
                if (!referenced.Contains(id))
                {
                    if (noteIdToNode[id] is DialogOptionNode rootNode)
                        dialogTree.StartNodes.Add(rootNode);
                }
            }

            EditorUtility.SetDirty(dialogTree);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return dialogTree;
        }

        [MenuItem("Assets/DialogBuilder/Import .scap File as DialogTree", true)]
        private static bool ValidateImportScap()
        {
            var obj = Selection.activeObject;
            if (obj == null) return false;
            var path = AssetDatabase.GetAssetPath(obj);
            return path != null && path.EndsWith(".scap");
        }

        [MenuItem("Assets/DialogBuilder/Import .scap File as DialogTree", false, 104)]
        private static void ImportScapMenu()
        {
            var obj = Selection.activeObject;
            if (obj == null) return;
            var scapPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(scapPath) || !scapPath.EndsWith(".scap")) return;

            string assetPath = scapPath.Substring(0, scapPath.LastIndexOf('.')) + ".asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            var dialogTree = ImportScapToDialogTree(scapPath, assetPath);
            if (dialogTree != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = dialogTree;
                Debug.Log($"Imported DialogTree from {scapPath} to {assetPath}");
            }
            else
            {
                Debug.LogError("Failed to import DialogTree from .scap file.");
            }
        }
    }
}