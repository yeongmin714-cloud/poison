using UnityEngine;

namespace UI.Core.Transitions
{
    public class AnimatedPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TransitionType _transitionType = TransitionType.Fade;

        private CanvasGroup _canvasGroup;
        private Vector2 _originalPosition;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _originalPosition = _panel.anchoredPosition;
        }

        public void Show()
        {
            if (_transitionType == TransitionType.Fade)
            {
                _canvasGroup.alpha = 1f;
            }
            else if (_transitionType == TransitionType.Slide)
            {
                _panel.anchoredPosition = _originalPosition;
            }
        }

        public void Hide()
        {
            if (_transitionType == TransitionType.Fade)
            {
                _canvasGroup.alpha = 0f;
            }
            else if (_transitionType == TransitionType.Slide)
            {
                _panel.anchoredPosition = _originalPosition + new Vector2(0, _panel.rect.height);
            }
        }
    }
}