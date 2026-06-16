using System.Collections;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-13: 암살 컷씬 시스템 — 흑백 연출로 영주 암살 장면을 재생합니다.
    /// 
    /// Unity Timeline에 의존하지 않는 간소화된 컷씬 시스템입니다.
    /// 시퀀스: FadeOut → 흑백 화면 → "암살" 텍스트 → 내레이션 텍스트 → FadeIn
    /// 
    /// 사용법:
    ///   AssassinationCutscene.AssassinateLord(territoryId);
    ///   (MonoBehaviour를 구현한 매니저에서 코루틴으로 실행)
    /// </summary>
    public static class AssassinationCutscene
    {
        // ===== 이벤트 =====

        /// <summary>컷씬이 시작되었을 때 발생 (territoryId)</summary>
        public static event System.Action<TerritoryId> OnCutsceneStarted;

        /// <summary>컷씬이 완료되었을 때 발생 (territoryId)</summary>
        public static event System.Action<TerritoryId> OnCutsceneCompleted;

        /// <summary>암살이 실행되었을 때 발생 (territoryId)</summary>
        public static event System.Action<TerritoryId> OnAssassinationExecuted;

        // ===== 상수 =====

        /// <summary>페이드 아웃 지속 시간 (초)</summary>
        public const float FADE_OUT_DURATION = 1.5f;

        /// <summary>검은 화면 유지 시간 (초)</summary>
        public const float BLACK_SCREEN_HOLD_TIME = 0.8f;

        /// <summary>"암살" 텍스트 표시 시간 (초)</summary>
        public const float TITLE_TEXT_DURATION = 2.0f;

        /// <summary>내레이션 텍스트 표시 시간 (초)</summary>
        public const float NARRATION_DURATION = 3.5f;

        /// <summary>페이드 인 지속 시간 (초)</summary>
        public const float FADE_IN_DURATION = 1.0f;

        // ===== 내레이션 텍스트 =====

        private static readonly Dictionary<TerritoryDifficulty, string[]> _narrationTexts = new Dictionary<TerritoryDifficulty, string[]>
        {
            { TerritoryDifficulty.Ring1, new[] {
                "조용한 밤, 성의 후문으로 독이 든 음식이 전달되었다.",
                "영주는 의심 없이 식탁에 앉았다. 그것이 마지막 식사였다.",
                "달빛 아래, 새로운 시대의 서막이 열렸다."
            }},
            { TerritoryDifficulty.Ring2, new[] {
                "축제의 소리가 성을 가득 채운 가운데, 독이 섞인 와인이 잔에 따라졌다.",
                "영주가 쓰러지자, 신하들은 혼란에 빠졌다.",
                "권력의 공백, 그리고 새로운 질서."
            }},
            { TerritoryDifficulty.Ring3, new[] {
                "경비가 삼엄한 성, 그러나 가장 가까운 자가 배신했다.",
                "한 방울의 독이 왕좌를 비웠다.",
                "백성을 위한 것인가, 야망을 위한 것인가. 역사가 기록하리라."
            }},
            { TerritoryDifficulty.Ring4, new[] {
                "거대한 요새도 내부의 적을 막을 순 없었다.",
                "영주는 자신의 침실에서, 가장 안전한 순간에 최후를 맞았다.",
                "제국의 기둥이 흔들린다."
            }},
            { TerritoryDifficulty.Empire, new[] {
                "황제의 식탁, 누구도 의심하지 않은 그 자리에서.",
                "독은 은쟁반에 담겨 황제 앞에 놓였다.",
                "역사가 바뀌는 순간, 아무도 눈치채지 못했다."
            }}
        };

        /// <summary>기본 내레이션 (국가/난이도 불명일 때)</summary>
        private static readonly string[] _defaultNarration = new[]
        {
            "어둠이 성을 뒤덮었다.",
            "독이 그의 운명을 결정지었다.",
            "새로운 주인이 도래했다."
        };

        // ===== GUID 스타일 (IMGUI) 텍스트 스타일 =====

        private static GUIStyle _titleStyle;
        private static GUIStyle _narrationStyle;
        private static bool _stylesInitialized;

        // ===== 내부 상태 =====

        private static bool _isPlaying;
        private static CoroutineRunner _runner;
        private static TerritoryId _currentTerritory;
        private static string _currentNarrationLine;

        // ===== 메인 퍼블릭 메서드 =====

        /// <summary>
        /// 암살 컷씬을 재생합니다. 흑백 오버레이 → "암살" 타이틀 → 내레이션 → 페이드 인 순서로 진행됩니다.
        /// CoroutineRunner MonoBehaviour가 자동 생성되어 코루틴을 실행합니다.
        /// </summary>
        /// <param name="territoryId">암살 대상 영지 ID</param>
        /// <param name="envoyName">특사 이름 (내레이션에 사용)</param>
        public static void AssassinateLord(TerritoryId territoryId, string envoyName = "특사")
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[AssassinationCutscene] 컷씬이 이미 재생 중입니다.");
                return;
            }

            _isPlaying = true;
            _currentTerritory = territoryId;

            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(territoryId);
            string lordName = def.lord.lordName ?? "영주";
            string territoryName = def.territoryName ?? "영지";

            // 난이도 기반 내레이션 선택
            var narrationPool = _narrationTexts.ContainsKey(def.difficulty)
                ? _narrationTexts[def.difficulty]
                : _defaultNarration;
            _currentNarrationLine = narrationPool[Random.Range(0, narrationPool.Length)];

            // 내레이션에 영주 이름 삽입
            _currentNarrationLine = _currentNarrationLine.Replace("영주", lordName);

            OnCutsceneStarted?.Invoke(territoryId);

            // CoroutineRunner 실행
            EnsureRunner();
            _runner.StartCoroutine(PlayCutsceneCoroutine(territoryId, lordName, territoryName, envoyName));
        }

        /// <summary>
        /// 컷씬이 현재 재생 중인지 확인합니다.
        /// </summary>
        public static bool IsPlaying => _isPlaying;

        /// <summary>
        /// 현재 재생 중인 영지 ID를 반환합니다. 없으면 기본값.
        /// </summary>
        public static TerritoryId CurrentTerritory => _currentTerritory;

        /// <summary>
        /// 컷씬을 강제로 중단합니다.
        /// </summary>
        public static void StopCutscene()
        {
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

        // ===== IMGUI OnGUI 호출 (MonoBehaviour의 OnGUI에서 호출) =====

        /// <summary>
        /// 컷씬 중 흑백 오버레이와 텍스트를 표시합니다.
        /// CoroutineRunner의 OnGUI에서 호출됩니다.
        /// </summary>
        public static void OnCutsceneGUI()
        {
            if (!_isPlaying) return;

            InitializeStyles();

            // 흑백 오버레이 (IMGUI fullscreen)
            Color originalColor = GUI.color;
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = originalColor;

            // 타이틀 "암살" — 화면 중앙 상단
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 100f), "암  살", _titleStyle);

            // 내레이션 텍스트 — 화면 중앙
            if (!string.IsNullOrEmpty(_currentNarrationLine))
            {
                GUI.Label(new Rect(60f, Screen.height * 0.5f, Screen.width - 120f, 200f), _currentNarrationLine, _narrationStyle);
            }
        }

        // ===== 내부 =====

        /// <summary>
        /// 컷씬 코루틴 — 시퀀스를 순서대로 실행합니다.
        /// </summary>
        private static IEnumerator PlayCutsceneCoroutine(TerritoryId territoryId, string lordName, string territoryName, string envoyName)
        {
            // 1. FadeOut (검은 화면)
            if (FadeManager.Instance != null)
                yield return FadeManager.Instance.FadeOut(FADE_OUT_DURATION);
            else
                yield return new WaitForSeconds(FADE_OUT_DURATION);

            // 2. 검은 화면 유지 (흑백 오버레이로 전환 준비)
            yield return new WaitForSeconds(BLACK_SCREEN_HOLD_TIME);

            // 3. 흑백 화면 + "암살" 텍스트 표시 (FadeManager는 투명으로 변경하여 IMGUI 오버레이가 보이게)
            if (FadeManager.Instance != null)
                FadeManager.Instance.SetAlpha(0f); // IMGUI 오버레이가 보이도록 페이드 제거

            yield return new WaitForSeconds(TITLE_TEXT_DURATION);

            // 4. 내레이션 텍스트 추가 표시
            yield return new WaitForSeconds(NARRATION_DURATION);

            // 5. 영주 사망 처리
            ExecuteAssassination(territoryId);

            // 6. FadeIn (다시 밝아짐)
            if (FadeManager.Instance != null)
                yield return FadeManager.Instance.FadeIn(FADE_IN_DURATION);
            else
                yield return new WaitForSeconds(FADE_IN_DURATION);

            // 완료
            _isPlaying = false;
            _currentNarrationLine = null;

            OnCutsceneCompleted?.Invoke(territoryId);

            Debug.Log($"[AssassinationCutscene] 🎬 암살 컷씬 완료: {lordName} ({territoryId})");
        }

        /// <summary>
        /// 실제 암살 실행 — 영주 사망 및 영지 소유권 변경.
        /// PoisonTakeoverSystem.OnLordPoisoned과 동일한 효과를 냅니다.
        /// </summary>
        private static void ExecuteAssassination(TerritoryId territoryId)
        {
            var db = TerritoryDatabase.Instance;
            var state = db.GetState(territoryId);
            if (state == null) return;

            // 영지 소유권 변경
            state.ownership = TerritoryOwnership.PlayerOwned;
            state.lordExecuted = true;
            state.lordSurrendered = true;

            OnAssassinationExecuted?.Invoke(territoryId);

            Debug.Log($"[AssassinationCutscene] ⚔️ {db.GetDefinition(territoryId).territoryName} 영주 암살 완료!");
        }

        /// <summary>
        /// 정적 스타일 초기화
        /// </summary>
        private static void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Screen.height * 0.08f),
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState { textColor = Color.white }
            };
            _titleStyle.normal.background = null;
            _titleStyle.wordWrap = false;

            _narrationStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Screen.height * 0.03f),
                fontStyle = FontStyle.Normal,
                normal = new GUIStyleState { textColor = new Color(0.9f, 0.9f, 0.9f, 0.95f) }
            };
            _narrationStyle.normal.background = null;
            _narrationStyle.wordWrap = true;

            _stylesInitialized = true;
        }

        /// <summary>
        /// CoroutineRunner MonoBehaviour가 없으면 생성하여 연결합니다.
        /// </summary>
        private static void EnsureRunner()
        {
            if (_runner != null) return;

            var go = new GameObject("[AssassinationCutsceneRunner]");
            _runner = go.AddComponent<CoroutineRunner>();
            Object.DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용)
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
            _currentTerritory = default;
            _currentNarrationLine = null;
            _stylesInitialized = false;
        }

        /// <summary>
        /// 코루틴 실행을 위한 내부 MonoBehaviour.
        /// OnGUI에서 AssassinationCutscene.OnCutsceneGUI를 호출하여 IMGUI 오버레이를 렌더링합니다.
        /// </summary>
        private class CoroutineRunner : MonoBehaviour
        {
            private void OnGUI()
            {
                AssassinationCutscene.OnCutsceneGUI();
            }
        }
    }
}