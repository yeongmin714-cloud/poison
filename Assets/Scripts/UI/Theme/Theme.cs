using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Theme
{
    public class Theme : MonoBehaviour
    {
        [SerializeField] private Color primaryColor;
        [SerializeField] private Color secondaryColor;

        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
    }
}