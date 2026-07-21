using UnityEngine;

public interface IDragDropHandler
{
    void OnDragStart();
    void OnDrag();
    void OnDragEnd();
}