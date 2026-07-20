using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class ToolTipManager : MonoBehaviour
    {
        public static ToolTipManager Instance { get; private set; }

        private Dictionary<string, GameObject> _toolTips = new Dictionary<string, GameObject>();

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

        public void ShowToolTip(string toolTipName, Vector3 position)
        {
            if (_toolTips.TryGetValue(toolTipName, out GameObject toolTip))
            {
                toolTip.SetActive(true);
                toolTip.transform.position = position;
            }
        }

        public void HideToolTip(string toolTipName)
        {
            if (_toolTips.TryGetValue(toolTipName, out GameObject toolTip))
            {
                toolTip.SetActive(false);
            }
        }

        public void RegisterToolTip(string name, GameObject toolTip)
        {
            if (!_toolTips.ContainsKey(name))
            {
                _toolTips.Add(name, toolTip);
            }
        }
    }
}