using UnityEngine;
using System.Collections.Generic;

public class ComponentManager : MonoBehaviour
{
    private static ComponentManager instance;
    public static ComponentManager Instance => instance;
    
    private Dictionary<string, MonoBehaviour> components = new Dictionary<string, MonoBehaviour>();
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void RegisterComponent(string name, MonoBehaviour component)
    {
        components[name] = component;
    }
    
    public T GetComponent<T>(string name) where T : MonoBehaviour
    {
        if (components.TryGetValue(name, out MonoBehaviour component))
        {
            return component as T;
        }
        return null;
    }
    
    public void UnregisterComponent(string name)
    {
        components.Remove(name);
    }
}