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
                { WarNotificationUI.NotificationType.WarStart,       ("⚔️", UTKColor.HealthRed) },
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
            var typeColor = BannerColor(entry.type);
            var banner = new VisualElement();
            banner.name = "WarBanner";
            banner.style.height = BannerHeight;
            banner.style.flexShrink = 0f;
            banner.style.marginBottom = BannerSpacing;
            banner.style.paddingLeft = 10f;
            banner.style.paddingRight = 10f;
            banner.style.paddingTop = 6f;
            banner.style.paddingBottom = 6f;
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.alignItems = Align.Center;
            banner.style.backgroundColor = new StyleColor(UTKTheme.Panel);
            banner.style.borderTopWidth = 1f;
            banner.style.borderBottomWidth = 1f;
            banner.style.borderLeftWidth = 3f;
            banner.style.borderRightWidth = 1f;
            banner.style.borderTopColor = new StyleColor(UTKTheme.Stroke);
            banner.style.borderBottomColor = new StyleColor(UTKTheme.Stroke);
            banner.style.borderLeftColor = new StyleColor(typeColor);
            banner.style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            banner.style.borderTopLeftRadius = 8f;
            banner.style.borderTopRightRadius = 8f;
            banner.style.borderBottomLeftRadius = 8f;
            banner.style.borderBottomRightRadius = 8f;

            // 유형 배지 — 아이콘을 어두운 카드 위에서 독립적으로 강조한다.
            var badge = new VisualElement();
            badge.name = "WarTypeBadge";
            badge.style.width = 32f;
            badge.style.height = 32f;
            badge.style.flexShrink = 0f;
            badge.style.marginRight = 10f;
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            badge.style.backgroundColor = new StyleColor(new Color(typeColor.r, typeColor.g, typeColor.b, 0.18f));
            badge.style.borderTopLeftRadius = 6f;
            badge.style.borderTopRightRadius = 6f;
            badge.style.borderBottomLeftRadius = 6f;
            badge.style.borderBottomRightRadius = 6f;

            var icon = new Label(BannerPrefix(entry.type));
            icon.style.fontSize = 16.8f;
            icon.style.color = new StyleColor(typeColor);
            badge.Add(icon);
            banner.Add(badge);

            var content = new VisualElement();
            content.style.flexGrow = 1f;
            content.style.flexShrink = 1f;
            content.style.justifyContent = Justify.Center;

            var typeLabel = new Label(BannerTypeLabel(entry.type));
            typeLabel.style.fontSize = 12f;
            typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            typeLabel.style.color = new StyleColor(typeColor);
            typeLabel.style.marginBottom = 2f;
            typeLabel.style.textOverflow = TextOverflow.Ellipsis;
            typeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            content.Add(typeLabel);

            var message = new Label(entry.message ?? string.Empty);
            message.style.flexShrink = 1f;
            message.style.fontSize = 13.2f;
            message.style.color = new StyleColor(UTKTheme.TextMain);
            message.style.textOverflow = TextOverflow.Ellipsis;
            message.style.whiteSpace = WhiteSpace.NoWrap;
            content.Add(message);
            banner.Add(content);
            return banner;
        }

        private string BannerPrefix(WarNotificationUI.NotificationType type)
        {
            return _typeMap.TryGetValue(type, out var v) ? v.prefix : "📢";
        }

        private static string BannerTypeLabel(WarNotificationUI.NotificationType type)
        {
            switch (type)
            {
                case WarNotificationUI.NotificationType.WarStart: return "전쟁 발생";
                case WarNotificationUI.NotificationType.WarEnd: return "전쟁 종료";
                case WarNotificationUI.NotificationType.TerritoryLost: return "영토 상실";
                case WarNotificationUI.NotificationType.TerritoryGained: return "영토 획득";
                default: return "알림";
            }
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