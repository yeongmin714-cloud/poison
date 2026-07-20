using UnityEngine;

namespace UI.Core.Transitions
{
    public class PanelTransition : Transition
    {
        private RectTransform _rectTransform;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;

        public void Initialize(RectTransform rectTransform, Vector2 startPosition, Vector2 targetPosition)
        {
            _rectTransform = rectTransform;
            _startPosition = startPosition;
            _targetPosition = targetPosition;
        }

        public override void Apply(float progress)
        {
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = Vector2.Lerp(_startPosition, _targetPosition, progress);
            }
        }
    }
}