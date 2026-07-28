using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class ToolTipManager : MonoBehaviour
    {
        [SerializeField] private GameObject tooltipPrefab;
        private Dictionary<string, GameObject> tooltips;

        public void Initialize()
        {
            tooltips = new Dictionary<string, GameObject>();
        }

        public void ShowTooltip(string tooltipId)
        {
            if(tooltips.ContainsKey(tooltipId))
            {
                tooltips[tooltipId].SetActive(true);
            }
        }

        public void HideTooltip(string tooltipId)
        {
            if(tooltips.ContainsKey(tooltipId))
            {
                tooltips[tooltipId].SetActive(false);
            }
        }
    }
}