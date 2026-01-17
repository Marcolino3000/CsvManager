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
            if(node.AudioClip == null) 
                return;
            
            audioSource.clip = node.AudioClip;
            
            // get markers
            
            audioSource.Play();
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