using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class UIProgressBar : MonoBehaviour
{
    public Slider progressBar;
    public float maxValue = 100f;
    
    public void SetValue(float value)
    {
        if(progressBar != null)
        {
            progressBar.value = Mathf.Clamp(value, 0, maxValue);
        }
    }
}