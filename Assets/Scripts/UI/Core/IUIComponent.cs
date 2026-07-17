using UnityEngine;

namespace ProjectName.UI.Core
{
    public interface IUIComponent
    {
        void Initialize();
        void Cleanup();
    }
}