using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 71-G2] 처치 콤보 카운터 — 3s 창 내 연속 처치 계수, 2연속부터 "xN" 팝업.
    ///  - 창 기준: Time.realtimeSinceStartup(unscaled — 히트스톱/슬로모 중에도 정합, VignettePulse 선례).
    ///  - 이중 훅 정합: 동일 처치가 PlayKillVFX(CombatVFXController 훅) + AnimalAI 직접 훅 양측에서 등록될 수 있어
    ///    KillDedupSec(0.25s) 내 재등록은 동일 처치로 무시(시간 기반 디듀프 — 결정론, 위치 비교 없음).
    ///  - 팝업: DamageNumberRunner(CombatVFXController:554 OnGUI 러너)와 동일 패턴의 신규 러너 — 기존 러너 무수정.
    ///    GUI 스타일 static 캐시(OnGUI 내 라인당 new 금지 선례), 8방향 외곽선(Phase J-3 선례), 유한 수명.
    ///  - 색: 데미지 숫자 Critical 골드와 동일 단일 소스 — FXPalette.Accent(1, 0.9, 0.5)
    ///    (PlayerCombat.cs:1967 Critical 경로 numberColor = FXPalette.Accent 소스 위임).
    ///  - 결정론 — Random 0건. EditMode 무동작 가드.
    /// </summary>
    public static class FxKillComboCounter
    {
        /// <summary>[71-G2] 콤보 창(초) — 이 시간 안의 연속 처치를 계수(unscaled realtime 기준).</summary>
        public const float WindowSec = 3f;

        /// <summary>[71-G2] 이중 훅 디듀프 창(초) — 이 시간 내 재등록은 동일 처치로 무시(양 훅 수렴 정합).</summary>
        public const float KillDedupSec = 0.25f;

        /// <summary>[71-G2] 팝 시작 계수 — 2연속부터 팝(1연속=단발은 무연출).</summary>
        public const int MinCombo = 2;

        /// <summary>[71-G2] 팝업 수명(초) — 유한 수명(누출 금지).</summary>
        public const float PopupLifetime = 1.2f;

        private static float _lastKillRealtime = -999f;
        private static int _count;

        /// <summary>[무번호 71-G2] 처치 등록 — 3s 창 계수, 2연속부터 최신 처치 지점 상단에 "xN" 팝업.</summary>
        public static void RegisterKill(Vector3 position)
        {
            if (!Application.isPlaying) return;   // EditMode 무동작 가드
            try
            {
                float now = Time.realtimeSinceStartup;   // unscaled — 히트스톱/슬로모 정합
                if (now - _lastKillRealtime <= KillDedupSec) return;   // 동일 처치 이중 훅 무시
                if (now - _lastKillRealtime <= WindowSec) _count++;    // 3s 창 내 연속 처치 계수
                else _count = 1;
                _lastKillRealtime = now;
                if (_count < MinCombo) return;   // 2연속부터 팝
                var host = new GameObject("KillComboPopup");
                // 최신 처치 지점 상단 — XP 팝업(position+1.5) 위(+2.2)로 겹침 방지.
                host.transform.position = position + Vector3.up * 2.2f;
                host.AddComponent<KillComboPopupRunner>().Init(_count);
            }
            catch (System.Exception) { }   // 연출 보조 — 전투 흐름 절대 방해 금지
        }

        /// <summary>[71-G2] 콤보 팝업 러너 — DamageNumberRunner(OnGUI) 동일 패턴. 위로 부양 + 페이드 + 유한 수명.</summary>
        private class KillComboPopupRunner : MonoBehaviour
        {
            private string _text;
            private float _elapsed;
            private Camera _cam;
            private GUIContent _guiContent;
            private float _popScale = 1.35f;   // 팝 시작 스케일(DamageNumberRunner 선례)

            // GUI 스타일 static 캐시 — GUI.skin 접근은 OnGUI 내부에서만 1회 수행(DamageNumberRunner 관례).
            private static GUIStyle _styleCache;
            private static GUIStyle _outlineStyleCache;
            private static float _styleScale = -1f;

            public void Init(int combo)
            {
                _text = "x" + combo.ToString();
                _elapsed = 0f;
                _cam = Camera.main;
                _popScale = 1.35f;
            }

            private void Update()
            {
                _elapsed += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 재생
                if (_elapsed < 0.15f) _popScale = Mathf.Lerp(1.35f, 1f, _elapsed / 0.15f);   // 팝 1.35→1.0 ease-out
                else _popScale = 1f;
                transform.position += Vector3.up * (1.2f * Time.unscaledDeltaTime);   // 위로 부양(DamageNumberRunner 선례)
                if (_elapsed >= PopupLifetime) Destroy(gameObject);   // 유한 수명
            }

            private static void EnsureStyles()
            {
                // 해상도 비례 스타일 스케일(DamageNumberRunner.GetStyleScale 동일 공식).
                float s = Mathf.Clamp(Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f)), 0.5f, 2.5f);
                if (_styleCache != null && _outlineStyleCache != null && Mathf.Approximately(_styleScale, s)) return;
                _styleScale = s;
                _styleCache = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(32 * s),   // 크리티컬 등급 크기(32px) — Critical 데미지 숫자 등급 승계
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                _outlineStyleCache = new GUIStyle(_styleCache)
                {
                    normal = { textColor = new Color(0, 0, 0, 0.92f) }   // 흑색 외곽선(Phase J-3 선례)
                };
            }

            private static readonly Vector2[] OutlineStep =
            {
                new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
                new Vector2(-1, 0),                         new Vector2(1, 0),
                new Vector2(-1, 1),  new Vector2(0, 1),  new Vector2(1, 1)
            };

            private void OnGUI()
            {
                if (string.IsNullOrEmpty(_text)) return;
                if (_cam == null)
                {
                    _cam = Camera.main;
                    if (_cam == null) return;
                }
                if (_guiContent == null) _guiContent = new GUIContent(_text);
                if (_styleCache == null || _outlineStyleCache == null)
                {
                    try { EnsureStyles(); }
                    catch { return; }
                }
                if (_styleCache == null || _outlineStyleCache == null) return;

                Vector3 screenPos = _cam.WorldToScreenPoint(transform.position);
                if (screenPos.z < 0) return;
                screenPos.y = Screen.height - screenPos.y;

                // [71-G2] Critical 데미지 숫자와 동일 골드 단일 소스 — FXPalette.Accent 위임.
                var gold = FXPalette.Accent;
                float alpha = Mathf.Lerp(1f, 0f, _elapsed / PopupLifetime);
                _styleCache.normal.textColor = new Color(gold.r, gold.g, gold.b, alpha);
                _outlineStyleCache.normal.textColor = new Color(0, 0, 0, alpha);

                Vector2 textSize = _styleCache.CalcSize(_guiContent);
                float scaledWidth = textSize.x * _popScale;
                float scaledHeight = textSize.y * _popScale;
                var rect = new Rect(screenPos.x - scaledWidth * 0.5f, screenPos.y - scaledHeight * 0.5f, scaledWidth, scaledHeight);

                // 8방향 흑색 외곽선 — 잔디/바위 위 판독 보장(DamageNumberRunner Phase J-3 선례).
                float thickness = 3f * _styleScale;   // 큰 등급 3px(isBig 선례)
                for (int i = 0; i < OutlineStep.Length; i++)
                {
                    GUI.Label(new Rect(rect.x + OutlineStep[i].x * thickness,
                                       rect.y + OutlineStep[i].y * thickness,
                                       rect.width, rect.height), _guiContent, _outlineStyleCache);
                }
                GUI.Label(rect, _guiContent, _styleCache);
            }
        }
    }
}
