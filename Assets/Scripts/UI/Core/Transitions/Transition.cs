using UnityEngine;

namespace UI.Core.Transitions
{
    public abstract class Transition : MonoBehaviour
    {
        public abstract void Apply(float progress);
    }
}