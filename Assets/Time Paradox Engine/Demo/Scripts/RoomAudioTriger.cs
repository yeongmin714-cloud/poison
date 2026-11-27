using UnityEngine;
using TimeWarp.Core; // Added TimeWarp system namespace

namespace TimeWarp.Gameplay
{
    [RequireComponent(typeof(AudioSource))]
    public class RoomAudioTrigger : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Drag the Player GameObject from the hierarchy here.")]
        public GameObject TargetPlayer;

        private AudioSource _audioSource;
        private TimeWarpIdentity _playerIdentity; // Reference to check if player is rewinding

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false; 
        }

        private void Start()
        {
            // Cache the TimeWarpIdentity so we don't use GetComponent every time
            if (TargetPlayer != null)
            {
                _playerIdentity = TargetPlayer.GetComponent<TimeWarpIdentity>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject == TargetPlayer)
            {
                // CRITICAL FIX: Ignore the trigger if the player is currently rewinding time!
                // This prevents the audio from restarting when the player is dragged backward into the room.
                if (_playerIdentity != null && _playerIdentity.IsRewinding) return;

                _audioSource.time = 0f;
                _audioSource.Play();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject == TargetPlayer)
            {
                _audioSource.Stop(); 
            }
        }
    }
}