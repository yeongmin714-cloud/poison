using System.Collections;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-17: 왕 독살 오프닝 컷씬 시스템.
    /// 게임 시작 시 자동으로 재생되는 오프닝 컷씬입니다.
    /// 시퀀스: FadeIn from black → "왕이 독살당했다" 텍스트 → 나레이션 스크롤 → FadeOut → 게임 시작
    /// Space/ESC 키로 스킵할 수 있습니다.
    /// </summary>
    public static class OpeningCutscene
    {
        // ===== 이벤트 =====

        /// <summary>컷씬이 시작되었을 때 발생</summary>
        public static event System.Action OnCutsceneStarted;

        /// <summary>컷씬이 완료되었을 때 발생</summary>
        public static event System.Action OnCutsceneCompleted;

        /// <summary>컷씬이 스킵되었을 때 발생</summary>
        public static event System.Action OnCutsceneSkipped;

        // ===== 상수 =====

        /// <summary>페이드 인 지속 시간 (초)</summary>
        public const float FADE_IN_DURATION = 1.0f;

        /// <summary>타이틀 텍스트 표시 시간 (초)</summary>
        public const float TITLE_TEXT_DURATION = 2.5f;

        /// <summary>내레이션 텍스트 표시 시간 (초)</summary>
        public const float NARRATION_DURATION = 4.0f;

        /// <summary>페이드 아웃 지속 시간 (초)</summary>
        public const float FADE_OUT_DURATION = 1.5f;

        /// <summary>PlayerPrefs 키 — 이미 오프닝을 본 적이 있는지 확인</summary>
        private const string HAS_SEEN_OPENING_KEY = "OpeningCutscene_Seen";

        // ===== 내레이션 텍스트 =====

        /// <summary>오프닝 내레이션 텍스트 배열 (한 줄씩 순차 표시)</summary>
        private static readonly string[] _narrationLines = new[]
        {
            "왕이 독살당했다...",
            "모든 영지가 혼란에 빠졌다...",
            "당신은 이 혼란을 틈타 세력을 키워야 한다..."
        };

        // ===== IMGUI 스타일 =====

        private static GUIStyle _titleStyle;
        private static GUIStyle _narrationStyle;
        private static bool _stylesInitialized;

        // ===== 내부 상태 =====

        private static bool _isPlaying;
        private static CoroutineRunner _runner;
        private static int _currentNarrationIndex;
        private static float _narrationTimer;

        // ===== 메인 퍼블릭 메서드 =====

        /// <summary>
        /// 오프닝 컷씬이 아직 재생되지 않았으면 재생합니다.
        /// PlayerPrefs로 한 번만 재생됨을 보장합니다.
        /// </summary>
        /// <param name="forcePlay">true이면 PlayerPrefs를 무시하고 강제 재생</param>
        public static void PlayIfNeeded(bool forcePlay = false)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[OpeningCutscene] 컷씬이 이미 재생 중입니다.");
                return;
            }

            if (!forcePlay && HasBeenSeen())
            {
                Debug.Log("[OpeningCutscene] 이미 재생된 오프닝, 스킵합니다.");
                return;
            }

            PlayCutscene();
        }

        /// <summary>
        /// 오프닝 컷씬을 재생합니다.
        /// </summary>
        public static void PlayCutscene()
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[OpeningCutscene] 컷씬이 이미 재생 중입니다.");
                return;
            }

            _isPlaying = true;
            _currentNarrationIndex = 0;
            _narrationTimer = 0f;

            OnCutsceneStarted?.Invoke();

            EnsureRunner();
            _runner.StartCoroutine(PlayCutsceneCoroutine());
        }

        /// <summary>
        /// 오프닝을 이미 보았는지 확인합니다.
        /// </summary>
        public static bool HasBeenSeen()
        {
            return PlayerPrefs.GetInt(HAS_SEEN_OPENING_KEY, 0) == 1;
        }

        /// <summary>
        /// 오프닝 시청 상태를 초기화하여 다음 게임 시작 시 다시 재생되도록 합니다.
        /// </summary>
        public static void ResetSeenState()
        {
            PlayerPrefs.DeleteKey(HAS_SEEN_OPENING_KEY);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 오프닝을 본 것으로 표시합니다 (테스트 또는 디버그용).
        /// </summary>
        public static void MarkAsSeen()
        {
            PlayerPrefs.SetInt(HAS_SEEN_OPENING_KEY, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 컷씬이 현재 재생 중인지 확인합니다.
        /// </summary>
        public static bool IsPlaying => _isPlaying;

        /// <summary>
        /// 현재 표시 중인 내레이션 인덱스 (-1 = 타이틀 표시 중).
        /// </summary>
        public static int CurrentNarrationIndex => _currentNarrationIndex - 1;

        /// <summary>
        /// 컷씬을 강제로 중단하고 종료 상태로 전환합니다.
        /// </summary>
        public static void StopCutscene()
        {
            if (!_isPlaying) return;

            _isPlaying = false;
            if (_runner != null)
            {
                _runner.StopAllCoroutines();
            }

            // 페이드 인으로 복구
            if (FadeManager.Instance != null)
            {
                FadeManager.Instance.FadeIn(FADE_IN_DURATION);
            }
        }

        // ===== IMGUI OnGUI =====

        /// <summary>
        /// 컷씬 중 텍스트를 화면에 렌더링합니다.
        /// CoroutineRunner의 OnGUI에서 호출됩니다.
        /// </summary>
        public static void OnCutsceneGUI()
        {
            if (!_isPlaying) return;

            InitializeStyles();

            // 검은색 반투명 배경 오버레이
            Color originalColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = originalColor;

            if (_currentNarrationIndex == 0)
            {
                // 타이틀: "왕이 독살당했다"
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 120f), "왕이 독살당했다", _titleStyle);
            }
            else
            {
                // 내레이션 텍스트 (순차 표시)
                int lineIndex = _currentNarrationIndex - 1;
                if (lineIndex >= 0 && lineIndex < _narrationLines.Length)
                {
                    string narration = _narrationLines[lineIndex];
                    GUI.Label(new Rect(60f, Screen.height * 0.4f, Screen.width - 120f, Screen.height * 0.4f), narration, _narrationStyle);
                }
            }

            // 스킵 안내
            GUIStyle skipStyle = new GUIStyle
            {
                alignment = TextAnchor.LowerRight,
                fontSize = Mathf.RoundToInt(Screen.height * 0.02f),
                normal = new GUIStyleState { textColor = new Color(0.6f, 0.6f, 0.6f, 0.6f) }
            };
            GUI.Label(new Rect(Screen.width - 200f, Screen.height - 60f, 180f, 40f), "Press ESC/Space to skip", skipStyle);
        }

        // ===== 내부 =====

        /// <summary>
        /// 컷씬 코루틴 — 전체 시퀀스를 순서대로 실행합니다.
        /// </summary>
        private static IEnumerator PlayCutsceneCoroutine()
        {
            // 1. FadeIn from black (검은 화면에서 밝아짐)
            if (FadeManager.Instance != null)
            {
                FadeManager.Instance.SetAlpha(1f);
                yield return FadeManager.Instance.FadeIn(FADE_IN_DURATION);
            }
            else
            {
                yield return new WaitForSeconds(FADE_IN_DURATION);
            }

            // 2. 타이틀 텍스트 표시: "왕이 독살당했다"
            _currentNarrationIndex = 0; // 타이틀 모드
            yield return new WaitForSeconds(TITLE_TEXT_DURATION);

            // 스킵 체크
            if (!_isPlaying) yield break;

            // 3. 내레이션 텍스트 순차 표시
            for (int i = 0; i < _narrationLines.Length; i++)
            {
                _currentNarrationIndex = i + 1;
                _narrationTimer = 0f;

                float lineDuration = NARRATION_DURATION / _narrationLines.Length;
                while (_narrationTimer < lineDuration)
                {
                    if (!_isPlaying) yield break; // 스킵됨
                    _narrationTimer += Time.deltaTime;
                    yield return null;
                }
            }

            // 스킵 체크
            if (!_isPlaying) yield break;

            // 4. FadeOut (다시 검은 화면)
            if (FadeManager.Instance != null)
            {
                yield return FadeManager.Instance.FadeOut(FADE_OUT_DURATION);
            }
            else
            {
                yield return new WaitForSeconds(FADE_OUT_DURATION);
            }

            // 5. 완료 처리
            CompleteOpening();
        }

        /// <summary>
        /// 오프닝 완료 처리 — 상태 저장 및 이벤트 발생.
        /// </summary>
        private static void CompleteOpening()
        {
            _isPlaying = false;
            _currentNarrationIndex = 0;
            _narrationTimer = 0f;

            // 다시 보지 않도록 표시
            MarkAsSeen();

            OnCutsceneCompleted?.Invoke();

            Debug.Log("[OpeningCutscene] 🎬 왕 독살 오프닝 완료");
        }

        /// <summary>
        /// 컷씬 스킵 처리.
        /// </summary>
        private static void SkipCutscene()
        {
            if (!_isPlaying) return;

            Debug.Log("[OpeningCutscene] ⏭️ 오프닝 스킵");

            // 현재 코루틴 중단
            if (_runner != null)
            {
                _runner.StopAllCoroutines();
            }

            _isPlaying = false;
            _currentNarrationIndex = 0;
            _narrationTimer = 0f;

            MarkAsSeen();
            OnCutsceneSkipped?.Invoke();
            OnCutsceneCompleted?.Invoke();

            // 페이드 인으로 복구
            if (FadeManager.Instance != null)
            {
                FadeManager.Instance.FadeIn(FADE_IN_DURATION);
            }
        }

        /// <summary>
        /// IMGUI 스타일을 초기화합니다.
        /// </summary>
        private static void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Screen.height * 0.07f),
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState { textColor = new Color(0.95f, 0.2f, 0.2f, 1f) } // 붉은색 강조
            };
            _titleStyle.normal.background = null;
            _titleStyle.wordWrap = false;

            _narrationStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Screen.height * 0.035f),
                fontStyle = FontStyle.Normal,
                normal = new GUIStyleState { textColor = new Color(1f, 1f, 1f, 0.95f) }
            };
            _narrationStyle.normal.background = null;
            _narrationStyle.wordWrap = true;

            _stylesInitialized = true;
        }

        /// <summary>
        /// CoroutineRunner MonoBehaviour가 없으면 생성합니다.
        /// </summary>
        private static void EnsureRunner()
        {
            if (_runner != null) return;

            var go = new GameObject("[OpeningCutsceneRunner]");
            _runner = go.AddComponent<CoroutineRunner>();
            Object.DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용).
        /// </summary>
        public static void ResetAll()
        {
            if (_runner != null)
            {
                _runner.StopAllCoroutines();
                Object.DestroyImmediate(_runner.gameObject);
                _runner = null;
            }

            _isPlaying = false;
            _currentNarrationIndex = 0;
            _narrationTimer = 0f;
            _stylesInitialized = false;
        }

        // ===== 내부 CoroutineRunner =====

        /// <summary>
        /// 코루틴 실행 및 IMGUI 렌더링을 위한 내부 MonoBehaviour.
        /// OnGUI에서 OpeningCutscene.OnCutsceneGUI를 호출하고,
        /// Update에서 ESC/Space 키 입력을 감지하여 스킵 처리합니다.
        /// </summary>
        private class CoroutineRunner : MonoBehaviour
        {
            private void OnGUI()
            {
                OpeningCutscene.OnCutsceneGUI();
            }

            private void Update()
            {
                if (!_isPlaying) return;

                // ESC 또는 Space 키로 스킵
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
                {
                    SkipCutscene();
                }
            }
        }
    }
}
