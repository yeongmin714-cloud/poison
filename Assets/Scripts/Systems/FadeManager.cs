using System.Collections;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-06: 페이드 인/아웃 연출 매니저.
    /// CanvasGroup 기반의 화면 오버레이를 통해 검은색 페이드 효과를 제공합니다.
    /// 싱글톤으로 동작하며 DontDestroyOnLoad로 씬 전환 시 유지됩니다.
    /// </summary>
    public class FadeManager : MonoBehaviour
    {
        private static FadeManager _instance;
        private static bool _instanceQuitting = false;

        /// <summary>FadeManager 싱글톤 인스턴스</summary>
        public static FadeManager Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("FadeManager");
                    _instance = go.AddComponent<FadeManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("페이드 설정")]
        [SerializeField] private float _defaultDuration = 0.3f;
        [SerializeField] private Color _fadeColor = Color.black;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _overlayRect;
        private Coroutine _activeFadeRoutine;

        /// <summary>현재 페이드 진행 중인지 여부</summary>
        public bool IsFading { get; private set; }

        /// <summary>현재 알파 값 (0=투명, 1=불투명)</summary>
        public float CurrentAlpha => _canvasGroup != null ? _canvasGroup.alpha : 0f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _instanceQuitting = false;
            DontDestroyOnLoad(gameObject);
            InitializeCanvas();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        /// <summary>
        /// UI Canvas와 검은색 오버레이를 초기화합니다.
        /// </summary>
        private void InitializeCanvas()
        {
            // Canvas 생성
            GameObject canvasGo = new GameObject("FadeCanvas");
            canvasGo.transform.SetParent(transform);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999; // 최상단 표시

            // CanvasGroup
            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // 검은색 오버레이 이미지
            GameObject overlayGo = new GameObject("FadeOverlay");
            overlayGo.transform.SetParent(canvasGo.transform);

            _overlayRect = overlayGo.AddComponent<RectTransform>();
            _overlayRect.anchorMin = Vector2.zero;
            _overlayRect.anchorMax = Vector2.one;
            _overlayRect.offsetMin = Vector2.zero;
            _overlayRect.offsetMax = Vector2.zero;

            var image = overlayGo.AddComponent<UnityEngine.UI.Image>();
            image.color = _fadeColor;
            image.raycastTarget = false;
        }

        /// <summary>
        /// 기본 지속 시간으로 페이드 인 (검은색 → 투명).
        /// </summary>
        public Coroutine FadeIn()
        {
            return FadeIn(_defaultDuration);
        }

        /// <summary>
        /// 지정된 시간 동안 페이드 인 (검은색 → 투명).
        /// </summary>
        /// <param name="duration">페이드 지속 시간 (초)</param>
        public Coroutine FadeIn(float duration)
        {
            if (_activeFadeRoutine != null)
                StopCoroutine(_activeFadeRoutine);

            _activeFadeRoutine = StartCoroutine(FadeCoroutine(1f, 0f, duration));
            return _activeFadeRoutine;
        }

        /// <summary>
        /// 기본 지속 시간으로 페이드 아웃 (투명 → 검은색).
        /// </summary>
        public Coroutine FadeOut()
        {
            return FadeOut(_defaultDuration);
        }

        /// <summary>
        /// 지정된 시간 동안 페이드 아웃 (투명 → 검은색).
        /// </summary>
        /// <param name="duration">페이드 지속 시간 (초)</param>
        public Coroutine FadeOut(float duration)
        {
            if (_activeFadeRoutine != null)
                StopCoroutine(_activeFadeRoutine);

            _activeFadeRoutine = StartCoroutine(FadeCoroutine(0f, 1f, duration));
            return _activeFadeRoutine;
        }

        /// <summary>
        /// 알파 값을 즉시 설정합니다.
        /// </summary>
        /// <param name="alpha">설정할 알파 값 (0=투명, 1=불투명)</param>
        public void SetAlpha(float alpha)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Clamp01(alpha);
                _canvasGroup.blocksRaycasts = alpha > 0.01f;
                _canvasGroup.interactable = alpha > 0.5f;
            }
        }

        /// <summary>
        /// 알파 값을 부드럽게 전환하는 코루틴.
        /// </summary>
        /// <param name="from">시작 알파</param>
        /// <param name="to">목표 알파</param>
        /// <param name="duration">지속 시간 (초)</param>
        private IEnumerator FadeCoroutine(float from, float to, float duration)
        {
            IsFading = true;

            if (_canvasGroup == null)
            {
                IsFading = false;
                yield break;
            }

            // 시작 알파 설정
            _canvasGroup.alpha = from;
            _canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 부드러운 페이드를 위해 SmoothStep 사용
                float smoothT = t * t * (3f - 2f * t);
                _canvasGroup.alpha = Mathf.Lerp(from, to, smoothT);
                yield return null;
            }

            // 최종 알파 설정
            _canvasGroup.alpha = to;
            _canvasGroup.blocksRaycasts = to > 0.01f;
            _canvasGroup.interactable = to > 0.5f;

            IsFading = false;
            _activeFadeRoutine = null;
        }
    }
}