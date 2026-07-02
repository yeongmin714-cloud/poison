using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MountUI : MonoBehaviour
{
    [Header("UI References")]
    public Text horseNameText;
    public Text horseStatusText;
    public Slider horseHPSlider;
    public Button mountButton;
    public Button dismountButton;
    
    [Header("UI Settings")]
    public Color normalColor = Color.white;
    public Color disabledColor = Color.gray;
    
    private MountSystem _mountSystem;
    private CanvasGroup _canvasGroup;
    
    void Start()
    {
        _mountSystem = FindObjectOfType<MountSystem>();
        _canvasGroup = GetComponent<CanvasGroup>();
        
        if (_mountSystem == null)
        {
            Debug.LogError("MountSystem not found in scene!");
            return;
        }
        
        // Set up button events
        if (mountButton != null) mountButton.onClick.AddListener(OnMountButtonClicked);
        if (dismountButton != null) dismountButton.onClick.AddListener(OnDismountButtonClicked);
        
        UpdateUI();
    }
    
    void Update()
    {
        if (_mountSystem != null)
        {
            UpdateUI();
        }
    }
    
    void UpdateUI()
    {
        // Update horse name
        if (horseNameText != null)
        {
            if (_mountSystem.CurrentHorseSpawner != null)
            {
                horseNameText.text = _mountSystem.CurrentHorseSpawner.name;
            }
            else
            {
                horseNameText.text = "No Horse";
            }
        }
        
        // Update horse status
        if (horseStatusText != null)
        {
            if (_mountSystem.CurrentHorseSpawner != null)
            {
                // Fixed string interpolation issue in the original code
                horseStatusText.text = $"⚡ 질주 중 - HP -{(_mountSystem.CurrentHorseSpawner != null ? "5" : "5")}/초";
            }
            else
            {
                horseStatusText.text = "준비 완료";
            }
        }
        
        // Update HP slider
        if (horseHPSlider != null)
        {
            if (_mountSystem.CurrentHorseSpawner != null)
            {
                horseHPSlider.value = _mountSystem.CurrentHorseSpawner.HP / _mountSystem.CurrentHorseSpawner.MaxHP;
            }
            else
            {
                horseHPSlider.value = 0f;
            }
        }
        
        // Update button states
        if (mountButton != null)
        {
            mountButton.interactable = _mountSystem.CurrentHorseSpawner != null && !_mountSystem.IsMounted;
        }
        
        if (dismountButton != null)
        {
            dismountButton.interactable = _mountSystem.IsMounted;
        }
    }
    
    void OnMountButtonClicked()
    {
        if (_mountSystem != null && _mountSystem.CurrentHorseSpawner != null && !_mountSystem.IsMounted)
        {
            _mountSystem.MountHorse();
        }
    }
    
    void OnDismountButtonClicked()
    {
        if (_mountSystem != null && _mountSystem.IsMounted)
        {
            _mountSystem.DismountHorse();
        }
    }
    
    public void SetActive(bool active)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = active ? 1f : 0f;
            _canvasGroup.interactable = active;
            _canvasGroup.blocksRaycasts = active;
        }
    }
    
    void OnDestroy()
    {
        if (mountButton != null) mountButton.onClick.RemoveListener(OnMountButtonClicked);
        if (dismountButton != null) dismountButton.onClick.RemoveListener(OnDismountButtonClicked);
    }
}