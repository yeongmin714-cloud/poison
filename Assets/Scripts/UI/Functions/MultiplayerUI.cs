using UnityEngine;

public class MultiplayerUI : MonoBehaviour
{
    public GameObject multiplayerPanel;
    public TMPro.TextMeshProUGUI serverStatus;
    
    private void Start()
    {
        // Initialize multiplayer UI
        Debug.Log("Multiplayer UI initialized");
    }
    
    public void ConnectToServer(string serverIP)
    {
        // Connect to server
        Debug.Log("Connecting to server: " + serverIP);
    }
}