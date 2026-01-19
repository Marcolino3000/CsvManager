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

            // Second pass: connect nodes using ConnectedNoteIDs child elements
            foreach (var noteElem in notes)
            {
                var id = noteElem.Attribute("ID")?.Value?.Trim();
                if (string.IsNullOrEmpty(id) || !noteIdToNode.ContainsKey(id))
                    continue;
                var node = noteIdToNode[id];
                var connectedElems = noteElem.Elements("PointsToNoteIDs");

                foreach (var connectedElem in connectedElems)
                {
                    var value = connectedElem.Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        var targets = value.Split(',');
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


            // for (int i = 0; i < dialogTree.StartNodes.Count; i++)
            // {
            //     var rootNode = dialogTree.StartNodes[i];
            //     NodePositionHandler.AssignPositions(rootNode, rootStartX + i * rootSpacing, rootStartY);
            // }

            // Set StartNodes (roots: not referenced as children)
            foreach (var noteElem in notes)
            {
                var id = noteElem.Attribute("ID")?.Value;
                if (!referenced.Contains(id))
                {
                    if (noteIdToNode[id] is DialogOptionNode rootNode)
                    {
                        if(rootNode.Children.Count > 0)
                            dialogTree.StartNodes.Add(rootNode);
                    }
                }
            }

            if (dialogTree.StartNodes.Count > 1)
            {
                Debug.LogWarning(dialogTree.name + "does have multiple start nodes.");
            }
            
            NodePositionHandler.AssignPositionsTreeLikeNew(dialogTree);
            // --- Save original positions to JSON in the same directory as the scap file ---
            Debug.Log($"[ScappleImporter] notes count: {notes.Count}");
            var originalPositions = new Dictionary<string, Vector2>();
            foreach (var noteElem in notes)
            {
                var id = noteElem.Attribute("ID")?.Value;
                var posAttr = noteElem.Attribute("Position")?.Value;
                Debug.Log($"[ScappleImporter] Note ID: {id}, Position: {posAttr}");
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(posAttr))
                {
                    var parts = posAttr.Split(',');
                    if (parts.Length == 2 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y))
                    {
                        originalPositions[id] = new Vector2(x, y);
                    }
                }
            }
            Debug.Log($"[ScappleImporter] originalPositions count: {originalPositions.Count}");
            SaveOriginalNodePositionsInScapDirectory(originalPositions, scapPath);

            EditorUtility.SetDirty(dialogTree);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return dialogTree;
        }

        /// <summary>
        /// Positions nodes based on their place in the hierarchy.
        /// Each node is placed 30px below its parent, and siblings are 100px apart horizontally at the same height.
        /// </summary>
        /// <param name="rootIds">IDs of root nodes</param>
        /// <param name="parentToChildren">Parent-to-children mapping</param>
        /// <param name="noteIdToNode">Node dictionary</param>
        public static void PositionNodesByHierarchy(List<string> rootIds, Dictionary<string, List<string>> parentToChildren, Dictionary<string, Node> noteIdToNode)
        {
            float startX = 0f;
            float startY = 0f;
            float verticalSpacing = 30f;
            float horizontalSpacing = 100f;

            // First pass: calculate subtree widths
            Dictionary<string, int> subtreeWidths = new Dictionary<string, int>();
            int CalculateSubtreeWidth(string nodeId)
            {
                if (!parentToChildren.ContainsKey(nodeId) || parentToChildren[nodeId].Count == 0)
                {
                    subtreeWidths[nodeId] = 1;
                    return 1;
                }
                int width = 0;
                foreach (var childId in parentToChildren[nodeId])
                {
                    width += CalculateSubtreeWidth(childId);
                }
                subtreeWidths[nodeId] = width;
                return width;
            }
            foreach (var rootId in rootIds)
                CalculateSubtreeWidth(rootId);

            // Second pass: position nodes using subtree widths
            void PositionSubtree(string nodeId, float x, float y)
            {
                if (!noteIdToNode.ContainsKey(nodeId) || noteIdToNode[nodeId] == null)
                    return;
                var node = noteIdToNode[nodeId];
                node.Position = new Vector2(x, y);
                if (parentToChildren.TryGetValue(nodeId, out var children) && children.Count > 0)
                {
                    float totalWidth = subtreeWidths[nodeId];
                    float childX = x - ((totalWidth - 1) * horizontalSpacing) / 2f;
                    foreach (var childId in children)
                    {
                        float childWidth = subtreeWidths[childId];
                        float childCenterX = childX + ((childWidth - 1) * horizontalSpacing) / 2f;
                        PositionSubtree(childId, childCenterX, y - verticalSpacing);
                        childX += childWidth * horizontalSpacing;
                    }
                }
            }
            float nextRootX = startX;
            foreach (var rootId in rootIds)
            {
                int width = subtreeWidths[rootId];
                float rootCenterX = nextRootX + ((width - 1) * horizontalSpacing) / 2f;
                PositionSubtree(rootId, rootCenterX, startY);
                nextRootX += width * horizontalSpacing;
            }
        }

        /// <summary>
        /// Saves the original positions of nodes (from Scapple import) to a JSON file for later recall.
        /// </summary>
        public static void SaveOriginalNodePositions(Dictionary<string, Vector2> idToPosition, string filePath)
        {
            UnityEngine.Debug.Log($"[ScappleImporter] SaveOriginalNodePositions: idToPosition count = {idToPosition?.Count ?? -1}");
            if (idToPosition != null && idToPosition.Count > 0)
            {
                foreach (var kvp in idToPosition)
                {
                    UnityEngine.Debug.Log($"[ScappleImporter] id: {kvp.Key}, pos: {kvp.Value}");
                    break; // Only log the first entry for brevity
                }
            }
            var dict = new Dictionary<string, float[]>();
            foreach (var kvp in idToPosition)
            {
                dict[kvp.Key] = new float[] { kvp.Value.x, kvp.Value.y };
            }
            string json = UnityEngine.JsonUtility.ToJson(new SerializationWrapper(dict), true);
            UnityEngine.Debug.Log($"[ScappleImporter] JSON output: {json}");
            System.IO.File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Saves the original positions of nodes (from Scapple import) to a JSON file in the same directory as the scap file.
        /// </summary>
        public static void SaveOriginalNodePositionsInScapDirectory(Dictionary<string, Vector2> idToPosition, string scapPath)
        {
            UnityEngine.Debug.Log($"[ScappleImporter] SaveOriginalNodePositionsInScapDirectory: idToPosition count = {idToPosition?.Count ?? -1}");
            if (idToPosition != null && idToPosition.Count > 0)
            {
                foreach (var kvp in idToPosition)
                {
                    UnityEngine.Debug.Log($"[ScappleImporter] id: {kvp.Key}, pos: {kvp.Value}");
                    break; // Only log the first entry for brevity
                }
            }
            var dict = new Dictionary<string, float[]>();
            foreach (var kvp in idToPosition)
            {
                dict[kvp.Key] = new float[] { kvp.Value.x, kvp.Value.y };
            }
            string json = UnityEngine.JsonUtility.ToJson(new SerializationWrapper(dict), true);
            UnityEngine.Debug.Log($"[ScappleImporter] JSON output: {json}");
            string directory = Path.GetDirectoryName(scapPath);
            string scapFileName = Path.GetFileNameWithoutExtension(scapPath);
            string outFile = Path.Combine(directory, scapFileName + "_original_positions.json");
            File.WriteAllText(outFile, json);
        }

        [System.Serializable]
        private class PositionEntry
        {
            public string id;
            public float[] position;
            public PositionEntry(string id, float[] position)
            {
                this.id = id;
                this.position = position;
            }
        }

        [System.Serializable]
        private class SerializationWrapper
        {
            public List<PositionEntry> positions;
            public SerializationWrapper(Dictionary<string, float[]> dict)
            {
                positions = new List<PositionEntry>();
                foreach (var kvp in dict)
                {
                    positions.Add(new PositionEntry(kvp.Key, kvp.Value));
                }
            }
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