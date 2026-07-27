using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class MathUtils
    {
        public static int Clamp(int value, int min, int max)
        {
            return Mathf.Clamp(value, min, max);
        }
    }
}