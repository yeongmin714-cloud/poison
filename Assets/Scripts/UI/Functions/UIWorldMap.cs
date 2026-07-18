using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIWorldMap : MonoBehaviour
    {
        [Header("UI References")]
        public Image worldMapImage;
        public RectTransform mapMarkerPanel;
        public GameObject mapMarkerPrefab;
        public Button zoomInButton;
        public Button zoomOutButton;
        
        [Header("Map Data")]
        public string mapName = "World Map";
        public float zoomLevel = 1.0f;
        public Vector2 mapCenter = Vector2.zero;

        private void Start()
        {
            InitializeMap();
        }

        public void InitializeMap()
        {
            // Setup map UI
            zoomInButton.onClick.AddListener(ZoomIn);
            zoomOutButton.onClick.AddListener(ZoomOut);
        }

        public void ZoomIn()
        {
            zoomLevel += 0.1f;
            // Zoom in logic
        }

        public void ZoomOut()
        {
            zoomLevel -= 0.1f;
            // Zoom out logic
        }

        public void AddMarker(string markerName, Vector2 position)
        {
            // Add a marker to the map
            GameObject marker = Instantiate(mapMarkerPrefab, mapMarkerPanel);
            // Set marker position and name
        }
    }
}