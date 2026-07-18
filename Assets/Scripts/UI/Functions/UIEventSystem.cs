using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Functions
{
    public class UIEventSystem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Event Data")]
        public bool isHovering = false;
        public Color hoverColor = Color.yellow;
        public Color normalColor = Color.white;

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            // Handle hover effect
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            // Handle hover end effect
        }

        public void HandleClick()
        {
            // Handle click event
            Debug.Log("UI Element Clicked");
        }
    }
}