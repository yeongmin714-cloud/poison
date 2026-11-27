using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026-09-13(45차 P6): 방어구 슬롯 GLB 비주얼 부착 시스템.
    /// EquipmentManager.OnEquipmentChanged(slot, itemId) 구독 →
    /// Helmet/Armor/Shoes/Gloves/Back 슬롯의 장착 GLB를 플레이어(Tag "Player") 아바타 본에 부착/파괴한다.
    /// - Weapon 슬롯은 WeaponEquipManager 소유 → 본 시스템이 건드리지 않음(스킵).
    /// - GLB 로드: WeaponEquipManager.Equip과 동일 방식 — Resources.Load("Models/UserProvided/{itemId}")
    ///   (방어구 GLB id = 파일명 그대로: wood_helmet/wood_armor/wood_boot_left/wood_boot_right/
    ///   wood_glove_left/wood_glove_right/wood_shield. steel/stone/crystal 계열도 동일 규칙).
    /// - 슬롯 매핑: Helmet→Head 1개 / Armor→Spine(없으면 Chest) 1개 / Shoes→좌우 토큰으로
    ///   LeftFoot|RightFoot(토큰 없는 통짜 id는 양발) / Gloves→LeftHand|RightHand(양손 동일) /
    ///   Back(방패)→LeftHand(없으면 LeftForeArm) 1개.
    /// - 아바타 지연도착 대응: 부트 직후 GLB 아바타의 본이 늦게 도착하므로(Rebind 선례 —
    ///   TestPlayerAnimatorBoot), 본 미발견 시 최대 5초 폴링(0.5s 간격) 후 재시도, 실패 시 경고 1회로 스킵.
    ///   Player는 GameObject.FindWithTag("Player") 기반으로 매 시도 재조회(대상 교체 대비).
    /// - 로그 스팸 가드: 경고는 키별 1회만(정적 HashSet).
    /// - [무번호 27] 바디 측정 bounds 오염 차단 — 비활성 GO(아바타 교체 잔재)/침하 감시용 placeholder
    ///   프리미티브(Unity 기본 도형)는 파괴 없이 측정에서만 제외 + fullBody 과대 시 렌더러별 정점 샘플
    ///   실측 로그(measure-pollute:{scene}) + measure-diag에 제외 렌더러 목록/오염 원인 출력.
    /// </summary>
    public class ArmorVisualAttachSystem : MonoBehaviour
    {
        const string LogTag = "[ArmorVisual]";
        const string PlayerTag = "Player";
        const string GlbResourceRoot = "Models/UserProvided/";   // WeaponEquipManager와 동일 GLB 경로
        const float BonePollTimeoutSec = 5f;                      // 본 도착 폴링 최대 대기
        const float BonePollIntervalSec = 0.5f;                   // 폴링 간격
        const float SubscribeWaitTimeoutSec = 10f;                // EquipmentManager 부트 대기 상한

        // ── 슬롯별 부착 포즈 튜닝 테이블 (초기 대략값 — Play 판정 후 로그 보고 튜닝) ──
        //   LocalPos  : 본 기준 부착 로컬 좌표 / LocalEuler: 부착 회전(Euler) / Scale: 균등 스케일.
        //   Helmet (0,0.1,0) = 머리 본 원점에서 위로 살짝. Armor (0,0,0) = 척추 본 원점 그대로.
        struct AttachPose
        {
            public Vector3 LocalPos;
            public Vector3 LocalEuler;
            public float Scale;
        }

        static readonly Dictionary<EquipmentManager.EquipmentSlot, AttachPose> _poseTable =
            new Dictionary<EquipmentManager.EquipmentSlot, AttachPose>
        {
            { EquipmentManager.EquipmentSlot.Helmet, new AttachPose { LocalPos = new Vector3(0f, 0.1f, 0f), LocalEuler = Vector3.zero, Scale = 1.0f } },
            { EquipmentManager.EquipmentSlot.Armor,  new AttachPose { LocalPos = Vector3.zero,             LocalEuler = Vector3.zero, Scale = 1.0f } },
            { EquipmentManager.EquipmentSlot.Shoes,  new AttachPose { LocalPos = new Vector3(0f, 0.05f, 0f), LocalEuler = Vector3.zero, Scale = 1.0f } },
            { EquipmentManager.EquipmentSlot.Gloves, new AttachPose { LocalPos = Vector3.zero,             LocalEuler = Vector3.zero, Scale = 1.0f } },
            { EquipmentManager.EquipmentSlot.Back,   new AttachPose { LocalPos = new Vector3(0f, 0.02f, 0.06f), LocalEuler = Vector3.zero, Scale = 1.0f } }, // [TEST25-66차] 방패 — 손 앞쪽 살짝 이격(판이 손 원점에 겹쳐 가려지지 않게)
        };

        // 슬롯별 부착 비주얼 인스턴스 (Shoes/Gloves는 좌우 2개 → List)
        readonly Dictionary<EquipmentManager.EquipmentSlot, List<GameObject>> _slotVisuals =
            new Dictionary<EquipmentManager.EquipmentSlot, List<GameObject>>();

        // [105차 F1] 파괴 예정 비주얼 1프레임 유지 — Destroy(프레임 말 지연) 직후 list.Clear()로 제외 목록에서
        //   즉시 빠지면 같은 프레임 바디 측정에 파괴 예정 비주얼이 여전히 기여한다(잘못 부착된 이전 투구 등의
        //   누수 경로). 파괴한 transform을 여기에 옮겨 다음 측정 1회까지 제외 유지 후 해제한다.
        readonly List<Transform> _pendingExcluded = new List<Transform>();

        // 로그 스팸 가드 (정적 캐시 — 인스턴스 재생성 사이에도 1회 유지)
        static readonly HashSet<string> _warnedKeys = new HashSet<string>();

        bool _subscribed;
        // [TEST26-67차] 구독 대상 인스턴스 추적 — Watchdog이 유실/교체 감지에 사용
        EquipmentManager _subscribedTo;
        /// <summary>[TEST26-67차] 싱글턴 — Watchdog 파괴 감지용.</summary>
        public static ArmorVisualAttachSystem Instance { get; private set; }

        // ===== 초기화 =====

        private void Awake()
        {
            // [TEST26-67차] 싱글턴 — Watchdog이 파괴/유실 감지에 사용
            Instance = this;

            // 부모 쪽 EnsureArmorVisualAttachSystem()이 AddComponent만 하므로 여기서 초기화.
            // EquipmentManager가 이미 부트되어 있으면 즉시 구독+초기 동기화,
            // 아니면 부트 순서 대기 후 구독(코루틴 — Edit 모드 Awake에서는 미실행).
            if (EquipmentManager.Instance != null)
            {
                Subscribe();
                InitialSyncAll();
            }
            else if (Application.isPlaying)
            {
                StartCoroutine(SubscribeWhenReadyRoutine());
            }

            // [TEST26-67차] 자가 치유 워치독 — 공유 GO 파괴/구독 유실과 무관하게 장비 비주얼 유지.
            // 뿌리(실측): 공유 GameManager GO 파괴 시 구독이 사라져 "발화는 되는데 수신 0건"(Editor.log).
            if (Object.FindAnyObjectByType<ArmorVisualSyncWatchdog>(FindObjectsInactive.Include) == null)
            {
                var wgo = new GameObject("ArmorVisualSyncWatchdog");
                DontDestroyOnLoad(wgo);
                wgo.AddComponent<ArmorVisualSyncWatchdog>();
            }
        }

        /// <summary>[TEST26-67차] Watchdog 0.5s 주기 호출 — 재구독 + 슬롯 리컨실레이션(자가 치유).</summary>
        public void SyncTick()
        {
            var em = EquipmentManager.Instance;
            if (em == null) return;

            // ① 구독 유실/인스턴스 교체 감지 → 재구독 + 전체 재동기화
            if (!_subscribed || _subscribedTo != em)
            {
                Unsubscribe();
                Subscribe();
                if (_subscribed)
                {
                    Debug.Log($"{LogTag} 구독 유실 감지 — 재구독 완료(자가 치유) + 전체 재동기화");
                    InitialSyncAll();
                }
                if (!_subscribed) return;
            }

            // ② 슬롯 리컨실레이션 — 장착 itemId 대비 부착 비주얼 부재/불일치 → 즉시 재부착
            foreach (EquipmentManager.EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)))
            {
                if (slot == EquipmentManager.EquipmentSlot.Weapon) continue;
                string desired = em.GetItemId(slot);
                bool hasVisual = _slotVisuals.TryGetValue(slot, out var list) && list != null && list.Exists(v => v != null);
                bool hasDesiredVisual = hasVisual && list.Exists(v => v != null && v.name == desired);

                if (string.IsNullOrEmpty(desired))
                {
                    if (hasVisual) DestroySlotVisuals(slot, log: true);
                }
                else if (!hasDesiredVisual)
                {
                    Debug.Log($"{LogTag} 리컨실레이션: {slot} → {desired} (부착 부재/불일치 — 자가 치유)");
                    DestroySlotVisuals(slot, log: false);
                    if (!TryAttachImmediate(slot, desired))
                        StartCoroutine(AttachRoutine(slot, desired));
                }
            }

            // ④ [무번호 29-P6] 장비 월드 크기 자가 재정규화 — 부착 시점 본 월드 스케일과 정착 후 스케일의 괴차
            //    (아바타 적용으로 본 스케일이 변하는 리그 특성 — 11-13 Play 실측: 투구 0.41m/타깃 0.528m=78%,
            //    갑옷 0.81m/0.962m=84%)를 정착 후 계약 타깃으로 1회 재정렬한다(28 재피팅과 동일 자가치유 철학).
            TryReassertEquipmentWorldSize(em);

            // ③ [무번호 28-P4b2] 과도기 재피팅 자가교정 — Play 초반(장비 자동장착 시점) 측정이 과도 스트레치를
            //    밟았다가 침하감시(+2s) 시점엔 계약 수렴. 이미 피팅된 슬롯을 정상 측정 기준으로 세션 1회 재피팅.
            CheckTransientRefit(em);
        }

        // [무번호 28-P4b2] 세션당 플레이어 1회 한정 재피팅 가드 — 정적 캐시(인스턴스 재생성 사이에도 유지, WarnOnce 패턴 준용).
        static readonly HashSet<string> _refitKeys = new HashSet<string>();
        // [무번호 28-P4b2] 재피팅 예약 진행 중 플래그 — 1.0s 대기 구간의 중복 판정/조기 "재시도 금지" 오탐 방지.
        bool _refitPending;

        // [무번호 29-P6] 세션당 슬롯 1회 한정 크기 재정규화 가드 — static 캐시(인스턴스 재생성 사이 유지, WarnOnce 패턴 준용).
        static readonly System.Collections.Generic.Dictionary<string, float> _resizeCooldown = new System.Collections.Generic.Dictionary<string, float>();   // [무번호 29-P12] 인스턴스별 마지막 재정규화 시각
        internal const float ResizeCooldownSeconds = 1.5f;   // [무번호 29-P12] 인스턴스별 쿨다운(오실레이션 방지 — SyncTick 0.5s 대비 3틱)

        /// <summary>[무번호 29-P6] 장비 월드 크기 자가 재정규화 — 부착 비주얼의 월드 최장축이 계약 타깃
        ///   (GetTargetSize = 69차 원값×BodyScale) 대비 ±10% 초과 이탈 시 NormalizeVisualScale로 1회 재정렬.
        ///   근거: 11-13 Play 실측 — 부착 시점 정규화(본 스케일 포함 world bounds 기준) 후 아바타 적용으로
        ///   본 스케일이 변해 투구 0.41m(78%)/갑옷 0.81m(84%)로 축소 — "투구와 몸통이 작은 느낌"의 실측 뿌리.
        ///   부팅 3s 경과 후만 판정(아바타/애니 정착 대기 — 침하감시 +2s 선례), 인스턴스당 1회
        ///   [29-P7: 재피팅 재부착=새 인스턴스=재보정 허용; 오실레이션은 ±10% 문턱+동일 인스턴스 1회로 방지].</summary>
        void TryReassertEquipmentWorldSize(EquipmentManager em)
        {
            if (Time.time < 3f) return;   // [무번호 29-P6] 정착 대기 게이트
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            foreach (EquipmentManager.EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)))
            {
                if (slot == EquipmentManager.EquipmentSlot.Weapon) continue;
                string desired = em.GetItemId(slot);
                if (string.IsNullOrEmpty(desired)) continue;
                if (!_slotVisuals.TryGetValue(slot, out var list) || list == null) continue;
                foreach (var v in list)
                {
                    if (v == null || v.name != desired) continue;
                    // [무번호 29-P12] 가드 = 인스턴스별 쿨다운(1.5s) 지속 추적 — 본 스케일/포즈 변동(피격 플린치·애니)
                    //   에 장비 월드 크기가 계속 실격되는 실측(14:16: Helmet ×1.32→×0.89 진자)에 대응.
                    //   [29-P7] 인스턴스 1회 고정은 히트 후 변동에 무방비(재보정 소진) → 교체.
                    //   오실레이션 방지: ±10% 문턱 + 인스턴스당 1.5s 쿨다운 + 부팅 3s 정착 게이트 유지.
                    string key = $"resize:{scene}:{slot}:{v.GetInstanceID()}";
                    float nowT = Time.time;
                    if (_resizeCooldown.TryGetValue(key, out var lastAt) && nowT - lastAt < ResizeCooldownSeconds) continue;
                    float target = GetTargetSize(slot, desired);
                    target *= GetSkinK(scene);   // [무번호 29-P8] 스킨 스팬 비례 — 부착 크기와 동일 기준(불일치 시 재정규화 진동 방지)
                    float current = MeasureVisualWorldMaxDim(v);
                    if (current < 0.01f || target < 0.01f) continue;
                    if (Mathf.Abs(current - target) <= 0.10f * target) continue;   // ±10% 이내 — 무손상
                    float applied = NormalizeVisualScale(v, target);
                    _resizeCooldown[key] = nowT;   // [무번호 29-P12] 쿨다운 갱신(지속 추적 — 소진형 가드 폐기)
                    WarnOnce(key,
                        $"{LogTag} [29-P6] 장비 크기 재정규화({slot}) — 월드 최장축 {current:F2}m → 타깃 {target:F2}m (스케일 ×{applied:F2} — 부착 시점 본 스케일 괴차 보정)");
                    break;
                }
            }
        }

        /// <summary>[무번호 29-P6] 부착 비주얼의 월드 최장축 — 활성 렌더러 bounds 합산(이펙트 계열 제외 — M1 필터 선례).</summary>
        /// <summary>[무번호 29-P8] 스킨 랜드마크 — 화면에 렌더되는 실제 피부의 y 기준값.
        ///   근거(P8 프로브 실측): 스킨은 루트 스케일 S에 ×S²로 렌더(SMR.L2W×본.L2W 이중 스케일 —
        ///   S=1.000→0.999m / S=1.552→2.407m), 본(아바타 구동 스켈레톤)은 ×S 선형+스킨 대비 압축
        ///   (head 본 62% vs 스킨 두상 ~95%). 본-기준 앵커/크기는 화면 몸통과 어긋나므로
        ///   장비 앵커/크기 기준을 스킨 랜드마크로 전환.</summary>
        internal struct SkinLandmarks { public float minY, maxY, headTopY, span; public bool valid; public bool headValid; }

        /// <summary>[무번호 29-P8] 스킨 랜드마크 실측(부착 시점 1회 BakeMesh) — 관례=P8 프로브 naive
        ///   (BakeMesh 기본 → 전체 L2W = Play renderSpan 2.43m와 동일, 화면 진실). 두상(headTopY)=
        ///   중심축 필터(|x−hipsX| ≤ 0.28×span) 최상단 — 팔·무기(측면) 배제.</summary>
        internal static SkinLandmarks MeasureSkinLandmarksNow(Animator placeAnim)
        {
            var lm = new SkinLandmarks { minY = 0f, maxY = 0f, headTopY = 0f, span = 0f, valid = false, headValid = false };
            var smr = placeAnim != null ? placeAnim.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (smr == null || smr.sharedMesh == null) return lm;
            Mesh bake = new Mesh();
            try { smr.BakeMesh(bake); }
            catch (System.Exception) { bake = null; }
            if (bake == null) return lm;
            var verts = bake.vertices;
            Object.Destroy(bake);
            int n = verts.Length;
            if (n == 0) return lm;
            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            float hipsX = 0f;
            var hips = placeAnim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) hipsX = hips.position.x;
            var pts = new System.Collections.Generic.List<Vector3>(Mathf.Min(n, 2000));
            int stride = Mathf.Max(1, n / 2000);
            for (int i = 0; i < n; i += stride) pts.Add(l2w.MultiplyPoint3x4(verts[i]));
            float mn = float.PositiveInfinity, mx = float.NegativeInfinity;
            foreach (var p in pts) { if (p.y < mn) mn = p.y; if (p.y > mx) mx = p.y; }
            float span = mx - mn;
            if (span < 0.05f) return lm;
            // [무번호 29-P9] 두상 필터 축소 — 0.28×span(≈0.68m)은 부착 시점 포즈의 팔/손(x ±0.3~0.5)을
            //   포함해 두상을 손끝으로 측정 → 투구 공중 부유(13:27 Play 실측: anchor 3.01, headTop 역산 3.22=스킨 최대).
            //   수평은 ×S 단일 스케일이라 머리 반폭 ≈ 0.09m×k → 0.06×span(≈0.15m)이 정확한 중심축.
            float headTop = float.NegativeInfinity;
            float axis = 0.06f * span;
            foreach (var p in pts)
                if (Mathf.Abs(p.x - hipsX) <= axis && p.y > headTop) headTop = p.y;
            // [무번호 29-P9] 두상 유효성 게이트 — 두상은 스킨 상반부에 있어야 한다. 아니면 무효(본 폴백 강제).
            bool headValid = headTop != float.NegativeInfinity && headTop >= mn + 0.5f * span;
            lm.minY = mn; lm.maxY = mx; lm.headTopY = headValid ? headTop : 0f;
            lm.span = span; lm.valid = true; lm.headValid = headValid;
            return lm;
        }

        /// <summary>[무번호 29-P8] 스킨 스팬 비례 크기 계수(k=스킨스팬/1.8m) — 씬 캐시(부착 시 기록,
        ///   SyncTick 재정규화가 동일 k 사용 — 부착/재정규화 크기 일관). 미측정=1(기존 계약).</summary>
        internal static readonly System.Collections.Generic.Dictionary<string, float> _skinKByScene = new System.Collections.Generic.Dictionary<string, float>();
        internal static float GetSkinK(string scene)
        {
            float k = 1f;
            return _skinKByScene.TryGetValue(scene, out k) && k > 0.05f ? k : 1f;
        }

        /// <summary>[무번호 29-P8] 비주얼(정렬·정규화 후) 렌더러 집합 y 크기 — 투구 두상 피복 오프셋 산출용.</summary>
        static float MeasureVisualBoundsSizeY(GameObject visual)
        {
            if (visual == null) return 0f;
            bool any = false; float mn = 0f, mx = 0f;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is TrailRenderer || r is ParticleSystemRenderer || r is LineRenderer) continue;
                Bounds b = r.bounds;
                if (!any) { mn = b.min.y; mx = b.max.y; any = true; }
                else { mn = Mathf.Min(mn, b.min.y); mx = Mathf.Max(mx, b.max.y); }
            }
            return any ? mx - mn : 0f;
        }

        static float MeasureVisualWorldMaxDim(GameObject visual)
        {
            // [무번호 29-P13] 오염 강건 측정 — 렌더 정점 백분위(3~97%) 스팬. GLB 메시의 쏠림 정점(창 정점 계열)이
            //   bounds를 왜곡해 정규화/재정규화가 틀리는 실측(14:25 영상: 투구 뭉치=bounds 대비 측면 쏠림) 수리.
            if (!TryMeasureVisualBulk(visual, out _, out var bulkSize)) return 0f;
            return Mathf.Max(bulkSize.x, Mathf.Max(bulkSize.y, bulkSize.z));
        }

        /// <summary>[무번호 29-P13] 장비 비주얼의 렌더 덩치(bulk) 측정 — 정점 샘플(최대 1500) 축별 정렬
        ///   3~97% 스팬(왜곡 정점 트림) + 중앙값 중심. SMR=BakeMesh, 정적=mesh.vertices(비가독 시 폴백 false).
        ///   결정론(Random 0건). false=샘플 부족 — 호출부 기존 bounds 경로 유지.</summary>
        internal static bool TryMeasureVisualBulk(GameObject visual, out Vector3 centroid, out Vector3 size)
        {
            centroid = Vector3.zero; size = Vector3.zero;
            if (visual == null) return false;
            var pts = new System.Collections.Generic.List<Vector3>(256);
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is TrailRenderer || r is ParticleSystemRenderer || r is LineRenderer) continue;
                Matrix4x4 l2w = r.transform.localToWorldMatrix;
                Vector3[] verts = null;
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    var tmp = new Mesh();
                    try { smr.BakeMesh(tmp); } catch (System.Exception) { Object.Destroy(tmp); continue; }
                    verts = tmp.vertices;
                    Object.Destroy(tmp);
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    try { verts = mf.sharedMesh.vertices; } catch (System.Exception) { verts = null; }
                    if (verts == null || verts.Length == 0) continue;
                }
                int stride = Mathf.Max(1, verts.Length / 400);
                for (int i = 0; i < verts.Length; i += stride) pts.Add(l2w.MultiplyPoint3x4(verts[i]));
                if (pts.Count >= 1500) break;
            }
            int n = pts.Count;
            if (n < 12) return false;
            centroid = MedianVec(pts);
            size = PercentileSpan(pts, 0.03f, 0.97f);
            return true;
        }

        /// <summary>[무번호 29-P13] 축별 중앙값 벡터(왜곡 정점 강건 중심 — 결정론).</summary>
        internal static Vector3 MedianVec(System.Collections.Generic.List<Vector3> pts)
        {
            var xs = new float[pts.Count]; var ys = new float[pts.Count]; var zs = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) { xs[i] = pts[i].x; ys[i] = pts[i].y; zs[i] = pts[i].z; }
            System.Array.Sort(xs); System.Array.Sort(ys); System.Array.Sort(zs);
            int m = pts.Count / 2;
            return new Vector3(pts.Count % 2 == 1 ? xs[m] : 0.5f * (xs[m - 1] + xs[m]),
                               pts.Count % 2 == 1 ? ys[m] : 0.5f * (ys[m - 1] + ys[m]),
                               pts.Count % 2 == 1 ? zs[m] : 0.5f * (zs[m - 1] + zs[m]));
        }

        /// <summary>[무번호 29-P13] 축별 백분위 스팬 — [loP, hiP] 구간 크기(오염 정점 트림, 결정론).</summary>
        internal static Vector3 PercentileSpan(System.Collections.Generic.List<Vector3> pts, float loP, float hiP)
        {
            var xs = new float[pts.Count]; var ys = new float[pts.Count]; var zs = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) { xs[i] = pts[i].x; ys[i] = pts[i].y; zs[i] = pts[i].z; }
            System.Array.Sort(xs); System.Array.Sort(ys); System.Array.Sort(zs);
            int lo = Mathf.Clamp((int)(loP * (pts.Count - 1)), 0, pts.Count - 1);
            int hi = Mathf.Clamp((int)(hiP * (pts.Count - 1)), 0, pts.Count - 1);
            return new Vector3(Mathf.Abs(xs[hi] - xs[lo]), Mathf.Abs(ys[hi] - ys[lo]), Mathf.Abs(zs[hi] - zs[lo]));
        }

        /// <summary>[무번호 29-P13] 렌더러 집합 bounds 집계(스냅 배치 기준 — 중심화 보정의 대조 기준값).</summary>
        internal static Bounds? AggregateVisualBounds(GameObject visual)
        {
            if (visual == null) return null;
            bool any = false; Bounds b = default;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is TrailRenderer || r is ParticleSystemRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? (Bounds?)b : null;
        }

        /// <summary>[무번호 28-P4b2] 과도기 재피팅 판정 — 전 슬롯 부착 완료 상태에서 재측정 fullBody H가
        ///   계약(1.2×SoldierTargetHeight = 2.16m) 초과면 1.0s 후 슬롯 전체 재피팅을 예약한다(세션 1회 한정).</summary>
        void CheckTransientRefit(EquipmentManager em)
        {
            // 전 슬롯 부착 완료 상태만 판정 — 리컨실레이션 중(부착 부재/불일치)이면 다음 틱에 재판정.
            bool anyEquipped = false;
            foreach (EquipmentManager.EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)))
            {
                if (slot == EquipmentManager.EquipmentSlot.Weapon) continue;
                string desired = em.GetItemId(slot);
                if (string.IsNullOrEmpty(desired)) continue;
                anyEquipped = true;
                bool hasDesired = _slotVisuals.TryGetValue(slot, out var list) && list != null
                                  && list.Exists(v => v != null && v.name == desired);
                if (!hasDesired) return;
            }
            if (!anyEquipped) return;

            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (_refitPending) return; // 예약 진행 중 — 중복 판정 없음
            // 세션 가드 소진 + "재시도 금지" 로그까지 발화했다면 이후 재측정 자체가 무의미 — 비용 절감.
            if (_refitKeys.Contains($"refit:{scene}") && _warnedKeys.Contains($"refit-still:{scene}")) return;

            // 재측정 — 부착 경로와 동일 제외 정책([105차 F1] 장비/무기 비주얼 + 파괴 예정 transform).
            var playerGo = GameObject.FindWithTag(PlayerTag);
            var placeAnim = playerGo != null ? playerGo.GetComponentInChildren<Animator>() : null;
            var excludeRoots = new List<Transform>();
            foreach (var kv in _slotVisuals)
                if (kv.Value != null)
                    foreach (var v in kv.Value)
                        if (v != null) excludeRoots.Add(v.transform);
            if (WeaponEquipManager.CurrentVisual != null)
                excludeRoots.Add(WeaponEquipManager.CurrentVisual.transform);
            foreach (var t in _pendingExcluded)
                if (t != null) excludeRoots.Add(t);
            PlayerBodyMeasure.TryMeasurePlayerBody(placeAnim, excludeRoots,
                out _, out _, out _, out _, out _, out _, out var mFull);
            if (!mFull.valid) return;
            if (PlayerBodyMeasure.LastVertexUnreliable)
            {
                // [무번호 29-P5] 재피팅 스킵 — 정점 측정 무효(폴백 상수+본 앵커 경로 부착 중) — 재피팅 무의미.
                WarnOnce($"refit-skip:{scene}",
                    $"{LogTag} [29-P5] 재피팅 스킵 — 정점 측정 무효(폴백 경로 부착 중) — 재피팅 무의미");
                return;
            }

            float h = mFull.worldBounds.size.y;
            float contractH = 1.2f * TerritoryBuilder.SoldierTargetHeight; // 계약 1.2×SoldierTargetHeight = 2.16m
            if (h <= contractH) return;

            // 멱등 가드 — 세션당 플레이어 1회 한정(static HashSet 키 refit:{scene}). 소진 후에도 이탈이면 재시도 금지(로그만).
            if (!_refitKeys.Add($"refit:{scene}"))
            {
                WarnOnce($"refit-still:{scene}",
                    $"{LogTag} [28-P4b2] 재피팅 후에도 fullBody {h:F2}m(계약 {contractH:F2}m 초과) — 세션 1회 소진, 재시도 금지");
                return;
            }
            _refitPending = true;
            Debug.Log($"[ArmorVisual] [28-P4b2] 재피팅 — fullBody {h:F2}m(계약 {contractH:F2}m 초과) → 1.0s 후 슬롯 전체 재피팅(1회 한정)"); // LogTag 리터럴 — 브리프 §2-2 로그 형식 그대로(Source 잠금)
            StartCoroutine(TransientRefitRoutine(em));
        }

        /// <summary>[무번호 28-P4b2] 1.0s 후 슬롯 전체 재피팅 — 기존 Attach 경로 재사용(파괴→재부착,
        ///   [105차 F1] _pendingExcluded 계약 재사용: DestroySlotVisuals가 파괴 예정 transform을 다음 측정 1회까지 제외 유지).</summary>
        IEnumerator TransientRefitRoutine(EquipmentManager em)
        {
            yield return new WaitForSeconds(1.0f);
            _refitPending = false;
            if (em == null) yield break;
            foreach (EquipmentManager.EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)))
            {
                if (slot == EquipmentManager.EquipmentSlot.Weapon) continue;
                string desired = em.GetItemId(slot);
                if (string.IsNullOrEmpty(desired)) continue;
                DestroySlotVisuals(slot, log: false);
                if (!TryAttachImmediate(slot, desired))
                    StartCoroutine(AttachRoutine(slot, desired));
            }
        }

        /// <summary>EquipmentManager 부트 대기 후 구독 + 초기 동기화 (0.5s 간격 폴링).</summary>
        IEnumerator SubscribeWhenReadyRoutine()
        {
            for (float t = 0f; t <= SubscribeWaitTimeoutSec; t += BonePollIntervalSec)
            {
                if (EquipmentManager.Instance != null)
                {
                    Subscribe();
                    InitialSyncAll();
                    yield break;
                }
                yield return new WaitForSeconds(BonePollIntervalSec);
            }
            WarnOnce("no-em", $"{LogTag} EquipmentManager 미발견({SubscribeWaitTimeoutSec:0}초) — 구독/초기 동기화 스킵");
        }

        void Subscribe()
        {
            if (_subscribed || EquipmentManager.Instance == null) return;
            EquipmentManager.Instance.OnEquipmentChanged += HandleEquipmentChanged;
            _subscribed = true;
            _subscribedTo = EquipmentManager.Instance;
            Debug.Log($"{LogTag} EquipmentManager.OnEquipmentChanged 구독 완료");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            if (EquipmentManager.Instance != null)
                EquipmentManager.Instance.OnEquipmentChanged -= HandleEquipmentChanged;
            _subscribed = false;
        }

        /// <summary>구독 직후 현재 장착 상태 전체 동기화(Weapon 슬롯 제외).</summary>
        void InitialSyncAll()
        {
            var em = EquipmentManager.Instance;
            if (em == null) return;
            foreach (EquipmentManager.EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)))
            {
                if (slot == EquipmentManager.EquipmentSlot.Weapon) continue;
                string itemId = em.GetItemId(slot);
                if (!string.IsNullOrEmpty(itemId))
                    HandleEquipmentChanged(slot, itemId);
            }
        }

        // ===== 이벤트 처리 =====

        void HandleEquipmentChanged(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            // Weapon 슬롯은 WeaponEquipManager 소유 — 스킵
            if (slot == EquipmentManager.EquipmentSlot.Weapon) return;

            // 2026-09-15(H-3): 이벤트 수신 실측 로그 — 부착 전 도달 여부/해제(Detach) 여부를
            // 홉 단위로 판별. 부착 성공만(✅)이 아니라 수신 자체를 로그로 남겨 dead-end을 구분한다.
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.Log($"{LogTag} OnEquipmentChanged 수신 ({slot}) — 해제(Detach)");
                DestroySlotVisuals(slot, log: true);
                return;
            }
            Debug.Log($"{LogTag} OnEquipmentChanged 수신 ({slot}) item={itemId} — 부착 시작");
            // [TEST25-66차] 동기 즉시 부착 1차 시도 — 본+GLB가 이 프레임에서 해결되면 코루틴 없이 즉시 완료.
            //   뿌리(실측): 부트 Awake 중 발화된 AttachRoutine 코루틴이 첫 yield 후 재개되지 않고 침묵 사망해
            //   "부착 시작" 로그만 남고 장비가 안 보였다(Editor.log: 성공/미발견/로드실패 로그 전부 0건).
            //   동기 경로는 코루틴 수명과 무관하게 같은 프레임에 부착+로그가 고정된다.
            if (TryAttachImmediate(slot, itemId)) return;
            StartCoroutine(AttachRoutine(slot, itemId));
        }

        /// <summary>
        /// [TEST25-66차] 동기 즉시 부착 — 본 해석+GLB 로드가 즉시 성공하면 코루틴 없이 그 자리에서 완료.
        /// 성공 true(코루틴 불필요) / 실패 false(폴링 코루틴 폴백). 예외는 로그 후 false(코루틴이 재시도·경고).
        /// </summary>
        bool TryAttachImmediate(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            try
            {
                // ① 기존 비주얼 파괴 (동일 슬롯 재부착/교체 공통)
                DestroySlotVisuals(slot, log: false);

                // ② 부착 본 즉시 해석
                var bones = ResolveBones(slot, itemId);
                if (bones == null) return false;

                // ③ 즉시 부착 — [TEST28-69차 후속3] 프리팹 로드를 본별로 위임
                //   (통합 부츠/장갑 id의 단일 GLB는 없고 좌우 GLB만 존재 — 기존 단일 게이트에서 전체 실패였음)
                return InstantiateAttached(slot, itemId, bones, null);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogTag} 동기 즉시 부착 예외({slot}, {itemId}): {e.Message} — 폴링 코루틴으로 폴백");
                return false;
            }
        }

        /// <summary>[TEST28-69차 후속3] 본별 인스턴스 부착 + 슬롯 목표 크기 정규화 + 본 스냅 + 성공 로그.
        ///   plainPrefab은 통합 GLB(없으면 null 허용 — 부츠/장갑은 본별 좌우 GLB 로드). 하나라도 부착하면 true.</summary>
        bool InstantiateAttached(EquipmentManager.EquipmentSlot slot, string itemId, Transform[] bones, GameObject plainPrefab)
        {
            var pose = _poseTable.TryGetValue(slot, out var p) ? p : new AttachPose { LocalPos = Vector3.zero, LocalEuler = Vector3.zero, Scale = 1f };
            var boneNames = new List<string>();
            float appliedScale = 1f;   // [TEST27-68차] 마지막 적용 스케일(로그용) — 루프 밖 로그 참조
            Vector3 lastAnchorWorld = default;
            bool hasAnchor = false;    // [TEST28-69차] 월드 앵커 실측값(콘솔 튜닝용)
            bool anyRenderer = false;  // [69차 후속4] 본별 실측 로그 — 쌍슬롯은 좌우 합산 bounds 중심이 손에서 멀어 보이는 오판(장갑 0.24m)을 만들므로 본별 최대값 사용
            float maxCenterDist = -1f;
            string maxDistBone = null;
            // [무번호 25-A2] 비주얼 자체 실측 bounds 최장축 — ✅ 로그에 기록해 Play 후 목표 최장축
            //   (_targetSize 원값 × BodyScale) ±0.02 범위 수렴을 실측 대비로 판정(계획서 A2 검증).
            float maxOwnLongest = -1f;
            // [69차 후속4] 방패 바깥오프셋용 Spine 본 조회(부착 시 1회 — transform 기반 pLeft는 모델 시선과 어긋남)
            Animator placeAnim = null;
            var playerGo = GameObject.FindWithTag(PlayerTag);
            if (playerGo != null) placeAnim = playerGo.GetComponentInChildren<Animator>();
            // [2026-09-16] 실측 피팅 — 부착 루프 밖에서 플레이어 메시 본 기반 바디 실측 1회(부착마다 재측정, 이동 대응).
            // [2026-09-16 69차 후속11] 측정 오염 제거 — 기존 부착 장비 비주얼이 플레이어 렌더러 목록에 포함되어
            //   부위 bounds가 부풀고(같은 장비 재장착 시 장비가 점점 커지는 버그의 뿌리) 측정이 불안정해진다.
            //   시스템이 추적 중인 모든 장비 비주얼 루트를 측정에서 제외한다.
            var excludeRoots = new List<Transform>();
            foreach (var kv in _slotVisuals)
                if (kv.Value != null)
                    foreach (var v in kv.Value)
                        if (v != null) excludeRoots.Add(v.transform);
            // [105차 F1] 무기 GLB 측정 오염 제외 — WeaponEquipManager가 부착한 무기 비주얼 루트도 측정에서 제외.
            //   매 부착마다 재조회하므로 재장착/교체와 무관하게 멱등. (근원 1: wood_spear bounds z=1.27m 정점이
            //   Head/Torso 밴드를 오염 → 투구 부유(본↔중심 0.81m)/갑옷 ×3.00 과대의 주 원인)
            if (WeaponEquipManager.CurrentVisual != null)
                excludeRoots.Add(WeaponEquipManager.CurrentVisual.transform);
            // [105차 F1] 파괴 예정 비주얼 1프레임 유지 — 이번 측정 1회까지 제외 유지.
            foreach (var t in _pendingExcluded)
                if (t != null) excludeRoots.Add(t);
            PlayerBodyMeasure.TryMeasurePlayerBody(placeAnim, excludeRoots, out var mHead, out var mTorso,
                out var mLeftFoot, out var mRightFoot, out var mLeftHand, out var mRightHand, out var mFullBody);
            // [105차 F1] 측정 1회 소비 — 파괴 예정 제외 유지 해제(이후 측정은 실제 파괴 완료 상태 기준).
            _pendingExcluded.Clear();
            float fullBodyH = mFullBody.valid ? mFullBody.worldBounds.size.y : 0f;
            foreach (var bone in bones)
            {
                if (bone == null) continue;

                // [TEST28-69차 후속2] 통합 부츠/장갑 — 좌우 GLB가 별도 파일이면 본 좌우에 맞는 쪽을 선택 부착
                //   (wood_boot 통합 id → LeftFoot엔 wood_boot_left, RightFoot엔 wood_boot_right. 전용 파일 없으면 통합/원본 유지)
                var bonePrefab = plainPrefab;
                bool pairSlot = slot == EquipmentManager.EquipmentSlot.Shoes || slot == EquipmentManager.EquipmentSlot.Gloves;
                string idLower = itemId.ToLowerInvariant();
                if (pairSlot && !idLower.Contains("left") && !idLower.Contains("right"))
                {
                    bool isLeftBone = bone.name.ToLowerInvariant().Contains("left");
                    string sideId = itemId + (isLeftBone ? "_left" : "_right");
                    var sidePrefab = Resources.Load<GameObject>(GlbResourceRoot + sideId)
                                     ?? Resources.Load<GameObject>(GlbResourceRoot + sideId + ".glb");
                    if (sidePrefab != null) bonePrefab = sidePrefab;
                }

                var visual = Instantiate(bonePrefab, bone);
                visual.name = itemId;
                visual.transform.localPosition = pose.LocalPos;
                visual.transform.localRotation = Quaternion.Euler(pose.LocalEuler);
                visual.transform.localScale = Vector3.one * pose.Scale;

                // [무번호 29-P7] 리그 정합 진단(첫 Helmet 부착 1회) — "장비 작음/부착 불가" 3차 보고의 판별 데이터:
                //   ①본 스케일(Head lossyScale 전체 — 아바타가 본 스케일을 바꾸는지) ②본 월드 y(스켈레톤 비율)
                //   ③바디 SMR r.bounds min/max(Unity 본 봉투) ④BakeMesh 렌더 스팬(실제 피부 — 후보A 매핑).
                //   ③과 ④의 괴차 = 피부 스트레치 확정, ①의 1.0 이탈 = 본 스케일 변형 확정 — 다음 수리 방향 결정.
                if (slot == EquipmentManager.EquipmentSlot.Helmet)
                {
                    string rigScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    var rigSmr = placeAnim != null ? placeAnim.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                    Transform rigHead = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.Head) : null;
                    Transform rigSpine = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.Spine) : null;
                    Transform rigFoot = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
                    string headScale = rigHead != null ? rigHead.lossyScale.ToString("F3") : "null";
                    string spineScaleY = rigSpine != null ? rigSpine.lossyScale.y.ToString("F3") : "null";
                    string footScaleY = rigFoot != null ? rigFoot.lossyScale.y.ToString("F3") : "null";
                    string boundsTxt = "null";
                    float renderSpan = -1f;
                    if (rigSmr != null && rigSmr.sharedMesh != null)
                    {
                        Bounds rb = rigSmr.bounds;
                        boundsTxt = $"[{rb.min.y:F2}~{rb.max.y:F2}]";
                        Mesh rigBake = new Mesh();
                        try { rigSmr.BakeMesh(rigBake); }
                        catch (System.Exception) { rigBake = null; }
                        if (rigBake != null)
                        {
                            var rv = rigBake.vertices;
                            var l2wR = rigSmr.transform.localToWorldMatrix;
                            float ryMin = float.PositiveInfinity, ryMax = float.NegativeInfinity;
                            for (int i = 0; i < rv.Length; i += Mathf.Max(1, rv.Length / 800))
                            {
                                float y = l2wR.MultiplyPoint3x4(rv[i]).y;
                                if (y < ryMin) ryMin = y;
                                if (y > ryMax) ryMax = y;
                            }
                            if (ryMin <= ryMax) renderSpan = ryMax - ryMin;
                            Object.Destroy(rigBake);
                        }
                    }
                    WarnOnce($"rig-diag:{rigScene}",
                        $"{LogTag} [29-P7] 리그 정합 — headScale={headScale} spineScaleY={spineScaleY} footScaleY={footScaleY}"
                        + $", headY={(rigHead != null ? rigHead.position.y.ToString("F2") : "null")}"
                        + $", bodyBounds={boundsTxt}, renderSpan={renderSpan:F2}m");
                }

                // [TEST27-68차] 슬롯 목표 크기 정규화 — 방어구 GLB가 플레이어 대비 3~4배(헬멧 1.37m 실측)로
                //   플레이어를 덮는 문제. 최장축을 슬롯 목표 치수로 균등 스케일.
                // [무번호 29-P8] ×스킨k — 화면 몸통(×S² 스트레치 렌더) 비례 크기(스킨스팬/1.8m).
                //   측정 실패 시 k=1(기존 계약 그대로).
                string skinScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var skinLm = MeasureSkinLandmarksNow(placeAnim);
                float skinK = 1f;
                if (skinLm.valid && skinLm.span > 0.05f)
                {
                    skinK = skinLm.span / TerritoryBuilder.SoldierTargetHeight;
                    _skinKByScene[skinScene] = skinK;
                }
                appliedScale = NormalizeVisualScale(visual, GetTargetSize(slot, itemId) * skinK);

                // [2026-09-16] 축 자가 정렬 — GLB 원본 축을 슬롯 규칙에 맞춰 회전 보정.
                //   스냅(SnapVisualToBone)보다 먼저 실행: 회전 → 이후 bounds 재계산 → 스냅이 올바른 앵커를 읽는다.
                //   위치 앵커 상수는 1도 변경하지 않는다(실측 검증 완료).
                TryGetPlayerPlacement(out _, out var alignFwd, out var alignLeft);
                ApplySlotAxisAlignment(visual, bone, slot, placeAnim, alignFwd, alignLeft);

                // [2026-09-16] 실측 피팅 — 슬롯 규칙 대상(Helmet/Armor/Shoes/Gloves/Mask/Bag)은 플레이어 메시 본 기반 피팅 시도.
                //   성공 시 기존 상수 worldAnchor + SnapVisualToBone 폴백을 건너뛴다(Back은 기존 상수 경로 그대로).
                bool bodyFitted = false;
                if (slot == EquipmentManager.EquipmentSlot.Helmet || slot == EquipmentManager.EquipmentSlot.Armor
                    || slot == EquipmentManager.EquipmentSlot.Shoes || slot == EquipmentManager.EquipmentSlot.Gloves
                    || slot == EquipmentManager.EquipmentSlot.Mask || slot == EquipmentManager.EquipmentSlot.Bag)
                {
                    PlayerBodyMeasure.BodyPart fitPart = default;
                    switch (slot)
                    {
                        case EquipmentManager.EquipmentSlot.Helmet: fitPart = mHead; break;
                        case EquipmentManager.EquipmentSlot.Armor:
                        case EquipmentManager.EquipmentSlot.Bag:    fitPart = mTorso; break;
                        case EquipmentManager.EquipmentSlot.Mask:   fitPart = mHead; break;
                        case EquipmentManager.EquipmentSlot.Shoes:
                            fitPart = bone.name.ToLowerInvariant().Contains("left") ? mLeftFoot : mRightFoot;
                            break;
                        case EquipmentManager.EquipmentSlot.Gloves: // [2026-09-16] 본 이름(left 포함)으로 좌/우 손 part 선택 — Shoes와 동일 규칙
                            fitPart = bone.name.ToLowerInvariant().Contains("left") ? mLeftHand : mRightHand;
                            break;
                    }
                    // [105차 F2] 부위 측정 정상성 게이트 — 오염 bounds(창 정점 등)로 fit 진행 금지, 폴백 상수로 강등.
                    bodyFitted = false;
                    if (fitPart.valid && mFullBody.valid)
                    {
                        if (!IsPartPlausible(slot, fitPart.worldBounds, mFullBody.worldBounds))
                            WarnOnce($"gate:{slot}",
                                $"{LogTag} 부위 측정 게이트 실패 {slot} part=({fitPart.worldBounds.size.x:F2}, {fitPart.worldBounds.size.y:F2}, {fitPart.worldBounds.size.z:F2}) H={fullBodyH:F2} — 폴백 상수 사용");
                        else
                            bodyFitted = FitVisualToBodyPart(visual, slot, bone, fitPart, alignFwd);
                    }
                }

                //   ①CC 높이 기준은 모델보다 커서 투구가 머리 위 20~25% 부유(스크린샷 65 실측)
                //   ②SkinnedMesh 몸 bounds 왜곡(3.2m) ③본 로컬축(손 본 +Z=손가락 방향) 모두 폐기.
                Vector3? worldAnchor = null;
                if (!bodyFitted && TryGetPlayerPlacement(out var ground, out var pFwd, out var pLeft))
                {
                    switch (slot)
                    {
                        case EquipmentManager.EquipmentSlot.Helmet:
                            // [69차 후속5] 실측: 플레이어=1.0m 치비(발=원점, Player_Rigged.fbx VERT 0.89×1.0×0.4),
                            //   Head 본=턱/두개골 하단(로컬 0.57), 두개골=로컬 0.6~1.0(0.4m 대형 머리).
                            //   본±수cm 앵커는 투구(높이 0.267)가 두개골 내부에 매몰 → 67차 영상 "투구 안 보임"의 뿌리.
                            //   헬멧 중심 = 본+26cm(정수리 감쌈, 상단이 머리 꼭대기와 맞물림).
                            // [무번호 29-P10] 투구 앵커 = 105차 공식(본+0.26×BodyScale) 복원 — 사용자 승인 이력 위치.
                            //   근거: 12:51 Play(앵커 2.21) = "거의 맞는데 작은 느낌"(위치 승인, 크기만 불만) →
                            //   크기는 P8 ×k로 수리 완료. 스킨 두상 앵커(P8/P9)는 14:01 Play에서 부유 재현 —
                            //   상부 본 추종 오염 정점(창 정점 계열, 포즈별 3.22/2.59 변동 실측)으로 두상 측정 신뢰 불가 → 기각.
                            //   [29-P9] 진단 로그는 유지(참고 데이터).
                            {
                                float helmetH = MeasureVisualBoundsSizeY(visual);
                                WarnOnce($"helmet-anchor:{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}",
                                    $"{LogTag} [29-P10] 투구 앵커 = 105차 공식(본 기준) — headTop(참고, 비사용)={(skinLm.valid && skinLm.headValid ? skinLm.headTopY.ToString("F2") : "무효")}"
                                    + $" helmetH={helmetH:F2} anchorY={(bone.position + Vector3.up * (0.26f * BodyScale)).y:F2} (boneY={bone.position.y:F2})");
                            }
                            worldAnchor = bone.position + Vector3.up * (0.26f * BodyScale); // [105차 F3] 원값×BodyScale — 1.8m 정규화 동행
                            break;
                        case EquipmentManager.EquipmentSlot.Mask:
                            worldAnchor = bone.position + pFwd * (0.06f * BodyScale) + Vector3.up * (0.19f * BodyScale); // [69차 후속5] 가면 중심 = 눈높이(로컬 0.76) — 본-12cm는 턱 아래로 과납(67차 실측) / [105차 F3] 원값×BodyScale
                            break;
                        case EquipmentManager.EquipmentSlot.Armor:
                            // [무번호 29-P11] 앞쪽 당깨기 +0.05×BodyScale(≈8cm — 사용자 피드백 "몸통 조금 더 앞쪽").
                            //   전방=pFwd(페이싱) — Mask/Bag 선례와 동일 경로. 수직 성분(0.15×BodyScale) 무손상.
                            worldAnchor = bone.position + Vector3.up * (0.15f * BodyScale) + pFwd * (0.10f * BodyScale);   // [69차 후속5] 갑옷 중심 = 가슴(로컬 0.40, 갑옷 높이 0.341 → 허리~목하단) / [105차 F3] 원값×BodyScale / [무번호 29-P12] 전방 +0.10×BodyScale(≈15cm — "더 앞쪽" 2차 트림)
                            break;
                        case EquipmentManager.EquipmentSlot.Bag:
                            worldAnchor = bone.position + Vector3.up * (0.12f * BodyScale) - pFwd * (0.09f * BodyScale); // [69차 후속5] 가방 중심 = 등 중앙(로컬 0.37, 등 표면 뒤 9cm) / [105차 F3] 원값×BodyScale
                            break;
                        case EquipmentManager.EquipmentSlot.Gloves:
                            worldAnchor = bone.position - Vector3.up * (0.02f * BodyScale);        // [69차 후속5] 장갑(손목보호대, 높이 0.08) 중심 = 손 본-2cm / [105차 F3] 원값×BodyScale
                            break;
                        case EquipmentManager.EquipmentSlot.Back:
                        {
                            // [69차 후속4] 방패 = 손 본에서 팔 바깥쪽 10cm — Spine↔손 벡터(본 기준, 시선 무관).
                            var lat = Vector3.zero;
                            var spineBone = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.Spine) : null;
                            if (spineBone != null)
                            {
                                var d = bone.position - spineBone.position; d.y = 0f;
                                if (d.sqrMagnitude > 0.0001f) lat = d.normalized * (0.10f * BodyScale); // [105차 F3] 원값×BodyScale
                            }
                            worldAnchor = bone.position + lat + Vector3.up * (0.02f * BodyScale); // [105차 F3] 원값×BodyScale
                            break;
                        }
                        case EquipmentManager.EquipmentSlot.Shoes:
                            worldAnchor = bone.position - Vector3.up * (0.05f * BodyScale);       // [69차 후속4·5 검증] 복사 본(로컬 0.06)-5cm = 지면+1cm — 실측 부합, 유지 / [105차 F3] 원값×BodyScale
                            break;
                    }
                }
                if (!bodyFitted)
                    SnapVisualToBone(visual, bone, GetCenterOffset(slot, itemId), GetBottomAnchor(slot, itemId), worldAnchor);
                // [무번호 29-P13] 덩치 재중심화 — GLB 메시의 쏠림 정점이 bounds를 왜곡해 스냅 후 "보이는 뭉치"가
                //   앵커에서 옆으로 밀리는 실측(14:25 영상: 투구 중심이 머리폭의 58% 측면 오프셋) 수리.
                //   bounds 중심(스냅 배치 기준)과 렌더 덩치 중앙값 중심의 차이만큼평 이동 — 머리/몸통 뭉치가 앵커에 안착.
                if (TryMeasureVisualBulk(visual, out var bulkC, out _) && AggregateVisualBounds(visual) is Bounds ab)
                {
                    Vector3 shift = ab.center - bulkC;
                    if (shift.sqrMagnitude > 0.000001f)
                        visual.transform.position += shift;
                }
                if (worldAnchor.HasValue) { lastAnchorWorld = worldAnchor.Value; hasAnchor = true; }

                AddVisual(slot, visual);
                boneNames.Add(bone.name);
                // [69차 후속4] 본별 중심 거리 실측 — 스냅 후 이 비주얼 자체 bounds 기준(쌍슬롯 합산 왜곡 제거)
                bool ownInit = false;
                var ownB = new Bounds();
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || !r.enabled) continue;
                    if (!ownInit) { ownB = r.bounds; ownInit = true; }
                    else ownB.Encapsulate(r.bounds);
                }
                if (ownInit)
                {
                    anyRenderer = true;
                    var d = Vector3.Distance(ownB.center, bone.position);
                    if (d > maxCenterDist) { maxCenterDist = d; maxDistBone = bone.name; }
                    // [무번호 25-A2] 실측 bounds 최장축 기록 — 본별 최대값 집계
                    maxOwnLongest = Mathf.Max(maxOwnLongest, Mathf.Max(ownB.size.x, Mathf.Max(ownB.size.y, ownB.size.z)));
                }
            }

            if (boneNames.Count == 0)
            {
                Debug.LogWarning($"{LogTag} ⚠️ {slot} 비주얼 부착 실패: {itemId} — 유효 본 0개/부착 가능 프리팹 없음");
                return false;
            }
            if (anyRenderer)
            {
                string distInfo = maxCenterDist >= 0f
                    ? $"본↔중심 {maxCenterDist:F2}m{(bones.Length > 1 && maxDistBone != null ? $" (최대 {maxDistBone})" : "")}"
                    : "본↔중심 측정 불가";
                // [무번호 25-A2] 실측 bounds 최장축 기록 — Play 후 목표 최장축(_targetSize × BodyScale) 수렴 대비
                if (maxOwnLongest >= 0f)
                    distInfo += $", 실측최장축 {maxOwnLongest:F2}m";
                Debug.Log($"{LogTag} ✅ {slot} 비주얼 부착: {itemId} → {string.Join(", ", boneNames)} (스케일 x{appliedScale:F2}, anchor={(hasAnchor ? lastAnchorWorld.ToString("F2") : "본 기준")}, {distInfo})");
                // [105차 F3] 경고 임계 원값 0.35(자연키 1.16m 기준) × BodyScale
                if (maxCenterDist > 0.35f * BodyScale)
                    Debug.LogWarning($"{LogTag} ⚠️ {slot} 비주얼 중심이 본에서 {0.35f * BodyScale:F2}m 이상 벗어남 — 포즈 튜닝 필요(slot={slot}, item={itemId})");
            }
            else
            {
                Debug.Log($"{LogTag} ✅ {slot} 비주얼 부착: {itemId} → {string.Join(", ", boneNames)} (활성 렌더러 없음 — 시각 확인 요망)");
            }
            return true;
        }

        // [105차 F3] 폴백 절대 상수의 자연 키 기준 원값 — 플레이어 리그는 Heat.fbx(자연 1.16m)를
        //   TerritoryBuilder.SoldierTargetHeight(1.8m)로 등비 정규화(×1.56)한다.
        //   (근거: Editor.log "[TerritoryBuilder] 병사 크기 정규화 — PlayerModel 높이 1.16m → 1.80m (scale ×1.56)")
        //   69차 원값(1.16m 기준 절대치)은 ×BodyScale로 산정한다 — 하드코딩 결과값 금지(배율 근거 보존).
        const float NaturalBodyHeight = 1.16f;
        static float BodyScale => TerritoryBuilder.SoldierTargetHeight / NaturalBodyHeight;

        // [TEST27-68차] 슬롯별 목표 최대 치수(m) — 67차 Play 실측(헬멧 bounds 1.37m 등, 플레이어 ~1.7m) 기반
        //   [105차 F3] 아래 수치는 69차 원값(자연 1.16m 기준) × BodyScale 산정.
        static readonly Dictionary<EquipmentManager.EquipmentSlot, float> _targetSize =
            new Dictionary<EquipmentManager.EquipmentSlot, float>
        {
            { EquipmentManager.EquipmentSlot.Helmet, 0.34f * BodyScale },
            { EquipmentManager.EquipmentSlot.Armor,  0.62f * BodyScale },
            { EquipmentManager.EquipmentSlot.Shoes,  0.36f * BodyScale },
            { EquipmentManager.EquipmentSlot.Gloves, 0.24f * BodyScale },
            { EquipmentManager.EquipmentSlot.Back,   0.85f * BodyScale },
            { EquipmentManager.EquipmentSlot.Mask,   0.30f * BodyScale },   // [TEST28-69차] 가면(가스 마스크)
            { EquipmentManager.EquipmentSlot.Bag,    0.45f * BodyScale },   // [TEST28-69차] 가방(가스팩)
        };
        // [TEST28-69차] 슬롯별 bounds 중심/앵커 오프셋(본 로컬) — Bottom 앵커용 접지 오프셋
        static readonly Dictionary<EquipmentManager.EquipmentSlot, Vector3> _centerOffset =
            new Dictionary<EquipmentManager.EquipmentSlot, Vector3>
        {
            { EquipmentManager.EquipmentSlot.Helmet, new Vector3(0f, 0.02f, 0f) },   // 투구 최하단 → 머리 본 +2cm
            { EquipmentManager.EquipmentSlot.Armor,  Vector3.zero },
            { EquipmentManager.EquipmentSlot.Shoes,  Vector3.zero },                 // 부츠 최하단 → 발 본
            { EquipmentManager.EquipmentSlot.Gloves, Vector3.zero },
            { EquipmentManager.EquipmentSlot.Back,   new Vector3(0f, 0.02f, 0.10f) }, // 방패 중심 → 손 +전방
        };
        /// <summary>[무번호 29-P11] 슬롯 크기 트림 — 사용자 Play 피드백(14:41 "헬멧 조금 더 크게").
        ///   원값 잠금(ArmorVisualFitGuardTests: 0.34f×BodyScale) 무손상 — 트림은 읽기 배수 경로.
        ///   부착 정규화와 P6 재정규화가 동일 GetTargetSize를 쓰므로 양쪽 일관 적용.</summary>
        internal const float HelmetSizeTrim = 1.25f;   // [무번호 29-P12] +25% — "여전히 작다" 2차 트림(1차 1.15 승격)

        static float ApplySlotSizeTrim(EquipmentManager.EquipmentSlot slot, float baseSize)
            => slot == EquipmentManager.EquipmentSlot.Helmet ? baseSize * HelmetSizeTrim : baseSize;

        static float GetTargetSize(EquipmentManager.EquipmentSlot slot, string itemId)
            => _targetSize.TryGetValue(slot, out var t) ? ApplySlotSizeTrim(slot, t) : 0.5f;
        static Vector3 GetCenterOffset(EquipmentManager.EquipmentSlot slot, string itemId)
            => _centerOffset.TryGetValue(slot, out var o) ? o : Vector3.zero;
        static bool GetBottomAnchor(EquipmentManager.EquipmentSlot slot, string itemId)
            => slot == EquipmentManager.EquipmentSlot.Shoes; // [69차 후속5] Helmet 제외 — 치비 두개골 매몰 수리로 Center 앵커(본+26cm)로 전환, Shoes만 접지 Bottom

        /// <summary>[TEST27-68차] 비주얼 최장축을 목표 크기로 균등 스케일(보정 계수 클램프 0.4~5.0). 적용 계수 반환.</summary>
        static float NormalizeVisualScale(GameObject visual, float targetSize)
        {
            var rends = visual.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return 1f;
            var b = rends[0].bounds;
            foreach (var r in rends)
                if (r.enabled) b.Encapsulate(r.bounds);
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxDim < 0.01f) return 1f;
            float scale = Mathf.Clamp(targetSize / maxDim, 0.12f, 5f);   // [TEST28-69차] 하한 0.4→0.12 — 0.4 클램프가 목표 스케일(부츠 0.26 등)을 잘라 0.55m 잔존(68차 로그 실측)
            visual.transform.localScale *= scale;
            return scale;
        }

        /// <summary>
        /// [2026-09-16] 축 자가 정렬 — GLB 원본 축을 슬롯 규칙에 맞춰 bone-local 공간에서 회전 보정.
        /// Swing-Twist 2단계로 long축→목표(FromToRotation, R1) 후 long축 기준 thin축의 부호 있는 twist(R2).
        /// 위치 앵커는 건드리지 않고 회전만 보정한다. 렌더러가 없거나 축 실측에 실패하면 스킵(기존 동작 유지).
        /// </summary>
        static void ApplySlotAxisAlignment(GameObject visual, Transform bone, EquipmentManager.EquipmentSlot slot,
                                           Animator placeAnim, Vector3 pFwd, Vector3 pLeft)
        {
            if (bone == null || visual == null) return;

            // 1) [69차 후속14] 자작 프레임 — GLB 정점 파싱 + 실착 판정으로 확정한 자작 축:
            //    up = -Z 공통 / front 부호는 슬롯별 상이 — 투구·가면·신발·갑옷=+Y, 가방·방패·장갑=-Y(테스트 33/34 실측).
            //    extent 랭킹/부르주/고정 yaw 전부 폐기 — 자작 up/front를 월드 up/전방에 직접 대응.
            Vector3 sUpW = bone.TransformDirection(new Vector3(0f, 0f, -1f));
            float frontSign = (slot == EquipmentManager.EquipmentSlot.Bag
                               || slot == EquipmentManager.EquipmentSlot.Back
                               || slot == EquipmentManager.EquipmentSlot.Gloves) ? -1f : 1f;
            Vector3 sFwdW = bone.TransformDirection(new Vector3(0f, frontSign, 0f));
            if (sUpW.sqrMagnitude < 0.0001f || sFwdW.sqrMagnitude < 0.0001f) return;
            sUpW.Normalize();
            sFwdW.Normalize();

            // 2) 슬롯별 목표 프레임(월드): up/전방. 장갑은 자작 up=팔꿈치→손가락, 방패는 면=바깥.
            Vector3 tgtUpW = Vector3.up;
            Vector3 tgtFwdW = pFwd;
            switch (slot)
            {
                case EquipmentManager.EquipmentSlot.Gloves:
                {
                    bool isL = bone.name.ToLowerInvariant().Contains("left");
                    var elbowBone = placeAnim != null
                        ? placeAnim.GetBoneTransform(isL ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm)
                        : null;
                    if (elbowBone != null)
                    {
                        var fd = bone.position - elbowBone.position;
                        if (fd.sqrMagnitude > 0.0001f) tgtUpW = fd.normalized;  // 자작 up(-Z)=팔꿈치→손가락 → 팔 방향
                    }
                    break;
                }
                case EquipmentManager.EquipmentSlot.Back:
                    tgtFwdW = pLeft;   // 방패 면(자작 front=-Y) = 바깥쪽
                    break;
            }

            // 3) 회전 구성(월드 공간 — 본 로컬 euler 혼란 제거): 스윙(up 정렬) + 트위스트(front 정렬).
            Quaternion R = Quaternion.FromToRotation(sUpW, tgtUpW);
            Vector3 f1 = R * sFwdW;
            Vector3 tgtF = tgtFwdW - tgtUpW * Vector3.Dot(tgtFwdW, tgtUpW);
            if (tgtF.sqrMagnitude > 0.0001f)
            {
                tgtF.Normalize();
                Vector3 fProj = f1 - tgtUpW * Vector3.Dot(f1, tgtUpW);
                if (fProj.sqrMagnitude > 0.0001f)
                {
                    fProj.Normalize();
                    float ang = Vector3.SignedAngle(fProj, tgtF, tgtUpW);
                    R = Quaternion.AngleAxis(ang, tgtUpW) * R;
                }
            }
            visual.transform.rotation = R * visual.transform.rotation;

            // 4) 실측 로그 1줄/본.
            Debug.Log($"[ArmorVisual] 정렬 {slot}: 자작프레임(up=-Z,front=-Y) 매핑 → worldEuler={visual.transform.rotation.eulerAngles.ToString("F0")} (bone={bone.name})");
        }

        /// <summary>bone-local(=현재 localRotation 동일) 공간의 AABB+정점 중심 실측. 렌더러 없으면 false(정렬 스킵).</summary>
        static bool MeasureRootLocalAABB(GameObject visual, out Bounds aabb, out Vector3 centroid)
        {
            aabb = default; centroid = default;
            var visualT = visual.transform;
            Matrix4x4 w2v = visualT.worldToLocalMatrix;
            var rends = visual.GetComponentsInChildren<Renderer>(true);
            bool init = false;
            Bounds acc = default;
            Renderer firstRend = null; Mesh firstMesh = null;
            foreach (var r in rends)
            {
                if (r == null || !r.enabled) continue;
                Mesh mesh = null;
                var smr = r as SkinnedMeshRenderer; if (smr != null && smr.sharedMesh != null) mesh = smr.sharedMesh;
                if (mesh == null)
                {
                    // MeshFilter는 Renderer의 하위 타입이 아니므로(as 불가) 같은 GO에서 GetComponent로 조회.
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) mesh = mf.sharedMesh;
                }
                // mesh bounds는 로컬 좌표, renderer.bounds는 이미 월드 좌표 → 각각 알맞은 변환 행렬 사용.
                Matrix4x4 useL2W = mesh != null ? r.transform.localToWorldMatrix : Matrix4x4.identity;
                Bounds src = mesh != null ? mesh.bounds : r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = new Vector3((i & 1) != 0 ? src.max.x : src.min.x, (i & 2) != 0 ? src.max.y : src.min.y, (i & 4) != 0 ? src.max.z : src.min.z);
                    Vector3 v = w2v.MultiplyPoint3x4(useL2W.MultiplyPoint3x4(p));
                    if (!init) { acc = new Bounds(v, Vector3.zero); init = true; }
                    else acc.Encapsulate(v);
                }
                if (firstMesh == null && mesh != null) { firstMesh = mesh; firstRend = r; }
            }

            if (!init) return false;
            aabb = acc;

            // 정점 중심(centroid) — 첫 메시 최대 2000개 샘플링, world→visual-root-local 변환 후 평균.
            if (firstMesh != null && firstRend != null)
            {
                var verts = firstMesh.vertices;
                int n = verts.Length;
                int step = Mathf.Max(1, n / 2000);
                Vector3 csum = Vector3.zero;
                Matrix4x4 l2wM = firstRend.transform.localToWorldMatrix;
                int cnt = 0;
                for (int i = 0; i < n; i += step)
                {
                    Vector3 v = w2v.MultiplyPoint3x4(l2wM.MultiplyPoint3x4(verts[i]));
                    csum += v; cnt++;
                }
                if (cnt > 0) centroid = csum / cnt;
            }
            return true;
        }

        /// <summary>[TEST28-69차] 비주얼을 본에 스냅 — 앵커 모드: Helmet/Boots=Bottom(쓰고/신는 배치), 나머지=Center.
        /// worldAnchor 지정 시 본 무관 월드 좌표(플레이어 몸 비례)에 배치.</summary>
        static void SnapVisualToBone(GameObject visual, Transform bone, Vector3 boneLocalOffset, bool bottomAnchor, Vector3? worldAnchor = null)
        {
            var rends = visual.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0 || bone == null) return;
            var b = rends[0].bounds;
            foreach (var r in rends)
                if (r.enabled) b.Encapsulate(r.bounds);
            Vector3 desired = worldAnchor.HasValue ? worldAnchor.Value : bone.TransformPoint(boneLocalOffset);
            Vector3 anchorPoint = bottomAnchor
                ? new Vector3(b.center.x, b.min.y, b.center.z)   // 최하단 중심 — 투구는 머리에 "쓰고", 부츠는 발에 "신는"
                : b.center;
            visual.transform.position += desired - anchorPoint;
        }

        /// <summary>
        /// [2026-09-16] 실측 피팅 — 플레이어 메시 실측 부위(part)에 비주얼을 배치.
        /// 슬롯별 균등 스케일 보정 후 월드 배치. part.valid=false면 false(기존 상수/스냅 폴백 사용). 성공 true 반환.
        /// part 비유효(정점 <5) 시 즉시 폴백 — 상수 경로(세계 고정치)로 기존대로 진행된다.
        /// </summary>
        static bool FitVisualToBodyPart(GameObject visual, EquipmentManager.EquipmentSlot slot, Transform bone,
                                        PlayerBodyMeasure.BodyPart part, Vector3 pFwd)
        {
            if (visual == null || !part.valid) return false;

            var rends = visual.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return false;
            var b = rends[0].bounds;
            foreach (var r in rends)
                if (r.enabled) b.Encapsulate(r.bounds);
            if (b.size.x < 0.001f && b.size.y < 0.001f && b.size.z < 0.001f) return false;

            // [무번호 25-A2] 부위별 배율 전 부위 1.0 고정(수리) — 실측 bounds 기반 이중 스케일링
            //   (Helmet ×1.18/Armor ×1.25/Shoes ×1.15/Gloves ×2.8/Mask ×1.15/Bag ×1.05)이 장비 과대의 뿌리.
            //   NormalizeVisualScale 목표 최장축(_targetSize 원값 × BodyScale = 1.8m 캐릭터키 고정계수 —
            //   SoldierTargetHeight 1.8 / NaturalBodyHeight 1.16) 단일 스케일링으로 정리 —
            //   측정 게이트(IsPartPlausible)와 실측 part 중심 배치(아래 target 스위치)는 유지.
            float scale = 1f;
            switch (slot)
            {
                case EquipmentManager.EquipmentSlot.Helmet:
                case EquipmentManager.EquipmentSlot.Armor:
                case EquipmentManager.EquipmentSlot.Shoes:
                case EquipmentManager.EquipmentSlot.Gloves:
                case EquipmentManager.EquipmentSlot.Mask:
                case EquipmentManager.EquipmentSlot.Bag:
                    scale = 1f;   // 전 부위 1.0 고정 — 실측 bounds 배율 폐기(계획서 A2 수리)
                    break;
                default:
                    return false;
            }
            scale = Mathf.Clamp(scale, 0.3f, 3.0f);
            // [2026-09-16 69차 후속10] 헬멧 치수 상한 캡 — 헤어 스파이크가 Head 영역 bounds를 비정상적으로
            //   키울 때(테스트 30: part=(0.73,0.25,0.54) → scale보정 x3.00 클램프 폭발) 투구 과대 방지.
            if (slot == EquipmentManager.EquipmentSlot.Helmet)
            {
                float helmMax = Mathf.Max(b.size.x, b.size.z) * scale;
                // [105차 F3] 치수 캡 원값 0.6(자연키 1.16m 기준) × BodyScale
                float helmCap = 0.6f * BodyScale;
                if (helmMax > helmCap) scale *= helmCap / helmMax;
            }
            visual.transform.localScale *= scale;

            // 스케일 후 bounds 재계산
            b = rends[0].bounds;
            foreach (var r in rends)
                if (r.enabled) b.Encapsulate(r.bounds);

            Vector3 partCenter = part.worldBounds.center;
            Vector3 target = b.center;
            switch (slot)
            {
                case EquipmentManager.EquipmentSlot.Helmet:
                    // [2026-09-16 후속12] 두개골 중심보다 위로 — 투구가 머리를 "쓴다"(낮게 안착 방지)
                    target = partCenter + Vector3.up * (part.worldBounds.size.y * 0.08f);
                    break;
                case EquipmentManager.EquipmentSlot.Armor:
                    target = partCenter;
                    break;
                case EquipmentManager.EquipmentSlot.Gloves:
                {
                    // [2026-09-16 69차 후속15] 손등 커버 — 손목 뭉침 개선: 손 중심에서 손가락 방향으로 15% 전진
                    //   (장갑이 손등+손가락까지 덮도록) + 바깥 오프셋 0.15 유지.
                    Vector3 outward = Vector3.zero;
                    var pvGo = GameObject.FindWithTag(PlayerTag);
                    if (pvGo != null)
                    {
                        outward = bone.position - pvGo.transform.position;
                        outward.y = 0f;
                    }
                    if (outward.sqrMagnitude < 0.0001f) outward = Vector3.left;
                    Vector3 towardFingers = partCenter - bone.position;
                    towardFingers.y *= 0.3f;
                    if (towardFingers.sqrMagnitude < 0.0001f) towardFingers = pFwd;
                    float gloveMax = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    target = partCenter
                           + outward.normalized * (gloveMax * 0.15f)
                           + towardFingers.normalized * (gloveMax * 0.15f);
                    break;
                }
                case EquipmentManager.EquipmentSlot.Shoes:
                    // 발바닥 접지 — center.xz → part.center.xz, min.y → part.min.y − 0.02(접지)
                    //   [후속15] xz를 발 중심에서 뒤꿈치 쪽으로 8% 후퇴 — 테스트 35: 앞쪽 쏠림 실측.
                    target = new Vector3(partCenter.x, b.center.y, partCenter.z);
                    target -= pFwd * (b.size.z * 0.08f);
                    target.y += part.worldBounds.min.y - 0.02f - b.min.y;
                    break;
                case EquipmentManager.EquipmentSlot.Mask:
                {
                    // faceCenter = part.center + pFwd*(part.size.z/2); 마스크 전체가 얼굴 앞에 살짝 떨어져 착용
                    //   ([후속14] 전방 면 = 얼굴 표면 +0.55·b.z — 얼굴에서 앞쪽으로 떨어뜨림, 사용자 지시), y는 눈높이.
                    Vector3 faceCenter = partCenter + pFwd * (part.worldBounds.size.z * 0.5f);
                    target = faceCenter + pFwd * (b.size.z * 0.55f - b.size.z * 0.5f);
                    target.y = partCenter.y + part.worldBounds.size.y * 0.15f; // 눈높이 ([2026-09-16] 0.1→0.15)
                    break;
                }
                case EquipmentManager.EquipmentSlot.Bag:
                {
                    // backCenter = part.center − pFwd*(part.size.z/2); 등 표면에 45% 매몰([후속13] 35→45% — 더 밀착).
                    Vector3 backCenter = partCenter - pFwd * (part.worldBounds.size.z * 0.5f);
                    target = backCenter - pFwd * (b.size.z * 0.5f - b.size.z * 0.45f);
                    break;
                }
            }
            visual.transform.position += target - b.center;

            Debug.Log($"[ArmorVisual] 피팅 {slot}: part={part.worldBounds.size.ToString("F2")} scale보정=x{scale:F2} center={visual.transform.position.ToString("F2")}");
            return true;
        }

        // [105차 F2] 부위 측정 정상성 게이트 — 순수 정적 판정(테스트 직접 호출: internal +
        //   InternalsVisibleTo(ProjectName.Tests.EditMode) — NPCDialogueAdapter 어셈블리 선언 선례).
        //   근거: 치비 리그 두개골 하단 = 키의 57%(69차 실측 Head 본 로컬 0.57).
        //   오염 bounds(창 정점 등)가 밴드를 벗어나면 false → 호출부가 폴백 상수 경로로 강등.
        internal static bool IsPartPlausible(EquipmentManager.EquipmentSlot slot, Bounds part, Bounds fullBody)
        {
            float H = fullBody.size.y;
            if (H < 0.5f) return false; // 전신 높이 미달 = 측정 무효
            switch (slot)
            {
                case EquipmentManager.EquipmentSlot.Helmet:
                case EquipmentManager.EquipmentSlot.Mask:
                    return part.size.y <= 0.55f * H
                        && part.center.y >= fullBody.min.y + 0.45f * H;
                case EquipmentManager.EquipmentSlot.Armor:
                case EquipmentManager.EquipmentSlot.Bag:
                    return part.size.x <= 0.75f * H
                        && part.size.y <= 0.55f * H
                        && part.center.y >= fullBody.min.y + 0.25f * H;
                case EquipmentManager.EquipmentSlot.Shoes:
                    return part.size.y <= 0.25f * H
                        && part.center.y <= fullBody.min.y + 0.20f * H;
                case EquipmentManager.EquipmentSlot.Gloves:
                {
                    float maxAxis = Mathf.Max(part.size.x, Mathf.Max(part.size.y, part.size.z));
                    return maxAxis <= 0.40f * H;
                }
                default:
                    return true; // 게이트 대상 외 슬롯(Back 등) — 기존 경로 유지
            }
        }

        // [무번호 27-P1] Unity 기본 도형 메시 판정 — GameObject.CreatePrimitive가 부여하는 sharedMesh 이름.
        //   아바타 GLB/FBX(스킨/리그 메시)와 구분되는 결정론적 placeholder 서명 — 침하 감시용 placeholder
        //   프리미티브(테스트 씬 부착, 제거 금지)를 바디 측정에서만 걸러낸다. 테스트 직접 호출(internal +
        //   InternalsVisibleTo(ProjectName.Tests.EditMode) — IsPartPlausible 선례).
        internal static bool IsUnityPrimitivePlaceholder(Renderer r)
        {
            if (r == null || r is SkinnedMeshRenderer) return false; // 아바타 스킨 메시(본체)는 대상 아님
            var mf = r.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) return false;
            switch (mesh.name)
            {
                case "Cube":
                case "Sphere":
                case "Capsule":
                case "Cylinder":
                case "Plane":
                case "Quad":
                    return true; // CreatePrimitive 기본 도형 = placeholder 서명
                default:
                    return false; // GLB/FBX 자체 이름 메시 — 정상 측정 대상
            }
        }

        /// <summary>
        /// [2026-09-16] 플레이어 메시 정점 실측 기반 바디 피팅 헬퍼 — 상수/추정을 폐기하고
        /// 실제 몸 메시 정점을 본 월드 y/거리 기준으로 영역 분류해 각 부위의 월드 Bounds를 측정한다.
        /// Humanoid 매핑 실패(Generic/FBX) 시 이름 기반 본 재탐색 폴백(FindBoneByName 재사용).
        /// </summary>
        private class PlayerBodyMeasure
        {
            public struct BodyPart
            {
                public Bounds worldBounds;
                public bool valid;
            }

            /// <summary>[무번호 29-P5] 직전 측정의 정점 신뢰성 — r.bounds 교차검증 실패 시 true(재피팅 판정 소비용).</summary>
            internal static bool LastVertexUnreliable;

            public static bool TryMeasurePlayerBody(Animator placeAnim, IReadOnlyList<Transform> excludeRoots,
                out BodyPart head, out BodyPart torso, out BodyPart leftFoot, out BodyPart rightFoot,
                out BodyPart leftHand, out BodyPart rightHand, out BodyPart fullBody)
            {
                head = default; torso = default; leftFoot = default; rightFoot = default;
                leftHand = default; rightHand = default; fullBody = default;
                LastVertexUnreliable = false;   // [무번호 29-P5] 측정마다 리셋

                var player = GameObject.FindWithTag(PlayerTag);
                if (player == null) return false;

                // 본 월드 y 기준점 — Humanoid 매핑 실패 시 이름 검색 폴백.
                Transform headBone = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.Head) : null;
                if (headBone == null && placeAnim != null) headBone = FindBoneByName(placeAnim, new[] { "head" });
                Transform spineBone = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.Spine) : null;
                if (spineBone == null && placeAnim != null) spineBone = FindBoneByName(placeAnim, new[] { "spine" });

                Transform lf = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
                if (lf == null && placeAnim != null) lf = FindBoneByName(placeAnim, new[] { "leftfoot", "left_foot", "foot_l" });
                Transform rf = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.RightFoot) : null;
                if (rf == null && placeAnim != null) rf = FindBoneByName(placeAnim, new[] { "rightfoot", "right_foot", "foot_r" });
                Transform lh = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.LeftHand) : null;
                if (lh == null && placeAnim != null) lh = FindBoneByName(placeAnim, new[] { "lefthand", "left_hand", "hand_l" });
                Transform rh = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (rh == null && placeAnim != null) rh = FindBoneByName(placeAnim, new[] { "righthand", "right_hand", "hand_r" });

                float headThreshold = headBone != null ? headBone.position.y - 0.05f : float.PositiveInfinity;
                float torsoLow = spineBone != null ? spineBone.position.y - 0.20f : float.NegativeInfinity;

                // [105차 F3] 반경 const 폐기 — 자연키(1.16m) 원값 ×BodyScale 산정(1.8m 정규화 동행).
                float footRadius = 0.28f * BodyScale; // [2026-09-16] 발 반경 원값 0.35→0.28 — 발목/정강이 오염 감소
                float handRadius = 0.25f * BodyScale; // [2026-09-16] 손 반경 원값 0.35→0.25 — 전완 오염 감소
                float footR2 = footRadius * footRadius;
                float handR2 = handRadius * handRadius;

                int headCnt = 0, torsoCnt = 0, lfCnt = 0, rfCnt = 0, lhCnt = 0, rhCnt = 0;
                bool hI = false, tI = false, lfI = false, rfI = false, lhI = false, rhI = false;
                Bounds hb = default, tb = default, lfb = default, rfb = default, lhb = default, rhb = default;
                int excludedRends = 0; // [105차 F4] 제외한 렌더러 수(진단)
                // [무번호 27-P1/P3] 제외 렌더러 이름 목록 + 오염 원인 분류 — measure-diag 보강(P3)과
                //   오염원 특정(P1) 재료. 이름은 "이름(사유)" 결합 문자열, 원인은 중복 없이 적립.
                var excludedNames = new List<string>();
                var pollutionCauses = new List<string>();
                void AddCause(string cause)
                {
                    if (!pollutionCauses.Contains(cause)) pollutionCauses.Add(cause);
                }

                const int PerRendererSamples = 1500; // [2026-09-16] 렌더러별 독립 예산 — 전역 상한(3000) 폐기: 앞 렌더러(몸통)가 예산을 소진해
                //   뒤 렌더러(머리/헤어/손/발)가 정점 0개만 기여 → 부위 과소 측정(헬멧 높이 0.15m 등)의 뿌리. 각 렌더러는 무조건
                //   stride 샘플링(≤1500)으로 기여해 모든 부위가 측정에 반영된다.
                var rends = player.GetComponentsInChildren<Renderer>(true);
                // [69차 후속10] 바디 측정 정점 접근 안전화:
                //   - SkinnedMeshRenderer는 sharedMesh.vertices 대신 BakeMesh로 현재 포즈를 읽는다(isReadable 무관).
                //   - MeshFilter는 isReadable == false면 조용히 스킵(해당 렌더러 0 기여 — 다른 렌더러는 계속).
                //   - 각 렌더러 처리를 try-catch로 감싸 어떤 예외가 나도 전체 측정이 중단되지 않게 한다.
                bool rendererWarned = false;

                // [105차 F2] 1단계 — 전신(fullBody) bounds 누적용 정점 샘플 수집.
                //   분류(2단계)는 fullBody 확정 후 수행한다: 본 정상성 교차검증이 밴드 임계를
                //   비율 밴드로 대체할 수 있어 분류 임계가 측정 도중에 확정되지 않기 때문.
                var samples = new List<Vector3>(1024);
                bool fullInit = false;
                Bounds fullBounds = default;
                bool rFullInit = false;          // [무번호 29-P5] r.bounds 기반 전신 후보(교차검증 심판 데이터)
                Bounds rFullBounds = default;
                var contribs = new List<string>(); // [무번호 27-P1] 렌더러별 정점 샘플 실측(오염원 특정 재료 — 이름+y범위+개수)
                foreach (var r in rends)
                {
                    if (r == null || !r.enabled) continue;
                    // [69차 후속11] 장비 비주얼 계열 제외 — 부위 측정 오염(재장착 커짐 버그) 차단
                    if (excludeRoots != null)
                    {
                        bool excluded = false;
                        foreach (var exRoot in excludeRoots)
                        {
                            if (exRoot != null && r.transform.IsChildOf(exRoot)) { excluded = true; break; }
                        }
                        if (excluded)
                        {
                            excludedRends++; // [105차 F4] 제외 집계
                            excludedNames.Add(r.name + "(장비/무기 루트)"); // [무번호 27-P3] 제외 이름 목록
                            continue;
                        }
                    }
                    // [무번호 27-P1] 제외 정책 ① — 비활성 GameObject 렌더러(아바타 교체 잔재/비실행 대상) — 하드 계약.
                    //   GetComponentsInChildren(true)는 비활성 GO의 Renderer까지 열거하며 r.enabled는 컴포넌트
                    //   자체 플래그라 GO 비활성과 무관 → activeInHierarchy 검사가 없으면 교체 잔재 아바타가
                    //   측정에 기여한다(fullBody 과대 오염 경로).
                    if (!r.gameObject.activeInHierarchy)
                    {
                        excludedRends++;
                        excludedNames.Add(r.name + "(비활성GO)");
                        AddCause("아바타 교체 잔재(비활성GO)");
                        continue;
                    }
                    // [무번호 27-P1] 제외 정책 ② — 침하 감시용 placeholder 프리미티브(테스트 씬 부착) — 하드 계약.
                    //   Unity 기본 도형 메시(Cube/Sphere/Capsule/Cylinder/Plane/Quad)는 아바타 GLB/FBX 구성원이
                    //   아니므로 플레이어 루트 아래 존재 시 실측 fullBody=4.34m(계약 1.8m ×2.4) 과대의 오염원.
                    //   침하 감시 placeholder는 유지(제거 금지) — 바디 측정에서만 제외한다.
                    if (IsUnityPrimitivePlaceholder(r))
                    {
                        excludedRends++;
                        excludedNames.Add(r.name + "(placeholder 프리미티브)");
                        AddCause("테스트 씬 placeholder 프리미티브");
                        continue;
                    }
                    try
                    {
                        Vector3[] verts = new Vector3[0];
                        var smr = r as SkinnedMeshRenderer;
                        Vector3[] vertsS = new Vector3[0];   // [무번호 29-P5] 후보C용 스케일 내장 베이크 정점
                        if (smr != null)
                        {
                            Mesh tmp = new Mesh();
                            try { smr.BakeMesh(tmp); }
                            catch (System.Exception) { tmp = null; }
                            if (tmp != null)
                            {
                                verts = tmp.vertices;
                                Object.Destroy(tmp); // 즉시 해제 — 에디터/런타임 겸용 Destroy
                            }
                            // [무번호 29-P5] 후보C — useScale 베이크(스케일 내장). 후보A/B 행렬 매핑이 이 리그
                            //   (rootBone=Hips+휴머노이드 애니)에서 모두 실패한 실측(2.43/1.32 vs refBounds 1.88)의 제3 해법.
                            //   C도 실패하면 아래 r.bounds 교차검증 게이트가 폴백 상수 경로로 강제한다.
                            Mesh tmpS = new Mesh();
                            try { smr.BakeMesh(tmpS, true); }
                            catch (System.Exception) { tmpS = null; }
                            if (tmpS != null)
                            {
                                vertsS = tmpS.vertices;
                                Object.Destroy(tmpS); // 즉시 해제 — 에디터/런타임 겸용 Destroy
                            }
                        }
                        if (verts.Length == 0)
                        {
                            var mf = r.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                                verts = mf.sharedMesh.vertices;
                        }
                        int n = verts.Length;
                        if (n == 0) continue;
                        int step = Mathf.Max(1, n / PerRendererSamples);
                        // [무번호 28-P4b2] SMR 매핑 정합 — 플레이어 SMR은 rootBone=Hips라 BakeMesh 정점 공간과
                        //   renderer transform이 Play 애니 상태에서 어긋나 "transform L2W 근사"가 측정 과대(2.43m)를 낳는다.
                        //   후보A(transform L2W)와 후보B(rootBone L2W)를 모두 계산해 Unity 자체 bounds(r.bounds) y스팬과의
                        //   절대 오차가 작은 쪽을 채택한다(동률 시 A 유지 — 기존 동작 보존). 일반(MeshFilter) 경로 무수정.
                        Matrix4x4 l2w;
                        if (smr != null)
                        {
                            Matrix4x4 l2wA = r.transform.localToWorldMatrix; // 후보A(기존 근사)
                            // 후보B = (r.rootBone != null ? r.rootBone : r.transform).localToWorldMatrix — 브리프 §2-1 원형 표기
                            //   (측정 루프의 r는 Renderer라 rootBone 접근이 불가 → 동일 의미의 SMR 변수로 기술, null 가드 동일)
                            Matrix4x4 l2wB = (smr.rootBone != null ? smr.rootBone : smr.transform).localToWorldMatrix;
                            Matrix4x4 l2wC = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one); // [무번호 29-P5] 후보C — 무스케일 행렬
                            bool aInit = false, bInit = false; float aMinY = 0f, aMaxY = 0f, bMinY = 0f, bMaxY = 0f;
                            bool csInit = false; float csMinY = 0f, csMaxY = 0f;
                            int nS = vertsS.Length; int stepS = nS > 0 ? Mathf.Max(1, nS / PerRendererSamples) : 1;
                            for (int i = 0; i < n; i += step)
                            {
                                float ya = l2wA.MultiplyPoint3x4(verts[i]).y;
                                float yb = l2wB.MultiplyPoint3x4(verts[i]).y;
                                if (!aInit) { aMinY = ya; aMaxY = ya; aInit = true; }
                                else { if (ya < aMinY) aMinY = ya; if (ya > aMaxY) aMaxY = ya; }
                                if (!bInit) { bMinY = yb; bMaxY = yb; bInit = true; }
                                else { if (yb < bMinY) bMinY = yb; if (yb > bMaxY) bMaxY = yb; }
                            }
                            for (int i = 0; i < nS; i += stepS)
                            {
                                float yc = l2wC.MultiplyPoint3x4(vertsS[i]).y;
                                if (!csInit) { csMinY = yc; csMaxY = yc; csInit = true; }
                                else { if (yc < csMinY) csMinY = yc; if (yc > csMaxY) csMaxY = yc; }
                            }
                            float refBoundsSpan = r.bounds.size.y; // Unity 자체 계산 bounds — 채택 심사 기준
                            float errA = aInit ? Mathf.Abs((aMaxY - aMinY) - refBoundsSpan) : float.PositiveInfinity;
                            float errB = bInit ? Mathf.Abs((bMaxY - bMinY) - refBoundsSpan) : float.PositiveInfinity;
                            float errC = csInit ? Mathf.Abs((csMaxY - csMinY) - refBoundsSpan) : float.PositiveInfinity;
                            bool useC = errC < errA && errC < errB;   // [무번호 29-P5] 3후보 최소 오차(동률 시 A 규약 유지)
                            bool useB = !useC && errB < errA;         // 동률 시 A 유지(기존 규약)
                            l2w = useC ? l2wC : (useB ? l2wB : l2wA);
                            if (useC) verts = vertsS;                 // 후보C 채택 시 스케일 내장 정점으로 측정 본체 스왑
                            // [28-P4b2→29-P5] 채택 결과 + 3후보 스팬 로그 1줄 — WarnOnce 키 smr-map:{scene}(스팸 가드 관행)
                            WarnOnce($"smr-map:{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}",
                                $"{LogTag} [29-P5] SMR 매핑 정합 — {r.name}: transformSpan={aMaxY - aMinY:F2}m rootBoneSpan={bMaxY - bMinY:F2}m scaleBakeSpan={(csInit ? csMaxY - csMinY : 0f):F2}m refBounds={refBoundsSpan:F2}m 채택={(useC ? "C" : (useB ? "B" : "A"))}(rootBone={(smr.rootBone != null ? smr.rootBone.name : "null")})");
                        }
                        else
                        {
                            l2w = r.transform.localToWorldMatrix; // 일반(MeshFilter) — 기존 경로 무수정
                        }
                        // [무번호 29-P5] r.bounds 기반 전신 후보 병행 누적 — 제외 정책(27-P1) 통과 렌더러만(오염원 재유입 방지).
                        if (rFullInit) rFullBounds.Encapsulate(r.bounds);
                        else { rFullBounds = r.bounds; rFullInit = true; }
                        bool cInit = false; float cMinY = 0f, cMaxY = 0f; int cCnt = 0; // [무번호 27-P1] 렌더러별 실측
                        for (int i = 0; i < n; i += step)
                        {
                            Vector3 v = l2w.MultiplyPoint3x4(verts[i]);
                            // [105차 F2] 전신 bounds — 분류 이전 샘플 전부 누적
                            if (!fullInit) { fullBounds = new Bounds(v, Vector3.zero); fullInit = true; }
                            else fullBounds.Encapsulate(v);
                            // [무번호 27-P1] 렌더러별 정점 샘플 y범위 누적 — 오염원 특정 재료
                            if (!cInit) { cMinY = v.y; cMaxY = v.y; cInit = true; }
                            else { if (v.y < cMinY) cMinY = v.y; if (v.y > cMaxY) cMaxY = v.y; }
                            samples.Add(v);
                            cCnt++;
                        }
                        // [무번호 27-P1] 렌더러별 정점 샘플 기록 — fullBody 과대 시 아래 오염 특정 로그에서 직독
                        contribs.Add($"{r.name} y[{cMinY:F2}~{cMaxY:F2}] n={cCnt}");
                    }
                    catch (System.Exception ex)
                    {
                        if (!rendererWarned)
                        {
                            rendererWarned = true;
                            Debug.LogWarning($"[ArmorVisual] 바디 측정 스킵: {r.name} ({ex.Message})");
                        }
                    }
                }

                // [무번호 27-P1/P3] 씬 이름 — 오염 특정 로그와 measure-diag 키가 공유(기존 diag 선언 위치에서 상향).
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                // [무번호 27-P1] fullBody 과대 오염 실측 특정 — 계약 키(1.8m) 대비 1.2배 초과 시
                //   렌더러별 정점 샘플 y범위를 Play 증거 로그로 남긴다(placeholder 프리미티브/비활성 잔재/
                //   제외 누락 무기 GLB 등 후보를 로그에서 직독). 정규화 아바타(정확 1.8m)는 미발화,
                //   실측 오염(4.34m = ×2.4)은 발화. 측정·피팅 경로는 기존 F2 게이트 그대로 유지.
                const float PollutionContractRatio = 1.2f;
                if (fullInit && fullBounds.size.y > PollutionContractRatio * TerritoryBuilder.SoldierTargetHeight)
                    WarnOnce($"measure-pollute:{sceneName}",
                        $"{LogTag} 바디 측정 오염 의심 — fullBody 높이 {fullBounds.size.y:F2}m > 계약 {TerritoryBuilder.SoldierTargetHeight:F2}m ×{PollutionContractRatio:F1}"
                        + $" — 렌더러별 정점 샘플: {(contribs.Count > 0 ? string.Join(", ", contribs) : "샘플 0개")}");

                // [105차 F2] 전신 유효성 게이트 — H < 0.5m면 측정 전체 무효(모든 부위 invalid → 폴백 상수 경로).
                bool fullValid = fullInit && fullBounds.size.y >= 0.5f;
                if (fullInit && !fullValid)
                    WarnOnce("measure-invalid", $"{LogTag} 바디 측정 무효 — 전신 높이 {fullBounds.size.y:F2}m(<0.5m) — 폴백 상수 사용");

                // [무번호 29-P5] r.bounds 교차검증 게이트 — 채택 매핑의 전신 스팬이 Unity 자체 r.bounds 전신과
                //   ±10% 초과 괴차면 정점 측정 자체를 무효로 판정한다(11-13 Play 실측: A=2.43/B=1.32 vs refBounds=1.88
                //   — 두 후보 모두 실패, 심판이 "덜 틀린" 쪽을 채택해도 피팅은 허측값을 따름). 무효 시 fullValid=false →
                //   기존 "측정 전체 무효 → 모든 부위 invalid → 폴백 상수 경로"(105차 F2 선례)가 그대로 발동한다 —
                //   신뢰 불가 측정으로 계약을 깨지 않는 종결책.
                if (fullValid && rFullInit)
                {
                    float vSpan = fullBounds.size.y;
                    float rSpan = rFullBounds.size.y;
                    if (rSpan > 0.01f && Mathf.Abs(vSpan - rSpan) > 0.10f * rSpan)
                    {
                        fullValid = false;
                        LastVertexUnreliable = true;
                        WarnOnce($"measure-unreliable:{sceneName}",
                            $"{LogTag} [29-P5] 정점 측정 무효 — vertexSpan={vSpan:F2}m vs refBounds={rSpan:F2}m 괴차 {Mathf.Abs(vSpan - rSpan) / rSpan * 100f:F0}% > 10% → 폴백 상수+본 앵커 경로 강제");
                    }
                }

                // [105차 F2] 본 정상성 교차검증(진단용) — 본 월드 y가 fullBody 대비 계약 밴드
                //   (Head 0.35~0.85×H / Spine 0.15~0.60×H / Foot ≤0.15×H)를 벗어나면 경고 1회 +
                //   해당 본 기준 밴드 임계를 비율 밴드로 대체(headThreshold = 하단+0.57×H —
                //   두개골 하단 ≈ 키의 57%(69차 실측) / torsoLow = 하단+0.20×H).
                if (fullValid)
                {
                    float H = fullBounds.size.y;
                    float minY = fullBounds.min.y;
                    bool headSuspect = false, spineSuspect = false, footSuspect = false;
                    if (headBone != null)
                    {
                        float r = (headBone.position.y - minY) / H;
                        if (r < 0.35f || r > 0.85f) { headSuspect = true; headThreshold = minY + 0.57f * H; }
                    }
                    if (spineBone != null)
                    {
                        float r = (spineBone.position.y - minY) / H;
                        if (r < 0.15f || r > 0.60f) { spineSuspect = true; torsoLow = minY + 0.20f * H; }
                    }
                    if (lf != null && (lf.position.y - minY) / H > 0.15f) footSuspect = true;
                    if (headSuspect || spineSuspect || footSuspect)
                        WarnOnce("bone-sanity",
                            $"{LogTag} 본 매핑 의심 — 본 월드 y가 계약 밴드 이탈(head={headSuspect}, spine={spineSuspect}, foot={footSuspect}) — 비율 밴드 임계로 대체");
                }

                // 2단계 분류 — 확정된(필요 시 비율 밴드로 대체된) 임계로 부위 bounds 구성
                if (fullValid)
                {
                    foreach (var v in samples)
                    {
                        if (headBone != null && v.y >= headThreshold)
                        {
                            if (!hI) { hb = new Bounds(v, Vector3.zero); hI = true; } else hb.Encapsulate(v);
                            headCnt++;
                            continue;
                        }
                        if (spineBone != null && v.y >= torsoLow && v.y < headThreshold)
                        {
                            if (!tI) { tb = new Bounds(v, Vector3.zero); tI = true; } else tb.Encapsulate(v);
                            torsoCnt++;
                            continue;
                        }
                        if (lf != null && (v - lf.position).sqrMagnitude <= footR2)
                        {
                            if (!lfI) { lfb = new Bounds(v, Vector3.zero); lfI = true; } else lfb.Encapsulate(v);
                            lfCnt++;
                            continue;
                        }
                        if (rf != null && (v - rf.position).sqrMagnitude <= footR2)
                        {
                            if (!rfI) { rfb = new Bounds(v, Vector3.zero); rfI = true; } else rfb.Encapsulate(v);
                            rfCnt++;
                            continue;
                        }
                        if (lh != null && (v - lh.position).sqrMagnitude <= handR2)
                        {
                            if (!lhI) { lhb = new Bounds(v, Vector3.zero); lhI = true; } else lhb.Encapsulate(v);
                            lhCnt++;
                            continue;
                        }
                        if (rh != null && (v - rh.position).sqrMagnitude <= handR2)
                        {
                            if (!rhI) { rhb = new Bounds(v, Vector3.zero); rhI = true; } else rhb.Encapsulate(v);
                            rhCnt++;
                            continue;
                        }
                    }
                }

                const int MinVerts = 5; // 정점 ≥5개일 때만 유효로 간주
                head.valid = headCnt >= MinVerts; head.worldBounds = hb;
                torso.valid = torsoCnt >= MinVerts; torso.worldBounds = tb;
                leftFoot.valid = lfCnt >= MinVerts; leftFoot.worldBounds = lfb;
                rightFoot.valid = rfCnt >= MinVerts; rightFoot.worldBounds = rfb;
                leftHand.valid = lhCnt >= MinVerts; leftHand.worldBounds = lhb;
                rightHand.valid = rhCnt >= MinVerts; rightHand.worldBounds = rhb;
                // [105차 F2] 전신 bounds 반환 — 부위 게이트(IsPartPlausible) 판정 재료
                fullBody.worldBounds = fullBounds;
                fullBody.valid = fullValid;

                // [105차 F4] 바디 측정 진단 — 씬별 첫 부착 1회(정상이면 정보성 1줄).
                //   지면 기준 = fullBody.min.y(측정 하단 ≈ 발밑 접지점 근사).
                //   (sceneName은 [무번호 27-P1] 오염 특정 로그와 공유 — 위에서 선언)
                float diagGroundY = fullInit ? fullBounds.min.y : player.transform.position.y;
                string RelY(Transform b) => b != null ? $"{b.position.y - diagGroundY:F2}m" : "null";
                static string PartSum(BodyPart p) => p.valid
                    ? $"({p.worldBounds.size.x:F2},{p.worldBounds.size.y:F2},{p.worldBounds.size.z:F2})"
                    : "invalid";
                WarnOnce($"measure-diag:{sceneName}",
                    $"{LogTag} 바디 측정 진단({sceneName}): anim={(placeAnim != null ? placeAnim.name : "null")}"
                    + $", avatar={(placeAnim != null && placeAnim.avatar != null ? "OK" : "null")}"
                    + $", Head본={RelY(headBone)} Spine본={RelY(spineBone)} LeftFoot본={RelY(lf)} RightHand본={RelY(rh)}"
                    + $", fullBody={(fullInit ? fullBounds.size.ToString("F2") : "n/a")}"
                    + $", parts h={PartSum(head)} t={PartSum(torso)} fl={PartSum(leftFoot)} fr={PartSum(rightFoot)} hl={PartSum(leftHand)} hr={PartSum(rightHand)}"
                    + $", 제외 렌더러={excludedRends}"
                    // [무번호 27-P3] 진단 보강 — 제외 렌더러 이름 목록 + 오염 원인 분류(placeholder 프리미티브 등) 출력.
                    //   measure-diag:{scene} 키 계약은 유지(105차 F4).
                    + $", 제외 목록=[{(excludedNames.Count > 0 ? string.Join(", ", excludedNames) : "없음")}]"
                    + $", 오염 원인={(pollutionCauses.Count > 0 ? string.Join(", ", pollutionCauses) : "미탐지")}");
                return true;
            }
        }

        /// <summary>[TEST28-69차 후속2] 플레이어 배치 기준 — SkinnedMeshRenderer 몸 bounds는 왜곡(헬멧 3.2m 오프 실측)되므로
        ///   루트 위치 + 월드 방향만 사용한다. 본 로컬축도 손가락 방향 문제로 배제.
        ///   [69차 후속4] ⚠️ player.transform.position의 Y는 지면이 아니라 CC 중심(스폰 실측 0.87m — 발은 ~0m, Head 본 1.44m).
        ///   후속4에서 Shoes는 본 기준 앵커로 전환했으므로 ground는 더 이상 접지에 사용하지 않는다.</summary>
        static bool TryGetPlayerPlacement(out Vector3 ground, out Vector3 forward, out Vector3 left)
        {
            ground = default; forward = Vector3.forward; left = Vector3.left;
            var player = GameObject.FindWithTag("Player");
            if (player == null) return false;
            ground = player.transform.position;
            forward = player.transform.forward; forward.y = 0f; forward.Normalize();
            left = Vector3.Cross(Vector3.up, forward);
            if (left.sqrMagnitude < 0.0001f) left = Vector3.left;
            return true;
        }

        /// <summary>
        /// 슬롯 비주얼 부착 폴백: 기존 파괴 → 부착 본 폴링(최대 5초) → GLB 로드+부착.
        /// [TEST25-66차] 동기 즉시 부착(TryAttachImmediate) 실패분만 도달 — 본/GLB 지연 도착 대비 폴링.
        /// </summary>
        IEnumerator AttachRoutine(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            // ① 기존 비주얼 파괴 (동일 슬롯 재부착/교체 공통 — 즉시 부착 실패분이면 이미 비어 있어 멱등)
            DestroySlotVisuals(slot, log: false);

            // ② 부착 본 폴링 — Player 매 시도 재조회(대상 교체 대비)
            Transform[] bones = null;
            for (float t = 0f; t <= BonePollTimeoutSec; t += BonePollIntervalSec)
            {
                bones = ResolveBones(slot, itemId);
                if (bones != null) break;
                yield return new WaitForSeconds(BonePollIntervalSec);
            }
            if (bones == null)
            {
                WarnOnce($"bones:{slot}", $"{LogTag} {slot} 부착 본 미발견({BonePollTimeoutSec:0}초 폴링) — {itemId} 부착 스킵");
                yield break;
            }

            // ③ GLB 로드 — [TEST28-69차 후속3] 프리팹 로드를 본별로 위임(통합 부츠/장갑은 좌우 GLB 별도 파일).
            //    단일/좌우 모두 실패할 때만 경고(WarnOnce) 후 스킵.
            var prefab = Resources.Load<GameObject>(GlbResourceRoot + itemId);
            if (prefab == null)
                prefab = Resources.Load<GameObject>(GlbResourceRoot + itemId + ".glb");
            if (!InstantiateAttached(slot, itemId, bones, prefab))
            {
                WarnOnce($"load:{itemId}", $"{LogTag} GLB 로드 실패: {GlbResourceRoot}{itemId}(.glb/좌우 포함) — {slot} 부착 스킵");
            }
        }

        // ===== 본 해석 =====

        /// <summary>
        /// 슬롯+id 토큰으로 부착 본 집합 해석. 미완성이면 null(폴링 재시도).
        /// id의 "left"/"right" 토큰으로 좌우 분기(Shoes/Gloves). 토큰 없는 통짜 id는 양쪽 모두.
        /// </summary>
        Transform[] ResolveBones(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            var playerGO = GameObject.FindWithTag(PlayerTag);
            if (playerGO == null) return null;

            var animator = playerGO.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isActiveAndEnabled) return null;

            string idLower = itemId != null ? itemId.ToLowerInvariant() : string.Empty;
            bool left = idLower.Contains("left");
            bool right = idLower.Contains("right");

            switch (slot)
            {
                case EquipmentManager.EquipmentSlot.Helmet:
                {
                    var head = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head == null) head = FindBoneByName(animator, new[] { "head" });
                    return Single(head);
                }

                case EquipmentManager.EquipmentSlot.Armor:
                {
                    var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                    if (spine == null) spine = FindBoneByName(animator, new[] { "spine" });
                    if (spine != null) return Single(spine);
                    var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                    if (chest == null) chest = FindBoneByName(animator, new[] { "chest" });
                    return Single(chest);
                }

                case EquipmentManager.EquipmentSlot.Shoes:
                {
                    var lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    if (lf == null) lf = FindBoneByName(animator, new[] { "leftfoot", "left_foot", "foot_l" });
                    var rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    if (rf == null) rf = FindBoneByName(animator, new[] { "rightfoot", "right_foot", "foot_r" });
                    if (left) return Single(lf);
                    if (right) return Single(rf);
                    return Pair(lf, rf);
                }

                case EquipmentManager.EquipmentSlot.Gloves:
                {
                    var lh = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    if (lh == null) lh = FindBoneByName(animator, new[] { "lefthand", "left_hand", "hand_l" });
                    var rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (rh == null) rh = FindBoneByName(animator, new[] { "righthand", "right_hand", "hand_r" });
                    if (left) return Single(lh);
                    if (right) return Single(rh);
                    return Pair(lh, rh);
                }

                case EquipmentManager.EquipmentSlot.Back: // 방패류 — 왼손 우선, 없으면 왼팔뚝
                {
                    var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    if (hand == null) hand = FindBoneByName(animator, new[] { "lefthand", "left_hand", "hand_l" });
                    if (hand != null) return Single(hand);
                    var foreArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    if (foreArm == null) foreArm = FindBoneByName(animator, new[] { "leftlowerarm", "left_lower_arm", "lowerarm_l", "forearm_l" });
                    return Single(foreArm);
                }

                case EquipmentManager.EquipmentSlot.Mask: // [TEST28-69차] 가면 — 얼굴(Head 본)
                {
                    var head = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head == null) head = FindBoneByName(animator, new[] { "head" });
                    return Single(head);
                }

                case EquipmentManager.EquipmentSlot.Bag: // [TEST28-69차] 가방 — 등(Spine 본)
                {
                    var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                    if (spine == null) spine = FindBoneByName(animator, new[] { "spine" });
                    return Single(spine);
                }

                default:
                    return null;
            }
        }

        static Transform[] Single(Transform bone) => bone != null ? new[] { bone } : null;

        static Transform[] Pair(Transform a, Transform b)
        {
            if (a == null || b == null) return null; // 쌍 본은 둘 다 도착해야 부착
            return new[] { a, b };
        }

        /// <summary>
        /// 2026-09-16(45차 P6): 이름 기반 본 재탐색 폴백 — WeaponEquipManager의 H-GRIP2 패턴.
        /// 플레이어 아바타가 Humanoid 매핑이 아닌 Generic/FBX라 GetBoneTransform이 null을 반환할 때,
        /// GetComponentsInChildren 순회 + 소문자 이름 부분일치 키워드로 본을 찾는다. 첫 매칭 반환.
        /// </summary>
        static Transform FindBoneByName(Animator animator, string[] keywords)
        {
            if (animator == null || keywords == null || keywords.Length == 0) return null;
            var ts = animator.GetComponentsInChildren<Transform>(true);
            foreach (var t in ts)
            {
                var name = t.name.ToLowerInvariant();
                for (int i = 0; i < keywords.Length; i++)
                {
                    if (name.Contains(keywords[i])) return t;
                }
            }
            return null;
        }

        // ===== 비주얼 관리 =====

        void AddVisual(EquipmentManager.EquipmentSlot slot, GameObject visual)
        {
            if (!_slotVisuals.TryGetValue(slot, out var list))
            {
                list = new List<GameObject>();
                _slotVisuals[slot] = list;
            }
            list.Add(visual);
        }

        /// <summary>슬롯의 기존 비주얼 전부 파괴(이미 파괴된 참조는 무시 — 멱등).</summary>
        void DestroySlotVisuals(EquipmentManager.EquipmentSlot slot, bool log)
        {
            if (!_slotVisuals.TryGetValue(slot, out var list)) return;
            bool destroyedAny = false;
            foreach (var v in list)
            {
                if (v == null) continue; // 아바타 교체 등으로 이미 파괴됨
                Destroy(v);
                // [105차 F1] 파괴 예정 transform 유지 — Destroy는 프레임 말 지연이라 즉시 Clear하면
                //   같은 프레임 측정에 파괴 예정 비주얼이 여전히 기여한다 → 다음 측정 1회까지 제외 유지.
                _pendingExcluded.Add(v.transform);
                destroyedAny = true;
            }
            list.Clear();
            if (log && destroyedAny)
                Debug.Log($"{LogTag} 🔓 {slot} 비주얼 해제");
        }

        // 경고 1회성 가드 — 동일 키 재경고 억제(스팸 방지)
        static void WarnOnce(string key, string message)
        {
            if (!_warnedKeys.Add(key)) return;
            Debug.LogWarning(message);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            // 잔존 비주얼 정리(부모 본과 함께 파괴되는 경우가 대부분이지만 멱등 처리)
            foreach (EquipmentManager.EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)))
                DestroySlotVisuals(slot, log: false);
        }
    }

    /// <summary>
    /// [TEST26-67차] ArmorVisual 자가 치유 워치독 — 자체 GO(DontDestroyOnLoad)에서 0.5s 주기 감시.
    /// 뿌리(실측): ArmorVisualAttachSystem이 공유 GameManager GO에 붙어 있어 그 GO가 파괴되면
    /// OnDestroy→Unsubscribe로 구독이 사라지고, 이후 장착 이벤트는 "발화는 되는데 수신 0건"(Editor.log)이 됐다.
    /// ① 시스템 파괴 감지 → 자체 GO에 재생성 ② SyncTick으로 재구독 + 슬롯 리컨실레이션.
    /// </summary>
    public class ArmorVisualSyncWatchdog : MonoBehaviour
    {
        float _nextTick;
        void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 0.5f;

            if (ArmorVisualAttachSystem.Instance == null)
            {
                var go = new GameObject("ArmorVisualAttachSystem");
                DontDestroyOnLoad(go);
                go.AddComponent<ArmorVisualAttachSystem>();
                Debug.Log("[ArmorVisualWatchdog] 시스템 부재 감지 — 재생성(자가 치유)");
                return;
            }
            ArmorVisualAttachSystem.Instance.SyncTick();
        }
    }
}
