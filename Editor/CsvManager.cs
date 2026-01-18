using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Nodes;
using Tree;
using UnityEditor;
using UnityEngine;

namespace DefaultNamespace
{
    public class Formatting
    {
        [Optional]
        public string DialogName { get; set; }
        [Optional]
        public string DialogLine { get; set; }
        // public string ID { get; set; }
        [Optional]
        public string AudioClipName { get; set; }
    }
    public class DialogData
    {
        public string DialogName { get; set; }
        public List<NodeData> nodeData {get; set;}
    }
    public class CsvManager
    {
            private List<DialogTree> dialogTrees;
        
            public void ExportCsv(List<DialogTree> trees)
            {
                dialogTrees = trees;
                if (trees == null || trees.Count == 0)
                {
                    Debug.LogWarning("No DialogTrees found");
                    return;
                }
                
                if(trees.Any(t => t.Blackboard.CharacterData == null))
                {
                    Debug.LogWarning("One or more DialogTrees have no CharacterData assigned in the Blackboard!");
                }
                
                string characterName = trees[0].Blackboard.CharacterData.name;
                // string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                
                string path = EditorUtility.OpenFolderPanel("Select location for CSV export", "", "");

                using (var writer = new StreamWriter(path + "/" + $"formatting_{characterName}.csv"))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    var dialogDataList = TreesToDialogData();

                    var formattingList = new List<Formatting>();
                    formattingList.Add(new Formatting());
                    csv.WriteRecords(formattingList);
                     
                    foreach (var dialogData in dialogDataList)
                    {
                        csv.WriteComment(dialogData.DialogName);
                        csv.WriteRecords("\n");
                        csv.WriteRecords(dialogData.nodeData);
                    }
                }
            }
        
            private List<DialogData> TreesToDialogData()
            {
                var dialogDataList = new List<DialogData>();
                
                foreach (var tree in dialogTrees)
                {
                    var dialogData = new DialogData
                    {
                        DialogName = tree.name,
                        nodeData = new List<NodeData>()
                    };
                    
                    foreach (var node in tree.nodes)
                    {
                        dialogData.nodeData.Add(new NodeData(node.DialogLine,
                            GlobalObjectId.GetGlobalObjectIdSlow(node).ToString(),
                            "", tree.name));
                    }
                    
                    dialogDataList.Add(dialogData);
                }

                return dialogDataList;
            }
            
            public void ImportCsv(string folderPath, string csvPath, List<AudioClip> audioClips)
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                var audioClipManager = GetAudioClipManager();

                Debug.Log(csvPath);
                // using (var reader = new StreamReader(desktopPath + "/" + "formatting_ExampleCharacter.csv"))
                var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    MissingFieldFound = null
                };

                using (var reader = new StreamReader(csvPath))
                using (var csv = new CsvReader(reader, config))
                {
                    var formattingInstances = csv.GetRecords<Formatting>();
                    foreach (var formattingInstance in formattingInstances)
                    {
                        var id = new GlobalObjectId();
                        var idString = GetIDFromClipName(formattingInstance.AudioClipName);
                        GlobalObjectId.TryParse(idString, out id);
                        var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);

                        var node = (Node)obj;

                        if (node is Node n)
                        {
                            n.DialogLine = formattingInstance.DialogLine;
                            Debug.Log(n.DialogLine);

                            var audioClip = audioClips.Find(clip => clip.name == formattingInstance.AudioClipName);
                            if (audioClip != null)
                            {
                                // audioClipManager.AddAudioClip(node, audioClip);
                                node.AudioClip = audioClip;
                                EditorUtility.SetDirty(node);
                            }

                            else
                            {
                                Debug.Log("audioclip " + formattingInstance.AudioClipName + " not found");
                            }
                        }
                    }
                }
            }


            private AudioClipManager GetAudioClipManager()
            {
#if UNITY_EDITOR
                // Find the AudioClipManager asset in the project
                string[] guids = AssetDatabase.FindAssets("t:AudioClipManager");

                if (guids.Length == 0)
                {
                    Debug.LogWarning("No AudioClipManager asset found in the project.");
                    return null;
                }
                if (guids.Length == 1)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    return AssetDatabase.LoadAssetAtPath<AudioClipManager>(path);
                }

                Debug.LogWarning("Multiple AudioClipManagers found. Plz delete duplicates.");
                return null;
#endif
            }

            private string GetIDFromClipName(string clipName)
            {
                if (string.IsNullOrEmpty(clipName))
                    return string.Empty;

                int idx = clipName.IndexOf("ID_");
                if (idx >= 0)
                {
                    return clipName.Substring(idx + 3);
                }

                return string.Empty;
            }
    }
    
    public class NodeData
    {
        public NodeData () { }

        public NodeData(string line, string globalObjectId, string characterDataName, string dialogName)
        {
            DialogLine = line;
            // ID = globalObjectId;
            Character = characterDataName;
        
            CreateAudioClipName(dialogName, globalObjectId);
        }
    
        public string Character { get; set; }
        public string DialogLine { get; set; }
        // public string ID;
        public string AudioClipName { get; set; }

        private void CreateAudioClipName(string dialogName, string globalobjectId)
        {
            AudioClipName = Character + "-" + dialogName + "-" + "Take" + "_" + "0" + "-" + "ID_" + globalobjectId;
        }
    }
}