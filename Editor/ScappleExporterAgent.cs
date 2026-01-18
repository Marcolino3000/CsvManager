using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using Nodes.Decorator;
using Tree;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class ScappleExporterAgent
    {
        [MenuItem("Assets/DialogBuilder/Export Selected DialogTrees as .scap-File", false, 103)]
        private static void ExportSelectedDialogTrees()
        {
            // We support selecting DialogTree assets directly, or selecting folders (DefaultAsset) to export all DialogTrees inside.
            var guidsList = new List<string>();

            // Selected DialogTree assets
            var selectedTrees = Selection.GetFiltered<DialogTree>(SelectionMode.Assets);
            if (selectedTrees != null && selectedTrees.Length > 0)
            {
                foreach (var t in selectedTrees)
                {
                    string path = AssetDatabase.GetAssetPath(t);
                    if (!string.IsNullOrEmpty(path))
                    {
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (!string.IsNullOrEmpty(guid) && !guidsList.Contains(guid))
                            guidsList.Add(guid);
                    }
                }
            }

            // Selected folders (DefaultAsset) - find DialogTree assets inside
            var selectedDefaults = Selection.GetFiltered<DefaultAsset>(SelectionMode.Assets);
            if (selectedDefaults != null && selectedDefaults.Length > 0)
            {
                foreach (var def in selectedDefaults)
                {
                    string folderPath = AssetDatabase.GetAssetPath(def);
                    if (AssetDatabase.IsValidFolder(folderPath))
                    {
                        string[] found = AssetDatabase.FindAssets("t:DialogTree", new[] { folderPath });
                        foreach (var g in found)
                        {
                            if (!guidsList.Contains(g))
                                guidsList.Add(g);
                        }
                    }
                }
            }

            if (guidsList.Count == 0)
            {
                Debug.LogWarning("No DialogTree assets selected or found in selected folders.");
                return;
            }

            string defaultName = "dialog_export";
            if (selectedTrees != null && selectedTrees.Length == 1)
                defaultName = selectedTrees[0].name;
            else if (selectedDefaults != null && selectedDefaults.Length == 1)
                defaultName = System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(selectedDefaults[0]));

            string savePath = EditorUtility.SaveFilePanel(
                "Save Scapple File",
                Application.dataPath,
                defaultName,
                "scap"
            );

            if (string.IsNullOrEmpty(savePath))
                return;

            try
            {
                // Gather all DialogTrees to generate names
                var dialogTrees = new List<DialogTree>();
                foreach (var guid in guidsList)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var dialogTree = AssetDatabase.LoadAssetAtPath<DialogTree>(path);
                    if (dialogTree != null)
                        dialogTrees.Add(dialogTree);
                }
                var nodeLetterNames = GenerateLetterNames(dialogTrees);

                XDocument doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", "no"),
                    new XElement("ScappleDocument",
                        new XAttribute("Version", "1.3"),
                        new XAttribute("ID", System.Guid.NewGuid().ToString().ToUpper())
                    )
                );

                XElement notesElement = new XElement("Notes");
                // XElement connectionsElement = new XElement("Connections");

                int noteId = 1;
                var nodeIdMap = new Dictionary<string, int>();
                var noteElementMap = new Dictionary<string, XElement>();
                var noteConnections = new Dictionary<string, List<int>>(); // Map node key to list of connected note IDs
                int xPos = 100;
                int yPos = 100;

                // Create notes
                foreach (var guid in guidsList)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var dialogTree = AssetDatabase.LoadAssetAtPath<DialogTree>(path);
                    if (dialogTree == null || dialogTree.nodes == null)
                        continue;

                    foreach (var node in dialogTree.nodes)
                    {
                        string treeKey = $"{dialogTree.name}_{node.Guid}";
                        string rawKey = node.Guid;
                        nodeIdMap[treeKey] = noteId;
                        if (!nodeIdMap.ContainsKey(rawKey))
                            nodeIdMap[rawKey] = noteId;

                        // Prepare connection list for this node
                        noteConnections[treeKey] = new List<int>();
                        noteConnections[rawKey] = noteConnections[treeKey];

                        // Use node.Position for Scapple position, fallback to grid if (0,0)
                        float posX = node.Position.x;
                        float posY = node.Position.y;
                        if (Mathf.Approximately(posX, 0f) && Mathf.Approximately(posY, 0f))
                        {
                            posX = xPos;
                            posY = yPos;
                        }

                        // Build Appearance element (Scapple default yellow style)
                        string fillColor;
                        // Beige for PlayerDialogOption, desaturated green for others
                        if (node is PlayerDialogOption)
                            fillColor = "0.96 0.93 0.80"; // beige
                        else
                            fillColor = "0.75 0.85 0.75"; // desaturated green
                        var appearance = new XElement("Appearance",
                            new XElement("Alignment", "Center"),
                            new XElement("Border", new XAttribute("Weight", "1"), "0.9024707674980164 0.8555303812026978 0.628868043422699"),
                            new XElement("Fill", fillColor)
                        );

                        // Build Note element in correct order
                        // Prefix node text with generated letter name, then a newline, then dialog text
                        string nodeLetterName = nodeLetterNames.TryGetValue(node, out var n) ? n : "?";
                        string dialogText = node.DialogLine ?? "";
                        string noteText = nodeLetterName + "\n" + dialogText;
                        var note = new XElement("Note",
                            new XAttribute("ID", noteId),
                            new XAttribute("FontSize", "12.0"),
                            new XAttribute("Position", $"{posX:0.0},{posY:0.0}"),
                            new XAttribute("Width", "200.0"),
                            appearance,
                            new XElement("String", noteText)
                        );

                        notesElement.Add(note);
                        noteElementMap[treeKey] = note;
                        noteElementMap[rawKey] = note;
                        noteId++;

                        xPos += 250;
                        if (xPos > 1000)
                        {
                            xPos = 100;
                            yPos += 150;
                        }
                    }
                }

                // Collect connections for each note
                foreach (var guid in guidsList)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var dialogTree = AssetDatabase.LoadAssetAtPath<DialogTree>(path);
                    if (dialogTree == null || dialogTree.nodes == null)
                        continue;

                    foreach (var node in dialogTree.nodes)
                    {
                        string treeNodeKey = $"{dialogTree.name}_{node.Guid}";
                        string rawNodeKey = node.Guid;
                        var children = node.GetChildNodes();
                        if (children == null)
                            continue;

                        foreach (var child in children)
                        {
                            if (child == null)
                                continue;
                            string childNodeGuid = child.Guid;
                            int destId;
                            string treeChildKey = $"{dialogTree.name}_{childNodeGuid}";
                            bool hasDest = nodeIdMap.TryGetValue(treeChildKey, out destId) || nodeIdMap.TryGetValue(childNodeGuid, out destId);
                            if (hasDest)
                            {
                                // Add to this node's connection list
                                if (noteConnections.ContainsKey(treeNodeKey))
                                    noteConnections[treeNodeKey].Add(destId);
                                else if (noteConnections.ContainsKey(rawNodeKey))
                                    noteConnections[rawNodeKey].Add(destId);
                            }
                        }
                    }
                }

                // Add ConnectedNoteIDs element to each note if needed
                foreach (var pair in noteConnections)
                {
                    var noteElem = noteElementMap[pair.Key];
                    var ids = pair.Value.Distinct().ToList();
                    if (ids.Count > 0)
                    {
                        noteElem.Add(new XElement("ConnectedNoteIDs", string.Join(",", ids)));
                    }
                }

                // Add required root elements after Notes
                var backgroundShapes = new XElement("BackgroundShapes");
                var noteStyles = new XElement("NoteStyles");
                var uiSettings = new XElement("UISettings",
                    new XElement("BackgroundColor", "1.0 0.99 0.96"),
                    new XElement("DefaultFont", "Helvetica"),
                    new XElement("NoteXPadding", "8.0")
                );
                var printSettings = new XElement("PrintSettings",
                    new XAttribute("PaperSize", "595.0,842.0"),
                    new XAttribute("LeftMargin", "72.0"),
                    new XAttribute("RightMargin", "72.0"),
                    new XAttribute("TopMargin", "90.0"),
                    new XAttribute("BottomMargin", "90.0"),
                    new XAttribute("PaperType", "iso-a4"),
                    new XAttribute("Orientation", "Portrait"),
                    new XAttribute("HorizontalPagination", "Clip"),
                    new XAttribute("VerticalPagination", "Auto"),
                    new XAttribute("ScaleFactor", "1.0"),
                    new XAttribute("HorizontallyCentered", "Yes"),
                    new XAttribute("VerticallyCentered", "Yes"),
                    new XAttribute("Collates", "Yes"),
                    new XAttribute("PagesAcross", "1"),
                    new XAttribute("PagesDown", "1")
                );

                if (doc.Root != null)
                {
                    doc.Root.Add(notesElement);
                    doc.Root.Add(backgroundShapes);
                    doc.Root.Add(noteStyles);
                    doc.Root.Add(uiSettings);
                    doc.Root.Add(printSettings);
                    doc.Save(savePath);
                    Debug.Log($"Exported {guidsList.Count} selected DialogTree(s) to: {savePath}");

                    // Reveal resulting file in Finder for convenience
                    EditorUtility.RevealInFinder(savePath);
                }
                else
                {
                    Debug.LogError("Failed to build Scapple document root.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error exporting Scapple file: {ex}");
            }
        }

        // Generates a unique letter-based hierarchical name for each node in the dialog tree(s), with the first root node named '0', and its children named 'a', 'b', ...
        private static Dictionary<Nodes.Node, string> GenerateLetterNames(List<DialogTree> trees)
        {
            var result = new Dictionary<Nodes.Node, string>();
            foreach (var tree in trees)
            {
                if (tree.nodes == null) continue;
                // Find root nodes (nodes not referenced as children)
                var allNodes = new HashSet<Nodes.Node>(tree.nodes);
                var referenced = new HashSet<Nodes.Node>();
                foreach (var node in tree.nodes)
                {
                    var children = node.GetChildNodes();
                    if (children != null)
                        foreach (var c in children)
                            if (c != null) referenced.Add(c);
                }
                var roots = allNodes.Except(referenced).ToList();
                if (roots.Count == 0) roots = tree.nodes.Take(1).ToList(); // fallback: first node
                for (int r = 0; r < roots.Count; r++)
                {
                    var root = roots[r];
                    string rootName = (r == 0) ? "0" : ToLetters(r - 1); // first root is '0', others a, b, ...
                    AssignLetterNamesRecursive(root, rootName, result, new HashSet<Nodes.Node>(), isZeroRoot: r == 0);
                }
            }
            return result;
        }

        // Helper: recursively assign letter names
        private static void AssignLetterNamesRecursive(Nodes.Node node, string prefix, Dictionary<Nodes.Node, string> map, HashSet<Nodes.Node> visited, bool isZeroRoot)
        {
            if (node == null || visited.Contains(node)) return;
            visited.Add(node);
            map[node] = prefix;
            var children = node.GetChildNodes();
            if (children == null) return;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;
                string childLetter = ToLetters(i);
                // If this is the first root ('0'), its children start with 'a', 'b', ... (not '0a', '0b', ...)
                string childPrefix = isZeroRoot ? childLetter : prefix + childLetter;
                AssignLetterNamesRecursive(child, childPrefix, map, visited, false);
            }
        }

        // Converts 0->A, 1->B, ..., 25->Z, 26->AA, 27->AB, ...
        private static string ToLetters(int index)
        {
            string s = "";
            index++;
            while (index > 0)
            {
                index--;
                s = (char)('A' + (index % 26)) + s;
                index /= 26;
            }
            return s;
        }
    }
}