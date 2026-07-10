using UnityEngine;

namespace UI.Core
{
    public interface IUIComponent
    {
        void Initialize();
        void Cleanup();
    }
}