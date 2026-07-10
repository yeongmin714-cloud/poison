using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Core
{
    public class EventSystemManager : MonoBehaviour
    {
        private EventSystem eventSystem;
        private StandaloneInputModule inputModule;

        private void Awake()
        {
            eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystem = eventSystemGO.AddComponent<EventSystem>();
                inputModule = eventSystemGO.AddComponent<StandaloneInputModule>();
            }
            else
            {
                inputModule = eventSystem.GetComponent<StandaloneInputModule>();
            }
        }

        public void SetInputModule(StandaloneInputModule module)
        {
            inputModule = module;
        }

        public StandaloneInputModule GetInputModule()
        {
            return inputModule;
        }
    }
}