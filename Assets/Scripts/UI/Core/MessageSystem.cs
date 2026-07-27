using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class MessageSystem : MonoBehaviour
    {
        public void SendMessage(string message)
        {
            Debug.Log(message);
        }
    }
}