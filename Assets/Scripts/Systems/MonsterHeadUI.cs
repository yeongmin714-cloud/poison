using System.Collections;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_10 몬스터 헤드 UI (IMGUI/OnGUI) — 이름 + 레벨 + HP바.
    ///
    /// [동작] 몬스터 머리 위 지점(모델 bounds 기반)을 카메라로 투영해 IMGUI로 그린다.
    ///  - 뒷면(vp.z &lt; 0) / 화면 밖(마진 40px) / 거리 40m 초과 시 렌더 스킵.
    ///  - headHeight = 모델 Renderer 전체 bounds.extents.y * 1.35 (최소 1.2m).
    ///    Start에서 1회 계산 + 2초 후 갱신(AnimalAI.Start의 티어별 localScale 적용 반영).
    ///  - 스타일은 Awake에서 1회 생성(OnGUI 내 생성 금지). 텍스트는 2회 렌더(검정 오프셋 + 본색)로 그림자.
    ///  - world→screen은 GUI 좌표계와 y축이 반전되므로 guiY = Screen.height - vp.y.
    ///  - IMGUI는 리치텍스트 미지원이므로 레벨 색은 GUIStyle.normal.textColor에 직접 적용.
    ///    티어는 MonsterLevelManager.EstimateTierByName(표시명) 기준:
    ///    Beginner=녹 / Intermediate=노랑 / Advanced=주황 / 그 외(Boss 등장 시)=빨강.
    /// </summary>
    public class MonsterHeadUI : MonoBehaviour
    {
        [Header("참조 (SpawnMonster 훅에서 Setup으로 주입)")]
        [SerializeField] private AnimalAI _ai;

        [Header("표시 옵션")]
        [SerializeField] private float _maxDistance = 40f;   // 이 거리(m) 초과 시 스킵
        [SerializeField] private float _screenMargin = 40f;  // 화면 밖 마진(px)

        // ── 상수 ──
        private const float BarWidth = 90f;          // HP바 폭
        private const float BarHeight = 8f;          // HP바 높이
        private const float MinHeadHeight = 1.2f;    // 머리 높이 하한(m)
        private const float HeadHeightFactor = 1.35f; // bounds.extents.y 배율
        private const float BoundsRefreshDelay = 2f; // bounds 재계산 지연
        private const float CamRefreshInterval = 0.5f; // Camera.main 재조회 주기

        // ── 캐시 ──
        private Camera _cam;
        private float _camNextRefresh;
        private float _headHeight = MinHeadHeight;
        private MonsterLevelManager _levelMgr; // Update에서 지연 해결(OnGUI에서의 생성 부작용 방지)

        // ── 스타일 (Awake 1회 생성 — OnGUI 생성 금지) ──
        private GUIStyle _nameStyle;   // 폰트 12
        private GUIStyle _levelStyle;  // 폰트 12
        private GUIStyle _hpStyle;     // 폰트 11
        private bool _stylesReady;

        // ── 색 팔레트 ──
        private static readonly Color ColorBeginner = new Color(0.30f, 0.78f, 0.35f);      // 녹
        private static readonly Color ColorIntermediate = new Color(0.95f, 0.85f, 0.25f);  // 노랑
        private static readonly Color ColorAdvanced = new Color(0.95f, 0.55f, 0.20f);      // 주황
        private static readonly Color ColorBossFallback = new Color(0.85f, 0.20f, 0.20f);  // 빨강(Boss/미지정)
        private static readonly Color ColorHpGreen = new Color(0.30f, 0.78f, 0.35f);
        private static readonly Color ColorHpYellow = new Color(0.95f, 0.85f, 0.25f);
        private static readonly Color ColorHpRed = new Color(0.85f, 0.20f, 0.20f);
        private static readonly Color ColorBarBg = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color ColorShadow = new Color(0f, 0f, 0f, 0.85f);

        // ================================================================
        // 부착 API
        // ================================================================

        /// <summary>
        /// 몬스터에 헤드 UI를 부착한다. AnimalAI 필수 — 없으면 null 반환 + 경고 1회.
        /// </summary>
        public static MonsterHeadUI AttachTo(GameObject monster)
        {
            if (monster == null) return null;

            AnimalAI ai = monster.GetComponent<AnimalAI>();
            if (ai == null)
            {
                Debug.LogWarning($"[MonsterHeadUI] ⚠️ '{monster.name}'에 AnimalAI 없음 — 헤드 UI 부착 생략");
                return null;
            }

            MonsterHeadUI head = monster.AddComponent<MonsterHeadUI>();
            head.Setup(ai);
            return head;
        }

        /// <summary>AnimalAI 참조 주입 (TestTerritoryCombatSetup.SpawnMonster 훅에서 호출).</summary>
        public void Setup(AnimalAI ai)
        {
            _ai = ai;
        }

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            CreateStyles(); // OnGUI에서 생성 금지 — 여기서 1회만
        }

        private void Start()
        {
            ComputeHeadHeight();                  // 1회
            StartCoroutine(RefreshBoundsLater()); // 2초 후 갱신(티어 스케일 반영)
        }

        private void Update()
        {
            // 레벨 매니저 지연 해결 — Instance getter는 부재 시 자동 생성하므로
            // OnGUI가 아닌 Update에서 해결해 OnGUI는 캐시만 소비한다.
            if (_levelMgr == null)
                _levelMgr = MonsterLevelManager.Instance;
        }

        // ================================================================
        // 렌더
        // ================================================================

        private void OnGUI()
        {
            if (!_stylesReady || _ai == null) return;

            RefreshCamera();
            if (_cam == null) return;

            Vector3 headPos = transform.position + Vector3.up * _headHeight;
            float dist = Vector3.Distance(_cam.transform.position, headPos);
            if (dist > _maxDistance) return; // 거리 스킵

            Vector3 vp = _cam.WorldToScreenPoint(headPos);
            if (vp.z < 0f) return; // 뒷면
            if (vp.x < -_screenMargin || vp.x > Screen.width + _screenMargin ||
                vp.y < -_screenMargin || vp.y > Screen.height + _screenMargin)
                return; // 화면 밖(마진 40px)

            float guiX = vp.x;
            float guiY = Screen.height - vp.y; // GUI 좌표계 y 반전

            string displayName = ResolveDisplayName();
            Color tierColor = ResolveTierColor(displayName);
            string levelText = ResolveLevelText();

            // 레이아웃: [이름] / [Lv.N] / [HP바] / [HP숫자] — guiY가 머리 위치이므로 블록을 위로 그린다
            const float NameW = 160f;
            const float NameH = 16f;
            const float LvH = 15f;
            const float HpTextH = 13f;
            float blockH = NameH + LvH + BarHeight + HpTextH + 4f;
            float top = guiY - blockH;

            float y = top;
            DrawShadowed(new Rect(guiX - NameW * 0.5f, y, NameW, NameH), displayName, _nameStyle, Color.white);
            y += NameH + 1f;
            DrawShadowed(new Rect(guiX - NameW * 0.5f, y, NameW, LvH), levelText, _levelStyle, tierColor);
            y += LvH + 2f;

            // HP바 — 배경(0,0,0,0.75) → 잔량 채움
            float ratio = _ai.MaxHP > 0f ? Mathf.Clamp01(_ai.CurrentHP / _ai.MaxHP) : 0f;
            Rect bgRect = new Rect(guiX - BarWidth * 0.5f, y, BarWidth, BarHeight);
            GUI.color = ColorBarBg;
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            if (ratio > 0f)
            {
                Rect fillRect = new Rect(bgRect.x, bgRect.y, BarWidth * ratio, BarHeight);
                GUI.color = HpColor(ratio);
                GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            y += BarHeight + 1f;

            // HP 숫자 "cur/max" (작게 11px)
            string hpText = $"{Mathf.CeilToInt(_ai.CurrentHP)}/{Mathf.CeilToInt(_ai.MaxHP)}";
            DrawShadowed(new Rect(guiX - NameW * 0.5f, y, NameW, HpTextH), hpText, _hpStyle, Color.white);
        }

        /// <summary>텍스트 2회 렌더(검정 오프셋 +1,+1 → 본색)로 살짝한 그림자 처리.</summary>
        private static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color)
        {
            Color prev = style.normal.textColor;
            style.normal.textColor = ColorShadow;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = prev;
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        /// <summary>카메라 캐시 갱신 (Camera.main null 가드 — 호출부에서 null 확인).</summary>
        private void RefreshCamera()
        {
            if (_cam != null && Time.unscaledTime < _camNextRefresh) return;
            _cam = Camera.main;
            _camNextRefresh = Time.unscaledTime + CamRefreshInterval;
        }

        /// <summary>
        /// 표시명: MonsterDatabase.displayName 우선, 없으면 _monsterId.
        /// (AnimalAI에는 displayName 프로퍼티가 없어 기존 로그 패턴과 동일하게 DB 우선조회)
        /// </summary>
        private string ResolveDisplayName()
        {
            string id = _ai != null ? _ai.MonsterId : null;
            if (string.IsNullOrEmpty(id)) return "?";

            MonsterDef def = MonsterDatabase.Get(id);
            if (def != null && !string.IsNullOrEmpty(def.displayName)) return def.displayName;
            return id;
        }

        /// <summary>
        /// 레벨 문자열: "Lv.N" (MonsterLevelManager.GetLevelDisplay 사용).
        /// 매니저 미해결 시 "Lv.?". GetLevelDisplay의 이모지 접두(🟢 등)는 IMGUI 기본 폰트에
        /// 글리프가 없어 토문자로 깨지므로 "Lv."부터 잘라 사용하고 색은 티어색으로 대신한다.
        /// </summary>
        private string ResolveLevelText()
        {
            if (_levelMgr == null) return "Lv.?";
            string raw = _levelMgr.GetLevelDisplay(_ai.Level); // "🟢 Lv.5"
            int idx = raw.IndexOf("Lv.", System.StringComparison.Ordinal);
            return idx >= 0 ? raw.Substring(idx) : raw;
        }

        /// <summary>
        /// 티어 색 매핑 — EstimateTierByName(표시명) 기반.
        /// MonsterTier는 Beginner/Intermediate/Advanced 3값(MonsterData.cs) — Boss 미정의,
        /// 추후 추가 대비 default는 빨강.
        /// </summary>
        private Color ResolveTierColor(string displayName)
        {
            MonsterTier tier = _levelMgr != null
                ? _levelMgr.EstimateTierByName(displayName)
                : MonsterTier.Beginner;

            switch (tier)
            {
                case MonsterTier.Beginner: return ColorBeginner;
                case MonsterTier.Intermediate: return ColorIntermediate;
                case MonsterTier.Advanced: return ColorAdvanced;
                default: return ColorBossFallback;
            }
        }

        /// <summary>HP 잔량 색: ≥0.6 녹 / ≥0.3 노랑 / 나머지 빨강.</summary>
        private static Color HpColor(float ratio)
        {
            if (ratio >= 0.6f) return ColorHpGreen;
            if (ratio >= 0.3f) return ColorHpYellow;
            return ColorHpRed;
        }

        /// <summary>
        /// headHeight 계산 — 모델 Renderer 전체 bounds.extents.y * 1.35 (최소 1.2m).
        /// </summary>
        private void ComputeHeadHeight()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                _headHeight = MinHeadHeight;
                return;
            }

            try
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);

                _headHeight = Mathf.Max(bounds.extents.y * HeadHeightFactor, MinHeadHeight);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MonsterHeadUI] bounds 계산 예외(무시, 최소 높이 사용): {ex.GetType().Name}: {ex.Message}");
                _headHeight = MinHeadHeight;
            }
        }

        private IEnumerator RefreshBoundsLater()
        {
            yield return new WaitForSeconds(BoundsRefreshDelay);
            ComputeHeadHeight(); // AnimalAI.Start의 티어별 localScale 적용 후 갱신
        }

        /// <summary>스타일 생성 — Awake 1회(폰트 12/11, MiddleCenter, white).</summary>
        private void CreateStyles()
        {
            Font font = LoadBuiltinFont();

            _nameStyle = NewStyle(font, 12);
            _levelStyle = NewStyle(font, 12);
            _hpStyle = NewStyle(font, 11);
            _stylesReady = true;
        }

        private static GUIStyle NewStyle(Font font, int fontSize)
        {
            var s = new GUIStyle
            {
                font = font,
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            s.normal.textColor = Color.white;
            return s;
        }

        /// <summary>폰트 로드 — P7-1: 한글 서포트 커스텀 폰트 우선, 실패 시 빌트인 폴백.
        /// Systems asmdef는 ProjectName.UI 참조 불가 → Resources.Load를 로컬로 수행.</summary>
        private static Font LoadBuiltinFont()
        {
            try
            {
                Font kr = Resources.Load<Font>("Fonts/NotoSansKR-VF"); // Assets/Resources/Fonts/
                if (kr == null) kr = Resources.Load<Font>("Fonts/malgun"); // 폴백
                if (kr != null) return kr;
            }
            catch { /* 다음 후보 진행 */ }

            try
            {
                Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 2022.2+
                if (f != null) return f;
            }
            catch { /* 다음 후보 진행 */ }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf"); // 구버전 폴백
            }
            catch
            {
                return null;
            }
        }
    }
}
