using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class ResourceUtils
    {
        public static T LoadResource<T>(string path) where T : UnityEngine.Object
        {
            return Resources.Load<T>(path);
        }
    }
}