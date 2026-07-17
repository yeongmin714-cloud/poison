using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectName.UI.Core
{
    public class DragDropManager : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform rectTransform;
        private Canvas canvas;
        private RectTransform canvasRect;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Handle pointer down logic
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Handle drag start logic
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Handle drag logic
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, canvas.worldCamera, out localPoint))
            {
                rectTransform.localPosition = localPoint;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Handle drag end logic
        }
    }
}