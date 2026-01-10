using System.Collections.Generic;
using DefaultNamespace;
using Tree;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Windows;

namespace Editor.UIToolkit
{
    public class DialogTreeContextMenu
    {
        [OnOpenAsset(1)]
        public static bool step1(int instanceID, int line)
        {
            Object obj = EditorUtility.InstanceIDToObject(instanceID);
            if (obj is AudioClip)
            {
                Selection.activeObject = obj;
                EditorApplication.ExecuteMenuItem("Tools/AudioPlayer");
                return true;
            }
            string name = obj.name;
            Debug.Log("Open Asset step: 1 (" + name + ")");
            return false;
        }
        
        [MenuItem("Assets/DialogBuilder/Export Dialog as .csv-File %e", true)]
        private static bool ValidateExportDialogToCSV()
        {
            string path = null;
            foreach (Object obj in Selection.objects)
            {
                string objPath = AssetDatabase.GetAssetPath(obj);

                // Check if the selected object is a folder
                if (!string.IsNullOrEmpty(objPath) && Directory.Exists(objPath))
                {
                    path = objPath;
                    Debug.Log(path);
                }
            }

            return !string.IsNullOrEmpty(path);
        }
        
        [MenuItem("Assets/DialogBuilder/Export Dialog as .csv-File %e", false, 100)]
        private static void ExportDialogToCSV()
        {
            foreach (Object obj in Selection.objects)
            {
                string folderPath = AssetDatabase.GetAssetPath(obj);
        
                if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
                {
                    // Collect all DialogTrees
                    var allDialogTrees = new List<DialogTree>();
                    string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
            
                    foreach (string guid in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                
                        if (asset != null)
                        {
                            Debug.Log($"Found asset: {asset.name} at {assetPath}");
                        }

                        if (asset is DialogTree tree)
                        {
                            allDialogTrees.Add(tree);
                        }
                    }

                    // Group DialogTrees by character
                    var characterGroups = new Dictionary<string, List<DialogTree>>();
                    foreach (var tree in allDialogTrees)
                    {
                        if(tree.Blackboard.CharacterData == null)
                        {
                            Debug.LogWarning($"DialogTree '{tree.name}' has no CharacterData assigned in the Blackboard. Skipping to next tree.");
                            continue;
                        }
                        
                        string characterName = tree.Blackboard.CharacterData.name;
                        if (!characterGroups.ContainsKey(characterName))
                        {
                            characterGroups[characterName] = new List<DialogTree>();
                        }
                        characterGroups[characterName].Add(tree);
                    }

                    // Export CSV for each character
                    var csvManager = new CsvManager();
                    foreach (var characterGroup in characterGroups)
                    {
                        csvManager.ExportCsv(characterGroup.Value);
                    }
                }
            }
        }
        
        [MenuItem("Assets/DialogBuilder/Import Dialog from .csv-File %y", true)]
        private static bool ValidateImportDialogFromCSV()
        {
            string path = null;
            foreach (Object obj in Selection.objects)
            {
                string objPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(objPath) && AssetDatabase.IsValidFolder(objPath))
                {
                    path = objPath;
                    break;
                }
            }
            if (string.IsNullOrEmpty(path))
                return false;

            // var allFiles = System.IO.Directory.GetFiles(path);
            // foreach (var file in allFiles)
            // {
            //     Debug.Log(file);
            // }
            // Find all assets in the folder (non-recursive)
            
            string[] guids = AssetDatabase.FindAssets("", new[] { path });
            string filePath = null;
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath == path || AssetDatabase.IsValidFolder(assetPath))
                    continue; // skip the folder itself and subfolders
                if (assetPath.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                    continue; // skip meta files
                if(assetPath.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
                    filePath = assetPath;
            }
            return !string.IsNullOrEmpty(filePath) && filePath.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem("Assets/DialogBuilder/Import Dialog from .csv-File %y", false, 101)]
        private static void ImportDialogFromCSV()
        {
            foreach (Object obj in Selection.objects)
            {
                string folderPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
                {
                    string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
                    string csvPath = null;
                    var audioClips = new List<AudioClip>();
                    
                    foreach (var guid in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

                        if (audioClip != null)
                        {
                            audioClips.Add(audioClip);
                            continue;
                        }    
                        if (assetPath == folderPath || AssetDatabase.IsValidFolder(assetPath))
                            continue;
                        if (assetPath.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                            continue;
                        if(assetPath.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
                            csvPath = assetPath;
                    }
                    if (!string.IsNullOrEmpty(csvPath))
                    {
                        Debug.Log($"Ready to import dialog from: {csvPath}");
                        var csvManager = new CsvManager();
                        csvManager.ImportCsv(folderPath, csvPath, audioClips);
                    }
                }
            }
        }
    }
}