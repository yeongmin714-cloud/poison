using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// 모든 UI 윈도우의 베이스 클래스.
    /// 
    /// [Unity 초보자 설명]
    /// 이 클래스를 상속받으면 Show()/Hide()/Toggle() 기능이 자동으로 제공됩니다.
    /// 각 윈도우(퀘스트, 레시피, 인벤토리, 지도)는 이 클래스를 상속받아 만듭니다.
    /// 
    /// - Show(): 창을 화면에 표시 (테마 애니메이션)
    /// - Hide(): 창을 화면에서 숨김 (테마 애니메이션)
    /// - Toggle(): 열려있으면 닫고, 닫혀있으면 염
    /// 
    /// G2-03: CanvasGroup을 통한 Fade(0→1, 0.2s / 1→0, 0.15s) + Slide Up (y 20→0) 애니메이션.
    /// Phase 33 UI-01: UIDesignTheme을 통한 8종 애니메이션 지원.
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

        [Header("Theme (Phase 33)")]
        [SerializeField] protected UIDesignTheme _theme;              // UI 테마 (null 허용)

        [Header("Events")]
        public UnityEvent OnWindowOpen;
        public UnityEvent OnWindowClose;

        // 현재 상태
        protected bool _isOpen = false;
        protected CanvasGroup _canvasGroup;
        protected CanvasGroup _dimCanvasGroup;  // 캐시: 애니메이션 루프에서 GetComponent GC 방지
        protected Canvas _parentCanvas;         // 캐시: CreateDimBackground에서 GetComponentInParent GC 방지

        // IMGUI 배경 드로잉 상태 (OnGUI에서만 GUI.* 호출)
        private bool _needsBackgroundDraw;
        private Rect _backgroundRect;
        private Texture2D _backgroundTexture;
        private bool _useMedievalBackground;
        private string _medievalPanelType;

        /// <summary>윈도우 열기 (비애니메이션, 즉시 표시)</summary>
        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            // 즉시 시각적 표시
            gameObject.SetActive(true);
            if (_windowRoot != null)
                _windowRoot.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
            if (_dimBackground != null)
            {
                _dimBackground.SetActive(true);
                if (_dimCanvasGroup != null)
                    _dimCanvasGroup.alpha = _dimAlpha;
            }

            OnWindowOpen?.Invoke();
        }
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

        /// <summary>현재 테마 (Phase 33)</summary>
        public UIDesignTheme Theme => _theme;

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

            // 부모 Canvas 캐싱 (GetComponentInParent GC 방지)
            _parentCanvas = GetComponentInParent<Canvas>();

            // 배경 딤드 자동 생성 (미할당 시)
            if (_dimBackground == null)
                _dimBackground = CreateDimBackground();

            // 딤드 CanvasGroup 캐싱
            if (_dimBackground != null)
                _dimCanvasGroup = _dimBackground.GetComponent<CanvasGroup>();

            // 시작할 때는 항상 닫힌 상태
            // 중요: _windowRoot를 비활성화하면 Coroutine이 동작하지 않음.
            // CanvasGroup으로만 숨기고 게임오브젝트는 활성 상태 유지.
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            if (_dimBackground != null)
                _dimBackground.SetActive(false);
            _isOpen = false;

            // 게임오브젝트가 비활성화되어 있으면 Awake()가 나중에 다시 불릴 수 있으므로
            // 이미 Show() 요청이 있었다면 비활성화하지 않음. 여기서는 항상 활성화 유지.
        }

        /// <summary>
        /// 딤드 배경을 자동 생성합니다. Canvas 하위에서만 생성합니다.
        /// </summary>
        private GameObject CreateDimBackground()
        {
            if (_parentCanvas == null) return null;

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

            // Show()가 호출되면 게임오브젝트가 활성 상태여야 Coroutine 실행 가능.
            // CloseAnimation 종료 시 _windowRoot가 비활성화될 수 있으므로 여기서 재활성화.
            gameObject.SetActive(true);

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

            // CloseAnimation 실행을 위해 게임오브젝트 활성화 보장
            gameObject.SetActive(true);

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
                if (_dimCanvasGroup != null)
                    _dimCanvasGroup.alpha = 0f;
            }

            // CanvasGroup이 없으면 구식 동작 (즉시 표시)
            if (_canvasGroup == null)
            {
                // 딤드만 처리
                if (_dimCanvasGroup != null)
                    _dimCanvasGroup.alpha = _dimAlpha;
                _animCoroutine = null;
                OnWindowOpen?.Invoke();
                OnShow();
                yield break;
            }

            // 테마 애니메이션 사용
            if (_theme != null)
            {
                yield return WindowAnimationProfile.GetOpenAnimation(
                    _theme.CurrentAnimation,
                    _canvasGroup, _rectTransform,
                    _dimCanvasGroup, _dimAlpha,
                    _openFadeDuration, _slideOffset);
                _animCoroutine = null;
                OnWindowOpen?.Invoke();
                OnShow();
                yield break;
            }

            // 기본 FadeSlide

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
                if (_dimCanvasGroup != null)
                    _dimCanvasGroup.alpha = t * _dimAlpha;

                yield return null;
            }

            // 최종 상태 보정
            _canvasGroup.alpha = 1f;
            if (_rectTransform != null)
                _rectTransform.anchoredPosition = startPos;
            if (_dimCanvasGroup != null)
                _dimCanvasGroup.alpha = _dimAlpha;

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

            // 테마 애니메이션 사용
            if (_theme != null)
            {
                yield return WindowAnimationProfile.GetCloseAnimation(
                    _theme.CurrentAnimation,
                    _canvasGroup, _rectTransform,
                    _dimCanvasGroup, _dimAlpha,
                    _closeFadeDuration, _slideOffset);
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
                if (_dimCanvasGroup != null)
                    _dimCanvasGroup.alpha = (1f - t) * _dimAlpha;

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

        /// <summary>
        /// Called after the window has opened and the open animation completes.
        /// Phase 33: Prepares the background texture for the window.
        /// Actual IMGUI drawing happens in OnGUI() — DO NOT call GUI.* here.
        /// If a medieval panel texture is configured on the theme, renders it via
        /// MedievalBackgroundRenderer instead of the procedural texture.
        /// </summary>
        protected virtual void OnShow()
        {
            if (_theme == null || _windowRoot == null)
                return;

            if (_rectTransform == null)
                return;

            var rectRect = _rectTransform.rect;
            _backgroundRect = new Rect(
                _rectTransform.position.x + rectRect.x,
                _rectTransform.position.y + rectRect.y,
                rectRect.width, rectRect.height);

            // Medieval (PNG texture) background takes priority over procedural
            if (_theme.UseMedievalBackground)
            {
                _useMedievalBackground = true;
                _medievalPanelType = _theme.MedievalPanelTexture;
                _backgroundTexture = null;
            }
            else
            {
                _useMedievalBackground = false;
                _medievalPanelType = null;
                _backgroundTexture = ProceduralTextureGenerator.GetPatternTexture(_theme.CurrentPattern);
            }
            _needsBackgroundDraw = true;
        }

        /// <summary>
        /// OnGUI — IMGUI 배경 드로잉. 이 메서드 내에서만 GUI.* 호출.
        /// OnShow()에서 준비된 텍스처를 실제로 그립니다.
        /// </summary>
        private void OnGUI()
        {
            if (!_isOpen || !_needsBackgroundDraw)
                return;

            if (_useMedievalBackground && !string.IsNullOrEmpty(_medievalPanelType))
            {
                MedievalBackgroundRenderer.DrawBackground(_backgroundRect, _medievalPanelType);
            }
            else if (_backgroundTexture != null)
            {
                GUI.DrawTexture(_backgroundRect, _backgroundTexture, ScaleMode.StretchToFill);
            }
        }
        protected virtual void OnHide()
        {
            _needsBackgroundDraw = false;
            _backgroundTexture = null;
            _useMedievalBackground = false;
            _medievalPanelType = null;
        }  // 닫힐 때 추가 동작
        protected virtual void OnRefresh() { } // 내용 갱신 (외부에서 호출)
        protected virtual void DrawWindowContent() { }  // IMGUI 창 내용 그리기 (ChurchUI/WarehouseUI 등에서 사용)

        /// <summary>
        /// Phase 33: UI 테마를 적용합니다. 애니메이션 타입과 배경 패턴을 변경합니다.
        /// </summary>
        public void ApplyTheme(UIDesignTheme theme)
        {
            _theme = theme;
        }
    }
}