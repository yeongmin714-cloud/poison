using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // CombatLog, CombatLogEntry, ProjectName.Systems.LogType

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round C — 전투 로그 창 (L 토글 / ESC 닫기, 스크롤 목록).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/CombatLogUI.cs, 데이터: Systems/CombatLog.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 원본 CombatLog.GetRecentEntries(100) 실측 — 별도 큐 불필요 (원본이 최대100 유지).
    ///   ② L키 토글 / ESC 닫기 (원본 입력 규약 동일). UTKWindowBase에 등록하지 않는 상시 HUD — 순수 VisualElement.
    ///   ③ 로그 타입(Normal/Damage/Heal/Kill/Warning)별 색상 라벨 + [MM:SS] 타임스탬프.
    ///   ④ "전체 지우기" 버튼 → CombatLog.Clear() (원본 데이터 동일 경로).
    ///   ⑤ 편의 정적 AddLog → 원본 CombatLog.AddEntry 위임 (피드 API 직접 접근).
    ///   ⑥ MonoBehaviour Updater 300ms 폴링 — 새 로그 반영.
    /// </summary>
    public class CombatLogUTK : VisualElement
    {
        private static CombatLogUTK _instance;
        public static CombatLogUTK Instance => _instance;

        private const float PollInterval = 0.3f;           // 300ms 폴링
        private const int MaxVisible = 100;                // 원본 MAX_VISIBLE_ENTRIES
        private const float EntryHeight = 22f;

        private readonly ScrollView _scroll;
        private readonly Button _clearBtn;
        private bool _isVisible;
        private int _lastRenderCount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Ensure();
            EnsureUpdater();
        }

        /// <summary>싱글턴 보장 — UIRoot에 부재 시 생성·부착. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            _instance = new CombatLogUTK();
            root.Add(_instance);
        }

        private static void EnsureUpdater()
        {
            if (_instance != null) return;
            var go = new GameObject("CombatLogUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>();
            Debug.Log("[CombatLogUTK] 부트스트랩 완료 — Updater 등록, 전투 로그 L키 준비");
        }

        private CombatLogUTK()
        {
            name = "CombatLogUIBar";
            style.position = Position.Absolute;
            style.right = 20f;
            style.top = 60f;
            style.width = 500f;
            style.height = 450f;
            style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
            style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
            style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
            style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
            style.display = DisplayStyle.None;

            // 제목
            var title = new Label("⚔️ 전투 기록");
            title.style.fontSize = 15f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(UTKColor.TextPrimary);
            title.style.paddingTop = 5f;
            title.style.paddingBottom = 4f;
            Add(title);

            // 스크롤 로그
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.name = "LogList";
            _scroll.style.flexGrow = 1f;
            Add(_scroll);

            var _tmp = new Label();   // 플렉스/높이 안정화용 스페이서
            _tmp.style.height = 0f;
            Add(_tmp);

            // 전체 지우기
            _clearBtn = UTKButton.Create("전체 지우기", () =>
            {
                CombatLog.Clear();
                _lastRenderCount = -1;
                Refresh();
                Debug.Log("[CombatLogUTK] 전체 로그 지움 (원본 CombatLog.Clear)");
            }, UTKButton.Variant.Danger);
            _clearBtn.style.marginTop = 6f;
            Add(_clearBtn);

            UTKWindowBase.ApplyUIToolkitFont(this);
            Refresh();
        }

        /// <summary>원본 피드 직접 접근 — CombatLog.AddEntry 위임.</summary>
        public static void AddLog(string message, ProjectName.Systems.LogType type = ProjectName.Systems.LogType.Normal)
        {
            CombatLog.AddEntry(message, type);
        }

        // ─────────────────────────── 입력 (L/ESC) ───────────────────────────

        private void HandleInput()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.L))
            {
                _isVisible = !_isVisible;
                style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                Debug.Log($"[CombatLogUTK] L키 — 로그 {(_isVisible ? "열림" : "닫힘")}");
                _lastRenderCount = -1;
                Refresh();
            }
            else if (_isVisible && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                _isVisible = false;
                style.display = DisplayStyle.None;
                Debug.Log("[CombatLogUTK] ESC — 로그 닫힘");
            }
        }

        // ─────────────────────────── 폴링 갱신 ───────────────────────────

        private void Refresh()
        {
            if (!_isVisible) return;
            var entries = CombatLog.GetRecentEntries(MaxVisible);
            if (entries == null) return;
            if (entries.Count == _lastRenderCount) return;

            _lastRenderCount = entries.Count;
            BuildList(entries);
        }

        private void BuildList(List<CombatLogEntry> entries)
        {
            _scroll.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.height = EntryHeight;
                row.name = "LogRow_";

                // [MM:SS] 타임스탬프
                int minutes = Mathf.FloorToInt(entry.timestamp / 60f);
                int seconds = Mathf.FloorToInt(entry.timestamp % 60f);
                var ts = new Label($"[{minutes:D2}:{seconds:D2}]");
                ts.style.width = 62f;
                ts.style.fontSize = 11f;
                ts.style.color = new StyleColor(UTKColor.TextSecondary);
                row.Add(ts);

                // 메시지 (타입별 색상)
                var msg = new Label(entry.message);
                msg.style.flexGrow = 1f;
                msg.style.fontSize = 13f;
                msg.style.color = new StyleColor(ColorForType(entry.type));
                row.Add(msg);

                _scroll.Add(row);
            }
            _scroll.scrollOffset = new Vector2(0f, float.MaxValue);   // 최신(하단)으로
        }

        private static Color ColorForType(ProjectName.Systems.LogType type)
        {
            switch (type)
            {
                case ProjectName.Systems.LogType.Damage:  return new Color(0.9f, 0.3f, 0.3f);
                case ProjectName.Systems.LogType.Heal:    return new Color(0.3f, 0.9f, 0.4f);
                case ProjectName.Systems.LogType.Kill:    return new Color(0.95f, 0.85f, 0.3f);
                case ProjectName.Systems.LogType.Warning: return new Color(1f, 0.5f, 0.0f);
                default:              return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        // ─────────────────────────── Updater ───────────────────────────

        /// <summary>MonoBehaviour 폴링 — UIRoot 부착 + 300ms 갱신 + 입력 처리.</summary>
        public class Updater : MonoBehaviour
        {
            private float _tick;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && _instance != null && _instance.parent == null)
                    root.Add(_instance);

                if (_instance == null) return;

                _instance.HandleInput();

                _tick -= Time.unscaledDeltaTime;
                if (_tick <= 0f)
                {
                    _tick = PollInterval;
                    if (_instance.parent != null)
                        _instance.Refresh();
                }
            }
        }
    }
}