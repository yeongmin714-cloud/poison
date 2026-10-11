using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// 공용 "E키로 상호작용" 프롬프트 — Figma interaction-panel(82:45) 재현.
    /// 어떤 E 상호작용이든(채집/채광/농경/문/자물쇠/낚시/잠자기/영주실 등) 대상이 가능한 상태로
    /// 플레이어 근접하면 대상 머리 위에 표시. 월드 앵커 HUD(NameplateOverlayUTK 패턴).
    ///
    /// 기본 감지: HerbPickup(Gather) / ResourceNode(Mine) / FarmPlot(Farm·소유·성장중 제외) /
    ///             LockedDoor(Door·잠김).
    /// 확장: Register(transform, label, range)로 임의 시스템(낚시/잠자기/영주실 등) 등록 가능.
    /// 채널링 진행 중 또는 창 열림 시 숨김.
    /// </summary>
    public class EInteractionPromptUTK : MonoBehaviour
    {
        // ── Figma 82:45 (288×52.8 → ÷1.2 논리 240×44) ──
        private const float PanelW = 240f;
        private const float PanelH = 44f;
        private const float KeyCapSize = 28f;
        private const float LabelFontSize = 13.2f;      // Figma fontSize 유지(÷1.2 안 함 — InteractionPanelUTK 선례)

        private const float ScanInterval = 0.2f;
        private const float ScreenMargin = 24f;
        private const float CamRefreshInterval = 0.5f;

        private static EInteractionPromptUTK _instance;
        private static bool _spawnRequested;

        // 커스텀 등록(custom) — 낚시/잠자기/영주실/기타 E 시스템이 등록.
        private sealed class Entry { public Transform tr; public string label; public float range; }
        private static readonly List<Entry> _custom = new List<Entry>(8);

        private VisualElement _overlayRoot;
        private VisualElement _panel;
        private Label _label;
        private float _rootW, _rootH;
        private Camera _cam;
        private float _camNextRefresh;
        private float _scanNext;
        private Transform _player;
        private float _playerNextRefresh;

        public static EInteractionPromptUTK Instance => _instance;

        /// <summary>전 씬 자동 배선 — Systems→UI 직접 참조가 불가(asmdef 단방향)하므로 UI 내부 부트스트랩.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EInteractionPromptBridge.OnRegisterRequested += Register;
            EInteractionPromptBridge.OnUnregisterRequested += Unregister;
            Ensure();
        }

        public static void Ensure()
        {
            if (_instance != null || _spawnRequested) return;
            _spawnRequested = true;
            var go = new GameObject("EInteractionPromptUTK");
            DontDestroyOnLoad(go);
            go.AddComponent<EInteractionPromptUTK>();
        }

        /// <summary>임의 E 상호작용 등록(채집/채광/농경/문은 기본 감지). fishing/sleep/lordroom 등용.</summary>
        public static void Register(Transform target, string label, float range)
        {
            if (target == null) return;
            _custom.RemoveAll(e => e.tr == target);
            _custom.Add(new Entry { tr = target, label = label, range = Mathf.Max(0.5f, range) });
        }

        public static void Unregister(Transform target)
        {
            if (target == null) return;
            _custom.RemoveAll(e => e.tr == target);
        }

        private void OnEnable() => _instance = this;

        private void Update()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            if (_overlayRoot == null || _overlayRoot.panel == null)
                AttachToRoot(root);
            if (_overlayRoot == null) return;

            RefreshCamera();
            if (_cam == null) return;

            // 창 열림 or 채널링 진행 중 → 숨김.
            if (ProjectName.Core.UITransitionState.AnyWindowOpen || GatherChannelSystem.Instance.IsActive)
            {
                _panel.style.display = DisplayStyle.None;
                return;
            }

            _rootW = _overlayRoot.worldBound.width;
            _rootH = _overlayRoot.worldBound.height;
            if (_rootW <= 0f || _rootH <= 0f) return;

            var candidate = FindNearestInteractable();
            if (candidate == null || candidate.tr == null)
            {
                _panel.style.display = DisplayStyle.None;
                return;
            }

            Vector3 vp = _cam.WorldToViewportPoint(candidate.tr.position + Vector3.up * candidate.headOffset);
            if (vp.z <= 0f)
            {
                _panel.style.display = DisplayStyle.None;
                return;
            }
            float x = vp.x * _rootW;
            float y = (1f - vp.y) * _rootH;

            float uiScaleX = _rootW / FigmaCanvasLayout.CanvasWidth;
            float uiScaleY = _rootH / FigmaCanvasLayout.CanvasHeight;
            float w = PanelW * uiScaleX;
            float h = PanelH * uiScaleY;
            // 머리 위, 수평 중앙, 화면 밖 방지.
            float left = Mathf.Clamp(x - w / 2f, ScreenMargin, Mathf.Max(ScreenMargin, _rootW - w - ScreenMargin));
            float top = y - h - 8f;
            top = Mathf.Clamp(top, ScreenMargin, Mathf.Max(ScreenMargin, _rootH - h - ScreenMargin));

            if (_label.text != candidate.label)
                _label.text = candidate.label;
            _panel.style.left = left;
            _panel.style.top = top;
            _panel.style.width = w;
            _panel.style.height = h;
            _panel.style.display = DisplayStyle.Flex;
        }

        // ── 대상 스캔 (0.2s 스로틀, 타입별 scratch 재사용 → GC 최소) ──
        private sealed class Candidate
        {
            public Transform tr;
            public string label;
            public float headOffset;
        }

        private readonly List<HerbPickup> _herbs = new List<HerbPickup>(16);
        private readonly List<ResourceNode> _nodes = new List<ResourceNode>(16);
        private readonly List<FarmPlot> _plots = new List<FarmPlot>(16);
        private readonly List<LockedDoor> _doors = new List<LockedDoor>(16);
        private Candidate _lastCandidate;

        private Candidate FindNearestInteractable()
        {
            if (Time.unscaledTime < _scanNext)
                return _lastCandidate;
            _scanNext = Time.unscaledTime + ScanInterval;

            RefreshPlayer();
            if (_player == null)
                return _lastCandidate = null;

            Vector3 playerPos = _player.position;
            float bestSqr = float.MaxValue;
            Candidate best = null;

            // HerbPickup(Gather) — Ready된 밭은 HerbPickup 부착이라 농경 수확도 이 경로로 커버.
            Object.FindObjectsByType<HerbPickup>(FindObjectsSortMode.None).ToListShell(_herbs);
            foreach (var h in _herbs)
                Collect(h.transform, h.IsAvailable, "E키를 눌러 상호작용", 2.6f, ref best, ref bestSqr, playerPos, 1.9f);

            // ResourceNode(Mine / Woodcutting)
            Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).ToListShell(_nodes);
            foreach (var r in _nodes)
                Collect(r.transform, r.IsAvailable, "E키를 눌러 상호작용", 2.8f, ref best, ref bestSqr, playerPos, 1.1f);

            // FarmPlot(Farm — 소유·성장중 제외: Empty=파종 / Ready=수확)
            Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None).ToListShell(_plots);
            foreach (var f in _plots)
            {
                bool avail = f.IsOwned && f.Phase != FarmPlot.CropPhase.Growing;
                Collect(f.transform, avail, "E키를 눌러 상호작용", 2.8f, ref best, ref bestSqr, playerPos, 1.0f);
            }

            // LockedDoor(Door — 잠김)
            Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None).ToListShell(_doors);
            foreach (var d in _doors)
                Collect(d.transform, d.IsLocked, "E키를 눌러 자물쇠 따기", 3.0f, ref best, ref bestSqr, playerPos, 1.5f);

            // 커스텀 등록(낚시/잠자기/영주실 등)
            for (int i = 0; i < _custom.Count; i++)
            {
                var e = _custom[i];
                if (e == null || e.tr == null) continue;
                Collect(e.tr, true, e.label, e.range, ref best, ref bestSqr, playerPos, 1.6f);
            }

            return _lastCandidate = best;
        }

        private static void Collect(Transform tr, bool available, string label, float range,
            ref Candidate best, ref float bestSqr, Vector3 playerPos, float headOffset)
        {
            if (tr == null || !available) return;
            Vector3 to = tr.position - playerPos;
            float dSqr = to.x * to.x + to.z * to.z;
            if (dSqr > range * range) return;
            if (dSqr >= bestSqr) return;
            bestSqr = dSqr;
            if (best == null) best = new Candidate();
            best.tr = tr;
            best.label = label;
            best.headOffset = headOffset;
        }

        // ── 부트스트랩 / 오버레이 루트 ──
        private void AttachToRoot(VisualElement root)
        {
            _overlayRoot = new VisualElement { name = "EInteractionPromptOverlay" };
            _overlayRoot.style.position = Position.Absolute;
            _overlayRoot.style.left = 0f; _overlayRoot.style.right = 0f;
            _overlayRoot.style.top = 0f; _overlayRoot.style.bottom = 0f;
            _overlayRoot.pickingMode = PickingMode.Ignore;
            root.Insert(0, _overlayRoot);

            _panel = new VisualElement { name = "EInteractionPrompt" };
            _panel.style.position = Position.Absolute;
            _panel.style.alignItems = Align.Center;
            _panel.style.justifyContent = Justify.Center;
            _panel.style.flexDirection = FlexDirection.Row;
            _panel.style.backgroundColor = new StyleColor(UTKTheme.Panel);
            _panel.style.borderTopWidth = _panel.style.borderBottomWidth =
                _panel.style.borderLeftWidth = _panel.style.borderRightWidth = 1f;
            _panel.style.borderTopColor = _panel.style.borderBottomColor =
                _panel.style.borderLeftColor = _panel.style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            _panel.style.borderTopLeftRadius = _panel.style.borderTopRightRadius =
                _panel.style.borderBottomLeftRadius = _panel.style.borderBottomRightRadius = 6f;
            _panel.style.display = DisplayStyle.None;
            _overlayRoot.Add(_panel);

            // keycap-E
            var key = new Label("E");
            key.style.width = KeyCapSize;
            key.style.height = KeyCapSize;
            key.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
            key.style.borderTopWidth = key.style.borderBottomWidth =
                key.style.borderLeftWidth = key.style.borderRightWidth = 1f;
            key.style.borderTopColor = key.style.borderBottomColor =
                key.style.borderLeftColor = key.style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            key.style.borderTopLeftRadius = key.style.borderTopRightRadius =
                key.style.borderBottomLeftRadius = key.style.borderBottomRightRadius = 4f;
            key.style.unityTextAlign = TextAnchor.MiddleCenter;
            key.style.color = new StyleColor(UTKTheme.TextMain);
            key.style.fontSize = 15f;
            key.style.marginRight = 10f;
            key.style.flexShrink = 0f;
            _panel.Add(key);

            _label = new Label("E키를 눌러 상호작용");
            _label.style.fontSize = LabelFontSize;
            _label.style.color = new StyleColor(UTKTheme.Accent);
            _label.style.whiteSpace = WhiteSpace.NoWrap;
            _label.style.flexShrink = 0f;
            _panel.Add(_label);
        }

        private void RefreshCamera()
        {
            if (_cam != null && Time.unscaledTime < _camNextRefresh) return;
            _cam = Camera.main;
            _camNextRefresh = Time.unscaledTime + CamRefreshInterval;
        }

        private void RefreshPlayer()
        {
            if (_player != null && Time.unscaledTime < _playerNextRefresh) return;
            var go = GameObject.FindGameObjectWithTag("Player");
            _player = go != null ? go.transform : null;
            _playerNextRefresh = Time.unscaledTime + 0.5f;
        }
    }

    internal static class ArrayExtensionsShell
    {
        /// <summary>FindObjectsByType 결과를 전달받은 리스트에 채운다(GC 최소화 재사용).</summary>
        internal static void ToListShell<T>(this T[] arr, List<T> dest) where T : Component
        {
            dest.Clear();
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] != null) dest.Add(arr[i]);
        }
    }
}