using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // HerbPickup

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round C — 약초 리스폰 알림 (좌상단 컴팩트 리스트).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/HerbRespawnUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 원본 HerbPickup 실측 — IsHarvested / RespawnProgress / RespawnTimeLeft / transform.position.
    ///   ② 원본과 동일 탐색: FindObjectsByType&lt;HerbPickup&gt;, 0.5초 캐시 갱신.
    ///   ③ 원본과 동일 거리 컬링: 카메라 30m(_maxDisplayDistance) 초과 제외.
    ///   ④ 리스폰 중 약초만 좌상단 리스트로 [재생성 중 X.X초] + 색 게이지 표시.
    ///   ⑤ 채집 가능 약초는 [E] 채집 라벨로 별도 색 표시.
    ///   ⑥ 원본은 월드 3D 오버레이(IMGUI) — 본 UTK는 화면 좌상단 리스트(overlay panel)로 재현.
    ///   ⑦ MonoBehaviour Updater 300ms 폴링.
    /// </summary>
    public class HerbRespawnUTK : VisualElement
    {
        private static HerbRespawnUTK _instance;
        public static HerbRespawnUTK Instance => _instance;

        private const float PollInterval = 0.3f;      // 300ms 폴링
        private const float SearchInterval = 0.5f;    // 원본 FindObjectsByType 캐시 주기
        private const float MaxDisplayDistance = 30f; // 원본 _maxDisplayDistance
        private const float GaugeWidth = 120f;
        private const float GaugeHeight = 8f;

        private HerbPickup[] _herbCache;
        private float _searchTimer = SearchInterval;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private Camera _mainCamera;
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
            _instance = new HerbRespawnUTK();
            root.Add(_instance);
        }

        private static void EnsureUpdater()
        {
            if (_instance != null) return;
            var go = new GameObject("HerbRespawnUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>();
            Debug.Log("[HerbUTK] 부트스트랩 완료 — Updater 등록, 약초 리스폰 알림 준비");
        }

        private HerbRespawnUTK()
        {
            name = "HerbRespawn";
            style.position = Position.Absolute;
            style.left = 10f;
            style.top = 60f;
            style.width = 220f;
            style.paddingTop = 10f;
            style.paddingBottom = 10f;
            style.paddingLeft = 12f;
            style.paddingRight = 12f;
            style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = new StyleColor(UTKColor.IronLine);
            style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            style.borderRightColor = new StyleColor(UTKColor.IronLine);
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;
            pickingMode = PickingMode.Ignore;

            var title = new Label("🌿 약초 상태");
            title.style.fontSize = 13.2f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(UTKColor.TextPrimary);
            title.style.paddingTop = 2f;
            title.style.paddingBottom = 8f;
            title.style.marginBottom = 6f;
            title.style.borderBottomWidth = 1f;
            title.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            Add(title);

            _mainCamera = Camera.main;
            UTKWindowBase.ApplyUIToolkitFont(this);
            Refresh();
        }

        // ─────────────────────────── 탐색 / 폴링 ───────────────────────────

        private void Refresh()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // 원본과 동일 0.5초 캐시 탐색 — 매 폴링마다 FindObjectsByType 호출 방지
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
            {
                _herbCache = GameObject.FindObjectsByType<HerbPickup>();
                _searchTimer = SearchInterval;
            }
            if (_herbCache == null || _herbCache.Length == 0)
            {
                Rebuild(new List<HerbPickup>());
                return;
            }

            // 30m 컬링 + 리스폰/채집 상태 수집
            Vector3 camPos = _mainCamera.transform.position;
            var matches = new List<HerbPickup>();
            foreach (HerbPickup herb in _herbCache)
            {
                if (herb == null) continue;
                if (Vector3.Distance(herb.transform.position, camPos) > MaxDisplayDistance) continue;
                matches.Add(herb);
            }
            Rebuild(matches);
        }

        private void Rebuild(List<HerbPickup> matches)
        {
            int count = matches.Count;
            if (count == _lastRenderCount) return;   // 개수 기준 불필요 재렌더 방지

            _lastRenderCount = count;
            foreach (var row in _rows)
                row.RemoveFromHierarchy();
            _rows.Clear();

            foreach (HerbPickup herb in matches)
            {
                var r = BuildRow(herb);
                if (r != null)
                {
                    Add(r);
                    _rows.Add(r);
                }
            }
            bool any = count > 0;
            style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            if (any)
                Debug.Log($"[HerbUTK] 약초 {count}개 상태 표시");
        }

        private VisualElement BuildRow(HerbPickup herb)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 8f;
            row.name = "HerbRow";

            var details = new VisualElement();
            details.style.flexDirection = FlexDirection.Row;
            details.style.alignItems = Align.Center;
            row.Add(details);

            var status = new Label();
            status.style.fontSize = 12f;
            status.style.flexGrow = 1f;
            status.style.color = new StyleColor(UTKColor.TextSecondary);
            details.Add(status);

            var timer = new Label();
            timer.style.fontSize = 12f;
            timer.style.color = new StyleColor(UTKColor.TextPrimary);
            details.Add(timer);

            var gauge = new VisualElement();
            gauge.style.width = GaugeWidth;
            gauge.style.height = GaugeHeight;
            gauge.style.marginTop = 5f;
            gauge.style.backgroundColor = new StyleColor(UTKColor.BgPanelDark);
            gauge.style.borderTopLeftRadius = 4f;
            gauge.style.borderTopRightRadius = 4f;
            gauge.style.borderBottomLeftRadius = 4f;
            gauge.style.borderBottomRightRadius = 4f;
            row.Add(gauge);

            var fill = new VisualElement();
            fill.style.height = GaugeHeight;
            fill.style.width = 0f;
            fill.style.borderTopLeftRadius = 4f;
            fill.style.borderTopRightRadius = 4f;
            fill.style.borderBottomLeftRadius = 4f;
            fill.style.borderBottomRightRadius = 4f;
            gauge.Add(fill);

            if (herb.IsHarvested)
            {
                // 리스폰 중 — 원본 타이머 및 진행률 계산 유지
                float remaining = herb.RespawnTimeLeft;
                float progress = herb.RespawnProgress;   // 0=방금수확 1=곧리스폰
                status.text = "재생성 중";
                timer.text = $"{remaining.ToString("F1")}초";
                fill.style.width = new Length(GaugeWidth * progress, LengthUnit.Pixel);
                fill.style.backgroundColor = new StyleColor(GaugeColor(progress));
            }
            else
            {
                // 채집 가능
                status.text = "[E] 채집 가능";
                timer.text = "준비 완료";
                status.style.color = new StyleColor(UTKColor.TextPrimary);
                fill.style.width = new Length(GaugeWidth, LengthUnit.Pixel);
                fill.style.backgroundColor = new StyleColor(new Color(0.2f, 0.9f, 0.2f));
            }
            return row;
        }

        /// <summary>진행률 그라데이션: 0→1 = 녹→노랑→빨강 (원본 Color.Lerp 2구간 재현).</summary>
        private static Color GaugeColor(float progress)
        {
            var green = new Color(0.2f, 0.9f, 0.2f);
            var yellow = new Color(0.9f, 0.9f, 0.2f);
            var red = new Color(0.9f, 0.2f, 0.2f);
            if (progress < 0.5f)
                return Color.Lerp(green, yellow, progress / 0.5f);
            return Color.Lerp(yellow, red, (progress - 0.5f) / 0.5f);
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