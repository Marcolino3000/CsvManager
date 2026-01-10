using System.Collections.Generic;
using Nodes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DefaultNamespace
{
    [CreateAssetMenu(menuName = "AudioEditor/AudioClipManager")]
    public class AudioClipManager : SerializedScriptableObject
    {
        [SerializeField] public Dictionary<Node, AudioClip> NodesToAudioClips;
        
        public void AddAudioClip(Node node, AudioClip clip)
        {
            Debug.Log(clip.name + "added to node " + node.name);
            NodesToAudioClips[node] = clip;
            node.AudioClip = clip;
        }

        
        public bool TryAddAudioClip(Node node, AudioClip clip)
        {
            if (!NodesToAudioClips.TryAdd(node, clip))
            {
                Debug.LogWarning("Node already has an AudioClip assigned to it. Clip was not overwritten.");
                return false;
            }
            
            return true;
        }
        
    }
}