using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Tree;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class ScappleExporter
    {
        [MenuItem("Assets/DialogBuilder/Export Dialog as .scap-File", false, 102)]
        private static void ExportDialogToScapple()
        {
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Select folder containing DialogTrees",
                "Assets",
                ""
            );

            if (string.IsNullOrEmpty(selectedPath))
                return;

            string relativePath = FileUtil.GetProjectRelativePath(selectedPath);

            if (string.IsNullOrEmpty(relativePath))
            {
                Debug.LogError("Selected folder is outside the Unity project!");
                return;
            }

            ExportFromFolder(relativePath);
        }

        private static void ExportFromFolder(string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:DialogTree", new[] { folderPath });

            if (guids.Length == 0)
            {
                Debug.LogWarning("No DialogTree assets found in selected folder.");
                return;
            }

            string savePath = EditorUtility.SaveFilePanel(
                "Save Scapple File",
                Application.dataPath,
                "dialog_export",
                "scap"
            );

            if (string.IsNullOrEmpty(savePath))
                return;

            GenerateScappleFile(guids, savePath);
            Debug.Log($"Exported {guids.Length} DialogTrees to: {savePath}");
        }

        private static void GenerateScappleFile(string[] dialogTreeGuids, string savePath)
        {
            XDocument doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "no"),
                new XElement("ScappleDocument",
                    new XAttribute("Version", "1.3"),
                    new XAttribute("ID", System.Guid.NewGuid().ToString().ToUpper())
                )
            );

            XElement notesElement = new XElement("Notes");
            XElement connectionsElement = new XElement("Connections");
            
            int noteId = 1;
            Dictionary<string, int> nodeIdMap = new Dictionary<string, int>();
            int xPos = 100;
            int yPos = 100;

            foreach (string guid in dialogTreeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                DialogTree dialogTree = AssetDatabase.LoadAssetAtPath<DialogTree>(path);

                if (dialogTree == null || dialogTree.nodes == null)
                    continue;

                foreach (var node in dialogTree.nodes)
                {
                    string nodeKey = $"{dialogTree.name}_{node.Guid}";
                    nodeIdMap[nodeKey] = noteId;

                    XElement note = new XElement("Note",
                        new XAttribute("ID", noteId),
                        new XAttribute("FontSize", "12.0"),
                        new XAttribute("Position", $"{xPos},{yPos}"),
                        new XAttribute("Width", "200.0"),
                        new XElement("String", node.DialogLine ?? ""),
                        new XElement("Appearance",
                            new XElement("Alignment", "Left"),
                            new XElement("TextColor", "0.0 0.0 0.0"),
                            new XElement("BackgroundColor", "1.0 1.0 0.8")
                        )
                    );

                    notesElement.Add(note);
                    noteId++;
                    
                    xPos += 250;
                    if (xPos > 1000)
                    {
                        xPos = 100;
                        yPos += 150;
                    }
                }
            }

            // Create connections based on node children
            foreach (string guid in dialogTreeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                DialogTree dialogTree = AssetDatabase.LoadAssetAtPath<DialogTree>(path);

                if (dialogTree == null || dialogTree.nodes == null)
                    continue;

                foreach (var node in dialogTree.nodes)
                {
                    string nodeKey = $"{dialogTree.name}_{node.Guid}";
                    
                    if (node.GetChildNodes() != null)
                    {
                        foreach (var childGuid in node.GetChildNodes())
                        {
                            string childKey = $"{dialogTree.name}_{childGuid}";
                            
                            if (nodeIdMap.ContainsKey(nodeKey) && nodeIdMap.ContainsKey(childKey))
                            {
                                XElement connection = new XElement("Connection",
                                    new XAttribute("SourceID", nodeIdMap[nodeKey]),
                                    new XAttribute("DestinationID", nodeIdMap[childKey]),
                                    new XAttribute("Type", "Arrow")
                                );
                                connectionsElement.Add(connection);
                            }
                        }
                    }
                }
            }

            doc.Root.Add(notesElement);
            doc.Root.Add(connectionsElement);
            doc.Save(savePath);
        }
    }
}

