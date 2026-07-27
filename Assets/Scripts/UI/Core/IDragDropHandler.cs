using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public interface IDragDropHandler
    {
        void OnDragStart(PointerEventData eventData);
        void OnDrag(PointerEventData eventData);
        void OnDragEnd(PointerEventData eventData);
    }
}