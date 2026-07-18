using UnityEngine;
using UnityEngine.UI;

public class UIGrid : MonoBehaviour
{
    public GridLayoutGroup grid;
    public GameObject cellPrefab;
    
    public void AddCell()
    {
        if(cellPrefab != null && grid != null)
        {
            GameObject newCell = Instantiate(cellPrefab, grid.transform);
            // Configure cell
        }
    }
}