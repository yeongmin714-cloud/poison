using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class DragDropManager : MonoBehaviour
    {
        public static DragDropManager Instance { get; private set; }

        private Dictionary<string, IDragDropHandler> _dragDropHandlers = new Dictionary<string, IDragDropHandler>();
        private GameObject _draggedObject;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void RegisterDragDropHandler(string name, IDragDropHandler handler)
        {
            _dragDropHandlers.Add(name, handler);
        }

        public void StartDrag(GameObject draggedObject)
        {
            _draggedObject = draggedObject;
        }

        public void EndDrag()
        {
            _draggedObject = null;
        }

        public void HandleDrag(PointerEventData eventData)
        {
            if (_draggedObject != null && _dragDropHandlers.TryGetValue(_draggedObject.name, out IDragDropHandler handler))
            {
                handler.OnDrag(eventData);
            }
        }
    }
}