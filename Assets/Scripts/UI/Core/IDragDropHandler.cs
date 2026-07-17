using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectName.UI.Core
{
    public interface IDragDropHandler
    {
        void OnDragStart(PointerEventData eventData);
        void OnDrag(PointerEventData eventData);
        void OnDragEnd(PointerEventData eventData);
    }
}