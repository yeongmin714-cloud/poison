using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class CanvasController : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;

        public void Initialize()
        {
            if(canvas == null)
                canvas = GetComponent<Canvas>();

            if(canvasScaler == null)
                canvasScaler = GetComponent<CanvasScaler>();
        }

        public void SetCanvasActive(bool active)
        {
            if(canvas != null)
                canvas.enabled = active;
            else
                Debug.LogError("Canvas reference is missing in CanvasController");
        }

        public void SetCanvasScale(float scale)
        {
            if(canvasScaler != null)
                canvasScaler.scaleFactor = scale;
        }
    }
}