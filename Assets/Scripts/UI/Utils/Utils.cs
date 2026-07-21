using UnityEngine;
using System.Collections.Generic;

public class Utils : MonoBehaviour
{
    public static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
        {
            component = go.AddComponent<T>();
        }
        return component;
    }
    
    public static void SetActiveRecursively(GameObject go, bool active)
    {
        go.SetActive(active);
        foreach (Transform child in go.transform)
        {
            SetActiveRecursively(child.gameObject, active);
        }
    }
}