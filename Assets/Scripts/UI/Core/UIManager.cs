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
            foreach(var element in uiElements)
            {
                element.SetActive(true);
            }
        }
    }
}