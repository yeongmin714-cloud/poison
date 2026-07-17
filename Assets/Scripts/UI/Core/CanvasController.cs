using UnityEngine;

namespace ProjectName.UI.Core
{
    public class CanvasController : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform rectTransform;

        private void Start()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
        }

        public void SetCanvasScale(Vector2 scale)
        {
            rectTransform.localScale = scale;
        }

        public void SetCanvasSortingOrder(int order)
        {
            canvas.sortingOrder = order;
        }
    }
}