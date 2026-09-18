using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;       // PlayerHealth / PlayerStats / PlayerInventory
using ProjectName.Systems;    // QuickSlotManager
using ProjectName.UI;         // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round A — 메인 HUD 통합 (HUD 1098줄 IMGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/HUD.cs — 절대 수정 금지.
    ///
    /// [기능] 비윈도우 상시 HUD (HotbarUIUTK/QuickSlotUTK 패턴 — rootVisualElement 직속):
    ///   ① 상단좌: 체력 바 + ❤ + 수치 (원본 DrawHearts/DrawHPNumberText 소스
    ///      PlayerHealth 실측 — CurrentHP / MaxHP / HPRatio).
    ///   ② 하단: 스킬 퀵슬롯 (원본 QuickSlotManager SLOT_COUNT=6 실측, 아이콘 + 키 라벨 1~6).
    ///   ③ 하단 우측: 경험치/레벨 바 (원본 DrawExpBar 소스 PlayerStats 실측 —
    ///      Level / CurrentEXP / GetExpForLevel, MaxLevel(50) MAX 처리).
    ///   ④ 250ms 폴링 갱신 — schedule.Execute().Every(250ms) → IVisualElementScheduledItem.Pause 정지.
    ///   ⑤ static Ensure() — 멱등 부트스트랩 (UIToolkitBootstrap.Ensure 선례).
    ///
    /// [규약] foreach/for(색인 슬롯), 컴포넌트 클래스 public(CS0050), UnityEngine.Debug,
    ///        IStyle 4면 개별 속성, 삼항연산자, 보간문자열 중첩따옴표 금지.
    ///        Unity 배치 실행 금지. 초기화 로그는 1회만 — 폴링 중 로그 금지.
    /// </summary>
    public class HUDUTK : VisualElement
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static HUDUTK _instance;
        public static HUDUTK Instance => _instance;

        /// <summary>멱등 부트스트랩 보장 — UIToolkitBootstrap.Ensure 스타일 (여러 번 호출해도 1회만 생성).</summary>
        public static void Ensure()
        {
            if (_instance != null && _instance.parent != null) return;
            _instance = new HUDUTK();
            var go = new GameObject("HUDUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MonoKeeper>().hud = _instance;
            Debug.Log("[HUDUTK] 초기화 완료 — 체력/퀵슬롯/경험치 HUD 준비");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null && _instance.parent != null) return;
            Ensure();
        }

        // ===== 설정 =====
        private const int    PollMs     = 250;   // 폴링 주기 (요구사항)
        private const float  QuickSize  = 56f;
        private const float  QuickGap   = 6f;
        private const float  Bottom     = 12f;   // 하단 정렬 기준
        private readonly Color _heartColor = new Color(0.9f, 0.15f, 0.15f, 1f);
        private readonly Color _expFillColor = new Color(0.55f, 0.75f, 1f, 1f);
        private readonly Color _expBorderColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        private readonly Color _fillBgColor = new Color(0.08f, 0.10f, 0.16f, 0.85f);

        // 체력/경험치
        private VisualElement _hpFill;
        private Label         _hpText;
        private VisualElement _hpBorder;
        private Label         _levelText;      // [U8] BuildExpBar 바인딩
        private VisualElement _expFill;        // [U8] BuildExpBar 바인딩
        private Label         _expText;        // [U8] BuildExpBar 바인딩
        private Label         _expValueText;   // [U8] BuildExpBar 바인딩
        private VisualElement _staminaFill;    // [예시 정합] 스태미너 바
        private VisualElement _circles;        // [예시 정합] 원형 게이지 호스트
        private VisualElement _ringHpFill;      // [예시 정합] 원형 HP 게이지
        private VisualElement _ringStaminaFill; // [예시 정합] 원형 스태미너 게이지
        private UnityEngine.UIElements.IVisualElementScheduledItem _pollTask; // [U8] 폴링
        // [U8 정리] 퀵슬롯 섹션 제거 — HotbarUIUTK(아이템 핫바 1~8)가 담당 (중복 슬롯 은퇴)

        private HUDUTK()
        {
            // [U8 수리] 풀스크린 Ignore 오버레이 — 자식 절대좌표가 화면 기준으로 잡히고
            // pickingMode Ignore로 게임 클릭을 막지 않는다 (클러스터 미표시 뿌리 수리)
            style.position = Position.Absolute;
            style.left = 0f; style.right = 0f; style.top = 0f; style.bottom = 0f;
            pickingMode = PickingMode.Ignore;

            BuildRings();
            // [U8 은퇴] EXP/상태 바 제거 — 사용자 지정 (핫바 위 녹색 바)

            UTKWindowBase.ApplyUIToolkitFont(this);

            StartPolling();
        }


        /// <summary>[예시 정합] 원형 게이지 갱신 — HP/스태미너 비율.</summary>
        private void RefreshRings()
        {
            // [예시 정합] 핫바 왼쪽 끝에 인접 배치 (핫바 8슬롯×72px 중앙 정렬 가정)
            if (_circles != null && panel != null)
            {
                float half = panel.visualTree.worldBound.width * 0.5f;
                _circles.style.left = half - 288f - 110f;
            }
            var ph = PlayerHealth.Instance;
            float hpRatio = ph != null && ph.MaxHP > 0f ? Mathf.Clamp01(ph.CurrentHP / ph.MaxHP) : 0f;
            var pm = Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>();
            float stRatio = pm != null ? pm.StaminaRatio : 0f;
            if (_ringHpFill != null)
                _ringHpFill.style.height = new Length(hpRatio * 100f, LengthUnit.Percent);
            if (_ringStaminaFill != null)
                _ringStaminaFill.style.height = new Length(stRatio * 100f, LengthUnit.Percent);
        }

        /// <summary>[예시 정합] 스태미너 바 갱신 — PlayerMovement.StaminaRatio 실측.</summary>
        private void RefreshStamina()
        {
            // [예시 정합] 스태미너 바 제거 — 원형 게이지(RefreshRings)로 통합 갱신
            RefreshRings();
        }


        // =====================================================================
        // ③ 하단 우측 경험치/레벨 바 (PlayerStats 실측)
        // =====================================================================
        /// <summary>[예시 정합] 원형 게이지 2종 — 골드 링+하단 게이지 채움(HP/스태미너), 핫바 왼쪽 인접.</summary>
        private void BuildRings()
        {
            _circles = new VisualElement();
            _circles.name = "ResourceRings";
            _circles.style.position = Position.Absolute;
            _circles.style.bottom = 12f;
            _circles.style.flexDirection = FlexDirection.Row;
            Add(_circles);

            string[] icons = { "❤", "⚡" };
            for (int ci = 0; ci < icons.Length; ci++)
            {
                var ring = new VisualElement();
                ring.style.width = 46f;
                ring.style.height = 46f;
                ring.style.backgroundColor = new StyleColor(new Color(0.14f, 0.09f, 0.05f, 0.9f));
                ring.style.borderTopWidth = 2f; ring.style.borderBottomWidth = 2f;
                ring.style.borderLeftWidth = 2f; ring.style.borderRightWidth = 2f;
                ring.style.borderTopColor = new StyleColor(UTKColor.BorderGold);
                ring.style.borderBottomColor = new StyleColor(UTKColor.BorderGold);
                ring.style.borderLeftColor = new StyleColor(UTKColor.BorderGold);
                ring.style.borderRightColor = new StyleColor(UTKColor.BorderGold);
                ring.style.borderTopLeftRadius = 23f; ring.style.borderTopRightRadius = 23f;
                ring.style.borderBottomLeftRadius = 23f; ring.style.borderBottomRightRadius = 23f;
                ring.style.overflow = Overflow.Hidden;
                ring.style.marginRight = 6f;
                _circles.Add(ring);

                var fill = new VisualElement();
                fill.style.position = Position.Absolute;
                fill.style.left = 0f; fill.style.right = 0f; fill.style.bottom = 0f;
                fill.style.height = new Length(100f, LengthUnit.Percent);
                fill.style.backgroundColor = ci == 0
                    ? new StyleColor(new Color(0.82f, 0.31f, 0.31f, 0.9f))
                    : new StyleColor(new Color(0.95f, 0.78f, 0.30f, 0.9f));
                ring.Add(fill);

                var icon = new Label(icons[ci]);
                icon.style.position = Position.Absolute;
                icon.style.left = 0f; icon.style.right = 0f; icon.style.top = 0f; icon.style.bottom = 0f;
                icon.style.unityTextAlign = TextAnchor.MiddleCenter;
                icon.style.fontSize = 18f;
                icon.style.color = new StyleColor(UTKColor.TextPrimary);
                ring.Add(icon);

                if (ci == 0) _ringHpFill = fill;
                else _ringStaminaFill = fill;
            }
        }

        private void BuildExpBar()
        {
            var host = new VisualElement();
            host.name = "ExpHost";
            host.style.position = Position.Absolute;
            host.style.right = 18f;
            host.style.bottom = Bottom;
            host.style.flexDirection = FlexDirection.Column;
            host.style.alignItems = Align.FlexEnd;
            Add(host);

            _levelText = new Label("Lv.1");
            _levelText.name = "LevelText";
            _levelText.style.fontSize = 14f;
            _levelText.style.color = new StyleColor(UTKColor.TextPrimary);
            host.Add(_levelText);

            var barWrap = new VisualElement();
            barWrap.style.position = Position.Relative;
            barWrap.style.width = 300f;
            barWrap.style.height = 18f;
            barWrap.style.marginTop = 4f;
            host.Add(barWrap);

            _expFill = new VisualElement();
            _expFill.name = "ExpFill";
            _expFill.style.position = Position.Absolute;
            _expFill.style.left = 0f;
            _expFill.style.top = 0f;
            _expFill.style.bottom = 0f;
            _expFill.style.backgroundColor = new StyleColor(_fillBgColor);
            _expFill.style.width = new Length(100f, LengthUnit.Percent);
            barWrap.Add(_expFill);

            var expFront = new VisualElement();
            expFront.name = "ExpFront";
            expFront.style.position = Position.Absolute;
            expFront.style.left = 0f;
            expFront.style.top = 0f;
            expFront.style.bottom = 0f;
            expFront.style.backgroundColor = new StyleColor(_expFillColor);
            expFront.style.width = new Length(0f, LengthUnit.Percent);
            barWrap.Add(expFront);
            _expFill = expFront;

            _expValueText = new Label("0/100");
            _expValueText.name = "ExpValue";
            _expValueText.style.position = Position.Absolute;
            _expValueText.style.left = 0f;
            _expValueText.style.right = 0f;
            _expValueText.style.top = 0f;
            _expValueText.style.bottom = 0f;
            _expValueText.style.fontSize = 12f;
            _expValueText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _expValueText.style.color = new StyleColor(UTKColor.TextPrimary);
            barWrap.Add(_expValueText);
            _expText = _expValueText;
        }

        // =====================================================================
        // ④ 폴링 — schedule.Execute().Every(250ms) → Pause 정지 (낙하 규약 준수)
        // =====================================================================
        private void StartPolling()
        {
            if (_pollTask != null) return;
            _pollTask = schedule.Execute(() =>
            {
                if (parent != null) RefreshAll();
            }).Every(PollMs);
        }

        private void StopPolling()
        {
            if (_pollTask != null)
            {
                _pollTask.Pause();
                _pollTask = null;
            }
        }

        // =====================================================================
        //  데이터 갱신 (원본 실측 경로 — 로그 금지, 폴링 중 무음)
        // =====================================================================
        private void RefreshAll()
        {
            RefreshRings();     // [예시 정합] 원형 게이지 갱신(HP/스태미너 통합)
        }

        private void RefreshExpBar()
        {
            var ps = PlayerStats.Instance;
            if (ps == null) return;

            int level = ps.Level;
            bool isMax = level >= PlayerStats.MaxLevel;

            int curExp;
            int spanExp;
            float ratio;
            if (isMax)
            {
                curExp = 0;
                spanExp = 0;
                ratio = 1f;
            }
            else
            {
                curExp = ps.CurrentEXP - ps.GetExpForLevel(level);
                spanExp = ps.GetExpForLevel(level + 1) - ps.GetExpForLevel(level);
                if (spanExp <= 0) spanExp = 1;
                ratio = Mathf.Clamp01((float)curExp / spanExp);
            }

            if (_expFill != null)
                _expFill.style.width = new Length(ratio * 100f, LengthUnit.Percent);
            if (_levelText != null)
                _levelText.text = "Lv." + level;
            if (_expText != null)
                _expText.text = isMax ? "MAX" : curExp + "/" + spanExp;
        }


        // =====================================================================
        //  부착용 MonoKeeper — HotbarUIUTK.Updater 관례 (UIRoot 부착)
        // =====================================================================
        private class MonoKeeper : MonoBehaviour
        {
            public HUDUTK hud;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && hud != null && hud.parent == null)
                    root.Add(hud);
            }
        }
    }
}