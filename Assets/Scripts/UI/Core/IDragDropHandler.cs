using UnityEngine;
using System.Collections.Generic;
using System.Collections;
public interface IDragDropHandler
{
    void OnDragStart();
    void OnDrag();
    void OnDragEnd();
}