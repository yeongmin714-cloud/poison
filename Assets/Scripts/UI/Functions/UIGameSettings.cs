using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIGameSettings : MonoBehaviour
    {
        [Header("UI References")]
        public Toggle soundToggle;
        public Toggle musicToggle;
        public Slider volumeSlider;
        public Dropdown qualityDropdown;
        public Button applyButton;
        
        [Header("Settings Data")]
        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public float volume = 0.5f;
        public int qualityLevel = 2;

        private void Start()
        {
            InitializeSettings();
        }

        public void InitializeSettings()
        {
            soundToggle.isOn = soundEnabled;
            musicToggle.isOn = musicEnabled;
            volumeSlider.value = volume;
            qualityDropdown.value = qualityLevel;
            
            // Add listeners
            soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
            musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            applyButton.onClick.AddListener(ApplySettings);
        }

        public void OnSoundToggleChanged(bool value)
        {
            soundEnabled = value;
        }

        public void OnMusicToggleChanged(bool value)
        {
            musicEnabled = value;
        }

        public void OnVolumeChanged(float value)
        {
            volume = value;
        }

        public void OnQualityChanged(int value)
        {
            qualityLevel = value;
        }

        public void ApplySettings()
        {
            // Apply settings to game
            Debug.Log("Settings applied");
        }
    }
}