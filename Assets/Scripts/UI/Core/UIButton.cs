using UnityEngine;
using UnityEngine.UI;

public class UIButton : MonoBehaviour
{
    public Button button;
    
    private void Start()
    {
        if(button == null)
        {
            button = GetComponent<Button>();
        }
    }
    
    public void AddClickListener(UnityEngine.Events.UnityAction listener)
    {
        if(button != null)
        {
            button.onClick.AddListener(listener);
        }
    }
}