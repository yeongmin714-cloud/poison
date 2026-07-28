using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private List<GameObject> uiElements;

        public void Initialize()
        {
            if(uiElements != null)
            {
                foreach(var element in uiElements)
                {
                    if(element != null)
                        element.SetActive(true);
                }
            }
            else
            {
                Debug.LogWarning("UIElements list is null in UIManager");
            }
        }
    }
}