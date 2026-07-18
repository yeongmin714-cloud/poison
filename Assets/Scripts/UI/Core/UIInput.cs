using UnityEngine;
using UnityEngine.UI;

public class UIInput : MonoBehaviour
{
    public InputField inputField;
    public string defaultValue = "";
    
    private void Start()
    {
        if(inputField != null)
        {
            inputField.text = defaultValue;
        }
    }
}