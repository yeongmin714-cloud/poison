using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// 공용 "E키로 상호작용" 프롬프트 — [33차] 월드공간 빌보드 전환(WorkProgressBar 게이지와 동일 메커니즘).
    ///
    /// 기존: UTK 오버레이 + WorldToViewportPoint 투영 → 카메라 선택/좌표계 문제로 프롬프트가
    ///       대상과 무관한 상단/코너에 떠 있었다(29~32차 반복 재보고 실측).
    /// 이제: 대상 렌더러 bounds 기준 월드 앵커(작은 대상=정면 위, 큰 대상(나무)=우측)에
    ///       베이크 PNG 쿼드+TextMesh를 카메라 정면 빌보딩 — 투영 계산 자체가 없어
    ///       어떤 카메라에서도 "항상 대상 근처"가 보장된다(게이지가 그렇듯이).
    ///
    /// 기본 감지: HerbPickup(Gather) / ResourceNode(Mine·Woodcutting) / FarmPlot(Farm) /
    ///             LockedDoor(Door) / CraftingStation / TerritoryWarehouse.
    /// 확장: Register(transform, label, range) — 낚시/잠자기/영주실 등. 채널링/창 열림 시 숨김.
    /// </summary>
    public class EInteractionPromptUTK : MonoBehaviour
    {
        private const float ScanInterval = 0.2f;

        // ── 월드 리그 치수 (베이크 PNG 1024×174 = 3.2×0.543m 고정 쿼드 — [34차] 확대) ──
        private const float QuadW = 3.2f;
        private const float QuadH = 0.543f;
        private const float TextHeight = 0.22f;      // 글자 높이(월드 m) — [34차] 0.18→0.22 확대
        private const float TextLocalX = -QuadW / 2f + 0.46f;   // 액센트 바(0.10m) 뒤 시작
        private const float SmallTargetMaxHeight = 2.5f;        // 이하 = "위" 앵커, 초과 = "우측" 앵커
        private const float AboveGap = 0.35f;
        private const float SideGap = 0.9f;

        private static EInteractionPromptUTK _instance;
        private static bool _spawnRequested;

        // 커스텀 등록(custom) — 낚시/잠자기/영주실/기타 E 시스템이 등록.
        private sealed class Entry { public Transform tr; public string label; public float range; }
        private static readonly List<Entry> _custom = new List<Entry>(8);

        // ── 월드 리그 ──
        private GameObject _worldRoot;
        private TextMesh _label;
        private Transform _labelT;
        private Camera _cam;
        private float _camNextRefresh;
        private float _scanNext;
        private Transform _player;
        private float _playerNextRefresh;

        public static EInteractionPromptUTK Instance => _instance;

        /// <summary>전 씬 자동 배선 — UI 내부 부트스트랩(asmdef 단방향 계약 유지).</summary>
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

        // ── 월드 리그 빌드 (WorkProgressBar 27차 베이크 PNG 규약 재현) ──
        private void BuildWorldRig()
        {
            _worldRoot = new GameObject("EInteractionPromptWorld");
            DontDestroyOnLoad(_worldRoot);
            _worldRoot.SetActive(false);

            // 패널 쿼드 — 베이크 PNG(라운드+스트로크+좌측 액센트 바), URP Unlit 투명.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
            quad.name = "PromptPanelQuad";
            quad.transform.SetParent(_worldRoot.transform, false);
            quad.transform.localScale = new Vector3(QuadW, QuadH, 1f);
            var rend = quad.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Sprites/Default")
                           ?? Shader.Find("Standard");
                var mat = new Material(s);
                var tex = Resources.Load<Texture2D>("UI/EInteractionPromptPanel");
                if (tex != null)
                    mat.mainTexture = tex;
                mat.color = Color.white;
                mat.SetFloat("_Surface", 1f);    // transparent
                rend.sharedMaterial = mat;
            }

            // 라벨 — TextMesh(월드 텍스트, 빌보드 쿼드 자식)
            var labelGo = new GameObject("PromptLabel");
            labelGo.transform.SetParent(_worldRoot.transform, false);
            labelGo.transform.localPosition = new Vector3(TextLocalX, 0f, -0.02f);   // 쿼드보다 카메라 쪽
            _labelT = labelGo.transform;
            _label = labelGo.AddComponent<TextMesh>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 48;
            _label.characterSize = TextHeight / (_label.fontSize * 0.1f);   // world height = fontSize×charSize×0.1
            _label.anchor = TextAnchor.MiddleLeft;
            _label.alignment = TextAlignment.Left;
            _label.color = new Color32(0xF0, 0xF6, 0xFC, 0xFF);   // #F0F6FC
            _label.text = "E키를 눌러 상호작용";
            var tmRend = labelGo.GetComponent<MeshRenderer>();
            if (tmRend != null && _label.font != null)
                tmRend.sharedMaterial = _label.font.material;
        }

        private void Update()
        {
            if (_worldRoot == null)
                BuildWorldRig();

            RefreshCamera();

            // 창 열림 or 채널링 진행 중 → 숨김(기존 계약 유지).
            bool hidden = ProjectName.Core.UITransitionState.AnyWindowOpen
                          || GatherChannelSystem.Instance.IsActive;
            Candidate candidate = hidden ? null : FindNearestInteractable();

            if (candidate == null || candidate.tr == null)
            {
                if (_worldRoot.activeSelf) _worldRoot.SetActive(false);
                return;
            }

            // ── 월드 앵커: 대상 렌더러 bounds — [33차 사용자 지정] 작은 대상=위, 큰 대상(나무)=우측 ──
            Bounds b = GetTargetBounds(candidate.tr);
            Vector3 anchor;
            if (b.size.y <= SmallTargetMaxHeight)
            {
                anchor = new Vector3(b.center.x, b.max.y + AboveGap, b.center.z);      // 돌/약초/밭 위
            }
            else
            {
                Vector3 camRight = Vector3.forward;
                if (_cam != null)
                {
                    camRight = _cam.transform.right;
                    camRight.y = 0f;
                    camRight = camRight.sqrMagnitude > 0.0001f ? camRight.normalized : Vector3.right;
                }
                anchor = b.center + camRight * (b.size.x * 0.5f + SideGap);            // 나무 우측
                anchor.y = b.center.y + b.size.y * 0.10f;
            }
            _worldRoot.transform.position = anchor;

            // 빌보딩 — [35차] "아까처럼(세워진 패널) 하되 탑다운에서 보이게 기울임" — 카메라
            //   수직 성분을 절반만 반영(34차 풀 직면은 LookRotation이 up과 거의 평행해져
            //   비틀려 오히려 가시성 저하 — 사용자 재보고). 세로 기울임 = 가독+자연스러움.
            if (_cam != null)
            {
                Vector3 toCam = _worldRoot.transform.position - _cam.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                {
                    Vector3 dir = new Vector3(toCam.x, toCam.y * 0.55f, toCam.z);   // 기울임 절반
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
                    dir.Normalize();
                    if (Mathf.Abs(dir.y) > 0.995f)
                    {
                        dir.y = 0f;
                        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;   // 수직 추월 가드
                    }
                    _worldRoot.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
            }

            string text = candidate.label ?? "E키를 눌러 상호작용";
            if (_label.text != text) _label.text = text;

            if (!_worldRoot.activeSelf)
            {
                _worldRoot.SetActive(true);
                // [진단] 후보가 바뀔 때 1회 — "프롬프트 안 뜸/멀리 뜸" 잔여 판별용
                Debug.Log($"[EInteractionPrompt] 대상 표시 — {candidate.tr.name} label=\"{text}\" dist={(_player != null ? Vector3.Distance(_player.position, candidate.tr.position) : 0f):F1}m anchor=({anchor.x:F1},{anchor.y:F1},{anchor.z:F1}) bounds={b.size.y:F1}m");
            }
            else if (candidate.tr != _lastLoggedTarget)
            {
                Debug.Log($"[EInteractionPrompt] 대상 표시 — {candidate.tr.name} label=\"{text}\" dist={(_player != null ? Vector3.Distance(_player.position, candidate.tr.position) : 0f):F1}m anchor=({anchor.x:F1},{anchor.y:F1},{anchor.z:F1}) bounds={b.size.y:F1}m");
            }
            _lastLoggedTarget = candidate.tr;
        }

        private Transform _lastLoggedTarget;

        private static readonly Bounds _zeroBounds = new Bounds(Vector3.zero, Vector3.zero);

        /// <summary>대상 시각 bounds — 렌더러 없으면 transform 위치의 점형 bounds.</summary>
        private static Bounds GetTargetBounds(Transform tr)
        {
            var rends = tr.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return new Bounds(tr.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            return b;
        }

        // ── 대상 스캔 (0.2s 스로틀, 타입별 scratch 재사용 → GC 최소) — 기존 로직 불변 ──
        private sealed class Candidate
        {
            public Transform tr;
            public string label;
        }

        private readonly List<HerbPickup> _herbs = new List<HerbPickup>(16);
        private readonly List<ResourceNode> _nodes = new List<ResourceNode>(16);
        private readonly List<FarmPlot> _plots = new List<FarmPlot>(16);
        private readonly List<LockedDoor> _doors = new List<LockedDoor>(16);
        private readonly List<CraftingStation> _stations = new List<CraftingStation>(8);
        private readonly List<TerritoryWarehouse> _warehouses = new List<TerritoryWarehouse>(8);
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
                Collect(h.transform, h.IsAvailable, "E키를 눌러 상호작용", 2.6f, ref best, ref bestSqr, playerPos);

            // ResourceNode(Mine / Woodcutting)
            Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).ToListShell(_nodes);
            foreach (var r in _nodes)
                Collect(r.transform, r.IsAvailable, "E키를 눌러 상호작용", 2.8f, ref best, ref bestSqr, playerPos);

            // FarmPlot(Farm — 소유·성장중 제외: Empty=파종 / Ready=수확)
            Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None).ToListShell(_plots);
            foreach (var f in _plots)
            {
                bool avail = f.IsOwned && f.Phase != FarmPlot.CropPhase.Growing;
                Collect(f.transform, avail, "E키를 눌러 상호작용", 2.8f, ref best, ref bestSqr, playerPos);
            }

            // LockedDoor(Door — 잠김)
            Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None).ToListShell(_doors);
            foreach (var d in _doors)
                Collect(d.transform, d.IsLocked, "E키를 눌러 자물쇠 따기", 3.0f, ref best, ref bestSqr, playerPos);

            // 크래프트 스테이션(제작소 — E키로 크래프트 창)
            Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None).ToListShell(_stations);
            foreach (var s in _stations)
                Collect(s.transform, true, "E키를 눌러 상호작용", 3f, ref best, ref bestSqr, playerPos);

            // 창고(저장고 — E키로 창고 창)
            Object.FindObjectsByType<TerritoryWarehouse>(FindObjectsSortMode.None).ToListShell(_warehouses);
            foreach (var w in _warehouses)
                Collect(w.transform, true, "E키를 눌러 상호작용", 3f, ref best, ref bestSqr, playerPos);

            // 커스텀 등록(낚시/잠자기/영주실 등)
            for (int i = 0; i < _custom.Count; i++)
            {
                var e = _custom[i];
                if (e == null || e.tr == null) continue;
                Collect(e.tr, true, e.label, e.range, ref best, ref bestSqr, playerPos);
            }

            return _lastCandidate = best;
        }

        private static void Collect(Transform tr, bool available, string label, float range,
            ref Candidate best, ref float bestSqr, Vector3 playerPos)
        {
            if (tr == null || !available) return;
            // [2026-10-11 28차] 3D 거리 일원화 — 각 시스템의 활성 판정(3D Distance)과 정합.
            Vector3 to = tr.position - playerPos;
            float dSqr = to.sqrMagnitude;
            if (dSqr > range * range) return;
            if (dSqr >= bestSqr) return;
            bestSqr = dSqr;
            if (best == null) best = new Candidate();
            best.tr = tr;
            best.label = label;
        }

        private void RefreshCamera()
        {
            if (_cam != null && Time.unscaledTime < _camNextRefresh) return;
            _cam = Camera.main;   // 빌보딩 전용 — WorkProgressBar와 동일 소스(투영 없음)
            _camNextRefresh = Time.unscaledTime + 0.5f;
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
