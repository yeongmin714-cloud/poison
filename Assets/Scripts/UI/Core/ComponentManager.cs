using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

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
        // Add actual component registration logic here
        Debug.Log("Component registered: " + name);
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
        // Add actual component unregistration logic here
        Debug.Log("Component unregistered: " + name);
    }
}