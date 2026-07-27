using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class CanvasController : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        
        public void SetCanvasActive(bool active)
        {
            canvas.enabled = active;
        }
    }
}