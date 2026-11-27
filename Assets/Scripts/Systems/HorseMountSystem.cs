using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;   // PlayerInventory(Core 네임스페이스 — MonsterSpawner 선례)

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 85-P3~P5] 말 시스템 — 야생 말 스폰(몬스터 배치 패턴 승계: 결정론 산포·지형 접지) +
    /// 길들이기(안장 보유 + E키 → HorseTamingUTK 미니게임 성공/실패) + 탑승/이동/하차(MountLocomotion).
    /// 말 리그 절차애니 부착 금지 — 클립 재생 방식 고정(계획 v2 계약).
    /// UI(길들이기 창)는 어셈블리 순환 방지를 위해 정적 이벤트로 역방향 알림(EInteractionPromptBridge 선례).
    /// </summary>
    public class HorseMountSystem : MonoBehaviour
    {
        public enum HorseState { Wild, Tamed }

        public class HorseEntity
        {
            public GameObject root;
            public Animator anim;
            public HorseState state;
            public string breed;
        }

        /// <summary>
        /// [207차 P2] 클립 루트 회전 런타임 보정 컴포넌트 — 말 GLB의 아마추어 루트 본("Root")이
        /// 애니메이션 클립(갈색말 대기 컨트롤러 백마_AC의 현재 클립 Take 001, len=2.30s)에 의해
        /// 옆으로 누운 자세로 구동되는 결함의 런타임 수리.
        /// 근거(P0 프로브 실측): 애니 적용 포즈A — 루트 본 localEuler=(270,0,0), bounds size=(0.79,2.16,1.62) = 옆으로 누움.
        /// 애니 무 포즈B(같은 GLB Instantiate, Animator 없음) — Root localEuler=(0,0,0), bounds=(0.34,0.87,1.09) = 정립.
        /// → 근원 = 클립이 루트 본 회전을 270도로 구동. applyRootMotion=false여도 뼈대 회전은 클립을 따른다.
        /// 동작: Animator 부착·컨트롤러 할당 직후(첫 평가 전 = 바인드 자세) localRotation 캡처 →
        /// LateUpdate에서 매 프레임 캡처값으로 복원. Animator는 Update와 LateUpdate 사이 평가되므로
        /// LateUpdate 복원이 렌더 포즈를 지배한다. 회전만 복원 — position은 클립 루트모션 가능성이
        /// 있어 손대지 않음(스폰 접지 로직과 상호작용 없음).
        /// </summary>
        private sealed class HorseRootPoseLock : MonoBehaviour
        {
            public Transform RootBone;
            private Quaternion _bindLocalRotation;
            private bool _captured;

            /// <summary>바인드 자세 캡처 — Animator 첫 평가 전(SpawnHorse 직후 같은 프레임) 호출 계약.</summary>
            public void CaptureBindPose()
            {
                if (RootBone == null) return;
                _bindLocalRotation = RootBone.localRotation;
                _captured = true;
            }

            private void LateUpdate()
            {
                if (!_captured || RootBone == null) return;
                RootBone.localRotation = _bindLocalRotation;   // 회전만 — position 불변
            }
        }

        struct BreedSpec { public string resPath, ctrlPath, breed; }

        static readonly BreedSpec[] Breeds = new BreedSpec[]
        {
            new BreedSpec { resPath = "Models/UserProvided/말/백마", ctrlPath = "Animation/Controllers/백마_AC", breed = "백마" },
            new BreedSpec { resPath = "Models/UserProvided/말/적토마", ctrlPath = "Animation/Controllers/적토마_AC", breed = "적토마" },
            // [166-A] 갈색말 — 파생 GLB(백마 bcf틴트, 무번호 90/91) 삭제 → 황마 GLB 사용. ctrlPath/breed 문자열 유지(세이브 호환).
            new BreedSpec { resPath = "Models/UserProvided/말/황마", ctrlPath = "Animation/Controllers/갈색말_AC", breed = "갈색말" },
        };

        // [85-P1 실측] 원본 체고 0.75m → 탑승 척도 스케일. 이동 임계는 컨트롤러 전이(0.1/3.0)와 정합.
        public const float HorseScale = 2.4f;
        /// <summary>[195차 P5] 목표 체장(m) — GLB 원본 스케일과 무관하게 수평 최대차원(체장)을 이 값으로 정규화.
        ///   실측: 적토마 GLB 원본 수평차원 ~0.45m(HorseScale 2.4 배 후에도 1.09m — 사용자 지적 "찌그러진 로드" 근원).</summary>
        public const float HorseTargetLength = 2.3f;
        public const float InteractRadius = 3.5f;
        public const float WalkSpeed = 2.2f;
        public const float GallopSpeed = 7.5f;
        const float SpawnDist = 14f;
        // [206차] 길들인 말 안장 시각 부착 — 등 높이/안장 폭 결정론 상수(Play 튜닝 대상)
        internal const float SaddleBackHeightRatio = 0.65f;   // 등 높이 = 전신 AABB 높이 × 비율(머리 정점 제외 등 높이 근사)
        internal const float SaddleWidthRatio = 0.22f;        // 안장 폭 = 목표 체장 × 비율(2.3m × 0.22 ≈ 0.5m)

        static HorseMountSystem _instance;
        public static HorseMountSystem Instance => _instance;

        /// <summary>[무번호 86-P3] 승마 중 여부 — AttackSystem/PlayerCombat 좌클릭 억제 게이트가 소비.</summary>
        public static bool IsMounted { get; private set; }

        readonly List<HorseEntity> _horses = new List<HorseEntity>();
        Transform _playerT;
        PlayerMovement _playerMover;
        HorseEntity _mounted;
        bool _playerParented;
        Vector3 _preMountPlayerPos;

        // [무번호 86-P1] 세이브 pending — Instance 미성립 시 적용 보류 후 스폰 완료 시점에 복원
        internal static List<HorseSaveEntry> PendingImport;
        // [무번호 86-P3] 발길질 — 쿨다운/사거리 결정론 상수(Play 튜닝 대상)
        internal const float KickCooldown = 1.0f;
        internal const float KickRadius = 3f;
        float _nextKickTime;
        float _kickRecoverAt;
        HorseEntity _kickRecoverHorse;

        /// <summary>[85-P4] 길들이기 요청 — UI 측(HorseTamingUTK)이 구독해 미니게임을 열고
        /// 결과(성공=true)를 콜백으로 반환한다. 어셈블리 순환 방지 이벤트(EInteractionPromptBridge 선례).</summary>
        public static event System.Action<string, System.Action<bool>> OnTamingRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("HorseMountSystem");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<HorseMountSystem>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            SpawnHorses();
            // [무번호 86-P1] 스폰 완료 후 pending 세이브 복원(길들임+위치+접지)
            // [195차 P5] pending 선클리어 후 인계 — ApplyImport가 비 MainScene이면 데이터를 pending에 재보존하므로
            //   기존처럼 호출 후 무조건 null로 지우면 보존분이 유실된다(MainScene 동작은 무변경).
            if (PendingImport != null)
            {
                var pending = PendingImport;
                PendingImport = null;
                ApplyImport(pending);
            }
            TestSceneSpawn();
        }

        // ── [85-P3] 야생 말 스폰 — 몬스터 배치 패턴 승계(결정론 황금각 산포 + 지형 접지) ──
        void SpawnHorses()
        {
            // [195차 P5] 씬 게이트 — 부트 씬이 MainScene이 아니면 야생 말 스폰 스킵(던전·테스트 씬 말 반입 차단).
            //   MainScene 동작은 무변경. 스킵 로그는 1회만(브리드 루프 진입 전 반환).
            if (!IsMainScene())
            {
                Debug.Log("[Horse] 비 MainScene(" + ActiveSceneName() + ") — 야생 말 스폰 스킵 [195차 P5]");
                return;
            }
            Vector3 basePos = Vector3.zero;
            var found = Object.FindObjectOfType<PlayerMovement>();
            if (found != null) basePos = found.transform.position;

            for (int i = 0; i < Breeds.Length; i++)
            {
                float ang = i * 137.5f;   // 황금각 — 결정론 산포(Random 금지 계약)
                Vector3 pos = basePos + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * SpawnDist;
                SpawnHorse(Breeds[i], pos);
            }
        }

        // ── [204차] Test_10 전용 스폰 — 야생 말 1 + 안장 바구니 1 ──
        void TestSceneSpawn()
        {
            // [204차] Test_10 말 이동 테스트 — 야생 말 1 + 안장 바구니 1 (사용자 지시: "테스트씬 10에 안장도 하나 넣어줘 말 움직임 테스트해보게")
            // 195차 P5 야생 스폰 게이트(SpawnHorses)는 무손상 — 본 경로는 명시적 테스트 씬 전용 별도 경로.
            if (ActiveSceneName() != "Test_10_TerritoryCombat") return;

            Vector3 basePos = Vector3.zero;
            var found = Object.FindObjectOfType<PlayerMovement>();
            if (found != null) basePos = found.transform.position;

            // 야생 말 1(백마) — 길들이기(안장+E)→탑승→이동 전체 흐름 검증용. SpawnHorse 경로 재사용(크기 정규화+접지+애니 포함)
            SpawnHorse(Breeds[0], basePos + new Vector3(8f, 0f, 5f));

            // 안장 바구니 1 — 픽업해야 길들이기 가능. lifetime 3600s(기본 30s 소멸 방지 — 테스트 중 소멸 금지)
            var basket = LootBasket.Create(basePos + new Vector3(2f, 0f, 4f), 3600f);
            if (basket != null) basket.AddItem(PlayerInventory.Saddle, 1);

            Debug.Log("[Horse] Test_10 스폰 — 야생 말(백마) 1 + 안장 바구니 1 [204차]");
        }

        void SpawnHorse(BreedSpec spec, Vector3 pos)
        {
            var prefab = Resources.Load<GameObject>(spec.resPath);
            if (prefab == null) { Debug.LogWarning("[Horse] 리소스 로드 실패: " + spec.resPath); return; }

            var go = Object.Instantiate(prefab, pos, Quaternion.identity);
            go.name = "WildHorse_" + spec.breed;
            NormalizeHorseScale(go);   // [195차 P5] 크기 정규화 — "찌그러진 로드" 수리(아래 헬퍼)
            GroundTo(go.transform);

            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            var ctrl = Resources.Load<RuntimeAnimatorController>(spec.ctrlPath);
            if (ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;   // 이동은 MountLocomotion transform 구동 — 루트모션 이중 계 금지
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else Debug.LogWarning("[Horse] 컨트롤러 로드 실패: " + spec.ctrlPath);

            // [207차 P2] 클립 루트 회전 보정 배선 — GLB 아마추어 루트 본("Root")이 클립(갈색말 Take 001 등)에
            // 의해 옆으로 눕는 결함의 런타임 수리(HorseRootPoseLock — 클래스 주석 참조). 컨트롤러 할당 직후
            // 같은 프레임 캡처 = Animator 첫 평가 전 바인드 자세 확보(이 순서 엄수).
            // 3품종 단일 경로(SpawnHorse) 적용 — SpawnHorses/TestSceneSpawn 자동 커버(별도 배선 없음).
            Transform root = null;
            var horseTransforms = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < horseTransforms.Length; i++)
            {
                if (horseTransforms[i].name == "Root") { root = horseTransforms[i]; break; }
            }
            if (root != null)
            {
                var lockComp = go.AddComponent<HorseRootPoseLock>();
                lockComp.RootBone = root;
                lockComp.CaptureBindPose();   // Animator 첫 평가 전 캡처 — 이 순서 엄수
            }
            else Debug.LogWarning("[Horse][207차 P2] 루트 본('Root') 미발견 — 클립 루트 회전 보정 스킵(폴백 안전)");

            _horses.Add(new HorseEntity { root = go, anim = anim, state = HorseState.Wild, breed = spec.breed });
            Debug.Log("[Horse] 야생 말 스폰: " + spec.breed + " @ " + go.transform.position);
        }

        /// <summary>[195차 P5] 크기 정규화 — 인스턴스 렌더러 bounds 수평 최대차원(체장)을 HorseTargetLength로 맞추는
        ///   스케일. GLB 원본 스케일/피벗 무관 결정론. 렌더러 부재 시 기존 HorseScale 폴백(계약 유지).
        ///   [206차] 균일 스케일 복귀 — 205차 y축 별도 보정(전신 world-Y 압축)이 "찌그러짐"의 근원으로 확인되어 제거.
        ///   원본 GLB 비율(체고/체장 0.74~0.76)은 정상 범위 — 모델 원본 비율 유지 계약.</summary>
        static void NormalizeHorseScale(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float maxDim = Mathf.Max(b.size.x, b.size.z);
                if (maxDim > 0.001f)
                {
                    float factor = HorseTargetLength / maxDim;
                    // [206차] 균일 스케일 복귀 — y축 별도 보정 제거(전신 world-Y 압축 = 찌그러짐 근원). 원본 비율 유지.
                    go.transform.localScale = Vector3.one * factor;
                    Debug.Log($"[Horse][195차 P5] 크기 정규화 — 원본 수평차원 {maxDim:F2}m → 균일 스케일 ×{factor:F2} (목표 체장 {HorseTargetLength}m) [206차]");
                    return;
                }
            }
            go.transform.localScale = Vector3.one * HorseScale;   // 폴백 — 기존 상수(렌더러 0건/최소차원)
        }

        static void GroundTo(Transform t)
        {
            // 지형 접지 — 상하 레이캐스트(디폴트 지면) → 실패 시 유지(수식 계약 폴백은 P4 튜닝)
            var origin = t.position + Vector3.up * 20f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                t.position = new Vector3(t.position.x, hit.point.y, t.position.z);
        }

        void EnsurePlayer()
        {
            if (_playerT != null) return;
            var pm = Object.FindObjectOfType<PlayerMovement>();
            if (pm == null) return;
            _playerMover = pm;
            _playerT = pm.transform;
        }

        HorseEntity Nearest(Vector3 p, float r)
        {
            HorseEntity best = null; float bestD = r * r;
            foreach (var h in _horses)
            {
                if (h.root == null) continue;
                float d = (h.root.transform.position - p).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = h; }
            }
            return best;
        }

        // ── [무번호 86-P1] 세이브 연동 — 말 상태(품종/길들임/위치) 수출입 ──
        public static List<HorseSaveEntry> ExportSaveData()
        {
            var list = new List<HorseSaveEntry>();
            var inst = _instance;
            if (inst == null) return list;   // 미부트 → 빈 리스트(저장 대상 없음)
            foreach (var h in inst._horses)
            {
                if (h?.root == null) continue;
                var p = h.root.transform.position;
                list.Add(new HorseSaveEntry
                {
                    breed = h.breed,
                    tamed = h.state == HorseState.Tamed,
                    posX = p.x, posY = p.y, posZ = p.z
                });
            }
            return list;
        }

        public static void ImportSaveData(List<HorseSaveEntry> entries)
        {
            if (entries == null || entries.Count == 0) return; // 구세이브/빈 값 — 전부 야생 유지(멱등)
            var inst = _instance;
            if (inst == null) { PendingImport = new List<HorseSaveEntry>(entries); return; }
            inst.ApplyImport(entries);
        }

        void ApplyImport(List<HorseSaveEntry> entries)
        {
            // [195차 P5] 씬 게이트 — 비 MainScene에서는 말 복원(인스턴스화/상태 적용) 스킵 + 세이브 데이터 보존.
            //   entries를 pending에 재적립해 데이터 유실을 방지한다(훗날 MainScene 경로에서 소비 여지). MainScene 동작 무변경.
            if (!IsMainScene())
            {
                PendingImport = new List<HorseSaveEntry>(entries);
                Debug.Log("[Horse] 비 MainScene(" + ActiveSceneName() + ") — 말 복원 스킵, 세이브 데이터 보존 " + entries.Count + "건 [195차 P5]");
                return;
            }
            foreach (var h in _horses)
            {
                if (h?.root == null) continue;
                foreach (var e in entries)
                {
                    if (e == null || e.breed != h.breed) continue;
                    if (e.tamed) h.state = HorseState.Tamed;
                    h.root.transform.position = new Vector3(e.posX, e.posY, e.posZ);
                    GroundTo(h.root.transform);
                }
            }
        }

        // ── [195차 P5] 씬 게이트 헬퍼 — 말 야생 스폰/세이브 복원은 MainScene 전용(Assets/Scenes/MainScene.unity 실측) ──
        static bool IsMainScene()
        {
            return ActiveSceneName() == "MainScene";
        }

        static string ActiveSceneName()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        // ── 상호작용 폴링 ──
        void Update()
        {
            EnsurePlayer();
            if (_playerT == null) return;

            // [무번호 86-P3] 발길질 복귀 — Rear 상태 출구 전이 없음(m_Transitions: []) → 쿨다운 경과 후 로코모션 복귀
            if (_kickRecoverHorse != null && Time.time >= _kickRecoverAt)
            {
                if (_kickRecoverHorse.anim != null) _kickRecoverHorse.anim.CrossFade("Idle", 0.15f);
                _kickRecoverHorse = null;
                _kickRecoverAt = 0f;
            }

            if (_mounted != null)
            {
                MountLocomotion.Tick(_mounted.root.transform, _mounted.anim, WalkSpeed, GallopSpeed);
                if (Input.GetKeyDown(KeyCode.E)) Dismount();
                else if (Input.GetMouseButtonDown(0)) TriggerKick(); // [무번호 86-P3] 승마 중 좌클릭 = 발길질
                return;
            }

            var near = Nearest(_playerT.position, InteractRadius);
            if (near == null) return;

            bool hasSaddle = PlayerInventory.Instance != null && PlayerInventory.Instance.HasItem("mat_saddle");
            if (near.state == HorseState.Wild)
            {
                string label = hasSaddle
                    ? "[E] 말 길들이기 (안장 장착)"
                    : "말 안장 필요 — 크래프트(가죽×5)";
                EInteractionPromptBridge.RaiseRegister(near.root.transform, label, InteractRadius);
                if (Input.GetKeyDown(KeyCode.E) && hasSaddle)
                {
                    Debug.Log("[Horse] 길들이기 시작 — " + near.breed);
                    OnTamingRequested?.Invoke(near.breed, ok => CompleteTaming(near, ok));
                }
            }
            else
            {
                EInteractionPromptBridge.RaiseRegister(near.root.transform, "[E] 말 탑승", InteractRadius);
                if (Input.GetKeyDown(KeyCode.E)) Mount(near);
            }
        }

        // ── [85-P4] 길들이기 완료 — HorseTamingUTK 결과 콜백 ──
        void CompleteTaming(HorseEntity h, bool success)
        {
            if (h == null || h.root == null) return;
            if (success)
            {
                h.state = HorseState.Tamed;
                if (PlayerInventory.Instance != null) PlayerInventory.Instance.RemoveItem("mat_saddle", 1);
                AttachSaddleVisual(h);   // [206차] 안장 시각 부착 — 길들이기 성공 시 등에 GLB 부착
                Debug.Log("[Horse] ✅ 길들이기 성공 — " + h.breed + " 탑승 가능");
            }
            else
            {
                // [85-P6] 실패 리액션 — 앞발 들고 서기 연출(Rear 상태 직접 재생, 시스템이 복귀 재생)
                h.anim?.Play("Rear");
                Debug.Log("[Horse] ❌ 길들이기 실패 — " + h.breed + " 재도전 가능");
            }
        }

        // ── [206차] 길들인 말 안장 시각 부착 — mat_saddle GLB(UserProvided/saddle.glb 실측 존재)를 등에 부착 ──
        // 등 높이는 말 렌더러 bounds(스포 정규화 스케일 반영) 기반 결정론. 안장 크기는 목표 체장 × 비율로 정규화.
        static void AttachSaddleVisual(HorseEntity h)
        {
            if (h?.root == null) return;
            var prev = h.root.transform.Find("SaddleVisual");
            if (prev != null) Object.Destroy(prev.gameObject);   // 멱등 — 재길들이기 중복 부착 방지

            var renderers = h.root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.LogWarning("[Horse] 안장 부착 생략 — 말 렌더러 부재"); return; }
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float backY = b.min.y + (b.max.y - b.min.y) * SaddleBackHeightRatio;   // 등 높이(월드) — Play 튜닝 대상

            var glb = Resources.Load<GameObject>("Models/UserProvided/saddle");
            if (glb == null) { Debug.LogWarning("[Horse] 안장 GLB 로드 실패 — 시각 부착 생략"); return; }
            var saddle = Object.Instantiate(glb, h.root.transform);
            saddle.name = "SaddleVisual";

            // 안장 크기 정규화 — 목표 폭(체장 × 비율)을 말 로컬 단위로 환산(부착 시 말 정규화 스케일 상속)
            var sRenderers = saddle.GetComponentsInChildren<Renderer>();
            if (sRenderers.Length == 0) { Debug.LogWarning("[Horse] 안장 부착 생략 — 안장 GLB 렌더러 부재"); return; }
            var sb = sRenderers[0].bounds;
            for (int i = 1; i < sRenderers.Length; i++) sb.Encapsulate(sRenderers[i].bounds);
            float horseScale = Mathf.Max(h.root.transform.localScale.x, 0.001f);
            float rawWidth = Mathf.Max(sb.size.x, sb.size.z) / horseScale;   // GLB 원본 폭(말 스케일 제거)
            if (rawWidth > 0.001f)
                saddle.transform.localScale = Vector3.one * (SaddleWidthRatio * HorseTargetLength / (rawWidth * horseScale));

            // 등 배치 — 안장 bounds 중심을 등 지점에 정렬(말 루트 역변환 — 회전/스케일 반영)
            var sb2 = sRenderers[0].bounds;
            for (int i = 1; i < sRenderers.Length; i++) sb2.Encapsulate(sRenderers[i].bounds);
            Vector3 desired = new Vector3(h.root.transform.position.x, backY, h.root.transform.position.z);
            saddle.transform.localPosition += h.root.transform.InverseTransformVector(desired - sb2.center);
            Debug.Log($"[Horse][206차] 안장 시각 부착 — {h.breed} 등 높이 {backY:F2}m, 안장 폭 {SaddleWidthRatio * HorseTargetLength:F2}m (Play 튜닝 대상)");
        }

        // ── [85-P5] 탑승/하차 ──
        void Mount(HorseEntity h)
        {
            if (_mounted != null || _playerT == null) return;
            _mounted = h;
            _preMountPlayerPos = _playerT.position;
            _playerT.SetParent(h.root.transform, false);
            _playerT.localPosition = new Vector3(0f, 1.05f, 0f);   // 안장부(스포등 위) — Play 튜닝 대상
            _playerT.localRotation = Quaternion.identity;
            if (_playerMover != null) _playerMover.enabled = false;
            _playerParented = true;
            h.anim.SetFloat("Speed", 0f);
            // [무번호 86-P2] 승마 자세 — 기존 Sit 파이프라인(SitDown→SitHold→SitUp) 진입 + 발길질 게이트 플래그
            IsMounted = true;
            var mountDriver = _playerT.GetComponentInChildren<HumanoidClipDriver>();
            if (mountDriver != null) mountDriver.SetMounted(true);
            Debug.Log("[Horse] 탑승 — " + h.breed);
        }

        void Dismount()
        {
            var h = _mounted;
            if (h == null || _playerT == null) return;
            _playerT.SetParent(null, true);
            var dropPos = h.root.transform.position + h.root.transform.right * 1.2f;
            _playerT.position = new Vector3(dropPos.x, h.root.transform.position.y + 0.2f, dropPos.z);
            if (_playerMover != null) _playerMover.enabled = true;
            _playerParented = false;
            _mounted = null;
            // [무번호 86-P2/P3] 승마 자세 해제 + 발길질 게이트 해제
            IsMounted = false;
            var dismountDriver = _playerT.GetComponentInChildren<HumanoidClipDriver>();
            if (dismountDriver != null) dismountDriver.SetMounted(false);
            Debug.Log("[Horse] 하차 — " + h.breed);
        }

        // ── [무번호 86-P3] 말 전투 — 승마 중 좌클릭 발길질(앞발 들고 서기 + 전방 타격) ──
        void TriggerKick()
        {
            var h = _mounted;
            if (h == null || h.root == null) return;
            if (Time.time < _nextKickTime) return;
            _nextKickTime = Time.time + KickCooldown;

            h.anim?.Play("Rear");
            _kickRecoverHorse = h;
            _kickRecoverAt = Time.time + KickCooldown;

            var horseT = h.root.transform;
            Vector3 center = horseT.position + horseT.forward * (KickRadius * 0.5f);
            var hits = Physics.OverlapSphere(center, KickRadius, ~0, QueryTriggerInteraction.Ignore);
            float dmg = ResolveKickDamage();
            var struck = new HashSet<IDamageable>();
            foreach (var col in hits)
            {
                if (col == null) continue;
                if (col.transform.IsChildOf(horseT)) continue;                       // 말 자신 제외
                if (_playerT != null && col.transform.IsChildOf(_playerT)) continue; // 탑승자 제외
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !struck.Add(target)) continue;
                Vector3 dir = (col.transform.position - horseT.position).normalized;
                target.TakeDamage(dmg, dir, "melee");
            }
            Debug.Log($"[Horse] 발길질 — 피해 {dmg:F0} / 판정 콜라이더 {hits.Length}건");
        }

        static float ResolveKickDamage()
        {
            // 플레이어 현재 무기 데미지 재사용(무장 부재 시 10 고정 — 계획서 최소설계)
            var pc = Instance != null && Instance._playerT != null
                ? Instance._playerT.GetComponent<PlayerCombat>()
                : null;
            return pc != null && pc.CurrentWeapon != null ? pc.CurrentWeapon.damage : 10f;
        }
    }
}
