using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class DragDropManager : MonoBehaviour
    {
        private GameObject currentlyDragging;
        private Vector3 offset;

        public void Initialize()
        {
            currentlyDragging = null;
        }

        public void StartDrag(GameObject draggedItem)
        {
            if(draggedItem != null)
            {
                currentlyDragging = draggedItem;
                // Calculate offset for smooth dragging
                offset = draggedItem.transform.position - Input.mousePosition;
            }
        }

        public void UpdateDrag()
        {
            if(currentlyDragging != null)
            {
                // Update position based on mouse
                Vector3 mousePosition = Input.mousePosition;
                currentlyDragging.transform.position = mousePosition + offset;
            }
        }

        public void EndDrag()
        {
            currentlyDragging = null;
        }
    }
}