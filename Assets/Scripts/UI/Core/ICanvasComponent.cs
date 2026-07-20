using UnityEngine;

namespace UI.Core
{
    public interface ICanvasComponent
    {
        Canvas Canvas { get; }
        void Initialize();
        void Cleanup();
    }
}