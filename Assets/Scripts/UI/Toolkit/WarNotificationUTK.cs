using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // WarNotificationUI

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round C — 전쟁 알림 배너 (top-right 스택).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/Systems/WarNotificationUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 원본 WarNotificationUI.ActiveNotifications(읽기 전용, 최대 5)를 실측해
    ///      우상단에 최신순 배너 스택으로 표시 (8초 자동 제거는 원본이 담당).
    ///   ② 알림 유형(WarStart/WarEnd/TerritoryLost/TerritoryGained/Info)별 색/아이콘 매핑 반영.
    ///   ③ 원본 이력 로그(History)는 그대로 두고 표시만 위임.
    ///   ④ MonoBehaviour Updater 300ms 폴링 — 배너 외부 조작(ShowNotification)이 반영됨.
    ///
    /// "표시 경로 실측": 원본은 우상단 NOTIFICATION_TOP_MARGIN=60px 부터 아래로 쌓음.
    ///   본 UTK는 우상단(top=60, right=10, width=35%ratio)에 동일 방향(최신 상단) 배너로 재현.
    /// </summary>
    public class WarNotificationUTK : VisualElement
    {
        private static WarNotificationUTK _instance;
        public static WarNotificationUTK Instance => _instance;

        private const float PollInterval = 0.3f;          // 300ms 폴링
        private const float BannerWidthPct = 0.35f;       // 원본 NOTIFICATION_WIDTH_RATIO
        private const float BannerHeight = 50f;           // 원본 NOTIFICATION_HEIGHT
        private const float BannerSpacing = 4f;           // 원본 NOTIFICATION_SPACING

        private readonly Dictionary<WarNotificationUI.NotificationType, (string prefix, Color color)> _typeMap;
        private int _lastRenderCount = -1;
        private readonly List<VisualElement> _banners = new List<VisualElement>();

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
            _instance = new WarNotificationUTK();
            root.Add(_instance);
        }

        private static void EnsureUpdater()
        {
            if (_instance != null) return;
            var go = new GameObject("WarNotificationUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>();
            Debug.Log("[WarUTK] 부트스트랩 완료 — Updater 등록, 전쟁 알림 배너 준비");
        }

        private WarNotificationUTK()
        {
            name = "WarNotifications";
            style.position = Position.Absolute;
            style.top = 60f;                                   // 원본 NOTIFICATION_TOP_MARGIN
            style.right = 10f;
            style.width = new Length(BannerWidthPct * 100f, LengthUnit.Percent);
            pickingMode = PickingMode.Ignore;

            _typeMap = new Dictionary<WarNotificationUI.NotificationType, (string, Color)>
            {
                { WarNotificationUI.NotificationType.WarStart,       ("⚔️", new Color(0.80f, 0.20f, 0.20f)) },
                { WarNotificationUI.NotificationType.WarEnd,         ("🏁", new Color(0.90f, 0.60f, 0.10f)) },
                { WarNotificationUI.NotificationType.TerritoryLost,  ("🏴", new Color(0.50f, 0.00f, 0.50f)) },
                { WarNotificationUI.NotificationType.TerritoryGained,("🏳️", new Color(0.10f, 0.70f, 0.20f)) },
                { WarNotificationUI.NotificationType.Info,          ("📢", new Color(0.30f, 0.50f, 0.80f)) },
            };

            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        // ─────────────────────────── 폴링 갱신 ───────────────────────────

        private void Refresh()
        {
            var active = WarNotificationUI.ActiveNotifications;
            if (active == null) return;

            if (active.Count == _lastRenderCount && active.Count == _banners.Count)
                return;   // 개수 기준 불필요 재렌더 방지 (라벨 텍스트 갱신은 안 함 — 성능 우선)

            _lastRenderCount = active.Count;
            foreach (var banner in _banners)
                banner.RemoveFromHierarchy();
            _banners.Clear();

            // 최신순: 원본은 인덱스 뒤(최신)를 위에 쌓음.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var entry = active[i];
                var b = BuildBanner(entry);
                if (b != null)
                {
                    Add(b);
                    _banners.Add(b);
                }
            }
            Debug.Log($"[WarUTK] 배너 갱신 — 활성 {active.Count}개 렌더");
        }

        private VisualElement BuildBanner(WarNotificationUI.NotificationEntry entry)
        {
            var banner = new VisualElement();
            banner.name = "WarBanner";
            banner.style.height = 10f;   // 플렉스 하위 축소 방지
            banner.style.marginBottom = BannerSpacing;
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.alignItems = Align.Center;
            banner.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.85f));
            banner.style.borderTopWidth = 1f;
            banner.style.borderBottomWidth = 1f;
            banner.style.borderLeftWidth = 4f;
            banner.style.borderRightWidth = 1f;
            banner.style.borderLeftColor = new StyleColor(BannerColor(entry.type));

            var label = new Label(BannerPrefix(entry.type) + " " + entry.message);
            label.style.flexGrow = 1f;
            label.style.flexShrink = 1f;
            label.style.paddingLeft = 6f;
            label.style.paddingRight = 6f;
            label.style.fontSize = 15f;
            label.style.color = new StyleColor(BannerColor(entry.type));
            label.style.textOverflow = TextOverflow.Ellipsis;
            banner.Add(label);
            return banner;
        }

        private string BannerPrefix(WarNotificationUI.NotificationType type)
        {
            return _typeMap.TryGetValue(type, out var v) ? v.prefix : "📢";
        }

        private Color BannerColor(WarNotificationUI.NotificationType type)
        {
            return _typeMap.TryGetValue(type, out var v) ? v.color : new Color(0.30f, 0.50f, 0.80f);
        }

        // ─────────────────────────── Updater ───────────────────────────

        /// <summary>MonoBehaviour 폴링 — UIRoot 부착 + 300ms 갱신.</summary>
        public class Updater : MonoBehaviour
        {
            private float _tick;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && _instance != null && _instance.parent == null)
                    root.Add(_instance);

                if (_instance == null) return;
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