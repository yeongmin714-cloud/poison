using UnityEngine;
using UnityEngine.UI;

public class InputFieldComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private InputField inputField;
    
    public void Initialize()
    {
        if (inputField == null)
            inputField = GetComponent<InputField>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
    
    public void OnValueChanged(string value)
    {
        // Input field value changed implementation
    }
}