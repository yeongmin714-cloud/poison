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
            PlayerBodyMeasure.TryMeasurePlayerBody(placeAnim, excludeRoots, out var mHead, out var mTorso,
                out var mLeftFoot, out var mRightFoot, out var mLeftHand, out var mRightHand);
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

                // [TEST27-68차] 슬롯 목표 크기 정규화 — 방어구 GLB가 플레이어 대비 3~4배(헬멧 1.37m 실측)로
                //   플레이어를 덮는 문제. 최장축을 슬롯 목표 치수로 균등 스케일.
                appliedScale = NormalizeVisualScale(visual, GetTargetSize(slot, itemId));

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
                    bodyFitted = FitVisualToBodyPart(visual, slot, bone, fitPart, alignFwd);
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
                            worldAnchor = bone.position + Vector3.up * 0.26f;
                            break;
                        case EquipmentManager.EquipmentSlot.Mask:
                            worldAnchor = bone.position + pFwd * 0.06f + Vector3.up * 0.19f; // [69차 후속5] 가면 중심 = 눈높이(로컬 0.76) — 본-12cm는 턱 아래로 과납(67차 실측)
                            break;
                        case EquipmentManager.EquipmentSlot.Armor:
                            worldAnchor = bone.position + Vector3.up * 0.15f;       // [69차 후속5] 갑옷 중심 = 가슴(로컬 0.40, 갑옷 높이 0.341 → 허리~목하단)
                            break;
                        case EquipmentManager.EquipmentSlot.Bag:
                            worldAnchor = bone.position + Vector3.up * 0.12f - pFwd * 0.09f; // [69차 후속5] 가방 중심 = 등 중앙(로컬 0.37, 등 표면 뒤 9cm)
                            break;
                        case EquipmentManager.EquipmentSlot.Gloves:
                            worldAnchor = bone.position - Vector3.up * 0.02f;        // [69차 후속5] 장갑(손목보호대, 높이 0.08) 중심 = 손 본-2cm
                            break;
                        case EquipmentManager.EquipmentSlot.Back:
                        {
                            // [69차 후속4] 방패 = 손 본에서 팔 바깥쪽 10cm — Spine↔손 벡터(본 기준, 시선 무관).
                            var lat = Vector3.zero;
                            var spineBone = placeAnim != null ? placeAnim.GetBoneTransform(HumanBodyBones.Spine) : null;
                            if (spineBone != null)
                            {
                                var d = bone.position - spineBone.position; d.y = 0f;
                                if (d.sqrMagnitude > 0.0001f) lat = d.normalized * 0.10f;
                            }
                            worldAnchor = bone.position + lat + Vector3.up * 0.02f;
                            break;
                        }
                        case EquipmentManager.EquipmentSlot.Shoes:
                            worldAnchor = bone.position - Vector3.up * 0.05f;       // [69차 후속4·5 검증] 복사 본(로컬 0.06)-5cm = 지면+1cm — 실측 부합, 유지
                            break;
                    }
                }
                if (!bodyFitted)
                    SnapVisualToBone(visual, bone, GetCenterOffset(slot, itemId), GetBottomAnchor(slot, itemId), worldAnchor);
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
                Debug.Log($"{LogTag} ✅ {slot} 비주얼 부착: {itemId} → {string.Join(", ", boneNames)} (스케일 x{appliedScale:F2}, anchor={(hasAnchor ? lastAnchorWorld.ToString("F2") : "본 기준")}, {distInfo})");
                if (maxCenterDist > 0.35f)
                    Debug.LogWarning($"{LogTag} ⚠️ {slot} 비주얼 중심이 본에서 0.35m 이상 벗어남 — 포즈 튜닝 필요(slot={slot}, item={itemId})");
            }
            else
            {
                Debug.Log($"{LogTag} ✅ {slot} 비주얼 부착: {itemId} → {string.Join(", ", boneNames)} (활성 렌더러 없음 — 시각 확인 요망)");
            }
            return true;
        }

        // [TEST27-68차] 슬롯별 목표 최대 치수(m) — 67차 Play 실측(헬멧 bounds 1.37m 등, 플레이어 ~1.7m) 기반
        static readonly Dictionary<EquipmentManager.EquipmentSlot, float> _targetSize =
            new Dictionary<EquipmentManager.EquipmentSlot, float>
        {
            { EquipmentManager.EquipmentSlot.Helmet, 0.34f },
            { EquipmentManager.EquipmentSlot.Armor,  0.62f },
            { EquipmentManager.EquipmentSlot.Shoes,  0.36f },
            { EquipmentManager.EquipmentSlot.Gloves, 0.24f },
            { EquipmentManager.EquipmentSlot.Back,   0.85f },
            { EquipmentManager.EquipmentSlot.Mask,   0.30f },   // [TEST28-69차] 가면(가스 마스크)
            { EquipmentManager.EquipmentSlot.Bag,    0.45f },   // [TEST28-69차] 가방(가스팩)
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
        static float GetTargetSize(EquipmentManager.EquipmentSlot slot, string itemId)
            => _targetSize.TryGetValue(slot, out var t) ? t : 0.5f;
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

            // 1) 에셋 축 실측 — bone-local(=visual-root-local, localRotation=identity) AABB/중심.
            if (!MeasureRootLocalAABB(visual, out var aabb, out _)) return; // 렌더러 0 → 정렬 스킵 (centroid는 후속11에서 미사용)

            // extents 랭킹 → long(최장)/mid/thin(최단) 축 인덱스 (x=0,y=1,z=2)
            float ex = aabb.size.x, ey = aabb.size.y, ez = aabb.size.z;
            int longIdx = 1, midIdx = 0, thinIdx = 2;
            if (ex >= ey && ex >= ez) longIdx = 0; else if (ey >= ex && ey >= ez) longIdx = 1; else longIdx = 2;
            if (longIdx == 0) { midIdx = (ey >= ez) ? 1 : 2; thinIdx = (ey >= ez) ? 2 : 1; }
            else if (longIdx == 1) { midIdx = (ex >= ez) ? 0 : 2; thinIdx = (ex >= ez) ? 2 : 0; }
            else { midIdx = (ex >= ey) ? 0 : 1; thinIdx = (ex >= ey) ? 1 : 0; }

            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            Vector3 longBasis = axes[longIdx];
            Vector3 thinBasis = axes[thinIdx];
            Vector3 midBasis = axes[midIdx];

            // 2) [69차 후속11] 부르주(bulge) 부호 휴리스틱 폐기 — 자산마다 오작동해 투구/신발/가면 180°,
            //   갑옷/가방 90° 엇갈림(테스트 31 실측). 전면 방향은 아래 슬롯별 고정 yaw 테이블로 결정한다.

            // 3) 슬롯별 long축 목표(고정) + 고정 yaw 오프셋 테이블 — 이 표의 yaw 숫자 한 줄만 바꾸면 해당 슬롯
            //   회전이 즉시 보정된다(정면이 반대면 ±180, 왼쪽/오른쪽이면 ±90).
            Vector3 tgtLongW = Vector3.up;
            float fixedYaw = 0f;
            switch (slot)
            {
                case EquipmentManager.EquipmentSlot.Helmet:
                    tgtLongW = Vector3.up; fixedYaw = 180f;   // 테스트 31: 앞뒤 180° — 정면 보정
                    break;
                case EquipmentManager.EquipmentSlot.Armor:
                    tgtLongW = Vector3.up; fixedYaw = 90f;    // 테스트 31: 측면 90° — 정면 보정(반대면 -90)
                    break;
                case EquipmentManager.EquipmentSlot.Bag:
                    tgtLongW = Vector3.up; fixedYaw = -90f;   // 테스트 31: 옆으로 90~110° — 정면 보정(반대면 +90)
                    break;
                case EquipmentManager.EquipmentSlot.Mask:
                    tgtLongW = Vector3.up; fixedYaw = 180f;   // 테스트 31: 앞뒤 180°
                    break;
                case EquipmentManager.EquipmentSlot.Back:
                    tgtLongW = Vector3.up; fixedYaw = 0f;     // 방패 — 평면 수직이면 충분
                    break;
                case EquipmentManager.EquipmentSlot.Gloves:
                {
                    // long → 손가락 방향(손 본−팔꿈치). 좌우 본 각각 계산.
                    bool isL = bone.name.ToLowerInvariant().Contains("left");
                    var elbowBone = placeAnim != null
                        ? placeAnim.GetBoneTransform(isL ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm)
                        : null;
                    Vector3 fingerW = Vector3.up;
                    if (elbowBone != null)
                    {
                        var fd = bone.position - elbowBone.position;
                        if (fd.sqrMagnitude > 0.0001f) fingerW = fd.normalized;
                    }
                    tgtLongW = fingerW; fixedYaw = 0f;
                    break;
                }
                case EquipmentManager.EquipmentSlot.Shoes:
                    // 부츠 긴축(long)=발길이 → long→+pFwd(발끝 전방, 고정) + 앞뒤 180° 보정(테스트 31: 발끝이 뒤)
                    tgtLongW = pFwd; fixedYaw = 180f;
                    break;
                default:
                    tgtLongW = Vector3.up; fixedYaw = 0f;
                    break;
            }

            // 4) 회전 구성(bone-local): R1(long→목표축 스윙 — 눕기/서기 결정) + 고정 yaw(월드 up 축 기준 — 전면 방향 결정).
            Vector3 tgtLongL = bone.InverseTransformDirection(tgtLongW);
            if (tgtLongL.sqrMagnitude < 0.0001f) return;
            tgtLongL.Normalize();

            Quaternion R1 = Quaternion.FromToRotation(longBasis, tgtLongL);
            Vector3 upLocal = bone.InverseTransformDirection(Vector3.up);
            if (upLocal.sqrMagnitude < 0.0001f) upLocal = tgtLongL;
            Quaternion yawRot = Quaternion.AngleAxis(fixedYaw, upLocal.normalized);
            visual.transform.localRotation = yawRot * R1 * visual.transform.localRotation;

            // 5) 실측 로그 1줄/본.
            Debug.Log($"[ArmorVisual] 정렬 {slot}: long={longIdx} thin={thinIdx} yaw={fixedYaw:F0} → localEuler={visual.transform.localRotation.eulerAngles.ToString("F0")} (bone={bone.name})");
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

            Vector3 pSize = part.worldBounds.size;
            float scale = 1f;
            switch (slot)
            {
                case EquipmentManager.EquipmentSlot.Helmet:
                    scale = (Mathf.Max(pSize.x, pSize.z) * 1.12f) / Mathf.Max(b.size.x, b.size.z); // 두개골 감쌈(배율 1.12 유지 — 렌더러별 샘플링으로 두개골이 제대로 측정되면 자동으로 커짐)
                    break;
                case EquipmentManager.EquipmentSlot.Armor:
                    scale = (pSize.x * 1.25f) / b.size.x;                                          // 몸통 감쌈
                    break;
                case EquipmentManager.EquipmentSlot.Shoes:
                    scale = (Mathf.Max(pSize.x, pSize.z) * 1.15f) / Mathf.Max(b.size.x, b.size.z); // 발길이 기준(본별 좌우 part)
                    break;
                case EquipmentManager.EquipmentSlot.Gloves:
                    // [2026-09-16 후속11] 손을 감싸도록 1.3→1.6배 — 테스트 31: 장갑이 눈에 안 보임(과소).
                    scale = (Mathf.Max(pSize.x, Mathf.Max(pSize.y, pSize.z)) * 1.6f)
                            / Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    break;
                case EquipmentManager.EquipmentSlot.Mask:
                    scale = (Mathf.Max(pSize.x, pSize.z) * 1.0f) / Mathf.Max(b.size.x, b.size.z); // [2026-09-16] 0.95→1.0 — 약간 작음 보정
                    break;
                case EquipmentManager.EquipmentSlot.Bag:
                    scale = (pSize.x * 1.05f) / b.size.x;                                          // [2026-09-16] 0.95→1.05 — 10~15% 작음 보정
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
                if (helmMax > 0.6f) scale *= 0.6f / helmMax;
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
                case EquipmentManager.EquipmentSlot.Armor:
                    target = partCenter;
                    break;
                case EquipmentManager.EquipmentSlot.Gloves:
                {
                    // [2026-09-16 69차 후속11] 손등 노출 — 손 부피 중심에 두면 장갑이 손 메시 안에 파묻힘
                    //   (테스트 31: 안 보임). 몸 중심축→손 방향(수평 바깥 = 팔이 자연 하강 시 손등 방향)으로
                    //   장갑 최대 치수의 30% 오프셋해 손등 쪽에 노출 배치.
                    Vector3 outward = Vector3.zero;
                    var pvGo = GameObject.FindWithTag(PlayerTag);
                    if (pvGo != null)
                    {
                        outward = bone.position - pvGo.transform.position;
                        outward.y = 0f;
                    }
                    if (outward.sqrMagnitude < 0.0001f) outward = Vector3.left;
                    float gloveMax = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    target = partCenter + outward.normalized * (gloveMax * 0.30f);
                    break;
                }
                case EquipmentManager.EquipmentSlot.Shoes:
                    // 발바닥 접지 — center.xz → part.center.xz, min.y → part.min.y − 0.01(미세 부유 흡수)
                    target = new Vector3(partCenter.x, b.center.y, partCenter.z);
                    target.y += part.worldBounds.min.y - 0.01f - b.min.y;
                    break;
                case EquipmentManager.EquipmentSlot.Mask:
                {
                    // faceCenter = part.center + pFwd*(part.size.z/2); center → faceCenter + pFwd*(0.4−0.5)*bounds.size.z
                    //   전방 면이 얼굴 표면에서 pFwd 방향 bounds.size.z*0.4 만큼 밖([2026-09-16] 0.35→0.4 — 매몰 수정), y는 눈높이.
                    Vector3 faceCenter = partCenter + pFwd * (part.worldBounds.size.z * 0.5f);
                    target = faceCenter + pFwd * (b.size.z * 0.4f - b.size.z * 0.5f);
                    target.y = partCenter.y + part.worldBounds.size.y * 0.15f; // 눈높이 ([2026-09-16] 0.1→0.15)
                    break;
                }
                case EquipmentManager.EquipmentSlot.Bag:
                {
                    // backCenter = part.center − pFwd*(part.size.z/2); center → backCenter − pFwd*(0.5−0.25)*bounds.size.z
                    //   등 표면에 75% 밀착·25% 매몰 허용 ([2026-09-16] 0.4→0.25 — 등에 더 밀착).
                    Vector3 backCenter = partCenter - pFwd * (part.worldBounds.size.z * 0.5f);
                    target = backCenter - pFwd * (b.size.z * 0.5f - b.size.z * 0.25f);
                    break;
                }
            }
            visual.transform.position += target - b.center;

            Debug.Log($"[ArmorVisual] 피팅 {slot}: part={part.worldBounds.size.ToString("F2")} scale보정=x{scale:F2} center={visual.transform.position.ToString("F2")}");
            return true;
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

            public static bool TryMeasurePlayerBody(Animator placeAnim, IReadOnlyList<Transform> excludeRoots,
                out BodyPart head, out BodyPart torso, out BodyPart leftFoot, out BodyPart rightFoot,
                out BodyPart leftHand, out BodyPart rightHand)
            {
                head = default; torso = default; leftFoot = default; rightFoot = default;
                leftHand = default; rightHand = default;

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

                const float footRadius = 0.28f; // [2026-09-16] 발 반경 0.35→0.28 — 발목/정강이 오염 감소
                const float handRadius = 0.25f; // [2026-09-16] 손 반경 0.35→0.25 — 전완 오염 감소
                float footR2 = footRadius * footRadius;
                float handR2 = handRadius * handRadius;

                int headCnt = 0, torsoCnt = 0, lfCnt = 0, rfCnt = 0, lhCnt = 0, rhCnt = 0;
                bool hI = false, tI = false, lfI = false, rfI = false, lhI = false, rhI = false;
                Bounds hb = default, tb = default, lfb = default, rfb = default, lhb = default, rhb = default;

                const int PerRendererSamples = 1500; // [2026-09-16] 렌더러별 독립 예산 — 전역 상한(3000) 폐기: 앞 렌더러(몸통)가 예산을 소진해
                //   뒤 렌더러(머리/헤어/손/발)가 정점 0개만 기여 → 부위 과소 측정(헬멧 높이 0.15m 등)의 뿌리. 각 렌더러는 무조건
                //   stride 샘플링(≤1500)으로 기여해 모든 부위가 측정에 반영된다.
                var rends = player.GetComponentsInChildren<Renderer>(true);
                // [69차 후속10] 바디 측정 정점 접근 안전화:
                //   - SkinnedMeshRenderer는 sharedMesh.vertices 대신 BakeMesh로 현재 포즈를 읽는다(isReadable 무관).
                //   - MeshFilter는 isReadable == false면 조용히 스킵(해당 렌더러 0 기여 — 다른 렌더러는 계속).
                //   - 각 렌더러 처리를 try-catch로 감싸 어떤 예외가 나도 전체 측정이 중단되지 않게 한다.
                bool rendererWarned = false;
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
                        if (excluded) continue;
                    }
                    try
                    {
                        Vector3[] verts = new Vector3[0];
                        var smr = r as SkinnedMeshRenderer;
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
                        Matrix4x4 l2w = r.transform.localToWorldMatrix; // smr는 transform L2W 근사
                        for (int i = 0; i < n; i += step)
                        {
                            Vector3 v = l2w.MultiplyPoint3x4(verts[i]);
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
                    catch (System.Exception ex)
                    {
                        if (!rendererWarned)
                        {
                            rendererWarned = true;
                            Debug.LogWarning($"[ArmorVisual] 바디 측정 스킵: {r.name} ({ex.Message})");
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
