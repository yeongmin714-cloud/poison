using UnityEngine;

public class TransitionManager : MonoBehaviour
{
    private static TransitionManager instance;
    public static TransitionManager Instance => instance;
    
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
    
    public void PlayTransition(TransitionType type, float duration)
    {
        // Implementation for playing transitions
    }
    
    public void PlayTransition(Transition transition, float duration)
    {
        // Implementation for playing custom transitions
    }
}