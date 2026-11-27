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
            // [44차] +Z(카메라 쪽)에 배치 — 로컬 +Z가 카메라를 향하는 관례에서 +0.02가 패널보다
            //   카메라 쪽(투명 정렬 후순위 → 텍스트가 패널 위에 선명 렌더). 기존 -0.02는 주석과
            //   반대(뒤쪽)로 배치돼 패널 반투명에 가려졌다.
            labelGo.transform.localPosition = new Vector3(TextLocalX, 0f, 0.02f);
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
            // 창 열림 or 채널링 진행 중 → 숨김(기존 계약 유지).
            bool hidden = ProjectName.Core.UITransitionState.AnyWindowOpen
                          || GatherChannelSystem.Instance.IsActive;
            Candidate candidate = hidden ? null : FindNearestInteractable();
            _screenCandidate = candidate;   // [45차] OnGUI(체력바 동일 스크린스페이스) 소비용

            if (candidate == null || candidate.tr == null)
                return;

            // [진단] 후보가 바뀔 때 1회 — "프롬프트 안 뜸/멀리 뜸" 잔여 판별용
            if (candidate.tr != _lastLoggedTarget)
            {
                Bounds bb = GetTargetBounds(candidate.tr);
                Debug.Log($"[EInteractionPrompt] 대상 표시 — {candidate.tr.name} label=\"{candidate.label}\" dist={(_player != null ? Vector3.Distance(_player.position, candidate.tr.position) : 0f):F1}m anchorY={bb.max.y:F1}m");
                _lastLoggedTarget = candidate.tr;
            }
        }

        private Candidate _screenCandidate;   // [45차] Update에서 확정한 최근접 대상 — OnGUI가 그린다
        private Transform _lastLoggedTarget;

        // ── [45차] 스크린스페이스 프롬프트 — 몬스터/병사 체력바(MonsterHeadUI)와 동일 메커니즘 ──
        //  월드 쿼드 빌보드 폐지(29~44차 반복 재보고: 기울임/부호/카메라 의존 가독성 문제).
        //  체력바처럼: 대상 머리 위 지점을 화면 투영 → 고정 픽셀 크기로 항상 정면 판독.
        //  피그마 스타일 유지: 베이크 PNG(EInteractionPromptPanel — 라운드+스트로크+좌측 액센트 바) 그대로.
        private Texture2D _panelTex;
        private GUIStyle _promptStyle;
        private GUIStyle _promptShadowStyle;
        private bool _stylesReady;

        private const float PanelW = 160f;                 // [46차 사용자 요구] 고정 화면 폭 절반(300→160) — 1024×174 PNG 등비
        private const float PanelH = 27f;                  // 174/1024×160 ≈ 27
        private const float TextPadX = 24f;                // 액센트 바+패딩 — 절반 비율(44×160/300)
        private const float TextFontSize = 11f;            // [46차] 20→11 — 패널 절반에 정합(체력바 폰트급)
        private const float AnchorGapM = 0.5f;             // 대상 상면 위 여유(m)
        private const float ScreenMargin = 12f;            // 화면 가장자리 클램프(px)

        // [94차 F4] 정면 앵커 정합 상수 — NPC 계열 대상은 상호작용 패널(InteractionPanelUTK)과 동일
        //   정면 앵커(bounds center + forward×0.6m, 높이 클램프 1.2~2.2m — 92차 후속 F-C 계약 승계).
        //   비NPC 대상(채집/문/스테이션 등)은 기존 상면 앵커(AnchorGapM) 무변경 — 회귀 방지.
        private const float FrontOffsetM = 0.6f;
        private const float AnchorClampMinM = 1.2f;
        private const float AnchorClampMaxM = 2.2f;
        private static Transform _frontAnchorProxy;   // 정면 지점 투영 프록시(렌더러 없음 — 폴백 산식 유도)

        private void OnGUI()
        {
            if (_screenCandidate == null || _screenCandidate.tr == null) return;
            EnsurePromptStyles();

            // [94차 F4] 정면 앵커 정합 — NPC 계열 대상(NPCAmbientDialogue/TerritoryNPCBehaviour/GuardPlaceholder)
            //   은 상호작용 패널과 동일 정면 앵커로 투영(즉시 부착 정합). 그 외 기존 경로 무변경.
            bool projected;
            Vector2 screenPx;
            if (IsFrontAnchorCandidate(_screenCandidate.tr))
                projected = TryProjectFrontAnchor(_screenCandidate.tr, out screenPx);
            else
            {
                // [91차 F3 → 92차 결함2] 투영 공용 유틸 — Camera.main → allCameras 폴백 일원화(멀티 카메라 씬 대응).
                //   [92차 결함2] 유효 = z>0(카메라 전방)만 — 화면 밖 좌표도 반환되며 아래 클램프로 가장자리 읽힘 보장.
                projected = TargetScreenProjector.TryProject(_screenCandidate.tr, AnchorGapM, out screenPx, out _);
            }
            if (!projected)
                return;   // 전 카메라 뒤(z≤0) — 미표시(구 z<0 스킵 계약 승계)

            float guiX = screenPx.x;
            float guiY = Screen.height - screenPx.y;   // GUI 좌표계 y 반전(기존 산식 유지)

            // 패널 — 대상 위 중앙 정렬, 화면 클램프(가장자리에서도 읽힘)
            float px = Mathf.Clamp(guiX - PanelW * 0.5f, ScreenMargin, Screen.width - PanelW - ScreenMargin);
            float py = Mathf.Clamp(guiY - PanelH - 2f, ScreenMargin, Screen.height - PanelH - ScreenMargin);
            Rect panelRect = new Rect(px, py, PanelW, PanelH);

            if (_panelTex == null)
                _panelTex = Resources.Load<Texture2D>("UI/EInteractionPromptPanel");
            if (_panelTex != null)
                GUI.DrawTexture(panelRect, _panelTex, ScaleMode.ScaleToFit);
            else
            {
                // 폴백 — PNG 누락 시에도 판독 보장(어두운 라운드 느낌 단색)
                GUI.color = new Color(0.086f, 0.106f, 0.133f, 0.92f);
                GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // 텍스트 — 좌측 액센트 바 뒤 시작(월드 리그와 동일 배치), 2회 렌더 그림자(체력바 선례)
            string text = _screenCandidate.label ?? "E키를 눌러 상호작용";
            var textRect = new Rect(px + TextPadX, py, PanelW - TextPadX - 12f, PanelH);
            GUI.Label(new Rect(textRect.x + 1f, textRect.y + 1f, textRect.width, textRect.height), text, _promptShadowStyle);
            GUI.Label(textRect, text, _promptStyle);
        }

        private void EnsurePromptStyles()
        {
            if (_stylesReady) return;
            _promptStyle = new GUIStyle();
            _promptStyle.fontSize = (int)TextFontSize;
            _promptStyle.fontStyle = FontStyle.Bold;
            _promptStyle.alignment = TextAnchor.MiddleLeft;
            _promptStyle.normal.textColor = new Color32(0xF0, 0xF6, 0xFC, 0xFF);   // #F0F6FC
            _promptStyle.richText = false;
            _promptShadowStyle = new GUIStyle(_promptStyle);
            _promptShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            _stylesReady = true;
        }

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

        // ── [94차 F4] 정면 앵커 정합 — 상호작용 패널(InteractionPanelUTK)과 동일 산식 ──

        /// <summary>[94차 F4] NPC 계열 대상 판정 — 주민/마을NPC/병사 계열만 정면 앵커 적용.
        /// GetComponentInParent(자식 콜라이더 히트 커버 — ContextCommandRouter 동일 규약).</summary>
        private static bool IsFrontAnchorCandidate(Transform tr)
        {
            if (tr == null) return false;
            return tr.GetComponentInParent<NPCAmbientDialogue>() != null
                   || tr.GetComponentInParent<GuardPlaceholder>() != null
                   || tr.GetComponentInParent<TerritoryNPCBehaviour>() != null;
        }

        /// <summary>
        /// [94차 F4] 정면 앵커 투영 — 앵커 = bounds center + 대상 forward×FrontOffset(0.6m, 수평 —
        /// NPC 정면 발 앞), 높이 = 기존 공용 산식(center.y + extents.y×0.15)을 기존 클램프(1.2~2.2m)로
        /// 제한(InteractionPanelUTK/NPCDialoguePanelUTK 동일 계약). 지점은 렌더러 없는 프록시 Transform으로
        /// 공용 유틸(z>0 가드/카메라 폴백)에 실어 투영(NPCDialoguePanelUTK 프록시 선례 — 복제 금지).
        /// </summary>
        private static bool TryProjectFrontAnchor(Transform tr, out Vector2 screenPx)
        {
            screenPx = default;
            Bounds b = GetTargetBounds(tr);
            bool has = tr.GetComponentsInChildren<Renderer>().Length > 0;
            Vector3 basePoint = has ? b.center : tr.position;
            float rawY = has ? b.center.y + b.extents.y * 0.15f : tr.position.y + AnchorGapM;
            float clampedY = Mathf.Clamp(rawY, AnchorClampMinM, AnchorClampMaxM);
            Vector3 fwd = tr.forward;
            fwd.y = 0f;   // 수평 정면 — 높이는 클램프 산식 전담(발 앞 지점 유지)
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            Vector3 anchor = new Vector3(basePoint.x, clampedY, basePoint.z) + fwd.normalized * FrontOffsetM;

            if (_frontAnchorProxy == null)
            {
                var proxyGo = new GameObject("EInteractionPromptFrontAnchorProxy");
                _frontAnchorProxy = proxyGo.transform;
            }
            _frontAnchorProxy.position = anchor;
            return TargetScreenProjector.TryProject(_frontAnchorProxy, 0f, out screenPx, out _);
        }

        // ── 대상 스캔 (0.2s 스로틀, 타입별 scratch 재사용 → GC 최소) — 기존 로직 불변 ──
        private sealed class Candidate
        {
            public Transform tr;
            public string label;
            public Vector3 sideDir = Vector3.right;                         // [44차] 고정 측면 오프셋 방향(대상 변경 시 1회 확정)
            public Quaternion frozenRot = Quaternion.Euler(-60f, 0f, 0f);   // [44차] 고정 기울임(빌보딩 폐지 — 탑다운 판독 기본값)
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

            // FarmPlot [26차 — 농경 업그레이드 8상태]: Grass/Dirt=굉이질, Tilled=파종,
            // Planted/Growing=물주기, Ready=수확, Withered/Rotted=정리. 성장 진행 중(Planted/Growing)도 물주기 가능.
            Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None).ToListShell(_plots);
            foreach (var f in _plots)
            {
                bool avail = f.IsOwned;
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
