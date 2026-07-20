using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class CanvasController : MonoBehaviour
    {
        public static CanvasController Instance { get; private set; }

        private Dictionary<string, Canvas> _canvases = new Dictionary<string, Canvas>();

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

        public Canvas GetCanvas(string canvasName)
        {
            if (_canvases.TryGetValue(canvasName, out Canvas canvas))
            {
                return canvas;
            }
            return null;
        }

        public void RegisterCanvas(string name, Canvas canvas)
        {
            _canvases.Add(name, canvas);
        }
    }
}