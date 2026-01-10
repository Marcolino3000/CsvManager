using System.Collections.Generic;
using Editor.AudioEditor;
using Nodes;
using Tree;
using UnityEngine;

namespace DefaultNamespace
{
    public class AudioClipPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClipManager clipManager;
        [SerializeField] private AudioSource audioSource;
        // [SerializeField] private DialogTreeRunner treeRunner;
 
        private List<MarkerManager.Marker> currentClipMarkers = new();
        
        private void Update()
        {
            CheckForPassedMarkers();
        }
        
        private void CheckForPassedMarkers()
        {
            if (audioSource.clip == null || !audioSource.isPlaying)
                return;
            
            // if(audioSource.timeSamples > marker.samples)
        }

        public void PlayClip(Node node)
        {
            if (!TryAssignAudioClip(node))
                return;
            
            // get markers
            
            audioSource.Play();
        }

        private bool TryAssignAudioClip(Node node)
        {
            if (clipManager.NodesToAudioClips.TryGetValue(node, out AudioClip audioClip))
            {
                audioSource.clip = audioClip;
                return true;
            }
            
            Debug.LogWarning("Node does not have an audioClip assigned to it!");
            return false;
        }
        
        private void Awake()
        {
            DialogTreeRunner.DialogNodeSelected += PlayClip;
        }

        // private void OnDestroy()
        // {
        //     DialogTreeRunner.DialogNodeSelected -= PlayClip;
        // }
    }
}