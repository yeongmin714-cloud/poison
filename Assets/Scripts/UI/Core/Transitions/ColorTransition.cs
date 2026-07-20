using UnityEngine;

namespace UI.Core.Transitions
{
    public class ColorTransition : Transition
    {
        private Color _startColor;
        private Color _targetColor;
        private Material _material;

        public void Initialize(Material material, Color startColor, Color targetColor)
        {
            _material = material;
            _startColor = startColor;
            _targetColor = targetColor;
        }

        public override void Apply(float progress)
        {
            if (_material != null)
            {
                _material.color = Color.Lerp(_startColor, _targetColor, progress);
            }
        }
    }
}