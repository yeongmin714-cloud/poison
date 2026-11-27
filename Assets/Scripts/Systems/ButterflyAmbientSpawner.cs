using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectName.Systems
{
    /// <summary>
    /// [133차-P3] 나비 앰비얼트 스포너 — 세계 생태(동물 생태: 나비 배회) 계층.
    ///  · 스폰: 씬의 자연 앵커(나무/꽃) 주변 반경 8m에 6~10마리. 앵커 수집은 태그/레이어 부재 실측
    ///    (마을씬 물리 레이어 = Default 단일 체계 — VillagerWanderer/VillageNpcSpawner 실측 동일)에 따라
    ///    컴포넌트 기반: 1순위 ResourceNode(Wood/Herb)=벌목 나무/허브(꽃류, NaturalResourceSpawner 실측),
    ///    2순위 IdyllicDeco 루트 자식 이름 필터(Tree/Flower/Bush — IdyllicDecoPlacer ROOT_NAME 계약),
    ///    3순위 마을 중심(VillagePlacementSystem — 최후 폴백, 스폰 보장).
    ///  · 나비 소스(실측): Assets/Idyllic Fantasy Nature/Prefabs/Code Related/Butterfly_01~03.prefab —
    ///    루트에 Animator(Butterfly_Path_0X 컨트롤러 장착)+에셋팩 자체 스폰 스크립트(ButterflySpawn).
    ///    프리팹을 그대로 사용하고 ButterflySpawn 컴포넌트만 제거한다(부모 ButterflySpawnArea 부재 →
    ///    Random.Range(_area...) NRE 방지). 새 에셋 생성 없음 — 기존 컨트롤러 애니(날개짓+경로) 재생 계약.
    ///  · 이동: 개체당 순찰 포인트 3개(스폰점 기준 랜덤) 순환 + 사인파 부유(y ±0.3m, 주기 2초).
    ///    루트 이동은 LateUpdate(애니메이터 평가 이후 최종 적용권) — 경로 클립이 루트 트랜지먼트를
    ///    구동하더라도 이동 코드가 우선권을 유지한다(충돌 무해 설계).
    ///  · 클립 재시작: 경로 클립은 1회성(FBX 임포트 wrap=Once + 종료 애니 이벤트) — normalizedTime 0.95
    ///    컷에서 상태 재시작. 0.95 컷은 클립 말미의 애니메이션 이벤트(수신처=제거된 ButterflySpawn)를
    ///    미발화로 처리해 "이벤트 수신처 부재" 경고 스팸을 차단한다.
    ///  · 밤: TimeManager.OnNightStart → 개체 SetActive(false), OnDayStart → 재활성(이벤트 기반+초기 동기화).
    ///  · 성능: 스폰 수 상한 상수(MaxButterflies), 앵커 지연 수집 재시도(데코/자원 빌드 순서 비의존),
    ///    DontDestroyOnLoad 금지(씬 소속 — 씬 전환 시 자연 소멸), 결정론 System.Random(마을 주민 규약 준용).
    /// </summary>
    public sealed class ButterflyAmbientSpawner : MonoBehaviour
    {
        // ===== [133차-P3] 임계 상수 — 스펙 지정치(6~10마리/앵커 반경 8m) + 성능 방어 =====
        private const int MinButterflies = 6;             // 최소 스폰 수(스펙 지정)
        private const int MaxButterflies = 10;            // 스폰 수 상한(성능) — 상수 고정
        private const float AnchorRadius = 8f;            // 앵커 주변 스폰/순찰 반경(m) — 스펙 지정
        private const int PatrolPointCount = 3;            // 개체당 순찰 포인트 수 — 스펙 지정
        private const float PatrolMinDist = 2.0f;          // 순찰 포인트 최소 거리(m) — 스폰점 과밀 방지
        private const float BobAmplitudeM = 0.3f;          // 사인파 부유 진폭(m) — 스펙 지정
        private const float BobPeriodSeconds = 2f;         // 부유 주기(초) — 스펙 지정
        private const float FlySpeed = 0.8f;               // 순찰 이동 속도(m/s) — 나비 체감 보행 정합
        private const float FaceTurnSlerpRate = 4f;        // 이동 방향 Slerp 계수(부드러운 선회)
        private const int MaxAnchorCandidates = 16;        // 앵커 후보 상한(수집 비용/분포 방어)
        private const float AnchorCollectRetrySeconds = 5f;    // 앵커 지연 수집 재시도 주기(초)
        private const float AnchorCollectGiveUpSeconds = 90f;  // 수집 포기(초 — 이후 스킵 로그 1회)
        private const float ClipRestartGateTime = 1f;          // 경로 클립 재시작 최소 간격(초 — 파킹 상태 스팸 방지)
        private const float ClipRestartCutoff = 0.95f;         // 재시작 컷(종료 애니 이벤트 미발화 경로)
        private const int TerrainSeed = 42;                    // 마을/접지 동일 시드 계약
        private const string DecoRootName = "IdyllicDeco";     // IdyllicDecoPlacer ROOT_NAME 계약
        private const string ResourcePathFmt = "Butterfly/Butterfly_{0:00}";          // 빌드 미러 계약 경로
        private const string EditorPrefabPathFmt = "Assets/Idyllic Fantasy Nature/Prefabs/Code Related/Butterfly_{0:00}.prefab";
        private const string RootName = "ButterflyAmbient_Root";
        private const string SpawnSeedKey = "ButterflyAmbient133";

        [SerializeField] private GameObject _butterflyPrefab;   // Inspector 배선 경로(최우선 — 씬 할당 시 빌드 무의존)

        private readonly List<ButterflyAgent> _agents = new List<ButterflyAgent>();
        private float _anchorRetryLeft;
        private bool _spawned;
        private bool _timeHooked;
        private bool _giveUpLogged;

        // ===== 정적 진입점 — CoreSystemsBootstrap 호출(멱등) =====

        /// <summary>[133차-P3] 부트 진입 — 루트 GO+컴포넌트 생성. 중복 생성 금지(멱등), Play 전용.</summary>
        public static void SpawnAmbientButterflies()
        {
            if (!Application.isPlaying) return;
            if (GameObject.Find(RootName) != null) return;
            var go = new GameObject(RootName);
            go.AddComponent<ButterflyAmbientSpawner>();
            Debug.Log("[133차-P3] 나비 앰비얼트 스포너 생성 — 자연 앵커 지연 수집 후 스폰 개시");
        }

        private void Awake()
        {
            // DontDestroyOnLoad 금지 — 씬 소속 계약(씬 전환 시 함께 소멸 — 명시 미호출)
            _anchorRetryLeft = AnchorCollectRetrySeconds;
        }

        private void Start()
        {
            HookTimeEvents();
        }

        private void OnDestroy()
        {
            UnhookTimeEvents();
        }

        private void Update()
        {
            if (!_spawned) TryCollectAndSpawn();
        }

        /// <summary>앵커 지연 수집 — 나무/꽃 앵커가 빌드 파이프라인 순서상 늦게 생길 수 있어 재시도(상한 내).</summary>
        private void TryCollectAndSpawn()
        {
            _anchorRetryLeft -= Time.deltaTime;
            if (_anchorRetryLeft > 0f) return;
            _anchorRetryLeft = AnchorCollectRetrySeconds;

            var anchors = CollectAnchors();
            if (anchors.Count == 0)
            {
                if (Time.time >= AnchorCollectGiveUpSeconds && !_giveUpLogged)
                {
                    _giveUpLogged = true;
                    _spawned = true;   // 수집 포기 — 스킵(경고 1회, 재시도 스팸 금지)
                    Debug.LogWarning("[133차-P3] 나비 앵커 수집 실패 — 스폰 스킵(데코/자원 노드 부재)");
                }
                return;
            }
            _spawned = true;
            SpawnAtAnchors(anchors);
        }

        /// <summary>
        /// 자연 앵커 수집 — 컴포넌트 기반 3단계 폴백(상단 헤더 계약). 수집은 세션당 1회(스폰 시점).
        /// </summary>
        private List<Vector3> CollectAnchors()
        {
            var result = new List<Vector3>();
            foreach (var node in Object.FindObjectsOfType<ResourceNode>())   // 1순위 — 나무/허브 노드
            {
                if (node == null || !node.gameObject.activeInHierarchy) continue;
                var type = node.NodeType;   // ResourceNode 계약 accessor(NodeType — 실측 L60)
                if (type != ResourceNode.ResourceType.Wood && type != ResourceNode.ResourceType.Herb) continue;
                result.Add(node.transform.position);
                if (result.Count >= MaxAnchorCandidates) return result;
            }
            if (result.Count == 0)
            {
                var deco = GameObject.Find(DecoRootName);   // 2순위 — 데코 루트 이름 필터
                if (deco != null)
                {
                    foreach (Transform child in deco.transform)
                    {
                        string n = child.name.ToLowerInvariant();
                        if (!n.Contains("tree") && !n.Contains("flower") && !n.Contains("bush")) continue;
                        result.Add(child.position);
                        if (result.Count >= MaxAnchorCandidates) break;
                    }
                }
            }
            if (result.Count == 0)
            {
                var villages = VillagePlacementSystem.GetAllVillages();   // 3순위 — 마을 중심 폴백
                if (villages != null)
                {
                    foreach (var v in villages)
                    {
                        result.Add(v.center);
                        if (result.Count >= MaxAnchorCandidates) break;
                    }
                }
            }
            return result;
        }

        /// <summary>앵커에 나비 배포 — 수 6~10(상수 상한), 프리팹 3종 랜덤(결정론), 접지는 마을 계약 수식 재사용.</summary>
        private void SpawnAtAnchors(List<Vector3> anchors)
        {
            var prefabs = ResolvePrefabs();
            if (prefabs == null || prefabs.Count == 0)
            {
                Debug.LogWarning("[133차-P3] 나비 프리팹 미확보 — 스폰 스킵(Inspector 배선 또는 에디터 경로 참조 — 신규 에셋 생성 없음)");
                return;
            }
            var rng = new System.Random(SpawnSeedKey.GetHashCode());   // 결정론 — 마을 주민 시드 규약 준용
            int count = MinButterflies + rng.Next(MaxButterflies - MinButterflies + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 anchor = anchors[rng.Next(anchors.Count)];
                double ang = rng.NextDouble() * System.Math.PI * 2.0;
                double rr = rng.NextDouble() * (double)AnchorRadius;
                Vector3 basePos = anchor + new Vector3(
                    Mathf.Cos((float)ang) * (float)rr, 0f, Mathf.Sin((float)ang) * (float)rr);
                try
                {
                    basePos.y = 1f + TerrainGenerator.GetHeightAt(basePos.x, basePos.z,
                        ProjectName.Core.Data.BiomeType.Plains, TerrainSeed);   // 마을 접지 계약 수식 재사용
                }
                catch (System.Exception)
                {
                    basePos.y = anchor.y;   // 폴백 — 앵커 높이(헤드리스/테스트 안전 — WandererDriver 선례)
                }
                GameObject src = prefabs[rng.Next(prefabs.Count)];
                if (src == null) continue;
                var go = Instantiate(src, basePos, Quaternion.Euler(0f, (float)(ang * Mathf.Rad2Deg), 0f), transform);
                go.name = $"Butterfly_{i + 1}";
                StripAssetPackSpawnScript(go);   // 에셋팩 자체 스폰 로직 제거 — ButterflySpawnArea 부재 NRE 방지
                var agent = go.AddComponent<ButterflyAgent>();
                agent.Configure(basePos, rng);
                _agents.Add(agent);
            }
            Debug.Log($"[133차-P3] 나비 스폰 완료 — count={_agents.Count} anchors={anchors.Count} prefabs={prefabs.Count}");
        }

        /// <summary>
        /// 에셋팩 스크립트 제거 — 어셈블리 참조 없이 타입명 매칭(Systems asmdef는 Assembly-CSharp
        /// (IdyllicFantasyNature 소속) 미참조 실측 — 컴파일 계약 방어). 애니메이터는 유지(기존 컨트롤러 계약).
        /// </summary>
        private static void StripAssetPackSpawnScript(GameObject go)
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (comp.GetType().Name == "ButterflySpawn")
                {
                    Destroy(comp);   // 제거 후 애니는 루트 Animator로 재생 유지 — 재시작 게이트는 ButterflyAgent 담당
                    break;
                }
            }
        }

        /// <summary>
        /// 프리팹 해석 순서 — ①Inspector 배선(씬 할당) ②Resources 미러(빌드 계약 경로, 운영 시 복제)
        /// ③에디터 AssetDatabase 직접 로드(에디터 Play 자동 배선 — 에셋 경로 실측 계약). 미확보 시
        /// 빈 목록 — 호출부 스킵(대체 에셋 생성 금지: 신규 에셋 제외 계약).
        /// </summary>
        private List<GameObject> ResolvePrefabs()
        {
            var result = new List<GameObject>();
            if (_butterflyPrefab != null) { result.Add(_butterflyPrefab); return result; }
            for (int i = 1; i <= 3; i++)
            {
                var p = Resources.Load<GameObject>(string.Format(ResourcePathFmt, i));
                if (p != null) result.Add(p);
            }
            if (result.Count > 0) return result;
#if UNITY_EDITOR
            for (int i = 1; i <= 3; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(EditorPrefabPathFmt, i));
                if (p != null) result.Add(p);
            }
#endif
            return result;
        }

        // ===== 밤/낮 게이트 — TimeManager 이벤트 기반(IsNight 계약) =====

        private void HookTimeEvents()
        {
            TimeManager tm = TimeManager.Instance;
            if (tm == null) { _timeHooked = false; return; }
            tm.OnNightStart += HandleNightStart;
            tm.OnDayStart += HandleDayStart;
            _timeHooked = true;
            ApplyNightState(tm.IsNight);   // 초기 동기화 — 밤 스폰이면 비활성 개시
        }

        private void UnhookTimeEvents()
        {
            if (!_timeHooked) return;
            TimeManager tm = TimeManager.Instance;
            if (tm != null)
            {
                tm.OnNightStart -= HandleNightStart;
                tm.OnDayStart -= HandleDayStart;
            }
            _timeHooked = false;
        }

        private void HandleNightStart() => ApplyNightState(true);
        private void HandleDayStart() => ApplyNightState(false);

        /// <summary>밤=숨김(SetActive false)/낮=재활성 — 개체 파괴 안전(스킵).</summary>
        private void ApplyNightState(bool night)
        {
            for (int i = 0; i < _agents.Count; i++)
                if (_agents[i] != null) _agents[i].gameObject.SetActive(!night);
        }

        // ===== 개체 드라이버 — 순찰 3포인트 순환 + 사인파 부유 =====

        /// <summary>
        /// 개체 이동 — 루트 이동은 LateUpdate(애니메이터 평가 이후)로 경로 클립과 무충돌. 밤 비활성 중
        /// LateUpdate 미실행(비용 0 — SetActive false 계약).
        /// </summary>
        private sealed class ButterflyAgent : MonoBehaviour
        {
            private Vector3 _base;                              // 스폰점(부유 기준 y)
            private readonly Vector3[] _patrol = new Vector3[PatrolPointCount];
            private int _idx;
            private float _phase;                               // 부유 페이즈(개체별 위상 — 시각 다양화)
            private float _restartAllowedAt;                    // 경로 클립 재시작 최소 간격 게이트
            private Animator _anim;

            public void Configure(Vector3 basePos, System.Random rng)
            {
                _base = basePos;
                _phase = (float)rng.NextDouble() * Mathf.PI * 2f;
                for (int i = 0; i < PatrolPointCount; i++)
                {
                    double a = rng.NextDouble() * System.Math.PI * 2.0;
                    double r = PatrolMinDist + rng.NextDouble() * (AnchorRadius - PatrolMinDist);
                    _patrol[i] = _base + new Vector3(
                        Mathf.Cos((float)a) * (float)r, 0f, Mathf.Sin((float)a) * (float)r);
                }
                transform.position = _base;
                _anim = GetComponent<Animator>();
            }

            private void LateUpdate()
            {
                // 순찰 — 도착(0.2m) 시 다음 포인트 순환
                Vector3 goal = _patrol[_idx];
                Vector3 flat = goal - transform.position; flat.y = 0f;
                if (flat.sqrMagnitude < 0.04f)
                {
                    _idx = (_idx + 1) % _patrol.Length;
                }
                else
                {
                    Vector3 step = flat.normalized * (FlySpeed * Time.deltaTime);
                    transform.position += step;
                    if (step.sqrMagnitude > 0.0000001f)
                    {
                        Quaternion want = Quaternion.LookRotation(step, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, want,
                            Mathf.Clamp01(FaceTurnSlerpRate * Time.deltaTime));
                    }
                }
                // 사인파 부유 — y = 기준 + sin(2π·t/2s) × 0.3m
                Vector3 pos = transform.position;
                pos.y = _base.y + Mathf.Sin((Time.time * Mathf.PI * 2f / BobPeriodSeconds) + _phase) * BobAmplitudeM;
                transform.position = pos;

                // 경로 클립 재시작 — 1회성 클립(임포트 wrap=Once)을 0.95 컷에서 되감기(종료 애니 이벤트 미발화 경로)
                if (_anim != null && _anim.runtimeAnimatorController != null && Time.time >= _restartAllowedAt)
                {
                    var st = _anim.GetCurrentAnimatorStateInfo(0);
                    if (st.length > 0f && !st.loop && st.normalizedTime >= ClipRestartCutoff)
                    {
                        _restartAllowedAt = Time.time + ClipRestartGateTime;
                        _anim.Play(st.shortNameHash, 0, 0f);
                    }
                }
            }
        }
    }
}
