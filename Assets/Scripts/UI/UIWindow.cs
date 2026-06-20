using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectName.UI
{
    /// <summary>
    /// 모든 UI 윈도우의 베이스 클래스.
    /// 
    /// [Unity 초보자 설명]
    /// 이 클래스를 상속받으면 Show()/Hide()/Toggle() 기능이 자동으로 제공됩니다.
    /// 각 윈도우(퀘스트, 레시피, 인벤토리, 지도)는 이 클래스를 상속받아 만듭니다.
    /// 
    /// - Show(): 창을 화면에 표시 (Fade + Slide 애니메이션)
    /// - Hide(): 창을 화면에서 숨김 (Fade 애니메이션)
    /// - Toggle(): 열려있으면 닫고, 닫혀있으면 염
    /// 
    /// G2-03: CanvasGroup을 통한 Fade(0→1, 0.2s / 1→0, 0.15s) + Slide Up (y 20→0) 애니메이션.
    /// 배경 딤드(반투명 검은색, alpha 0.5)가 자동 생성됩니다.
    /// </summary>
    public abstract class UIWindow : MonoBehaviour
    {
        [Header("Window Settings")]
        [SerializeField] protected GameObject _windowRoot;     // 윈도우 전체 루트 오브젝트
        [SerializeField] protected GameObject _dimBackground;  // 배경 딤드 (반투명 검정)
        [SerializeField] protected float _fadeDuration = 0.2f; // (Legacy) 나타날 때 시간

        [Header("Animation Settings")]
        [SerializeField] protected float _openFadeDuration = 0.2f;   // 열릴 때 Fade 시간
        [SerializeField] protected float _closeFadeDuration = 0.15f;  // 닫힐 때 Fade 시간
        [SerializeField] protected float _slideOffset = 20f;          // Slide 오프셋 (픽셀)
        [SerializeField] protected float _dimAlpha = 0.5f;            // 딤드 알파 값

        [Header("Events")]
        public UnityEvent OnWindowOpen;
        public UnityEvent OnWindowClose;

        // 현재 상태
        protected bool _isOpen = false;
        protected CanvasGroup _canvasGroup;

        /// <summary>윈도우 열기</summary>
        public void Open() { _isOpen = true; OnWindowOpen?.Invoke(); }
        protected RectTransform _rectTransform;
        protected Coroutine _animCoroutine;

        /// <summary>윈도우가 열려있는지?</summary>
        public bool IsOpen => _isOpen;

        /// <summary>CanvasGroup (테스트에서 접근)</summary>
        public CanvasGroup CanvasGroupComponent => _canvasGroup;

        /// <summary>RectTransform (테스트에서 접근)</summary>
        public RectTransform RectTransformComponent => _rectTransform;

        /// <summary>딤드 배경 GameObject (테스트에서 접근)</summary>
        public GameObject DimBackground => _dimBackground;

        protected virtual void Awake()
        {
            // _windowRoot가 설정되지 않았으면 자기 자신으로 지정
            if (_windowRoot == null)
                _windowRoot = gameObject;

            // CanvasGroup 자동 추가/찾기
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null && GetComponent<Canvas>() != null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // RectTransform 캐싱
            _rectTransform = _windowRoot.GetComponent<RectTransform>();

            // 배경 딤드 자동 생성 (미할당 시)
            if (_dimBackground == null)
                _dimBackground = CreateDimBackground();

            // 시작할 때는 항상 닫힌 상태
            if (_windowRoot != null)
            {
                _windowRoot.SetActive(false);
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 0f;
            }
            if (_dimBackground != null)
                _dimBackground.SetActive(false);
            _isOpen = false;
        }

        /// <summary>
        /// 딤드 배경을 자동 생성합니다. Canvas 하위에서만 생성합니다.
        /// </summary>
        private GameObject CreateDimBackground()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var dimObj = new GameObject("DimBackground", typeof(Image));
            var dimRect = dimObj.GetComponent<RectTransform>();
            dimObj.transform.SetParent(transform.parent, false);

            // 현재 윈도우 바로 앞에 배치
            dimObj.transform.SetSiblingIndex(transform.GetSiblingIndex());

            // Canvas 전체를 덮도록 설정
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            // 검은색 반투명
            var img = dimObj.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, _dimAlpha);

            // 자체 CanvasGroup (페이드용)
            var dimCG = dimObj.AddComponent<CanvasGroup>();
            dimCG.alpha = 0f;
            dimCG.interactable = false;
            dimCG.blocksRaycasts = true;

            dimObj.SetActive(false);
            return dimObj;
        }

        /// <summary>윈도우 열기 (애니메이션 적용)</summary>
        public virtual void Show()
        {
            if (_isOpen) return;

            // 이전 애니메이션 정리
            if (_animCoroutine != null)
                StopCoroutine(_animCoroutine);

            _isOpen = true;
            _animCoroutine = StartCoroutine(OpenAnimation());
        }

        /// <summary>윈도우 닫기 (애니메이션 적용)</summary>
        public virtual void Hide()
        {
            if (!_isOpen) return;

            // 이전 애니메이션 정리
            if (_animCoroutine != null)
                StopCoroutine(_animCoroutine);

            _isOpen = false;
            _animCoroutine = StartCoroutine(CloseAnimation());
        }

        /// <summary>열려있으면 닫고, 닫혀있으면 열기</summary>
        public virtual void Toggle()
        {
            if (_isOpen)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// 열기 애니메이션 코루틴:
        /// - CanvasGroup.alpha 0 → 1 (0.2s)
        /// - Slide Up: anchoredPosition.y (start - 20) → start (0.2s)
        /// - 딤드 배경 alpha 0 → 0.5 (0.2s)
        /// </summary>
        protected virtual IEnumerator OpenAnimation()
        {
            if (_windowRoot != null)
                _windowRoot.SetActive(true);

            // 딤드 배경 활성화
            if (_dimBackground != null)
            {
                _dimBackground.SetActive(true);
                var dimCG = _dimBackground.GetComponent<CanvasGroup>();
                if (dimCG != null) dimCG.alpha = 0f;
            }

            // CanvasGroup이 없으면 구식 동작 (즉시 표시)
            if (_canvasGroup == null)
            {
                // 딤드만 처리
                if (_dimBackground != null)
                {
                    var dimCG = _dimBackground.GetComponent<CanvasGroup>();
                    if (dimCG != null) dimCG.alpha = _dimAlpha;
                }
                _animCoroutine = null;
                OnWindowOpen?.Invoke();
                OnShow();
                yield break;
            }

            // 시작 위치 저장
            Vector2 startPos = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;

            // 초기 상태: alpha 0, slide offset 적용
            _canvasGroup.alpha = 0f;
            if (_rectTransform != null)
            {
                Vector2 slidePos = startPos;
                slidePos.y -= _slideOffset;
                _rectTransform.anchoredPosition = slidePos;
            }

            // 애니메이션 루프
            float elapsed = 0f;
            while (elapsed < _openFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _openFadeDuration);

                // Fade
                _canvasGroup.alpha = t;

                // Slide
                if (_rectTransform != null)
                {
                    Vector2 pos = _rectTransform.anchoredPosition;
                    pos.y = Mathf.Lerp(startPos.y - _slideOffset, startPos.y, t);
                    _rectTransform.anchoredPosition = pos;
                }

                // 딤드
                if (_dimBackground != null)
                {
                    var dimCG = _dimBackground.GetComponent<CanvasGroup>();
                    if (dimCG != null) dimCG.alpha = t * _dimAlpha;
                }

                yield return null;
            }

            // 최종 상태 보정
            _canvasGroup.alpha = 1f;
            if (_rectTransform != null)
                _rectTransform.anchoredPosition = startPos;
            if (_dimBackground != null)
            {
                var dimCG = _dimBackground.GetComponent<CanvasGroup>();
                if (dimCG != null) dimCG.alpha = _dimAlpha;
            }

            _animCoroutine = null;
            OnWindowOpen?.Invoke();
            OnShow();
        }

        /// <summary>
        /// 닫기 애니메이션 코루틴:
        /// - CanvasGroup.alpha 1 → 0 (0.15s)
        /// - Slide Down: anchoredPosition.y → start + 20 (0.15s)
        /// - 딤드 배경 alpha 0.5 → 0 (0.15s)
        /// </summary>
        protected virtual IEnumerator CloseAnimation()
        {
            Vector2 startPos = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;

            // CanvasGroup이 없으면 구식 동작 (즉시 숨김)
            if (_canvasGroup == null)
            {
                if (_windowRoot != null)
                    _windowRoot.SetActive(false);
                if (_dimBackground != null)
                    _dimBackground.SetActive(false);
                _animCoroutine = null;
                OnWindowClose?.Invoke();
                OnHide();
                yield break;
            }

            // 애니메이션 루프
            float elapsed = 0f;
            while (elapsed < _closeFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _closeFadeDuration);

                // Fade out
                _canvasGroup.alpha = 1f - t;

                // Slide down
                if (_rectTransform != null)
                {
                    Vector2 pos = _rectTransform.anchoredPosition;
                    pos.y = Mathf.Lerp(startPos.y, startPos.y + _slideOffset, t);
                    _rectTransform.anchoredPosition = pos;
                }

                // 딤드 fade out
                if (_dimBackground != null)
                {
                    var dimCG = _dimBackground.GetComponent<CanvasGroup>();
                    if (dimCG != null) dimCG.alpha = (1f - t) * _dimAlpha;
                }

                yield return null;
            }

            // 최종 상태
            _canvasGroup.alpha = 0f;
            if (_rectTransform != null)
            {
                Vector2 endPos = startPos;
                endPos.y += _slideOffset;
                _rectTransform.anchoredPosition = endPos;
            }

            if (_windowRoot != null)
                _windowRoot.SetActive(false);
            if (_dimBackground != null)
                _dimBackground.SetActive(false);

            _animCoroutine = null;
            OnWindowClose?.Invoke();
            OnHide();
        }

        // --- 상속받은 클래스가 필요하면 재정의하는 메서드 ---
        protected virtual void OnShow() { }  // 열릴 때 추가 동작
        protected virtual void OnHide() { }  // 닫힐 때 추가 동작
        protected virtual void OnRefresh() { } // 내용 갱신 (외부에서 호출)
        protected virtual void DrawWindowContent() { }  // IMGUI 창 내용 그리기 (ChurchUI/WarehouseUI 등에서 사용)
    }
}