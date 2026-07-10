using UnityEngine;

namespace UI.Core.Transitions
{
    public abstract class Transition : MonoBehaviour
    {
        [SerializeField] protected float duration = 0.5f;

        public abstract void Play();
    }
}