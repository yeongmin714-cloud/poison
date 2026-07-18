using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UINetworkUtils : MonoBehaviour
    {
        [Header("Network Settings")]
        public string serverAddress = "localhost";
        public int port = 7777;

        public void ConnectToServer()
        {
            // Connect to network server
            Debug.Log($"Connecting to {serverAddress}:{port}");
        }

        public void SendData(string data)
        {
            // Send data over network
            Debug.Log($"Sending data: {data}");
        }

        public void Disconnect()
        {
            // Disconnect from server
            Debug.Log("Disconnected from server");
        }
    }
}