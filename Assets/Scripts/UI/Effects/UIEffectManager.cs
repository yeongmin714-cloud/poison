using UnityEngine;

public class UIEffectManager : MonoBehaviour
{
    public GameObject effectsContainer;
    
    public void PlayEffect(string effectName)
    {
        // Play UI effect
        Debug.Log($"Playing UI effect: {effectName}");
    }
}