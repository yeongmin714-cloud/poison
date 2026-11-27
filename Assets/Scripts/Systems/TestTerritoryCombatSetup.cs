// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 618
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_10_TerritoryCombat 씬 전용: 저사양 공격 시스템 집중 점검.
    /// 최소 구성 — Player(AttackSystem) + 타영주 영지(DraculaLord) 1 + 병사(GuardPlaceholder) 1 + 몬스터 1.
    /// 좌클릭 → AttackSystem(Raycast) → IDamageable 데미지/사망 드랍을 단독 검증한다.
    /// 메인씬 확산 전 공격 시스템 통과 여부를 빠르게 판정하는 목적.
    /// </summary>
    public class TestTerritoryCombatSetup : MonoBehaviour
    {
        // [52차] 병사 키 검증 대상 — CreateGuard가 만든 비주얼 바디(FBX/GLB). 0.9s 후 실측·자가수리.
        //   근거: 생성 직후 bounds 실측은 SkinnedMeshRenderer가 렌더 갱신 전일 수 있어 실제와 어긋날 수 있고,
        //   47~51차 4차례 "병사가 작아 보임" 재보고의 잔여 용의선 — 렌더 안정화 후 플레이어와 같은 방식으로 재측정한다.
        private readonly System.Collections.Generic.List<GameObject> _heightTrackedBodies = new System.Collections.Generic.List<GameObject>();

        [Header("배치 좌표")]
        [SerializeField] private Vector3 _lordPos = new Vector3(0, 0, 15);
        [SerializeField] private Vector3 _guardPos = new Vector3(3, 0, 12);
        [SerializeField] private Vector3 _monsterPos = new Vector3(-5, 0, 8);
        [SerializeField] private Vector3 _myTerritoryPos = new Vector3(0, 0, 32);
        [SerializeField] private Vector3 _enemyTerritoryPos = new Vector3(0, 0, -32);

        // [2026-10-11] Test_15_Livelihood 전용 게이트 — Awake 최상단에서 판정해 더미/시딩 슬림화에 사용.
        private bool _isLivelihoodScene;

        // [148차] Test_20_LivelihoodResources 전용 게이트 — 자원/낚시 씬 분리(주민스폰 제거·농경만 Test_15 유지).
        private bool _isLivelihoodResourcesScene;

        // [147차] Test_15 설계 마을 기본 설계 id — BuildPlotFlat 교체용(계획 §5 확정: V1 강변 자급자족 마을,
        //   표준 설계만 평지 모드 대상). 사용자 지명 시 const 1줄로 교체 가능하게 노출(§6-3 ①).
        private const string Test15DefaultVillageDesignId = "V1";

        // ═══════════════ [147차] P0 계측 — Awake 주 단계별 Stopwatch ms 누적 + 1줄 요약(계획 §1-3/§6-3) ═══════════════
        // 용도: 로딩 원인 재분석(§0-C "미측정" 해소) — 단계별 ms 표가 1급 단서.
        //   첫 Play(강제 리컴파일 ~10.5s 고정비 §0-A) vs 2회차 Play(무변경) 구분 실측은 사용자 Play 로그로 판정.
        // 동일 키 재호출 시 누적 합산. Stopwatch는 정규한정 — using System.Diagnostics 추가 금지(Debug 모호성, §6-3 ⑤).
        private readonly System.Collections.Generic.Dictionary<string, long> _p0StageMs =
            new System.Collections.Generic.Dictionary<string, long>();

        /// <summary>[147차] 단계 실행 + 경과 ms를 동일 키에 누적(예외가 나도 finally로 계측 보장).</summary>
        private void P0Time(string stageKey, System.Action stage)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { stage(); }
            finally
            {
                _p0StageMs[stageKey] = (_p0StageMs.TryGetValue(stageKey, out long prev) ? prev : 0L) + sw.ElapsedMilliseconds;
            }
        }

        /// <summary>[147차] Awake 말미 1줄 요약 로그 — P0 판정용(단계별 ms 표).</summary>
        private void P0LogSummary()
        {
            var sb = new System.Text.StringBuilder("[147차][P0] Awake 단계별(ms):");
            foreach (var kv in _p0StageMs)
                sb.Append($" {kv.Key}={kv.Value}ms");
            Debug.Log(sb.ToString());
        }

        private void Awake()
        {
            bool isLivelihoodEarly = gameObject.scene.name != null && gameObject.scene.name.Contains("Livelihood");
            _isLivelihoodScene = isLivelihoodEarly;

            // [148차] Resources 전용 게이트 — isLivelihoodEarly와 동일 패턴(씬명 부분문자열 판정).
            _isLivelihoodResourcesScene = gameObject.scene.name != null && gameObject.scene.name.Contains("LivelihoodResources");

            // Test_10_TerritoryCombat 전용: HighSpec 모드 강제 (메인 씬은 Balanced 기본값 유지)
            ActionFeel.SetMode(ActionFeelMode.HighSpec);

            // Test 씬 분열 비활성(HP바/드랍 미정합) — 슬라임이 HP 30% 이하에서 초록 구체 2개로 늘어나
            // "몬스터가 안 죽고 늘어난다" 체감을 유발하는 것을 차단. 분열체는 AnimalAI만 부착된 풀HP 구체.
            MonsterSkillSystem.SlimeSplitEnabled = false;

            // [2026-09-11] 테스트 씬 몬스터 레벨 스케일 HP 게이트 — 몬스터 몇 타에 사망해야 공격 검증 가능.
            // 영상 실측(테스트씬 영상 4): 레벨 스케일링(hpPerLevel×level)으로 MaxHP가 커져 HP바 비율이
            // 거의 0처럼 보여도 실제 HP가 남아 계속 두드려도 안 죽는 증상 → 스케일링 비활성.
            MonsterLevelManager.LevelScalingEnabled = false;

            // GetHeightAt 계약 정합: 배치 좌표의 XZ는 존중하고 y만 수식 표면으로 재설정.
            // PlayerMovement.ClampToGroundByHeight / BlobShadow가 기대하는 표면(1+GetHeightAt) 위에
            // 엔티티가 정확히 떨어지도록 하여 텔레포트 진동을 근본 차단.
            float lordY = SurfaceY(_lordPos.x, _lordPos.z) + 1.6f;   // 영주 box(3m)
            float guardY = SurfaceY(_guardPos.x, _guardPos.z) + 1.0f; // 병사 box(1.8m)
            float monsterY = SurfaceY(_monsterPos.x, _monsterPos.z) + 0.9f; // 몬스터 box(1m, GO scale 1.5)
            _lordPos = new Vector3(_lordPos.x, lordY, _lordPos.z);
            _guardPos = new Vector3(_guardPos.x, guardY, _guardPos.z);
            _monsterPos = new Vector3(_monsterPos.x, monsterY, _monsterPos.z);

            EnsureGameManager();
            SetupCamera();      // 2026-09-10: SetupPlayer보다 먼저 — PlayerMovement.Awake의 "카메라 없음! 생성" 에러 방지
            SetupPlayer();
            P0Time("SetupGround", SetupGround);   // [147차] P0 계측(§6-3 ②)
            SetupLight();
            EnsureEventSystem();
            // [P21→P21b 사용자 정정] 영지/실내 상호작용(트리거) 전면 제거 — 실내 작업은
            //   IndoorScene을 에디터에서 직접 연다. 테스트10에는 병사 1명 + 몬스터 1마리만.
            //   (전투/F키 상호작용/화살 명중 등 실외 검증용 최소 구성)
            AttachAttackSystem();
            // [2026-10-11] Test_15_Livelihood는 생활기술 전용 검증 씬 — 병사/몬스터 더미 배치 제외(사용자 요구 슬림화).
            if (!_isLivelihoodScene)
                SetupTestDummies();   // 병사 1 + 몬스터 1
            // Test_10은 MainScene의 GameSetup/CoreSystemsBootstrap을 거치지 않으므로
            // UTK 루트/패널을 이 씬에서 명시적으로 준비하고, 자가부트 창은 각 UI 타입 Ensure로 동일하게 보장.
            P0Time("UTK", SetupUITestArena);   // UTK 창 부트 + 창고·크래프트 박스/전 아이템 시딩 — [147차] P0 계측(§6-3 ②)

            // [무번호 181] Test_10 폭탄 지급 — 퀵슬롯 등록 후 번호키 무장·좌클릭 투척 검증용(BombArmController 선배선).
            // 기존 지급 블록(Mat_Wood 등 Test15 블록) 무수정 — 신규 블록만(additive). Instance 부재 시 보류 경로(L775 선례).
            // 씬 가드: 본 Awake는 Test_10/13/14/15/17·Shop 공유 — Test_10_TerritoryCombat에서만 지급(isShopScene 선례).
            bool isBombTestScene = gameObject.scene.name != null && gameObject.scene.name.Contains("Test_10");
            if (isBombTestScene)
            {
                var bombInv = PlayerInventory.Instance;
                if (bombInv != null)
                {
                    bombInv.AddItem(PlayerInventory.Bomb_Explosive, 3);
                    bombInv.AddItem(PlayerInventory.Saddle, 1);   // [205차] Test_10 인벤 안장 지급 — 말 길들이기 테스트
                    Debug.Log("[TestTerritoryCombat] ✅ 폭탄 3개 지급; 안장 1개 지급 — 퀵슬롯 등록 후 번호키 무장·좌클릭 투척");
                }
                else
                {
                    // [206차] PlayerInventory 부재 → 지연 재시도(3회·1s 간격) — Awake 실행 순서 타이밍 스킵 보강
                    StartCoroutine(GrantBombAndSaddleDelayed());
                }
            }


            // 2026-09-10: Test_10에 몬스터 없음 — Aggro 등록 없으므로 시스템 인스턴스만 정리 대상.
            // (기존: EnsureGameManager가 MonsterAggroSystem을 GM에 부착 — DontDestroyOnLoad가 아니라 씬 정리 경고는
            //  AnimalAI 등이 런타임에 Instance 프로퍼티로 자동 생성한 별도 GO. 부팅 후 존재 시 씬 전환 유실 방지 차원에서 유지)

            // Phase 1 훅: Test_10 전용 애니 부트 — 레거시 Procedural/Neural 제거 + Player_AC/HumanoidClipDriver 부착
            var playerAnimBoot = GameObject.FindGameObjectWithTag("Player");
            if (playerAnimBoot != null) playerAnimBoot.AddComponent<TestPlayerAnimatorBoot>();

            bool isShopScene = gameObject.scene.name != null && gameObject.scene.name.Contains("Shop");
            if (isShopScene) SetupShopTestScene();

            // [2026-10-07] Test_14_LordsWarVillage 전용 — 영주 간 AI 전쟁(점령)·내 영지 문→실내·마을 상점/주민 NPC 검증 씬.
            // 씬 이름에 "LordsWar" 가 들어가면 배치. base Awake(플레이어/지형/시스템/UTK)는 이미 실행됨.
            bool isLordsWarScene = gameObject.scene.name != null && gameObject.scene.name.Contains("LordsWar");
            if (isLordsWarScene) SetupLordsWarVillageTestScene();

            // [62차] Test_17_LordInteraction 전용 — 야외 마을 상호작용(대화 패널) 검증 씬.
            //   씬 이름에 "LordInteraction"이 들어가면 야외 마을 배치(마을 NPC 2 + 영주 NPC 1 + 내 병사 3 + 적군 병사 3).
            //   base Awake(플레이어/지형/시스템/UTK + GSM·RTS·Router 자가부트)는 이미 실행됨. IndoorActive는 세팅하지 않음(야외 기본 false).
            bool isLordInteractionScene = gameObject.scene.name != null && gameObject.scene.name.Contains("LordInteraction");
            if (isLordInteractionScene) SetupLordInteractionTestScene();

            // [2026-10-07] Test_15_Livelihood 전용 — 낚시/채광/채집/농경 4활동 검증 씬.
            // 씬 이름에 "Livelihood" 가 들어가면 배치. base Awake(플레이어/지형/시스템/UTK)는 이미 실행됨.
            // [148차] 생활씬 스테이지는 Resources 전용 — 자원/낚시 검증은 Test_20_LivelihoodResources로 분리
            //   (Test_15 경량화: 주민스폰 제거·농경만 유지, 계획 §1-3). SetupLivelihoodTestScene 호출 소멸
            //   (원복 가능성 대비 보존 — 메서드는 남김).
            if (_isLivelihoodResourcesScene) P0Time("생활씬", SetupLivelihoodResourcesScene);   // [147차] P0 계측(§6-3 ②) + [148차] Resources 전용 분기 교체

            // [81차] Test_15_Livelihood 전용 — 메인씬 동일 마을 1개(건물+주민) 재현 + 음식점 마약 경영
            //   검증 상태 시딩(음식점 실제 건설 경로/재고/판매틱). 상세 계약은 메서드 요약 참조.
            if (_isLivelihoodScene && !_isLivelihoodResourcesScene) SetupTest15VillageDrugEconomy();   // [148차] 마을/마약 경로 — Resources 씬 제외(자원 전용 경량 씬 분리)

            // [147차] P0 요약 — Awake 주 단계별 ms 1줄 로그(계획 §1-3 P0 1급 단서).
            P0LogSummary();

            // [52차] 병사 키 검증+자가수리 — 렌더 안정화(0.9s) 후 실측(플레이어 대조 로그).
            //   이 setup 경로로 스폰된 병사가 있을 때만 실행.
            if (_heightTrackedBodies.Count > 0)
                StartCoroutine(VerifySoldierHeightsDelayed());
        }

        /// <summary>[206차] Test_10 폭탄/안장 지급 지연 재시도 — PlayerInventory.Instance 초기화 타이밍 대응.
        ///   Awake 순서에 따라 Instance가 아직 미성립일 수 있어(기존 즉시 스킵 경로) VerifySoldierHeightsDelayed
        ///   선례의 코루틴으로 3회·1s 간격 재시도. 성공 시 즉시 종료, 3회 실패 시 경고 로그(스킵 사유 추적).</summary>
        private System.Collections.IEnumerator GrantBombAndSaddleDelayed()
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                yield return new WaitForSeconds(1f);
                var inv = PlayerInventory.Instance;
                if (inv == null) continue;
                inv.AddItem(PlayerInventory.Bomb_Explosive, 3);
                inv.AddItem(PlayerInventory.Saddle, 1);   // [205차] 인벤 안장 동반 지급(말 길들이기 테스트)
                Debug.Log($"[TestTerritoryCombat] ✅ 폭탄 3개 지급; 안장 1개 지급(지연 재시도 {attempt}회차) — 퀵슬롯 등록 후 번호키 무장·좌클릭 투척");
                yield break;
            }
            Debug.LogWarning("[TestTerritoryCombat] PlayerInventory 3회 재시도 후에도 부재 — 폭탄/안장 지급 보류 [206차]");
        }

        // ═══════════════ [81차] Test_15 마을 + 음식점 마약 경영 검증 ═══════════════
        /// <summary>
        /// Test_15_Livelihood 전용 — 메인씬과 동일 코드로 마을 1개(건물+주민)를 재현하고 음식점
        /// 마약 경영(D1~D5) 엔드투엔드 검증 상태를 시딩한다. (사용자 요구: "메인씬 마을 하나를 그대로
        /// 구현해서 밀매(마약 경영) 시스템이 가능하게 확인")
        ///  ①마을: VillagePlacementSystem 결정론 좌표 중 플레이어 스폰에서 가장 가까운 '일반'(비촌락) 마을
        ///    → [147차] Village5DesignRenderer.BuildPlotFlat(기본 설계 id const — 설계 배치표 마을, 울타리 링 270 소멸)
        ///    → 대표면 상점주인 [148차] 주민 스폰은 제거(Awake 15.5s 절감) — 자원/낚시 검증은 Test_20 분리
        ///  ②음식점: 실제 건설 경로(골드600/나무5/돌3 시드 → TryPlace → ForceComplete → StructureCompleted
        ///    → RestaurantPropBuilder 실물 + SetRestaurantBuilt). 위치 = 마을 중심→성 방향 12m
        ///    (영지 반경 60m 내 보장 + 마을 반경 45m 매칭 내). TryPlace 실패 시 플래그만 세팅(실물 생략).
        ///  ③재고: 마약 요리(drugged_cook_01, 3단계, 30개, 기본가 30) + 일반 요리(20개, 기본가 10)
        ///    + 등록가 30(권장가 = 기본가 — 수요 팩터 1.0 시작)
        ///  ④판매 담당 병사: 씬 내 GuardPlaceholder.StableId(없으면 GTEST01 — 발각 시 실체 부재 경로)
        ///  ⑤틱: TimeManager.GetOrCreate + RestaurantDailyTicker 부착(OnDayStart → RunAllDaily — 프로덕션 경로)
        ///  ⑥Y키: 일일 판매 틱 즉시 1회(검증 루프 단축용 숏컷 — RunAllDaily+이탈 판정)
        /// </summary>
        private void SetupTest15VillageDrugEconomy()
        {
            try
            {
                // ① 마을 선택 — 플레이어 스폰에서 가장 가까운 '일반'(비촌락) 마을 (결정론)
                var playerGO = GameObject.FindGameObjectWithTag("Player");
                Vector3 origin = playerGO != null ? playerGO.transform.position : Vector3.zero;
                VillagePlacementSystem.VillageInfo best = default;
                float bestDist = float.MaxValue;
                bool found = false;
                foreach (var v in VillagePlacementSystem.GetAllVillages())
                {
                    if (v.isHamlet) continue;   // 일반 마을만 — 주민 6~10명(판매 수요 실측 최적)
                    float d = (v.center - origin).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = v; found = true; }
                }
                if (!found)
                {
                    Debug.LogWarning("[Test15VillageDrug] 마을 조회 실패 — 마을/마약 경영 셋업 스킵");
                    // [142차 C2] 폴백 — 마을 미발견 시 원점 앵커(호수 남측 배치 유지: dOut=남).
                    SetupLivelihoodCompactCluster(Vector3.zero, Vector3.forward * 10f);
                    return;
                }
                Debug.Log($"[Test15VillageDrug] 마을 선택: {best.nation} #{best.index} center=({best.center.x:F1},{best.center.z:F1}) dist={Mathf.Sqrt(bestDist):F0}m (메인씬 동일 좌표)");

                // [149차 P1] 농경 아군 영지 시딩(Test_15 전용) — 밭(FarmingManager.SpawnPlots East#1)의 소유 판정 통과.
                //   FarmPlot.ValidateOwnership은 TerritoryDatabase PlayerOwned를 요구하고, 미시딩 시
                //   "아군 영지에서만 경작 가능"으로 Grass 강제된다(사용자 보고). Test_10(L1933)/Test_14(L779) 선례와
                //   동일한 프로덕션 상태 API — 배너/가드 색상은 소유 전환의 정상 부작용(검증에 유리).
                TerritoryDatabase.Instance.SetOwnership(NationType.East, 1, TerritoryOwnership.PlayerOwned);
                Debug.Log("[149차] ✅ 농경 아군 영지 시딩 — East_01 PlayerOwned (밭 경작 가능)");

                // [146차] Test_15 기본 지형 복귀 — Idyllic 플롯/쇼케이스 10마을 빌드 제거(로딩 경량화, 사용자 지시). 메인씬 Idyllic 무영향(Village5 호출점은 본 파일이 유일). [145차 P2b] H1 분기는 Village5DesignRenderer에 보존.
                // [146차] 기본 지형 경로 — 86차 지면 패치 + 마약 경영(항상 실행).
                // [147차] 판정 — 유지: SetupGround는 원점 ±60m만 커버, 마을 중심은 ~454m 이격(미커버 — 계획 §7 확정사실 2).
                //   평지 게이트로 패치 정점 전부 동일 y(1+Test15FlatHeight) — 중복 무해, 호출 유지.
                P0Time("지면패치", () => BuildVillageGroundPatch(best.center, 140));   // [147차] P0 계측(§6-3 ③)

                // [86차] 플레이어를 마을 가장자리로 이동 — 건물/주민/음식점/펜스를 즉시 확인 가능(도보 455m 제거).
                //   카메라는 HandleCameraInput이 매 프레임 추종, 접지는 ClampToGroundByHeight가 흡수한다.
                if (playerGO != null)
                {
                    Vector3 toOrigin = (Vector3.zero - best.center); toOrigin.y = 0f;
                    Vector3 edge = best.center + (toOrigin.sqrMagnitude > 0.01f ? toOrigin.normalized : Vector3.back) * (best.radius + 18f);
                    edge.y = SurfaceY(edge.x, edge.z);
                    playerGO.transform.position = edge;
                    Debug.Log($"[Test15VillageDrug] 플레이어 이동: 마을 가장자리 ({edge.x:F0},{edge.z:F0}) — 마을 중심까지 {(edge - best.center).magnitude:F0}m");
                }

                // [142차 C2] 생활기술 컴팩트 클러스터 — 마을 가장자리(플레이어 착지지) 앵커 기준.
                // [151차 P4] 약초3/밭2x2/호수 — 호수는 142차 블록 복원 복귀(물뿌리개 안내 정합), 나무3/돌3은
                //   NatureCluster(Test_20) 분리 유지. 결정론 배치(마을 건물/식당 스팟 무충돌).
                SetupLivelihoodCompactCluster(
                    playerGO != null ? playerGO.transform.position : best.center + Vector3.back * (best.radius + 18f),
                    best.center);

                // [147차] 설계 마을 플롯 — 구형 hut GLB+울타리 링 270(BuildSingleVillage) → V1 설계 플롯으로 교체
                //   (울타리 270 인스턴스 소멸 — 로딩 경량화 L2, 계획 §1-2). 주민/상점NPC/음식점 키는 best 유지(§6-3 ④).
                P0Time("마을플롯", () => Village5DesignRenderer.BuildPlotFlat(Test15DefaultVillageDesignId, best.center, "_Game"));
                // [148차] 주민 10명 스폰 제거 — Awake 15.5s 절감(147차 P0 실측 66%), 자원/낚시 검증은 Test_20 분리.
                //   (구: 주민스폰 P0 스테이지 삭제 — 스포너 클래스 자체는 무수정, 메인씬 경로 유지)

                // [115차 후속] 상점 NPC 배치 — 세금제안(권장세금 UI) 진입점. VillageNpcSpawner의 대표 마을
                //   상점주인은 NPCAmbientDialogue 경로라 HoverTargetClassifier의 ShopNPC 분류
                //   (TerritoryNPCBehaviour.IsShopNPC 리플렉션) 대상이 아니므로, Test_13/14 SpawnVillageNpc
                //   패턴(캡슐→npc_shop_rigged GLB 교체+E프롬프트+F키)으로 별도 배치한다.
                // [147차] P0 계측 — 세금제안 진입점 경로 유지([115차 후속]).
                P0Time("상점NPC", () => SpawnVillageNpc("test15_shopkeeper", "마을 상인", true, "어서 오세요! 세금 제안도 받고 있습니다.",
                    best.center + new Vector3(4f, 0f, -4f), $"{best.nation}_{best.index:D2}"));

                // [149차 P5] 고객 NPC 1명 — 음식점 구매 액션 관측용 전용 주민(148차 "주민 0명" 계약 유지 — 이 1명은 고객 목적).
                //   RestaurantVisitorSystem 스캔 계약: GameObject.Find($"Village_{nation}_{index:D2}") 자식의 VillagerWanderer
                //   (VillageBuilder 명명 규약). Test_15 마을은 BuildPlotFlat(루트 Village5Designs_Root)이라 계약명 루트가
                //   없으므로 스캔용 루트를 계약명으로 생성하고 고객을 자식으로 스폰한다. 방문 시간대는 게임 시작 8시([134차 B-2])
                //   → 영업 시간(8≤h<22) 충족 — 별도 시각 시딩 불필요.
                GameObject customerRoot = GameObject.Find($"Village_{best.nation}_{best.index:D2}");
                if (customerRoot == null)
                    customerRoot = new GameObject($"Village_{best.nation}_{best.index:D2}");   // [149차 P5] 스캔 계약명 루트
                VillageNpcSpawner.SpawnSingleVillager(customerRoot.transform, "girl1",
                    best.center + new Vector3(6f, 0f, 6f), best.center, "Test15Customer", best.radius);

                // ② 음식점 — 실제 건설 경로(시드 → TryPlace → ForceComplete)
                var stats = PlayerStats.Instance ?? FindAnyObjectByType<PlayerStats>();
                if (stats != null) stats.AddGold(600, "test15_seed");
                else Debug.LogWarning("[Test15VillageDrug] PlayerStats 없음 — 골드 시드 생략(건설 실패 시 플래그 폴백)");
                var inv = PlayerInventory.Instance ?? FindAnyObjectByType<PlayerInventory>();
                if (inv != null)
                {
                    inv.AddItem(PlayerInventory.Mat_Wood, 5);
                    inv.AddItem(PlayerInventory.Mat_Stone, 3);
                }

                if (ConstructionManager.Instance == null)
                {
                    var cmGO = new GameObject("ConstructionManager");
                    cmGO.AddComponent<ConstructionManager>();
                }

                // [116차 R3] Test_15 음식점 프리빌드 폐기 — "처음엔 없음"(사용자 지시). U키 홀로그램으로 직접 건설 검증.
                //   기존 자동 건설(82차)은 실물 생성까지 성공했으나(2026-10-23 Editor.log 실측: ✅ 건설 완료+🏠 실물 생성)
                //   "이미 음식점이 있는 마을" 가드(88차)와 결합해 재건설 불가 체감 유발 → 시작 시 강제 리셋.
                P0Time("음식점리셋", () =>   // [147차] P0 계측 — 내부 문자열 계약 전수 보존(§6-3 ④)
                {
                    VillageDrugState.SetRestaurantBuilt(best.nation, best.index, false);   // 플래그 리셋 — 세이브 복원분 포함
                    foreach (var e in ConstructionManager.Instance.Structures)
                    {
                        if (e.blueprintId != "bp_restaurant" || !e.isComplete) continue;
                        if (ConstructionManager.Instance.TryRemoveStructure(e.structureId))
                            RestaurantPropBuilder.TryDestroyProp(e.structureId);   // 잔존 실물 정리(세이브 복원분 포함)
                    }
                    RestaurantPropBuilder.RebuildFromSave();   // 구조물 제거 후 무동작(멱등) — [106차] 순서 계약 유지
                });
                Debug.Log("[Test15VillageDrug] 음식점 상태 리셋 — restaurantBuilt=false, 잔존 구조물/실물 제거 (U키로 직접 건설)");

                // ④ 판매 담당 병사 — 씬 내 병사 있으면 그 StableId, 없으면 합성 ID(발각 시 실체 부재 경로 실측)
                var guard = FindAnyObjectByType<GuardPlaceholder>();
                string stableId = guard != null && !string.IsNullOrEmpty(guard.StableId) ? guard.StableId : "GTEST01";
                VillageDrugState.AssignGuard(best.nation, best.index, stableId);
                Debug.Log($"[Test15VillageDrug] 판매 담당 병사 배치: {stableId} (실체 {(guard != null ? "있음" : "없음 — 발각 시 플래그 클리어 경로")})");

                // ③ 재고 시딩 — 마약 요리(3단계) 30 + 일반 요리 20, 등록가 = 권장가(기본가) 시작
                VillageDrugState.AddStock(best.nation, best.index, new RestaurantStockItem
                {
                    foodItemId = "drugged_cook_01", displayName = "마약 뿌려진 요리",
                    count = 30, isDrugged = true, drugStage = 3, basePrice = 30,
                });
                VillageDrugState.AddStock(best.nation, best.index, new RestaurantStockItem
                {
                    foodItemId = "cook_01", displayName = "일반 요리",
                    count = 20, isDrugged = false, drugStage = 0, basePrice = 10,
                });
                VillageDrugState.SetRegisteredPrice(best.nation, best.index, 30);
                Debug.Log("[Test15VillageDrug] ✅ 재고 시딩: 마약 요리(3단계) 30 / 일반 요리 20 / 등록가 30");

                // ⑤ 판매 틱 배선 — TimeManager 보장 + RestaurantDailyTicker(OnDayStart → RunAllDaily)
                TimeManager.GetOrCreate();
                if (FindAnyObjectByType<RestaurantDailyTicker>() == null)
                {
                    var tickerGO = new GameObject("RestaurantDailyTicker");
                    tickerGO.AddComponent<RestaurantDailyTicker>();
                }
                Debug.Log("[Test15VillageDrug] 🧪 셋업 완료 — 확인: ①마을 건물+주민 ②음식점 실물 ③Y키=일일 판매 틱(골드+중독 상승) ④새벽 6시 경계 시 자동 틱(OnDayStart)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test15VillageDrug] 셋업 실패: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>[86차] 마을 국소 지형 패치 — SetupGround와 동일 계약(정점 y=SurfaceY=GetHeightAt+1)으로
        ///   지정 중심 ±halfSize 미니지형을 추가 생성한다. 이름을 달리해 SetupGround의 "Ground" 중복 가드와
        ///   독립(멱등: Ground_Village 존재 시 스킵). 높이 수식이 동일해 기존 지형/마을 건물 접지와 연속된다.</summary>
        private void BuildVillageGroundPatch(Vector3 center, int halfSize)
        {
            if (GameObject.Find("Ground_Village") != null) return;

            const int step = 2;   // 정점 간격 2m — ±140m → 141x141 = 19881 정점
            int vertCount = halfSize * 2 / step + 1;

            var vertices = new Vector3[vertCount * vertCount];
            var triangles = new int[(vertCount - 1) * (vertCount - 1) * 6];

            int vi = 0;
            for (int zi = 0; zi < vertCount; zi++)
            {
                for (int xi = 0; xi < vertCount; xi++)
                {
                    float x = center.x - halfSize + xi * step;
                    float z = center.z - halfSize + zi * step;
                    vertices[vi++] = new Vector3(x, SurfaceY(x, z), z);
                }
            }

            int ti = 0;
            for (int zi = 0; zi < vertCount - 1; zi++)
            {
                for (int xi = 0; xi < vertCount - 1; xi++)
                {
                    int tl = zi * vertCount + xi;
                    int tr = tl + 1;
                    int bl = tl + vertCount;
                    int br = bl + 1;
                    triangles[ti++] = tl;
                    triangles[ti++] = bl;
                    triangles[ti++] = tr;
                    triangles[ti++] = tr;
                    triangles[ti++] = bl;
                    triangles[ti++] = br;
                }
            }

            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var ground = new GameObject("Ground_Village");
            ground.transform.position = Vector3.zero;   // 메시는 월드 좌표 기준

            var meshFilter = ground.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = ground.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
            renderer.material = mat;

            var meshCollider = ground.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;

            Debug.Log($"[Test15VillageDrug] 마을 지형 패치 생성: center=({center.x:F0},{center.z:F0}) ±{halfSize}m 정점 {vertCount}x{vertCount}");
        }

        /// <summary>[86차] Test_15 전용 — Y키 = 일일 판매 틱 즉시 1회(RunAllDaily + 이탈 판정).
        /// 프로덕션 경로는 RestaurantDailyTicker(OnDayStart)지만, 검증 루프 단축용 숏컷을 제공한다.
        /// ⚠ T키는 TutorialGuideUTK 토글과 충돌(실측) → Y키 사용.</summary>
        private void Update()
        {
            if (!_isLivelihoodScene) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.yKey.wasPressedThisFrame) return;

            try
            {
                int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
                Debug.Log($"[Test15VillageDrug] Y키 — 일일 판매 틱 강제 1회(day={day})");
                RestaurantSystem.RunAllDaily(day);
                VillagerDepartureSystem.RunDailyDeparture(day);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test15VillageDrug] 판매 틱 실패: {ex.Message}");
            }
        }

        /// <summary>[52차→53차] 병사 최종 키 검증+자가수리 — 렌더링 안정화 후 실측.
        ///   [53차] 실측을 포즈/FX 독립(MeasureStandingHeight)으로 교체 — 52차 실측에서 애니 포즈로
        ///   병사 2.07m(걷기 포즈)/플레이어 4.01m(이펙트 렌더러 오염) 오판이 실증됨.
        ///   목표(1.8m) ±7% 초과 병사는 즉시 재스케일. 플레이어 비주얼도 동일 실측 — 1.8m 이탈 시
        ///   모델 자식을 자가수리(47~52차 "병사가 작아 보임"의 실체 = 플레이어 오스케일 가능성 차단).</summary>
        private System.Collections.IEnumerator VerifySoldierHeightsDelayed()
        {
            yield return new WaitForSeconds(0.9f);

            var player = GameObject.FindGameObjectWithTag("Player");
            // [54차] 플레이어 = **기준 자체** — 자가수리 폐지(53차 ×0.23 오축소 실측: localBounds 오측
            //   7.91m가 원인). 플레이어 모델 자식을 월드 bounds(FX 제외)로 실측해 기준치 로그만 남긴다.
            //   병사는 이 기준치와 동일 목표(SoldierTargetHeight=1.8m)로 정규화되므로 시각 일치.
            float playerH = 0f;
            if (player != null)
            {
                Transform pModel = FindPlayerVisualModel(player);
                playerH = TerritoryBuilder.MeasureStandingHeight(pModel != null ? pModel.gameObject : player, skinnedLocalBounds: false);
            }
            Debug.Log($"[키검증] 플레이어 기준 키 {playerH:0.00}m (모델 자식, FX 제외) — 병사 {_heightTrackedBodies.Count}명 대조 (목표 {TerritoryBuilder.SoldierTargetHeight:0.00}m)");

            int fixedCount = 0;
            foreach (var body in _heightTrackedBodies)
            {
                if (body == null) continue;
                float h = TerritoryBuilder.MeasureStandingHeight(body);
                if (h < 0.05f) continue;   // 렌더러 부재 등 측정 불가 — 스킵
                float deviation = Mathf.Abs(h - TerritoryBuilder.SoldierTargetHeight);
                if (deviation <= TerritoryBuilder.SoldierTargetHeight * 0.07f)
                {
                    Debug.Log($"[키검증] ✅ {body.name} 최종 키 {h:0.00}m — 정상");
                    continue;
                }
                float corr = TerritoryBuilder.SoldierTargetHeight / h;
                body.transform.localScale *= corr;
                fixedCount++;
                Debug.Log($"[키검증] 🔧 {body.name} 최종 키 {h:0.00}m → 자가수리 ×{corr:0.00} (목표 {TerritoryBuilder.SoldierTargetHeight:0.00}m)");
            }
            if (fixedCount > 0)
                Debug.Log($"[키검증] 자가수리 {fixedCount}명 완료 — 다음 프레임부터 1.8m 반영");
        }

        /// <summary>[53차] 플레이어 비주얼 모델 자식 탐색 — "PlayerModel" 이름 우선, 없으면 SkinnedMeshRenderer 보유 자식.</summary>
        private static Transform FindPlayerVisualModel(GameObject player)
        {
            foreach (Transform child in player.transform)
                if (child.name.Contains("PlayerModel"))
                    return child;
            foreach (Transform child in player.transform)
                if (child.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    return child;
            return null;
        }

        /// <summary>[52차] 게임오브젝트 전체 렌더러 월드 bounds 높이 실측(플레이어/병사 공용 자) — [53차] MeasureStandingHeight 위임.</summary>
        private static float MeasureVisualHeight(GameObject go)
        {
            return TerritoryBuilder.MeasureStandingHeight(go);
        }

        /// <summary>
        /// Test_15_Livelihood 전용 런타임 배치 — 채집/채광/농경/낚시 4종 생활기술이 실제로 동작하는지 검증.
        ///  1) 낚시: Water 태그 원판(콜라이더) + FishingSystem 보장 + 스타터 낚시대(fishing_rod) 지급.
        ///  2) 채광: ResourceNode(Wood/Stone/IronOre) 프리미티브 큐브 3종 (SetupMiningNodes 재사용).
        ///  3) 채집: HerbPickup(Red/Purple/Green) 약초 3종 (SetupHerbs 재사용).
        ///  4) 농경: FarmingManager.SpawnPlots 2x2 밭 (SetupFarm 재사용).
        /// [148차] 호출 소멸 — 생활씬 스테이지는 SetupLivelihoodResourcesScene(Test_20 전용)으로 교체.
        ///   메서드는 원복 가능성 대비 보존(L456 선례).
        /// </summary>
        private void SetupLivelihoodTestScene()
        {
            SetupFishingArea();
            // [142차 C2] 자원/농경/호수 배치 = SetupLivelihoodCompactCluster(마을 가장자리 앵커)로 이관.
            // 근거: [86차] 플레이어가 마을 가장자리로 텔레포트되는데 기존 원점 기준 배치(호수 (0,-22)·밭
            // (0,18)·자원 그리드)는 마을에서 수백 m 밖이라 전혀 안 보였다(142차 사용자 재보고 "나무와 돌
            // 보이지 않고, 농경 지역도 안보여"). 클러스터 호출은 SetupTest15VillageDrugEconomy의
            // 플레이어 텔레포트 직후(마을 확정 시점)에 수행. 기존 산포 배치 메서드
            // (SetupNatureResources/SetupHerbs/SetupFarm/SetupMiningNodes)는 원복 가능성 대비 보존(호출부만 제외).
            Debug.Log("[Test15Livelihood] 🧪 생활기술 인프라 준비(낚시 시스템+낚시대) — 자원 클러스터는 마을 앵커 기준[142차 C2]");
        }

        /// <summary>
        /// [142차 C2] 생활기술 컴팩트 클러스터 — 마을 가장자리(플레이어 착지지) 앵커 기준.
        /// [148차] 호수/나무/돌 블록은 SetupLivelihoodNatureCluster로 오프셋 수식 그대로 이식.
        /// [151차 P4] 호수 1개 복귀 배치 — 142차 호수 블록(anchor+dOut×16, 원판 r=12, tag "Water")을 본
        ///   메서드로 복원 이식(사용자 지시: 물뿌리개 "호수에서 충전하세요" 안내와 실제 호수 정합).
        ///   나무/돌은 NatureCluster(Test_20) 분리 유지 — 부분 복원만 수행.
        /// 결정론: 고정 오프셋+고정 요각(Random 0건 계약). dOut = 앵커→마을 반대편(외부), dL = 수직.
        ///  밭 2x2 = anchor+dL×14 · 약초 3 = 밭 주변(±3m) — 마을 반경 밖(무충돌), SurfaceY 접지 계약 준수.
        ///  호수 = anchor+dOut×16(원판 r=12) — 밭 클러스터(중심 dL×14, 최외곽 ~4m)와 중심간 21.3m
        ///  (dOut16 ⊥ dL14) > 12+4 → 무충돌(151차 P4 오프셋 실측 — 겹침 0, 오프셋 조정 불필요).
        /// </summary>
        private void SetupLivelihoodCompactCluster(Vector3 anchor, Vector3 villageCenter)
        {
            try
            {
                Vector3 dOut = anchor - villageCenter;
                dOut.y = 0f;
                dOut = dOut.sqrMagnitude > 0.0001f ? dOut.normalized : Vector3.back;
                Vector3 dL = new Vector3(dOut.z, 0f, -dOut.x);   // 수직(좌)
                // [148차] dR 계산은 SetupLivelihoodNatureCluster로 이식(나무/돌 블록 전용)

                // ── 호수(낚시/물담기) — anchor+dOut×16 ──
                // [151차 P4] 142차 호수 블록 복원 이식(SetupLivelihoodNatureCluster L551-566 소스, 오프셋 수식
                //   그대로 — 결정론 보존). 148차 분리로 Test_15에 호수가 없어 물뿌리개 "호수에서 충전하세요"
                //   안내와 불일치했던 점 해소. 나무/돌은 NatureCluster(Test_20) 분리 유지.
                {
                    float wx = anchor.x + dOut.x * 16f, wz = anchor.z + dOut.z * 16f;
                    float wy = SurfaceY(wx, wz);
                    GameObject waterDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    waterDisc.name = "Livelihood_Pond";
                    waterDisc.transform.position = new Vector3(wx, wy + 0.05f, wz);
                    waterDisc.transform.localScale = new Vector3(24f, 0.06f, 24f);   // 반경 12m(기존 계약)
                    waterDisc.tag = "Untagged";
                    var wr = waterDisc.GetComponent<MeshRenderer>();
                    if (wr != null)
                        wr.material = MaterialHelper.CreateLitMaterial(new Color(0.18f, 0.4f, 0.55f), "Test15Pond_Mat");
                    try { waterDisc.tag = "Water"; }
                    catch (UnityException) { Debug.LogWarning("[Test15Livelihood] 'Water' 태그 미정의 — Untagged 유지"); }
                    Debug.Log($"[Test15Livelihood] 🌊 호수(Water) 배치[151차 P4] @ ({wx:F0},{wz:F0})");
                }

                // ── 밭(농경 1부지) + 약초 3 — anchor+dL×14 ──
                Vector3 farmC = anchor + dL * 14f;
                float fx = farmC.x, fz = farmC.z;
                if (FarmingManager.Instance == null)
                {
                    var managerGO = new GameObject("FarmingManager");
                    managerGO.AddComponent<FarmingManager>();
                }
                var plots = FarmingManager.Instance.SpawnPlots(NationType.East, 1,
                    new Vector3(fx, SurfaceY(fx, fz) + 0.1f, fz), rows: 2, cols: 2, spacing: 2.5f,
                    HerbPickup.HerbType.Red, 3);   // [26차] 성장 3일 계약 유지
                Debug.Log($"[Test15Livelihood] 🌾 농경 부지 {plots.Count}칸 배치[142차 C2] @ ({fx:F0},{fz:F0})");
                SetupHerb("Herb_Red", "Models/UserProvided/herb_red", new Vector3(fx + 3f, 0f, fz + 2f),
                    HerbPickup.HerbType.Red, new Color(0.85f, 0.2f, 0.2f, 1f));
                SetupHerb("Herb_Purple", "Models/UserProvided/herb_purple", new Vector3(fx - 3f, 0f, fz + 2f),
                    HerbPickup.HerbType.Purple, new Color(0.7f, 0.2f, 0.8f, 1f));
                SetupHerb("Herb_Green", "Models/UserProvided/herb_green", new Vector3(fx, 0f, fz - 2f),
                    HerbPickup.HerbType.Green, new Color(0.2f, 0.7f, 0.2f, 1f));

                Debug.Log("[Test15Livelihood] ✅ 컴팩트 클러스터 완료[151차 P4 호수 복귀] — 약초 3·밭 2x2(좌14m)·호수(정면16m) — 나무/돌은 NatureCluster(Test_20) 분리 유지");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test15Livelihood] 컴팩트 클러스터 배치 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// [148차] 생활기술 자원 클러스터 — 호수(낚시/물담기)/나무 3(벌목)/돌 3(채광) 블록.
        /// 출처: SetupLivelihoodCompactCluster에서 오프셋 수식 그대로 이식(142차 C2/C3 — 결정론 보존, Random 0건).
        /// 로그 접두어 [Test15Livelihood]는 이동 후에도 유지(문자열 계약 — 검색 가능성 우선, 별도 계약 없음 확인).
        ///  호수(원판 r12) = anchor+dOut×16 · 나무 3 = anchor+dR×14+dOut×(−5,0,+5) · 돌 3 = anchor+dR×24+dOut×(−4,0,+4)
        ///  돌은 호수 원판(중심 dOut×16, r=12)에서 최근접 18m 밖(142차 C3 재배치 계약 유지).
        /// </summary>
        private void SetupLivelihoodNatureCluster(Vector3 anchor, Vector3 villageCenter)
        {
            try
            {
                Vector3 dOut = anchor - villageCenter;
                dOut.y = 0f;
                dOut = dOut.sqrMagnitude > 0.0001f ? dOut.normalized : Vector3.back;
                Vector3 dL = new Vector3(dOut.z, 0f, -dOut.x);   // 수직(좌)
                Vector3 dR = -dL;

                // ── 호수(낚시/물담기) — anchor+dOut×16 ──
                {
                    float wx = anchor.x + dOut.x * 16f, wz = anchor.z + dOut.z * 16f;
                    float wy = SurfaceY(wx, wz);
                    GameObject waterDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    waterDisc.name = "Livelihood_Pond";
                    waterDisc.transform.position = new Vector3(wx, wy + 0.05f, wz);
                    waterDisc.transform.localScale = new Vector3(24f, 0.06f, 24f);   // 반경 12m(기존 계약)
                    waterDisc.tag = "Untagged";
                    var wr = waterDisc.GetComponent<MeshRenderer>();
                    if (wr != null)
                        wr.material = MaterialHelper.CreateLitMaterial(new Color(0.18f, 0.4f, 0.55f), "Test15Pond_Mat");
                    try { waterDisc.tag = "Water"; }
                    catch (UnityException) { Debug.LogWarning("[Test15Livelihood] 'Water' 태그 미정의 — Untagged 유지"); }
                    Debug.Log($"[Test15Livelihood] 🌊 호수(Water) 배치[142차 C2] @ ({wx:F0},{wz:F0})");
                }

                // ── 나무 3(벌목 GLB E채널) — anchor+dR×14, dOut 전개 ──
                var woodVisuals = NaturalResourceSpawner.LoadWoodTreeVisuals();
                var container = new GameObject("NaturalResources_Test15");
                (float dx, float dz)[] treeSpots = { (-5f, 0f), (0f, 0f), (5f, 0f) };
                int wood = 0;
                foreach (var t in treeSpots)
                {
                    if (woodVisuals == null || woodVisuals.Length == 0)
                    {
                        Debug.LogWarning("[Test15Livelihood] ⚠️ 나무 GLB 로드 실패 — 나무 배치 생략");
                        break;
                    }
                    float x = anchor.x + dR.x * 14f + dOut.x * t.dx;
                    float z = anchor.z + dR.z * 14f + dOut.z * t.dz;
                    var prefab = woodVisuals[wood % woodVisuals.Length];   // 결정론 순환
                    var srcName = prefab != null ? prefab.name : null;
                    NaturalResourceSpawner.SpawnTestNode(container.transform, $"NaturalResource_Wood_{++wood:D2}",
                        prefab, x, SurfaceY(x, z), z, 0.9f, 15f + wood * 60f,
                        go =>
                        {
                            NaturalResourceSpawner.ConfigureWoodNode(go);
                            NaturalResourceSpawner.ApplyStumpType(go, srcName);   // 벌목 그루터기 종별 전달(28차)
                        });
                }

                // ── 돌 3(채광 GLB E채널) — [142차 C3 재배치] anchor+dR×24+dOut×(−4,0,+4) ──
                //   기존(dOut×14+dR±5)은 호수 원판(중심 dOut×16, r=12) 내부로 계산됐다(사용자 재보고:
                //   "돌이 호수 안에 있어서 채광 불가") — 호수 중심에서 최근접 18m 밖으로 이탈.
                var rockVisuals = NaturalResourceSpawner.LoadRockVisuals();
                (float dx, float dz)[] rockSpots = { (-4f, 0f), (0f, 0f), (4f, 0f) };
                int rock = 0;
                foreach (var t in rockSpots)
                {
                    if (rockVisuals == null || rockVisuals.Length == 0)
                    {
                        Debug.LogWarning("[Test15Livelihood] ⚠️ 돌 GLB 로드 실패 — 돌 배치 생략");
                        break;
                    }
                    float x = anchor.x + dR.x * 24f + dOut.x * t.dx;
                    float z = anchor.z + dR.z * 24f + dOut.z * t.dz;
                    var prefab = rockVisuals[rock % rockVisuals.Length];   // 결정론 순환
                    var srcName = prefab != null ? prefab.name : null;
                    NaturalResourceSpawner.SpawnTestNode(container.transform, $"NaturalResource_Rock_{++rock:D2}",
                        prefab, x, SurfaceY(x, z), z, 0.9f + 0.2f * rock, 140f + rock * 60f,
                        go =>
                        {
                            NaturalResourceSpawner.ConfigureRockNode(go);
                            NaturalResourceSpawner.ApplyStumpType(go, srcName);
                        });
                }

                Debug.Log($"[Test15Livelihood] ✅ 자원 클러스터 완료[148차 Test_20 이관] — 나무 {wood}(좌14m)·돌 {rock}(우24m·호수밖)·호수(정면16m) (앵커 기준)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test15Livelihood] 자원 클러스터 배치 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// [148차] Test_20_LivelihoodResources 전용 — 자원/낚시 씬 셋업.
        ///  낚시 인프라(SetupFishingArea: FishingSystem 보장+낚시대 지급) 이관 근거: 물 의존(물가 E키 낚시/
        ///  물뿌리개 물담기 리필)이므로 호수(Livelihood_Pond)와 함께 Test_15에서 분리 — 리필 검증은
        ///  Test_20 호수에서 수행(계획 §2). 자원 클러스터 = SetupLivelihoodNatureCluster(Vector3.zero,
        ///  Vector3.forward * 10f) — 기존 폴백 호출과 동일 지오메트리(dOut=남 고정 계약, 142차 C2 수식 보존).
        /// </summary>
        private void SetupLivelihoodResourcesScene()
        {
            SetupFishingArea();
            SetupLivelihoodNatureCluster(Vector3.zero, Vector3.forward * 10f);
        }

        /// <summary>
        /// [142차 C] Test_15 전용 — 컴팩트 검증 세트(사용자 지시): 나무(벌목 GLB) 3 · 돌(채광 GLB) 3 ·
        /// 과일나무 폐기(사용자 목록 외 — 산포 감축). 산포 그리드(20+20+3) 폐기 → 원점 근접 고정 배치로
        /// 각 시스템 확인 동선 최소화. 결정론: 고정 좌표+고정 요각(Random 0건 계약).
        /// 메인씬 NaturalResourceSpawner의 공개 로더/스폰 코어/구성기를 재사용(동일 코드 경로) →
        /// E 채널(파랑 진행바)·나무 스웨이·도끼 커서·소멸→리스폰이 그대로 검증된다.
        /// 배치(제외존 준수: 호수(0,-22 r=13)·클러스터(0,19 r=8)·성(0,25 r=9)·스폰(0,0 r=9) 전부 이탈):
        ///  나무 3 — 서측 (−9,−5)/(−12,−1)/(−9,3) / 돌 3 — 동측 (9,−4)/(12,0)/(9,4)
        /// </summary>
        private void SetupNatureResources()
        {
            try
            {
                var woodVisuals = NaturalResourceSpawner.LoadWoodTreeVisuals();
                var rockVisuals = NaturalResourceSpawner.LoadRockVisuals();
                // 과일나무 [142차 C] 폐기 — 사용자 지정 컴팩트 목록(나무/돌/채집/농경/호수) 외 → 로드 생략.
                if ((woodVisuals == null || woodVisuals.Length == 0)
                    && (rockVisuals == null || rockVisuals.Length == 0))
                {
                    Debug.LogWarning("[Test15Livelihood] ⚠️ 자원 GLB 로드 실패(폴백도 비어 있음) — 자원 배치 생략");
                    return;
                }

                var container = new GameObject("NaturalResources_Test15");

                const int WoodTarget = 3, RockTarget = 3;   // [142차 C] 사용자 지정 — 나무 2~3·돌 3
                const float woodX0 = -9f, woodZ0 = -5f;
                const float woodX1 = -12f, woodZ1 = -1f;
                const float woodX2 = -9f, woodZ2 = 3f;
                const float rockX0 = 9f, rockZ0 = -4f;
                const float rockX1 = 12f, rockZ1 = 0f;
                const float rockX2 = 9f, rockZ2 = 4f;
                const float yaw0 = 15f, yaw1 = 140f, yaw2 = 260f;   // 고정 요각 — 결정론(Random 0건)

                int wood = 0, rock = 0;
                // 나무(벌목 E채널) 3 — 서측 클러스터
                (float x, float z, float yaw)[] woodSpots = { (woodX0, woodZ0, yaw0), (woodX1, woodZ1, yaw1), (woodX2, woodZ2, yaw2) };
                foreach (var spot in woodSpots)
                {
                    if (woodVisuals == null || woodVisuals.Length == 0) break;
                    if (Mathf.Abs(spot.x) > 56f || Mathf.Abs(spot.z) > 56f) continue;   // 미니지형 밖(±60m)
                    var prefab = woodVisuals[wood % woodVisuals.Length];   // 결정론 순환
                    var srcName = prefab != null ? prefab.name : null;
                    string nodeName = $"NaturalResource_Wood_{++wood:D2}";
                    NaturalResourceSpawner.SpawnTestNode(container.transform, nodeName, prefab,
                        spot.x, SurfaceY(spot.x, spot.z), spot.z, 0.9f, spot.yaw,
                        go =>
                        {
                            NaturalResourceSpawner.ConfigureWoodNode(go);
                            NaturalResourceSpawner.ApplyStumpType(go, srcName);   // 벌목 그루터기 종별 전달(28차)
                        });
                }
                // 돌(채광 E채널) 3 — 동측 클러스터 (스케일 0.9/1.1/1.3 고정 다양화)
                (float x, float z, float yaw, float scale)[] rockSpots = { (rockX0, rockZ0, yaw0, 0.9f), (rockX1, rockZ1, yaw1, 1.1f), (rockX2, rockZ2, yaw2, 1.3f) };
                foreach (var spot in rockSpots)
                {
                    if (rockVisuals == null || rockVisuals.Length == 0) break;
                    if (Mathf.Abs(spot.x) > 56f || Mathf.Abs(spot.z) > 56f) continue;   // 미니지형 밖(±60m)
                    var prefab = rockVisuals[rock % rockVisuals.Length];   // 결정론 순환
                    var srcName = prefab != null ? prefab.name : null;
                    string nodeName = $"NaturalResource_Rock_{++rock:D2}";
                    NaturalResourceSpawner.SpawnTestNode(container.transform, nodeName, prefab,
                        spot.x, SurfaceY(spot.x, spot.z), spot.z, spot.scale, spot.yaw,
                        go =>
                        {
                            NaturalResourceSpawner.ConfigureRockNode(go);
                            NaturalResourceSpawner.ApplyStumpType(go, srcName);
                        });
                }

                Debug.Log($"[Test15Livelihood] 🌲 자원 배치 완료[142차 C 컴팩트] — 벌목 나무 {wood}/{WoodTarget}(서측), 돌 {rock}/{RockTarget}(동측), 과일나무 0(폐기) (실제 GLB·메인씬 동일 노드 경로)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test15Livelihood] 자원 배치 실패: {ex.Message}");
            }
        }

        /// <summary>XZ 평면 거리 제곱 — 제외존 판정용(sqrt 회피).</summary>
        static float Dxz2(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return dx * dx + dz * dz;
        }

        /// <summary>낚시 인프라: Water 태그 원판 + FishingSystem + fishing_rod 스타터 지급.</summary>
        private void SetupFishingArea()
        {
            try
            {
                // FishingSystem 싱글턴 보장 (MainScene GameSetup.EnsureFishingSystem와 동일 — 별도 GO에 부착)
                if (FishingSystem.Instance == null)
                {
                    var fishGO = new GameObject("FishingSystem");
                    fishGO.tag = "Untagged";
                    fishGO.AddComponent<FishingSystem>();
                    Debug.Log("[Test15Livelihood] ✅ FishingSystem 생성 (물가 E키 낚시 활성화)");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test15Livelihood] FishingSystem 생성 실패: {ex.Message}");
            }

            // PlayerInventory에 스타터 낚시대 지급 (FishingSystem.IsNearWater → HasItem("fishing_rod") 체크 아이디)
            try
            {
                var inv = PlayerInventory.Instance;
                if (inv == null)
                {
                    Debug.LogWarning("[Test15Livelihood] PlayerInventory.Instance 없음 — 낚시대 스타터 지급 보류");
                }
                else if (inv.HasItem("fishing_rod"))
                {
                    Debug.Log("[Test15Livelihood] ✅ 낚시대(fishing_rod) 이미 보유");
                }
                else if (inv.AddItem(PlayerInventory.FishingRodItem, 1))
                {
                    Debug.Log("[Test15Livelihood] ✅ 스타터 낚시대(FishingRodItem) 1개 지급");
                }
                else
                {
                    Debug.LogWarning("[Test15Livelihood] ⚠️ 낚시대 지급 실패 (인벤 가득 참)");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test15Livelihood] 낚시대 스타터 지급 실패: {ex.Message}");
            }

            // Water 태그 원판 [142차 C2] 원점 배치 폐기 — 마을(스폰 수백 m 밖)에서 안 보이는 실측.
            // 호수는 SetupLivelihoodCompactCluster(마을 가장자리 앵커, anchor+dOut×16)로 이전.
            // 낚시 시스템/낚시대 지급은 위 블록에서 유지(위치 무관 — Water 태그 레이캐스트 판정).
        }

        /// <summary>
        /// Test_14_LordsWarVillage 전용 런타임 배치:
        ///  1) 내 영지(East_01 = PlayerOwned) + 문(건물 트리거) → E키로 실내 진입.
        ///  2) 타 소속 영주 2명(West_01·South_01 = LordOwned) — West(Cruel, 공격성 높음)가 South를 침공.
        ///  3) 각 영지 병사 스폰(내 병사=아군, 타 영주 병사=적군) + 물리 행진(WarMarchSimulation)으로 영주 공격 시연.
        ///  4) 작은 마을: 상점 NPC(클릭→상점) + 일반 주민 NPC(클릭→대화 상호작용).
        /// </summary>
        private void SetupLordsWarVillageTestScene()
        {
            EnsureLordsWarDbOwnership();
            PlacePlayerTerritoryWithDoor();
            PlaceEnemyLordsAndWar();
            PlaceVillageWithNpcs();
            LogLordsWarSummary();
        }

        private void EnsureLordsWarDbOwnership()
        {
            try
            {
                var db = ProjectName.Core.Data.TerritoryDatabase.Instance;
                // 내 영지 — 동부(Ring1, 낮은 난이도)
                db.SetOwnership(new TerritoryId(NationType.East, 1), TerritoryOwnership.PlayerOwned);
                // 타 소속 영주 2명 — 서부(공격자, Cruel) / 남부(방어자)
                db.SetOwnership(new TerritoryId(NationType.West, 1), TerritoryOwnership.LordOwned);
                db.SetOwnership(new TerritoryId(NationType.South, 1), TerritoryOwnership.LordOwned);
                // 공격자(서부) 성향을 Cruel(aggression 0.90)로 — 리플렉션으로 private _definitions 교체 (공개 setter 없음)
                SetLordPersonality(new TerritoryId(NationType.West, 1), LordPersonality.Cruel);
                Debug.Log("[Test14LordsWar] ✅ 영지 소유 설정 완료 — 내:East_01(PlayerOwned) / 적:West_01·South_01(LordOwned), 서부=공격성 높음");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test14LordsWar] 영지 DB 설정 실패: {ex.Message}");
            }
        }

        /// <summary>런타임 영주 성격 교체 — TerritoryDatabase에 공개 setter가 없어 리플렉션 사용. 실패는 치명 아님.</summary>
        private void SetLordPersonality(TerritoryId id, LordPersonality personality)
        {
            try
            {
                var db = ProjectName.Core.Data.TerritoryDatabase.Instance;
                var dictField = typeof(ProjectName.Core.Data.TerritoryDatabase).GetField("_definitions",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dictField == null) { Debug.LogWarning("[Test14LordsWar] _definitions 필드 미발견 — 성격 기본 유지"); return; }
                var dict = (System.Collections.Generic.Dictionary<string, ProjectName.Core.Data.TerritoryDefinition>)dictField.GetValue(db);
                string key = id.ToString();
                if (!dict.TryGetValue(key, out var def))
                { Debug.LogWarning($"[Test14LordsWar] 영지 {key} 정의 미발견 — 성격 변경 생략"); return; }
                // TerritoryDefinition 및 LordInfo 모두 struct(값 타입) — LordInfo 사본을 고친 뒤
                // lord setter → TerritoryDefinition 재대입 순서로 반영해야 컴파일(C# struct getter 수정 금지)도 안전.
                var lordInfo = def.lord;
                lordInfo.personality = personality;
                def.lord = lordInfo;
                dict[key] = def;
                Debug.Log($"[Test14LordsWar] 영주 {key} 성격 → {personality} 설정");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Test14LordsWar] 영주 성격 교체 실패(계속): {ex.Message}");
            }
        }

        /// <summary>내 영지(East_01) 문 배치 — E키(4m)로 플레이어 성 실내 진입. + 아군 병사 2명 스폰.</summary>
        private void PlacePlayerTerritoryWithDoor()
        {
            try
            {
                // 내 성(문) 전면 좌표 — 플레이어(원점 근처)가 접근해 E키.
                Vector3 doorPos = new Vector3(18f, 0f, 40f);
                float doorY = SurfaceY(doorPos.x, doorPos.z);
                doorPos.y = doorY + 0.5f;

                // 내 성 건물 표현 (키오스크 큐브)
                var keep = GameObject.CreatePrimitive(PrimitiveType.Cube);
                keep.name = "LordsWarPlayerKeep";
                keep.transform.position = doorPos;
                keep.transform.localScale = new Vector3(5f, 5f, 5f);
                var keepR = keep.GetComponent<MeshRenderer>();
                if (keepR != null)
                    keepR.material = MaterialHelper.CreateLitMaterial(new Color(0.55f, 0.72f, 0.45f), "LordsWarPlayerKeep_Mat");

                // 문 BuildingTrigger (Castle + territoryKey=East_01) → 플레이어 소유라 PlayerCastleInteriorBuilder로 진입
                ProjectName.Systems.IndoorTransitionSetup.CreateBuildingTrigger(
                    doorPos, ProjectName.Systems.IndoorTransitionSetup.TYPE_CASTLE,
                    ProjectName.Systems.IndoorTransitionSetup.CASTLE_INTERACT_RANGE, keep.transform,
                    "Eastern", "East_01");
                Debug.Log($"[Test14LordsWar] 내 영지 성 문 배치 @ {doorPos} (E키 → 실내)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test14LordsWar] 내 영지 문 배치 실패: {ex.Message}");
            }

            // [50차 사용자 요구] 내 소속 병사 = **문지기 병사**로 선택 — 이 씬의 검증 목적
            //   "AI 영주가 내 영지로 실제 공격을 하러 오는가"에서 내 병사는 문지기(수비 격) 존재감.
            //   TerritoryGateSystem의 IsGatekeeper 소비 경로와 동일 플래그.
            SpawnGatekeeper("LordsWarPlayerGuard1", new Vector3(20f, 0f, 44f), "내 문지기 병사", 5, NationType.East,
                new Color(0.35f, 0.75f, 0.45f));
            SpawnGatekeeper("LordsWarPlayerGuard2", new Vector3(16f, 0f, 44f), "내 문지기 병사", 5, NationType.East,
                new Color(0.35f, 0.75f, 0.45f));
        }

        /// <summary>[50차→51차] 문지기 병사 — SpawnSoldier와 동일하되 IsGatekeeper=true + **SetHold(제자리 고정)**.
        ///   영지를 지키는 고정 수비 — 자동 추종/합세에서 제외(GuardCombatAI 51차 게이트), 근접 적만 제자리 반격.</summary>
        private GameObject SpawnGatekeeper(string goName, Vector3 pos, string guardName, int level, NationType nation, Color color)
        {
            var go = SpawnSoldier(goName, pos, guardName, level, nation, true, color);
            if (go != null)
            {
                var ph = go.GetComponent<GuardPlaceholder>();
                if (ph != null)
                {
                    ph.IsGatekeeper = true;
                    ph.SetHold(true);   // [51차] 나를 따라오지 않음 — 영지에 서서 지킴(근접 반격은 유지)
                    Debug.Log($"[Test14LordsWar] 🚪 문지기 병사 부여 — {goName} (IsGatekeeper=true + 홀드=제자리 고정)");
                }
            }
            return go;
        }

        /// <summary>타 소속 영주 2명 배치 + 영주 간 전쟁(점령) 트리거. West(Cruel)가 South를 물리로 침공.</summary>
        private void PlaceEnemyLordsAndWar()
        {
            // 서부 영주(공격자) 위치
            Vector3 westPos = new Vector3(-22f, 0f, -18f);
            Vector3 southPos = new Vector3(30f, 0f, -34f);
            float westY = SurfaceY(westPos.x, westPos.z);
            float southY = SurfaceY(southPos.x, southPos.z);

            // 영주 성 표현
            try
            {
                var westCastle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                westCastle.name = "LordsWarWestKeep";
                westCastle.transform.position = new Vector3(westPos.x, westY + 3f, westPos.z);
                westCastle.transform.localScale = new Vector3(5f, 6f, 5f);
                var wc = westCastle.GetComponent<MeshRenderer>();
                if (wc != null) wc.material = MaterialHelper.CreateLitMaterial(new Color(0.6f, 0.35f, 0.2f), "LordsWarWestKeep_Mat");

                var southCastle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                southCastle.name = "LordsWarSouthKeep";
                southCastle.transform.position = new Vector3(southPos.x, southY + 3f, southPos.z);
                southCastle.transform.localScale = new Vector3(5f, 6f, 5f);
                var sc = southCastle.GetComponent<MeshRenderer>();
                if (sc != null) sc.material = MaterialHelper.CreateLitMaterial(new Color(0.4f, 0.2f, 0.55f), "LordsWarSouthKeep_Mat");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test14LordsWar] 영주 성 표현 실패: {ex.Message}");
            }

            // 타 영주 병사 스폰 — [52차 사용자 재설계] **남부 = 공격자(즉시 출격) / 서부 = 수호자(제자리 방어)**.
            //   "AI 영주가 공격 명령을 내렸다고 가정" — 남부 영주의 공격 명령으로 남부 침공군이 남부 영지에서
            //   **즉시 출격**해 서부 영지로 행진하고, 서부 문지기는 서부 영지 방어선에서 **제자리 수호(홀드)**로 맞대응.
            //   도착 시 51차 국가 상이 교전 규칙(GuardPlaceholder.IsEnemyGuard)으로 상호 교전이 성립한다.
            var westGuards = new System.Collections.Generic.List<GameObject>();
            var westGuardColor = new Color(0.75f, 0.5f, 0.2f);
            for (int i = 0; i < 4; i++)
            {
                float gx = westPos.x - 2.25f + i * 1.5f;
                westGuards.Add(SpawnSoldier($"LordsWarWestGuard{i + 1}", new Vector3(gx, 0f, westPos.z + 2f),
                    "서부 문지기 병사", 8, NationType.West, false, westGuardColor));
            }
            var southGuards = new System.Collections.Generic.List<GameObject>();
            var southGuardColor = new Color(0.45f, 0.25f, 0.6f);
            for (int i = 0; i < 5; i++)
            {
                float gx = southPos.x - 3f + i * 1.5f;
                southGuards.Add(SpawnSoldier($"LordsWarSouthGuard{i + 1}", new Vector3(gx, 0f, southPos.z + 2f),
                    "남부 침공군", 8, NationType.South, false, southGuardColor));
            }
            // [52차] 서부 문지기 = 제자리 고정 수비(홀드) — 방어선에서 근접 반격만 수행(행진/추격 없음).
            int westHeld = 0;
            foreach (var wg in westGuards)
            {
                if (wg == null) continue;
                var wph = wg.GetComponent<GuardPlaceholder>();
                if (wph != null) { wph.SetHold(true); westHeld++; }
            }
            Debug.Log($"[Test14LordsWar] 🛡️ 서부 문지기 병사 {westHeld}명 — 제자리 수호(방어선 홀드)");

            // 영주 간 전쟁(점령) — [52차] 남부(공격자)가 서부(수호자)로 공격 행진.
            // 하드 SpawnGarrison으로 영지 전체 주둔군(70~80명)이 중복 스폰되는 WarMarchSimulation 대신,
            // 이미 배치한 남부 침공군 5명에게 직접 SetCommandTarget(서부 방어선, attack=true) 부여
            // → 씬 시작 즉시 출격(스폰과 명령 사이 지연 0) → Play에서 실제 물리 이동·도달 시 교전.
            try
            {
                // [52차] 행진 목표 = 서부 문지기 방어선 중앙(문지기들이 서 있는 z+2 라인) — 도착 즉시 교전이 눈에 보임.
                Vector3 westDefenseTarget = new Vector3(westPos.x, westY, westPos.z + 2f);
                int ordered = 0;
                foreach (var g in southGuards)
                {
                    if (g == null) continue;
                    CommandMarchToTarget(g, westDefenseTarget);
                    ordered++;
                }
                Debug.Log($"[Test14LordsWar] ⚔️ 남부 영주 공격 명령(가정): 침공군 {ordered}명 → 즉시 출격, 서부 문지기 방어선 행진(가시 교전)");

                // 영주 결정 체계 연동 로그 — AIWarSystem 사전조건(LordOwned · 국가 상이)도 함께 확인
                var db = ProjectName.Core.Data.TerritoryDatabase.Instance;
                var westDef = db.GetDefinition(new TerritoryId(NationType.West, 1));
                var southDef = db.GetDefinition(new TerritoryId(NationType.South, 1));
                float aggression = LordPersonalitySystem.GetAggression(new TerritoryId(NationType.South, 1));
                int attackPlan = LordPersonalitySystem.GetDeploymentPlan(new TerritoryId(NationType.South, 1)).attackSoldiers;
                Debug.Log($"[Test14LordsWar] 🔎 어질 판정 — 남부 공격성 {aggression:0.00} / 파견계획 {attackPlan}명 / 서부는 다른 국가 {southDef.nation != westDef.nation}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test14LordsWar] 영주 전쟁 행진 트리거 실패: {ex.Message}");
            }
        }

        /// <summary>작은 마을: 광장 표현 + 상점 NPC(클릭→상점) + 일반 주민 NPC(클릭→대화).</summary>
        private void PlaceVillageWithNpcs()
        {
            Vector3 villagePos = new Vector3(-40f, 0f, 34f);
            float vY = SurfaceY(villagePos.x, villagePos.z);

            // 광장 원판
            try
            {
                var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                plaza.name = "LordsWarVillagePlaza";
                plaza.transform.position = new Vector3(villagePos.x, vY + 0.06f, villagePos.z);
                plaza.transform.localScale = new Vector3(12f, 0.06f, 12f);
                plaza.tag = "Untagged";
                var pr = plaza.GetComponent<MeshRenderer>();
                if (pr != null) pr.material = MaterialHelper.CreateLitMaterial(new Color(0.52f, 0.5f, 0.47f), "LordsWarVillagePlaza_Mat");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test14LordsWar] 마을 광장 배치 실패: {ex.Message}");
            }

            // 상점 NPC — 좌클릭 → InteractionPanel "상점" → ShopWindowUTK
            SpawnVillageNpc("lords_war_shop", "잡화점 상인", true, "어서 오세요, 뭐 필요한가?",
                new Vector3(villagePos.x - 3f, vY, villagePos.z - 2f));
            // 일반 주민 NPC — 좌클릭 → InteractionPanel "대화하기" → NPCDialoguePanelUTK
            SpawnVillageNpc("lords_war_villager", "마을 주민 한백", false, "안녕하세요, 여행자님. 최근 서부 영주가 저지르는 일이 걱정돼요.",
                new Vector3(villagePos.x + 3f, vY, villagePos.z + 2f));
        }

        /// <summary>캡슐 + 콜라이더로 클릭 가능한 TerritoryNPCBehaviour(NPCInstance) 스폰 (Test_13 패턴 + 상점/일반 구분).
        /// [118차 후속] territoryId 전달 시 NPCInstance.TerritoryId 설정 — 세금제안 창이 영지 컨텍스트
        /// (세율/호감도/예상 징수액)를 해석하는 데 필수(115차 계약 "{nation}_{index:D2}").</summary>
        private void SpawnVillageNpc(string id, string name, bool isShop, string greeting, Vector3 pos, string territoryId = null)
        {
            try
            {
                float y = SurfaceY(pos.x, pos.z);
                GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                npc.name = name;
                npc.transform.position = new Vector3(pos.x, y + 1f, pos.z);
                npc.GetComponent<Renderer>().sharedMaterial.color = isShop ? new Color(0.85f, 0.66f, 0.25f) : new Color(0.6f, 0.5f, 0.4f);

                System.Type npcBehaviourType = System.Type.GetType("ProjectName.UI.TerritoryNPCBehaviour, ProjectName.UI");
                System.Type npcDataType = System.Type.GetType("ProjectName.UI.NPCInstance, ProjectName.UI");
                if (npcBehaviourType == null || npcDataType == null)
                {
                    Debug.LogError("[Test14LordsWar] NPC 타입 미발견 — TerritoryNPCBehaviour/NPCInstance");
                    return;
                }
                object npcData = System.Activator.CreateInstance(npcDataType);
                npcDataType.GetField("NpcId")?.SetValue(npcData, id);
                npcDataType.GetField("NpcName")?.SetValue(npcData, name);
                npcDataType.GetField("IsShopNPC")?.SetValue(npcData, isShop);
                npcDataType.GetField("Greeting")?.SetValue(npcData, greeting);
                if (!string.IsNullOrEmpty(territoryId))
                    npcDataType.GetField("TerritoryId")?.SetValue(npcData, territoryId);   // [118차 후속] 세금제안 영지 컨텍스트

                Component behaviour = npc.AddComponent(npcBehaviourType);
                npcBehaviourType.GetMethod("Initialize").Invoke(behaviour, new object[] { npcData });

                // [63차 사용자 요구 — NPC GLB] 캡슐 → 전용 NPC GLB 교체(성공 시). 실패/무렌더러 시 캡슐 유지(폴백).
                TryReplaceNpcVisualWithGlb(npc, npc.transform.position);

                // [116차 R4] 1.8m 캘리브레이션+애니 — VillageNpcSpawner 검증 블록 재사용(플레이어와 동일 키/Idle).
                VillageNpcSpawner.EnsureVillageNpcHeight(npc, SurfaceY(pos.x, pos.z), name);
                VillageNpcSpawner.EnsureVillageNpcAnimation(npc, isShop ? "shop_npc" : "villager", name);

                // [64차 P3] NPC 근접 상호작용 프롬프트(피그마 82:45 형식 — 베이크 PNG 패널 + 키캡 라벨) 등록.
                //   EInteractionPromptUTK(UI)는 리플렉션 등록(Systems→UI 참조 불가) — keycap-E가 기본이지만
                //   라벨에 F키 표기 → 실제 F키 상호작용(PlayerMovement [64차 P3])과 정합
                //   (패널 PNG의 keycap 이미지는 E 유지 — 라벨 텍스트가 F 안내).
                //   라벨 분기: 상점 / 영주(isShop=false && NpcId에 lord 포함) / 일반 주민.
                //   ⚠ "lords_war_*" 접두사도 "lord"를 포함하므로 단순 Contains("lord") 불가 —
                //     NpcId 끝 단어 기준("_lord" 포함)으로 영주만 판정한다(test17_lord ↔ lords_war_villager 구별).
                string idLowerForPrompt = (id ?? string.Empty).ToLowerInvariant();
                string promptLabel = isShop
                    ? "F키를 눌러 상점 이용"
                    : (idLowerForPrompt.Contains("_lord") ? "F키를 눌러 영주와 대화" : "F키를 눌러 대화");
                System.Type promptType = System.Type.GetType("ProjectName.UI.Toolkit.EInteractionPromptUTK, ProjectName.UI");
                if (promptType != null)
                {
                    var register = promptType.GetMethod("Register",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    register?.Invoke(null, new object[] { npc.transform, promptLabel, 3f });
                    Debug.Log($"[64차 P3] 근접 프롬프트 등록 — {name} label=\"{promptLabel}\" range=3f");
                }

                Debug.Log($"[Test14LordsWar] NPC 배치: {name} (IsShopNPC={isShop}) @ {pos}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test14LordsWar] NPC 배치 실패({name}): {ex.Message}");
            }
        }

        /// <summary>[63차 사용자 요구 — NPC GLB] 캡슐 NPC를 전용 NPC GLB로 교체.
        ///   매핑: IsShopNPC→npc_shop_rigged(NPC_Shop_Rigged), NpcId에 lord/king 포함→npc_lord_rigged(Npc_Lord_Rigged),
        ///   그 외 주민 = npc_man1_rigged/npc_girl1_rigged(정점 순환) — TerritoryNPCSpawner.GetNPCKey와 동일 규칙.
        ///   루트(캡슐 오브젝트)는 유지 — TerritoryNPCBehaviour/클릭 레이캐스트(BoxCollider) 보존, GLB 자식만 교체.
        ///   GLB 자식의 자체 콜라이더는 제거(루트 BoxCollider와 중복 — 클릭 판정 중복 방지).
        ///   높이 = TerritoryBuilder.SoldierTargetHeight(1.8m) bounds 정규화 + 발치 스냅(피벗 차이 무관).
        ///   실패 시 캡슐 유지(폴백 — 배치 절대 중단 금지).</summary>
        private void TryReplaceNpcVisualWithGlb(GameObject npcRoot, Vector3 groundPos)
        {
            try
            {
                // 키 매핑 — TerritoryNPCSpawner.GetNPCKey 규칙 동기(문자열 리플렉션: private이므로 로컬 재현)
                string key;
                string idLower = (npcRoot.name ?? string.Empty).ToLowerInvariant();
                bool isShop = false;
                var behaviour = npcRoot.GetComponent("TerritoryNPCBehaviour");
                if (behaviour != null)
                {
                    var npcDataField = behaviour.GetType().GetProperty("NPCData");
                    if (npcDataField != null)
                    {
                        var data = npcDataField.GetValue(behaviour);
                        if (data != null)
                        {
                            var shopField = data.GetType().GetField("IsShopNPC");
                            if (shopField != null) isShop = (bool)shopField.GetValue(data);
                            var idField = data.GetType().GetField("NpcId");
                            if (idField != null) idLower = (string)idField.GetValue(data) ?? string.Empty;
                        }
                    }
                }
                if (isShop) key = "npc_shop_rigged";
                else if (idLower.Contains("lord") || idLower.Contains("king")) key = "npc_lord_rigged";
                else key = (System.Math.Abs(npcRoot.GetInstanceID()) % 2 == 0)
                    ? "npc_man1_rigged" : "npc_girl1_rigged";

                if (!RuntimeModelLoader.TryGetModel(key, out var prefab) || prefab == null)
                {
                    Debug.LogWarning($"[Test17] NPC GLB 미로드 — {npcRoot.name} 캡슐 유지 (key={key})");
                    return;
                }

                var glb = Object.Instantiate(prefab, npcRoot.transform);
                glb.name = "NpcVisual";
                glb.transform.localPosition = Vector3.zero;
                glb.transform.localRotation = Quaternion.identity;

                // GLB 자식 콜라이더 제거 — 클릭 레이캐스트는 루트 BoxCollider 하나만(중복 판정 방지).
                var glbCols = glb.GetComponentsInChildren<Collider>(true);
                foreach (var c in glbCols) { if (c != null) Object.Destroy(c); }
                if (npcRoot.GetComponent<Collider>() == null)
                {
                    var col = npcRoot.AddComponent<BoxCollider>();
                    col.size = new Vector3(0.7f, 1.8f, 0.7f);
                    col.center = new Vector3(0f, 0.9f, 0f);
                }

                // 1.8m bounds 정규화 + 발치 스냅(TerritoryBuilder.NormalizeSoldierHeight — bounds.min.y 원점 복원 포함).
                TerritoryBuilder.NormalizeSoldierHeight(glb, TerritoryBuilder.SoldierTargetHeight);

                // 캡슐 비주얼 제거(렌더러 불필요 — GLB로 완전 교체). MeshFilter는 enabled 속성이 없어
                // sharedMesh=null로 비움. 루트는 유지(클릭 콜라이더/Behaviour).
                var capsuleRenderer = npcRoot.GetComponent<MeshRenderer>();
                if (capsuleRenderer != null) capsuleRenderer.enabled = false;
                var capsuleFilter = npcRoot.GetComponent<MeshFilter>();
                if (capsuleFilter != null) capsuleFilter.sharedMesh = null;

                Debug.Log($"[Test17] 🧍 NPC GLB 교체 — {npcRoot.name} ← {key} (1.8m 정규화, 클릭 레이캐스트 루트 유지)");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Test17] NPC GLB 교체 실패({npcRoot.name}) — 캡슐 유지: {ex.Message}");
            }
        }

        /// <summary>CreateGuard 헬퍼 재사용 — 병사 1명 스폰 (recruited=true=아군, false=적군). 생성된 병사 GameObject 반환(명령 부여용).</summary>
        private GameObject SpawnSoldier(string goName, Vector3 pos, string guardName, int level, NationType nation, bool recruited, Color color)
        {
            try
            {
                return CreateGuard(goName, pos, guardName, level, nation, recruited, color);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test14LordsWar] 병사 배치 실패({goName}): {ex.Message}");
                return null;
            }
        }

        /// <summary>[52차] 병사의 GuardPlaceholder에 목표 지점 공격 명령 부여 → Play에서 실제 물리 행진(공격 이동).
        ///   스폰 직후 즉시 호출 — "AI 영주가 공격 명령을 내렸다" 가정의 즉시 출격 재현.</summary>
        private void CommandMarchToTarget(GameObject guardGo, Vector3 target)
        {
            if (guardGo == null) return;
            try
            {
                var ph = guardGo.GetComponent<GuardPlaceholder>();
                if (ph == null)
                {
                    Debug.LogWarning("[Test14LordsWar] 명령 대상 병사에 GuardPlaceholder 없음 — 행진 생략");
                    return;
                }
                ph.SetCommandTarget(target, true); // IsAttackCommand=true → 목표로 걷고 도달 시 공격
                Debug.Log($"[Test14LordsWar] 🚩 침공군 명령 → 공격 목표 {target}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Test14LordsWar] 병사 공격 명령 부여 실패(계속): {ex.Message}");
            }
        }

        private void LogLordsWarSummary()
        {
            Debug.Log("[Test14LordsWar] 🧪 씬 구성 완료 — 확인 포인트: ①내 성 문(E)→실내 ②내 문지기 2명 = 제자리 고정(따라옴 없음) ③남부 침공군 5명이 남부 영지에서 즉시 출격 → 서부 문지기 방어선 행진 → 상호 교전(가시 전투) ④서부 문지기 4명 = 제자리 수호 ⑤마을 상점/주민 NPC 클릭 상호작용");
        }

        /// <summary>Test_13_Shop 전용 런타임 배치 — E키 상점, 클릭 상점 NPC, 테스트 골드.</summary>
        private void SetupShopTestScene()
        {
            GameObject player = null;
            try
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    Debug.LogWarning("[TestTerritoryCombat] 상점 테스트 배치 실패: Player를 찾을 수 없습니다.");
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TestTerritoryCombat] 상점 테스트 Player 탐색 실패: {ex.Message}");
                return;
            }

            try
            {
                PlayerStats stats = player.GetComponent<PlayerStats>();
                if (stats == null) stats = PlayerStats.Instance;
                if (stats != null)
                {
                    stats.AddGold(500, "shop_test_setup");
                    Debug.Log("[TestTerritoryCombat] 상점 테스트 골드 +500 보장");
                }
                else
                {
                    Debug.LogWarning("[TestTerritoryCombat] 상점 테스트 골드 지급 실패: PlayerStats를 찾을 수 없습니다.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TestTerritoryCombat] 상점 테스트 골드 지급 실패: {ex.Message}");
            }

            // 상점 상호작용 반경(3m) 안에서 E키/클릭을 바로 시험하도록 Ring1 동부 표면에 플레이어를 배치.
            try
            {
                player.transform.position = new Vector3(1450f, SurfaceY(1450f, 0f) + 1.02f, 0f);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TestTerritoryCombat] 상점 테스트 플레이어 배치 실패: {ex.Message}");
            }

            Vector3 playerPosition = player.transform.position;
            float kioskX = playerPosition.x + 2.5f;
            float npcX = playerPosition.x - 2.5f;
            float kioskY = SurfaceY(kioskX, playerPosition.z);
            float npcY = SurfaceY(npcX, playerPosition.z);

            try
            {
                GameObject kiosk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                kiosk.name = "TestShopKiosk";
                kiosk.transform.position = new Vector3(kioskX, kioskY + 0.5f, playerPosition.z);
                kiosk.GetComponent<Renderer>().sharedMaterial.color = new Color(0.85f, 0.66f, 0.25f);

                System.Type shopType = System.Type.GetType("ProjectName.UI.ShopPlaceholder, ProjectName.UI");
                if (shopType == null)
                    Debug.LogError("[TestTerritoryCombat] E키 상점 생성 실패: ShopPlaceholder 타입을 찾을 수 없습니다.");
                else
                    kiosk.AddComponent(shopType);

                Debug.Log($"[TestTerritoryCombat] E키 상점 배치 완료: {kiosk.transform.position}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TestTerritoryCombat] E키 상점 배치 실패: {ex.Message}");
            }

            try
            {
                GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                npc.name = "TestShopNPC";
                npc.transform.position = new Vector3(npcX, npcY + 1f, playerPosition.z);
                npc.GetComponent<Renderer>().sharedMaterial.color = new Color(0.85f, 0.66f, 0.25f);

                System.Type npcBehaviourType = System.Type.GetType("ProjectName.UI.TerritoryNPCBehaviour, ProjectName.UI");
                System.Type npcDataType = System.Type.GetType("ProjectName.UI.NPCInstance, ProjectName.UI");
                if (npcBehaviourType == null || npcDataType == null)
                {
                    Debug.LogError("[TestTerritoryCombat] 상점 NPC 생성 실패: TerritoryNPCBehaviour/NPCInstance 타입을 찾을 수 없습니다.");
                }
                else
                {
                    object npcData = System.Activator.CreateInstance(npcDataType);
                    npcDataType.GetField("NpcId")?.SetValue(npcData, "test_shopkeeper");
                    npcDataType.GetField("NpcName")?.SetValue(npcData, "상인 상점NPC");
                    npcDataType.GetField("IsShopNPC")?.SetValue(npcData, true);

                    Component behaviour = npc.AddComponent(npcBehaviourType);
                    System.Reflection.MethodInfo initialize = npcBehaviourType.GetMethod("Initialize");
                    if (initialize == null)
                        Debug.LogError("[TestTerritoryCombat] 상점 NPC 초기화 실패: Initialize 메서드를 찾을 수 없습니다.");
                    else
                        initialize.Invoke(behaviour, new object[] { npcData });

                    Debug.Log($"[TestTerritoryCombat] 상점 NPC 배치 완료: {npc.transform.position} (IsShopNPC=true)");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TestTerritoryCombat] 상점 NPC 배치 실패: {ex.Message}");
            }
        }

        // ================================================================
        // 매니저
        // ================================================================
        private void EnsureGameManager()
        {
            // [2026-09-15(59차 후속)] early-return 제거 — GameManager가 이미 존재해도
            // EquipmentManager/ArmorVisualAttachSystem은 항상 보장해야 한다.
            // 기존: if (GameManager.Instance != null) return; → 장비 시스템 생성이 스킵되어
            // 재실행/재스폰 상황에서 '드래그 장착 실패(사유: EquipmentManager 없음)'가 지속됐다.
            GameObject gm = GameManager.Instance != null ? GameManager.Instance.gameObject : new GameObject("GameManager");

            if (GameManager.Instance == null)
            {
                gm.AddComponent<GameManager>();
                gm.AddComponent<BuffManager>();
                gm.AddComponent<MonsterLevelManager>();
                gm.AddComponent<MonsterAggroSystem>();
                gm.AddComponent<MonsterSkillSystem>();
            }
            if (Object.FindAnyObjectByType<GuardHostilitySystem>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<GuardHostilitySystem>();   // [57차 후속] 타 영지 병사 적대화 — Instance 보장(중복 가드)
            }

            // [T4 2026-09-15] Test_10(영역 전투 씬)은 CoreSystemsBootstrap 미실행이므로
            // 장비 슬롯(방어구 장착)과 방어구 비주얼 부착 시스템을 이 씬에서 직접 생성 보장.
            // 54차 '장비 착용' 수정은 메인 씬(CoreSystemsBootstrap)만 커버했고, 테스트 씬엔
            // EquipmentManager가 없어 '우클릭 장착 wood_armor → 실패(사유: EquipmentManager 없음)'가 났다.
            // [59차 후속] GameManager 존재 여부와 무관하게 항상 보장(중복 가드 포함) — gm이 새로 생성된
            // 경우엔 이미 위에 없으므로, 여기서 일괄 보장. 중복 AddComponent는 Unity가 방지하나 가드 유지.
            if (Object.FindAnyObjectByType<EquipmentManager>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<EquipmentManager>();
                Debug.Log("[TestTerritoryCombat] ✅ EquipmentManager 생성 — 방어구 장착 활성화");
            }
            if (Object.FindAnyObjectByType<ArmorVisualAttachSystem>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<ArmorVisualAttachSystem>();
                Debug.Log("[TestTerritoryCombat] ✅ ArmorVisualAttachSystem 생성 — 방어구 비주얼 부착 활성화");
            }
            // [2026-09-17] 병사 방어구 비주얼 부착 (분리/ADDITIVE — 플레이어 시스템과 무관)
            if (Object.FindAnyObjectByType<GuardVisualAttachSystem>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<GuardVisualAttachSystem>();
                Debug.Log("[TestTerritoryCombat] ✅ GuardVisualAttachSystem 생성 — 병사 방어구 비주얼 부착 활성화");
            }
            // [2026-09-17] RTS 인터랙션 커서/호버/명령 + 농사/채집 (ADDITIVE)
            ContextCommandRouter.Ensure(gm);       // 커서 시스템(OS커서 Ctrl표시·컸텍스트 아이콘)+좌클릭 병사명령 자가확보
            FarmingSystem.Ensure(gm);              // 경지(농사) — 약초 파종→성장→수확
            GatheringSystem.Ensure(gm);            // 채집(풀) — 약초 리스폰
            GuardTaskSystem.Ensure(gm);            // 병사 역할(사냥/공격동행/수비문지기/채집/농경) 자동 수행
            // [59차 후속] 드래그 단체 지정 — Test_10은 CoreSystemsBootstrap 미실행이라
            // GuardSelectionManager(좌클릭 드래그 병사 선택)가 생성되지 않아 '내 병사 드래그 지정 모션'이 안 됐다.
            if (Object.FindAnyObjectByType<GuardSelectionManager>(FindObjectsInactive.Include) == null)
            {
                // [TEST27-68차] 전용 GO + DontDestroyOnLoad — 공유 GameManager GO가 중간에 파괴되면
                // GSM Update가 같이 죽어(67차 실측: [RTS] 로그 0건) 드래그가 완전히 무반응이 됐다.
                var ggo = new GameObject("GuardSelectionManager");
                DontDestroyOnLoad(ggo);
                ggo.AddComponent<GuardSelectionManager>();
                Debug.Log("[TestTerritoryCombat] ✅ GuardSelectionManager 생성(전용 GO — 자가치유 대상)");
            }
            if (Object.FindAnyObjectByType<RTSCommandSystem>(FindObjectsInactive.Include) == null)
            {
                var rgo = new GameObject("RTSCommandSystem");
                DontDestroyOnLoad(rgo);
                rgo.AddComponent<RTSCommandSystem>();
                Debug.Log("[TestTerritoryCombat] ✅ RTSCommandSystem 생성(전용 GO)");
            }
            // [TEST21-FOLLOWUP] 화살 발사 시스템 — Test_10은 CoreSystemsBootstrap 미실행이라 ArrowManager가
            // 생성되지 않아(Instance null) 활 공격이 스킵됐다. 여기서 보장.
            if (Object.FindAnyObjectByType<ArrowManager>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<ArrowManager>();
                Debug.Log("[TestTerritoryCombat] ✅ ArrowManager 생성 — 화살 발사 활성화");
            }

            Debug.Log("[TestTerritoryCombat] ✅ GameManager + 시스템 생성");
        }

        // ================================================================
        // 지형 수식 계약 (PlayerMovement.ClampToGroundByHeight와 동일 기준)
        // ================================================================
        /// <summary>
        /// GetHeightAt 수식 표면 y (1 + 높이). PlayerMovement가 플레이어/그림자에 대해
        /// 기대하는 바로 그 값 — 이 표면 위로 미니지형/스폰을 정렬해야 진동이 없다.
        /// </summary>
        private static float SurfaceY(float x, float z)
        {
            try
            {
                return 1f + ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    x, z, ProjectName.Core.Data.BiomeType.Plains, 42);
            }
            catch
            {
                return 1f;
            }
        }

        // ================================================================
        // 플레이어 (TestCombatSetup와 동일 구성)
        // ================================================================
        private void SetupPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }

            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
            }

            var pmType = typeof(ProjectName.Systems.PlayerMovement);
            if (player.GetComponent(pmType) == null)
                player.AddComponent(pmType);

            if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            }

            if (player.GetComponent<PlayerCombat>() == null)
                player.AddComponent<PlayerCombat>();
            if (player.GetComponent<PlayerHealth>() == null)
                player.AddComponent<PlayerHealth>();
            if (player.GetComponent<PlayerStats>() == null)
                player.AddComponent<PlayerStats>();
            if (player.GetComponent<PlayerPlaceholder>() == null)
                player.AddComponent<PlayerPlaceholder>();
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            EnsureStealthSystem();   // [211차] 은신 시스템 부트 보장 — Test_10 C키 은신 무음(씬 StealthSystem 0건) 수리

            // PlayerMovement.Awake의 (728, …) 스폰 오버라이드를 여기서 이긴다(실행 순서 의존 유지).
            // 캡슐 중심 = 표면 + 1 + 여유 → ClampToGroundByHeight 텔레포트 트리거 없음.
            player.transform.position = new Vector3(0f, SurfaceY(0f, 0f) + 1.02f, 0f);

            // 사거리 링은 P24에서 완전 제거 (PlayerRangeRing/WeaponRangeIndicator 폐기)

            // [TEST25-66차] 데모 자동장착 — 시작 즉시 방어구 풀셋(wood) + 창이 보이도록.
            // 뿌리(실측): Awake 중 즉시 장착하면 ArmorVisualAttachSystem.AttachRoutine 코루틴이 부팅 프레임에서
            //   첫 yield 후 재개되지 않고 침묵 사망("부착 시작" 로그 후 성공/실패 로그 전무 — Editor.log 실측).
            //   → 장착 이벤트를 부트 완료 후(0.25s)로 지연해 모든 시스템 기동 뒤에 발화. ArmorVisualAttachSystem
            //   쪽에도 동기 즉시 부착 경로를 추가해 이중 방어(패치 66-#3).
            StartCoroutine(EquipDemoStarterGearDelayed());
            Debug.Log($"[TestTerritoryCombat] ✅ Player 설정 완료 (pos={player.transform.position}, 표면 y={SurfaceY(0f, 0f):F2})");
            // [패링D2] 패링 훈련 안내 — Test_10 패링 체인(텔레그래프→TakeDamage→TryParryReflection→FX) 사용자 검증 경로 명시.
            //   체인 자체는 95차 F1~F5+159차 P3로 구현 완료(PlayerCombat/AnimalAI/PlayerHealth 소스 실측) — 발화 조건 안내만 추가.
            Debug.Log("[TestTerritoryCombat] 🛡️ 패링 훈련 안내 — ①V 입력 시 청색 소형 링=창 개방(0.45s) 확인 ②몬스터(-5,0,8 근방)에 선제 타격으로 어그로 ③추격 후 적색 텔레그래프 링(0.5s)에 맞춰 V ④성공 시 PERFECT(금색 2겹 링+히트스톱+반격 ×1.5)/GOOD(청색 1겹) [패링D2]");
        }

        /// <summary>
        /// [211차] StealthSystem 부트 보장 — Instance가 없으면 Player에 부착한다.
        /// 근거: Test_10 씬 StealthSystem 컴포넌트 0건(guid 9a0a2d1e53c8dfd46b3c27a40c9964ef) →
        ///   C키/은신 물약 모두 무반응(소비처 PlayerMovement.cs:481-495 C키→ToggleStealth,
        ///   GuardPlaceholder.cs:798 IsStealthed, HumanoidClipDriver.cs:986, NPCAwarenessSystem.cs:430).
        ///   TestAllInOneSetup.cs:566-581 EnsureStealthSystem 패턴 복제(Test_09 동일 증상의 작동 선례,
        ///   플레이어 생성 직후 호출). StealthSystem.Awake가 Instance를 할당하므로 AddComponent만으로 부트.
        /// StealthAssassination은 부착하지 않음(최소 변경): TestAllInOneSetup도 미부착 + 씬/프리팹
        ///   어디에도 배치 0건(guid 57ec07b331a307048bce088000342994) — GuardPlaceholder.cs:794는
        ///   Instance 부재 시 무음 스킵이라 부재 무해. 암살은 별도 과제로 검증.
        /// </summary>
        private void EnsureStealthSystem()
        {
            if (StealthSystem.Instance != null)
            {
                Debug.Log("[TestTerritoryCombat] ℹ️ StealthSystem 이미 존재 — 부트 스킵");
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[TestTerritoryCombat] ⚠️ Player 없음 — StealthSystem 부트 스킵");
                return;
            }

            player.AddComponent<StealthSystem>();
            Debug.Log("[TestTerritoryCombat] ✅ StealthSystem 부착 (C키 은신 토글 활성 — 비네트/속도감/반투명 피드)");
        }

        // [TEST25-66차] 부팅 레이스 흡수 — Awake 완료 후 0.25s 뒤 장착(위 주석 참조).
        System.Collections.IEnumerator EquipDemoStarterGearDelayed()
        {
            yield return new WaitForSeconds(0.25f);
            Debug.Log("[TestTerritoryCombat] 🛡️ 데모 자동장착 시작(부트 지연 해제 — 시스템 기동 완료 후)");
            EquipDemoStarterGear();
        }

        // [2026-09-16] 테스트씬 부팅용 시작 장비 데모 — wood 방어구 풀셋 + 창을 인벤 보장 후 즉시 장착.
        // 모든 매니저는 lazy/Get() 확보, 각 단계 예외 격리(실패해도 크래시 금지 — 데모 목적).
        private void EquipDemoStarterGear()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[TestTerritoryCombat] ⚠️ 데모 장착 스킵 — Player 없음");
                return;
            }

            // ① 방어구 아이템 인벤 보장(멱등) + 슬롯별 장착
            var em = ProjectName.Systems.EquipmentManager.Get();
            if (PlayerInventory.Instance != null && em != null)
            {
                // (id, 대상 슬롯) — ArmorVisualAttachSystem.ResolveBones 슬롯 규칙과 정합.
                var gear = new (string id, ProjectName.Systems.EquipmentManager.EquipmentSlot slot)[]
                {
                    ("wood_helmet", ProjectName.Systems.EquipmentManager.EquipmentSlot.Helmet),
                    ("wood_armor",  ProjectName.Systems.EquipmentManager.EquipmentSlot.Armor),
                    ("wood_boot",  ProjectName.Systems.EquipmentManager.EquipmentSlot.Shoes),      // [TEST28-69차] 좌우 분리 → 통합(양발 자동)
                    ("wood_glove", ProjectName.Systems.EquipmentManager.EquipmentSlot.Gloves),     // [TEST28-69차] 통합(양손 자동)
                    ("wood_shield", ProjectName.Systems.EquipmentManager.EquipmentSlot.Back),
                };

                foreach (var (id, slot) in gear)
                {
                    try
                    {
                        if (!PlayerInventory.Instance.HasItem(id))
                        {
                            var itemData = PlayerInventory.GetItemById(id);
                            if (itemData == null)
                            {
                                Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 데모 장착 스킵 — ItemData 미정의: {id}");
                                continue;
                            }
                            PlayerInventory.Instance.AddItem(itemData, 1);
                        }
                        // 해당 id 슬롯을 찾아 EquipmentManager.EquipItem(인벤 소모 + OnEquipmentChanged 발화).
                        PlayerInventory.ItemSlot invSlot = null;
                        var all = PlayerInventory.Instance.GetAllSlots();
                        if (all != null)
                        {
                            foreach (var s in all)
                            {
                                if (s != null && s.item != null && s.item.id == id)
                                { invSlot = s; break; }
                            }
                        }
                        if (invSlot == null)
                        {
                            Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 데모 장착 스킵 — 인벤 슬롯 미발견: {id}");
                            continue;
                        }
                        if (em.EquipItem(invSlot, slot))
                            Debug.Log($"[TestTerritoryCombat] 🛡️ 자동장착: {id} → {slot}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 데모 장착 예외({id}) — 계속: {e.Message}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[TestTerritoryCombat] ⚠️ 데모 방어구 장착 스킵 — PlayerInventory/EquipmentManager 없음");
            }

            // ② 창 시작 무기 — 시작 즉시 그립(전방 수리) 표시.
            try
            {
                ProjectName.Systems.WeaponEquipManager.Equip("weapon_spear_wood", player.transform, ProjectName.Core.WeaponType.Spear);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 데모 창 장착 예외 — 계속: {e.Message}");
            }

            Debug.Log("[TestTerritoryCombat] ✅ 데모 무기/방어구 자동장착 완료");
        }

        // ================================================================
        // 카메라 / 지면 / 조명 / 이벤트
        // ================================================================
        private void SetupCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                // [P206-P5] 재생성 방지 — 활성 태그 카메라가 없더라도 씬에 존재하는(비활성 포함) 카메라를
                // 재사용한다. 원점에 별개 "Main Camera"를 새로 만들면 기존 씬 카메라와 태그가 중복되어
                // Camera.main이 원점 매몰 카메라를 반환하는 조준 180° 반대 오류([BowAim] 88건 실측)의 근원.
                Camera existingCam = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
                if (existingCam != null)
                {
                    camGO = existingCam.gameObject;
                    camGO.tag = "MainCamera";
                    Debug.LogWarning($"[TestTerritoryCombat] 기존 카메라 '{camGO.name}' 재사용 — 원점 중복 Main Camera 생성 방지");
                }
                else
                {
                    camGO = new GameObject("Main Camera");
                    camGO.tag = "MainCamera";
                    // [207차 P1] 생성 위치 명시 — 원점(0,0,0) 생성임을 로그로 남긴다(로직 무변경 —
                    // 테스트 213건 의존). 원점 생성 카메라가 세션 잔존 시 조준을 원점 고정시킨 P0 실측
                    // (camId -2578, rayO=(0.03,-0.08,0.06)) 재발 판별용 좌표.
                    Debug.LogWarning($"[TestTerritoryCombat] 씬에 카메라 부재 — 원점에 새 Main Camera 생성 (생성 위치=({camGO.transform.position}))");
                }
            }

            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null)
                cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;

            var tdcType = typeof(ProjectName.Systems.TopDownCameraController);
            if (camGO.GetComponent(tdcType) == null)
                camGO.AddComponent(tdcType);
            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            Debug.Log("[TestTerritoryCombat] ✅ 카메라 설정 완료");
        }

        private void SetupGround()
        {
            if (GameObject.Find("Ground") != null) return;

            // Plane 프리미티브(표면 y=-0.5) 대신 GetHeightAt 계약 준수 미니지형.
            // 정점 y = SurfaceY(x, z) = 1 + GetHeightAt(x, z, Plains, 42) →
            // 스폰·ClampToGroundByHeight·BlobShadow가 기대하는 표면과 정확히 일치.
            const int halfSize = 60;   // 반경 ±60m
            const int step = 1;        // 정점 간격 1m
            int vertCount = halfSize * 2 / step + 1; // 121 → 14641 정점

            var vertices = new Vector3[vertCount * vertCount];
            var triangles = new int[(vertCount - 1) * (vertCount - 1) * 6];

            int vi = 0;
            for (int zi = 0; zi < vertCount; zi++)
            {
                for (int xi = 0; xi < vertCount; xi++)
                {
                    float x = -halfSize + xi * step;
                    float z = -halfSize + zi * step;
                    vertices[vi++] = new Vector3(x, SurfaceY(x, z), z);
                }
            }

            // 쿼드당 삼각형 2개 (아래→위 인덱스, 위쪽 면 시계방향 유지)
            int ti = 0;
            for (int zi = 0; zi < vertCount - 1; zi++)
            {
                for (int xi = 0; xi < vertCount - 1; xi++)
                {
                    int tl = zi * vertCount + xi;      // 좌하
                    int tr = tl + 1;                   // 우하
                    int bl = tl + vertCount;           // 좌상
                    int br = bl + 1;                   // 우상
                    triangles[ti++] = tl;
                    triangles[ti++] = bl;
                    triangles[ti++] = tr;
                    triangles[ti++] = tr;
                    triangles[ti++] = bl;
                    triangles[ti++] = br;
                }
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var ground = new GameObject("Ground");
            ground.transform.position = Vector3.zero; // 메시는 월드 좌표 기준

            var meshFilter = ground.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = ground.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
            renderer.material = mat;

            var meshCollider = ground.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;

            Debug.Log($"[TestTerritoryCombat] ✅ 미니지형 생성 (계약: GetHeightAt+1, 정점 {vertCount}x{vertCount}={vertices.Length}, 원점 표면 y={SurfaceY(0f, 0f):F2})");
        }

        private void SetupLight()
        {
            if (FindAnyObjectByType<Light>() != null) return;

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            Debug.Log("[TestTerritoryCombat] ✅ Directional Light 생성");
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[TestTerritoryCombat] ✅ EventSystem 생성");
        }

        // ================================================================
        // 타영주 영지 (DraculaLord — IDamageable)
        // ================================================================
        private void SpawnLord()
        {
            GameObject lordGO = new GameObject("TerritoryLord");
            lordGO.transform.position = _lordPos;
            lordGO.tag = "DraculaLord";

            var lord = lordGO.AddComponent<DraculaLord>();
            lord.SetTerritoryId(new TerritoryId(NationType.Dracula, 1));

            // Raycast 히트용 Collider (IDamageable 대상)
            if (lordGO.GetComponent<Collider>() == null)
            {
                var col = lordGO.AddComponent<BoxCollider>();
                col.size = new Vector3(2, 3, 2);
            }
            if (lordGO.GetComponent<Rigidbody>() == null)
            {
                var rb = lordGO.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 4f;
            }
            if (lordGO.GetComponent<HitReaction>() == null)
                lordGO.AddComponent<HitReaction>();

            Debug.Log($"[TestTerritoryCombat] ✅ 타영주 영지 생성 ({_lordPos}, HP:{lord.MaxHP}, ATK:{lord.AttackDamage})");
        }

        // ================================================================
        // 병사 (GuardPlaceholder — IDamageable)
        // ================================================================
        private void SpawnGuard()
        {
            GameObject guardGO = new GameObject("Guard_0");
            guardGO.transform.position = _guardPos;
            guardGO.tag = "Guard";

            var guard = guardGO.AddComponent<GuardPlaceholder>();

            // Raycast 히트용 Collider
            if (guardGO.GetComponent<Collider>() == null)
            {
                var col = guardGO.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 1.8f, 0.6f);
            }
            if (guardGO.GetComponent<Rigidbody>() == null)
            {
                var rb = guardGO.AddComponent<Rigidbody>();
                rb.useGravity = false;   // 2026-09-15 접지: 병사는 명령 기반 이동 — 중력 off로 지면 고정(TestTerritory CreateGuard와 동일)
                rb.isKinematic = true;
                rb.mass = 1f;
            }
            if (guardGO.GetComponent<HitReaction>() == null)
                guardGO.AddComponent<HitReaction>();

            // [TEST27-68차] 병사 헤드 UI — 이름 + Lv + HP바 (몬스터 MonsterHeadUI 대응)
            if (guardGO.GetComponent<GuardHeadUI>() == null)
                guardGO.AddComponent<GuardHeadUI>();

            // 시각 바디 (Collider 제거 — 물리 간섭 방지)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Guard_Visual_0";
            visual.transform.SetParent(guardGO.transform, false);
            visual.transform.localPosition = new Vector3(0, 1f, 0);
            visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.6f, 0.2f, 1f);
                renderer.material = mat;
            }
            var visCol = visual.GetComponent<Collider>();
            if (visCol != null)
                DestroyImmediate(visCol);

            Debug.Log("[TestTerritoryCombat] ✅ 병사 생성");
        }

        // ================================================================
        // 몬스터 1 (TestCombatSetup 패턴)
        // ================================================================
        private void SpawnMonster(string monsterId)
        {
            GameObject go = null;

            MonsterDef def = MonsterDatabase.Get(monsterId);
            if (def == null)
            {
                Debug.LogWarning($"[TestTerritoryCombat] MonsterDef '{monsterId}' 없음. 프리미티브 생성");
                def = new MonsterDef(monsterId, monsterId, MonsterTier.Intermediate, 20f, 5, 4f, Color.white);
            }

            string modelPath = GetMonsterModelPath(monsterId);
            if (!string.IsNullOrEmpty(modelPath))
            {
                GameObject modelPrefab = Resources.Load<GameObject>(modelPath);
                if (modelPrefab != null)
                {
                    go = Instantiate(modelPrefab, _monsterPos, Quaternion.identity);
                    // [2026-09-11] bounds 기반 접지 — GLB 피벗 오프셋과 무관하게 발끝이 _monsterPos.y
                    // (기존 SurfaceY 계약)에 정렬. 기존 접지 보정 코드 없음 → 신규 적용(중복 아님).
                    GroundModelToY(go, _monsterPos.y);
                }
            }

            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.position = _monsterPos;
                go.transform.localScale = Vector3.one * 1.5f;
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = Color.yellow;
                    r.material = mat;
                }
            }

            go.name = $"Monster_{monsterId}";
            go.tag = "Monster";

            AnimalAI ai = go.GetComponent<AnimalAI>();
            if (ai == null)
                ai = go.AddComponent<AnimalAI>();
            ai.SetMonsterId(monsterId);

            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                // 2026-09-11: glTFast GLB 프리팹은 메시 콜라이더를 전혀 생성하지 않는다 →
                // 이 BoxCollider가 몬스터의 '유일한' 레이캐스트 히트 볼륨. GLB 시각 몸체(토끼/늑대)는
                // 1.5박스(중심 0,0,0)보다 크고 위로 솟아 있어 커서가 몸통 위에 있어도 레이가 완전히 빗나갔다.
                // 2×2×2 + center y 0.75로 확대해 시각 몸체 대부분을 커버 (기존 1→1.5 확대 선례 연장).
                col.size = new Vector3(2.0f, 2.0f, 2.0f);
                col.center = new Vector3(0f, 0.75f, 0f);
                Debug.Log($"[TestTerritoryCombat] 📦 {go.name} 히트용 BoxCollider 추가 (2x2x2, center.y=0.75) — GLB 메시 콜라이더 부재 대비");
            }
            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 1f;
            }
            // 2026-09-10: 슬라임 비행 차단 — GLB 프리팹에 자동부착된 절차애니 컴포넌트(ModelAnimatorAssigner 계열)가
            // RequireComponent(Rigidbody)로 비관성 rb를 남기고, HitReaction 넉백(AddForce Impulse)+HighSpec 절트(홉 y+0.4, 2배)가
            // 중첩되면 몬스터가 공중으로 날아가 파란 스카이박스 위로 이동하는 증상이 발생했다.
            // Test_10은 공격 판정 점검용이므로 몬스터 rb를 완전 관성화(중력 유지, 외력 무시)하고 절트도 차단한다.
            {
                var mrb = go.GetComponent<Rigidbody>();
                if (mrb != null)
                {
                    mrb.isKinematic = false;
                    mrb.useGravity = true;
                    mrb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ
                                    | RigidbodyConstraints.FreezeRotation;   // y만 물리(접지), 수평은 AnimalAI Transform 제어
                    mrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    mrb.linearDamping = 0f; mrb.angularDamping = 0.05f;
                }
                // 넉백/절트 차단은 HitReaction 확정 '이후'에 적용 — GLB 프리팹은 HitReaction 미부착이므로
                // 이전 순서(GetComponent→null→스킵 후 AddComponent)로는 신규 추가분에 스위치가
                // 빠져 비행 증상이 재발한다 (2026-09-10 QA 순서 버그 수정)
            }
            if (go.GetComponent<HitReaction>() == null)
                go.AddComponent<HitReaction>();
            go.GetComponent<HitReaction>()?.DisableKnockback();   // 넉백/절트 완전 오프 — 절차애니 몬스터는 HitReaction 넉백이 Transform을 유령처럼 이동시킴

            // Test_10 Phase 2 훅: 몬스터 헤드 UI(이름/Lv/HP바) 부착 — 그 외 로직 무변경
            if (ai != null)
            {
                var head = go.AddComponent<MonsterHeadUI>();
                head.Setup(ai);
            }
            else
            {
                Debug.LogWarning("[TestTerritoryCombat] ⚠️ AnimalAI 없음 — MonsterHeadUI 부착 생략");
            }

            Debug.Log($"[TestTerritoryCombat] ✅ 몬스터 생성 ({monsterId}, {_monsterPos})");
        }

        private string GetMonsterModelPath(string monsterId)
        {
            return monsterId switch
            {
                "rabbit" => "Models/UserProvided/Rabbit_Rigged",
                "wolf" => "Models/UserProvided/Wolf_Rigged",
                "slime" => "Models/UserProvided/Slime_Rigged",
                _ => null
            };
        }

        // ================================================================
        // 공격 시스템 (점검 대상 중심)
        // ================================================================
        private void AttachAttackSystem()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            // PlayerCombat owns player Mouse0 melee input and combo progression. Adding AttackSystem
            // here would create a second player click consumer (and a second direct-damage path).
            if (player.GetComponent<PlayerCombat>() != null)
            {
                Debug.Log("[TestTerritoryCombat] PlayerCombat owns player melee clicks; AttackSystem not attached.");
                return;
            }

            // Keep legacy direct-damage behavior only for a player without PlayerCombat.
            if (player.GetComponent<AttackSystem>() == null)
                player.AddComponent<AttackSystem>();

            Debug.Log("[TestTerritoryCombat] ✅ AttackSystem 부착 완료 — PlayerCombat 없음");
        }

        // ================================================================
        // OnGUI 안내 — [TEST28-69차 후속6] "좌클릭=공격" 점검 안내창 사용자 요청으로 제거
        // ================================================================

        // ================================================================
        // 내/적 소속 영지 배치 (2026-09-10 신규 — 기존 셋업 무변경, 신규 배치만 추가)
        // ================================================================
        /// <summary>
        /// [P21 경량판] 영지 데이터 등록 + 실내 진입 트리거만 배치.
        /// 성 시각/병사 3+3/문지기/몬스터 더미는 제거(렉 해소 — 사용자 확정: "테스트는 실내씬에서 직접").
        /// TerritoryDatabase 등록은 유지 — Castle 트리거의 PlayerOwned 판정(East_01)과
        /// 창고/크래프트 wh_test 등 영지 키 의존 시스템이 계속 동작해야 한다.
        /// </summary>
        /// <summary>
        /// [P21b 사용자 정정] 테스트10 = 병사 1명 + 몬스터 1마리만.
        /// 영지 시각/상호작용(E키 실내 트리거) 전면 제거 — 실내 작업은 IndoorScene을
        /// 에디터에서 직접 열어 진행. 영지 데이터 등록도 제거(상호작용 대상 없음).
        /// </summary>
        private void SetupTestDummies()
        {
            // [62차] Test_17_LordInteraction — 씬이 자체 배치(마을 NPC 2 + 영주 NPC 1 + 내 병사 3 + 적군 병사 3)를 소유.
            //   TestGuard_1(병사1)·슬라임(몬스터1) 더미 전부 배제 — 검증 대상 혼선 방지. 조기 리턴은 CreateGuard 이전에 둔다.
            bool isLordInteractionScene = gameObject.scene.name != null && gameObject.scene.name.Contains("LordInteraction");
            if (isLordInteractionScene)
            {
                Debug.Log("[TestDummies][62차] Test_17_LordInteraction — 병사/몬스터 더미 미배치(야외 마을 상호작용 전용 구성)");
                return;
            }

            // 병사 1명 — 플레이어 근처(기존 단병사 위치), 포섭 상태(F키 상호작용/부대 테스트용)
            float gx = _guardPos.x, gz = _guardPos.z;
            var guardPos = new Vector3(gx, SurfaceY(gx, gz) + 1.0f, gz);
            CreateGuard("TestGuard_1", guardPos, "병사1", 10, NationType.East,
                true, new Color(0.2f, 0.4f, 0.9f, 1f));

            // [50차 사용자 요구] Test_14_LordsWar는 영주 전쟁 테스트 씬 — 몬스터(슬라임) 불필요.
            //   해당 씬에서는 병사 1 + 몬스터 1 배치를 생략(내 소속 병사는 문지기 병사로 선택 — 아래 LordsWar 배선).
            bool isLordsWarScene = gameObject.scene.name != null && gameObject.scene.name.Contains("LordsWar");
            if (isLordsWarScene)
            {
                Debug.Log("[TestDummies][P21b→50차] Test_14_LordsWar — 병사/몬스터 더미 미배치(영주 전쟁 전용 구성: 문지기 병사+영주 침공군)");
                return;
            }

            // 몬스터 1마리 — 슬라임(전투/화살 명중 테스트용)
            SpawnMonster("slime");

            Debug.Log("[TestDummies][P21b] ✅ 병사 1명 + 몬스터 1마리 배치 완료 — 영지/실내 트리거 없음(IndoorScene 직접 작업)");
        }

        /// <summary>
        /// 내 소속 영지(PlayerOwned): 파란 성 1채 + 내 병사 3명(레벨 10, East, 파랑, 포섭됨).
        /// 뒤쪽(+z)에 배치해 SpawnLord(0,0,15)/SpawnGuard(3,0,12)와 겹치지 않는다.
        /// </summary>
        private void SetupMyTerritory()
        {
            TerritoryDatabase.Instance.SetOwnership(NationType.East, 1, TerritoryOwnership.PlayerOwned);

            // 성 (순수 시각 — Collider 제거, 큐브 피벗이 중심이므로 y = 표면 + 높이/2 로 바닥 정렬)
            float cx = _myTerritoryPos.x, cz = _myTerritoryPos.z;
            float baseY = SurfaceY(cx, cz);
            var castle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            castle.name = "Territory_My_PlayerOwned";
            castle.transform.position = new Vector3(cx, baseY + 4f, cz); // 높이 8 → 중심 +4, 바닥 = 수식 표면
            castle.transform.localScale = new Vector3(14f, 12f, 14f);   // 2026-09-10: 대형화 — 카메라(원점 추적)에서도 확실히 보이게
            var castleRenderer = castle.GetComponent<MeshRenderer>();
            if (castleRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.25f, 0.4f, 0.85f, 1f); // 내 진영 파랑
                castleRenderer.material = mat;
            }
            var castleCol = castle.GetComponent<Collider>();
            if (castleCol != null) DestroyImmediate(castleCol); // Raycast 히트 대상은 병사뿐

            // 내 병사 3명 — 성 전면(-z)에 3m 간격
            float frontZ = cz - 6f;
            for (int i = 0; i < 3; i++)
            {
                float gx = cx - 3f + i * 3f;
                Vector3 pos = new Vector3(gx, SurfaceY(gx, frontZ) + 1.0f, frontZ);
                CreateGuard($"MyGuard_{i}", pos, $"내병사{i + 1}", 10, NationType.East,
                    true, new Color(0.2f, 0.4f, 0.9f, 1f));
            }

            // ═══ [P7] 실내 진입 트리거 — 성 입구(전면) E키 → PlayerCastle 실내(침대 세이브 포함) ═══
            // Test_10_TerritoryCombat 씬 전용: 가벼운 씬에서 실내 전환(E키) 검증용.
            // isPlayerOwned는 BuildingTrigger가 E키 시점에 TerritoryDatabase(East_01=PlayerOwned)에서 자동 판정.
            var entryTrigger = IndoorTransitionSetup.CreateBuildingTrigger(
                new Vector3(cx, baseY + 1.5f, cz - 7.5f),   // 성 전면 입구 (14×14 성, 중심 cz → 전면 -7.5)
                IndoorTransitionSetup.TYPE_CASTLE,
                IndoorTransitionSetup.CASTLE_INTERACT_RANGE, // 4m
                null,                                        // 부모 없음 — 씬 루트 (Setup 오브젝트 정리와 무관)
                "Eastern",                                   // nationStyle — 동국 실내 텍스처
                "East_01");                                  // 영지 키 — PlayerOwned 판정용
            if (entryTrigger != null)
                Debug.Log("[MyTerritory] 🚪 성 입구 트리거 배치 완료 — 전면 4m 내 E키로 실내 진입 (PlayerCastle)");

            // ═══ [P18] 크래프트하우스 진입 트리거 — 성 동측 E키 → CraftHouse 실내(제작/요리/연금 스테이션 3종) ═══
            // HQ 실내 텍스처(IndoorMaterialFactory)는 OnIndoorSceneLoaded 공통 경유라 자동 적용.
            var craftTrigger = IndoorTransitionSetup.CreateBuildingTrigger(
                new Vector3(cx + 9f, baseY + 1.5f, cz - 4f),   // 성 동측 (14×14 성 중심 +9m)
                IndoorTransitionSetup.TYPE_CRAFT_HOUSE,
                IndoorTransitionSetup.DEFAULT_INTERACT_RANGE,
                null,
                "Eastern",
                "East_01");
            if (craftTrigger != null)
                Debug.Log("[MyTerritory] 🔨 크래프트하우스 트리거 배치 완료 — 동측 3m 내 E키로 실내 진입 (무기/요리/물약 제작)");

            Debug.Log($"[MyTerritory] ✅ 내 소속 영지(PlayerOwned) 배치 — 성 'Territory_My_PlayerOwned' @({cx:F1}, {baseY:F1}, {cz:F1}), 내병사 3명(레벨 10, East, 파랑)");
        }

        /// <summary>
        /// 적 소속 영지(EnemyOwned 표기): 빨간 성 1채 + 적 문지기 3명(레벨 15, North, 빨강, 미포섭).
        /// 참고: TerritoryOwnership 열거형에는 EnemyOwned 값이 없어(Unoccupied/PlayerOwned/LordOwned/Contested)
        /// 소유권은 '적 AI 영주 소유'에 해당하는 LordOwned로 등록하고, GO 이름은 요구대로 EnemyOwned 표기를 유지한다.
        /// </summary>
        private void SetupEnemyTerritory()
        {
            TerritoryDatabase.Instance.SetOwnership(NationType.North, 1, TerritoryOwnership.LordOwned);

            // 성 (순수 시각 — Collider 제거, 바닥 정렬 동일 계약)
            float cx = _enemyTerritoryPos.x, cz = _enemyTerritoryPos.z;
            float baseY = SurfaceY(cx, cz);
            var castle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            castle.name = "Territory_Enemy_EnemyOwned";
            castle.transform.position = new Vector3(cx, baseY + 4f, cz);
            castle.transform.localScale = new Vector3(14f, 12f, 14f);   // 2026-09-10: 대형화 — 원점 카메라에서도 가시성 확보
            var castleRenderer = castle.GetComponent<MeshRenderer>();
            if (castleRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.85f, 0.3f, 0.3f, 1f); // 적 진영 빨강
                castleRenderer.material = mat;
            }
            var castleCol = castle.GetComponent<Collider>();
            if (castleCol != null) DestroyImmediate(castleCol);

            // 적 문지기 3명 — 성문(-z) 방향, 성에서 4~6m 앞
            float gateZ = cz - 10f;
            for (int i = 0; i < 3; i++)
            {
                float gx = cx - 3f + i * 3f;
                Vector3 pos = new Vector3(gx, SurfaceY(gx, gateZ) + 1.0f, gateZ);
                CreateGuard($"EnemyGateGuard_{i}", pos, $"적문지기{i + 1}", 15, NationType.North,
                    false, new Color(0.9f, 0.3f, 0.3f, 1f));
            }

            Debug.Log($"[EnemyTerritory] ✅ 적 소속 영지(EnemyOwned 표기) 배치 — 성 'Territory_Enemy_EnemyOwned' @({cx:F1}, {baseY:F1}, {cz:F1}), 적문지기 3명(레벨 15, North, 빨강)");
        }

        // ================================================================
        // 2026-09-10: 하트 HUD 부착 (Test_09 EnsurePlayerHUD 이식 — 리플렉션, 중복 방지)
        // ================================================================
        private System.Type _hudType;

        private void EnsurePlayerHUD()
        {
            try
            {
                if (_hudType == null)
                {
                    var uiAssembly = System.Reflection.Assembly.Load("ProjectName.UI");
                    _hudType = uiAssembly != null ? uiAssembly.GetType("ProjectName.UI.HUD") : null;
                }
                if (_hudType == null)
                {
                    Debug.LogWarning("[TestTerritoryCombat] ⚠️ ProjectName.UI.HUD 타입 미발견 — HUD 건너뜀");
                    return;
                }

                var existing = UnityEngine.Object.FindObjectsByType(_hudType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (existing != null && existing.Length > 0)
                {
                    Debug.Log("[TestTerritoryCombat] ℹ️ 이미 HUD 존재 — 건너뜀");
                    return;
                }

                var hudGO = new GameObject("HUD");
                hudGO.AddComponent(_hudType);
                Debug.Log("[TestTerritoryCombat] ✅ 하트 HUD 부착 (하트 아이콘 + 숫자HP)");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[TestTerritoryCombat] HUD 부착 실패(무시): {ex.Message}");
            }
        }

        // ================================================================
        // 병사 공용 생성 (SpawnGuard 패턴 그대로 — 이름/위치/레벨/국가/포섭/색만 파라미터화)
        // ================================================================
        private GameObject CreateGuard(string goName, Vector3 pos, string guardName, int level,
            NationType nation, bool recruited, Color color)
        {
            // [59차 후속] 병사 접지 — 루트를 지면(SurfaceY)에 직접 놓는다.
            // 기존: pos.y = SurfaceY+1.0(레이케스트용 박스 오프셋)을 루트로 쓰고, 모델만 GroundModelToY로
            // 지면에 내려 발이 땅에 닿게 했다. 이 구조는 StepToward(루트.y를 TryGetGroundY=지면으로 보정)가
            // 루트를 지면으로 내리면 모델은 상대 -1.0 이 되어 지면 아래로 파묻히는 모순을 낳았다('병사 땅으로 사라짐').
            // → 루트 자체를 지면에 두고, BoxCollider 히트 볼륨은 루트 기준 위쪽(+0.9)으로 조정한다.
            pos.y = SurfaceY(pos.x, pos.z);
            GameObject guardGO = new GameObject(goName);
            guardGO.transform.position = pos;
            guardGO.tag = "Guard";

            var guard = guardGO.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo(guardName, level, nation);
            guard.SetRecruited(recruited);

            // Raycast 히트용 Collider — [59차 후속] 루트가 지면으로 내려왔으므로 히트 볼륨을 위쪽(+0.9)으로 올려 몸통 커버
            if (guardGO.GetComponent<Collider>() == null)
            {
                var col = guardGO.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 1.8f, 0.6f);
                col.center = new Vector3(0f, 0.9f, 0f);
            }
            if (guardGO.GetComponent<Rigidbody>() == null)
            {
                var rb = guardGO.AddComponent<Rigidbody>();
                // 2026-09-15 접지 수정: useGravity=true면 모델 부착 후에도 중력이 발끝을 지면 밑으로
                // 가라앉히거나(GroundModelToY로 맞춘 pos.y가 매 프레임 깨짐) 떠 있는 상태를 유지한다.
                // 내/적 병사 이동은 GuardPlaceholder.StepToward가 명령 기반 transform(MovePosition 또는
                // transform.position)으로 수행하므로 중력이 불필요 → 중력 off + isKinematic으로 지면 고정.
                rb.useGravity = false;    // 중력 off — 명령 외력으로 y를 흔들지 않음(접지 안정화)
                rb.isKinematic = true;   // kinematic → StepToward가 transform.position 직접 이동 채택
                rb.mass = 1f;
            }
            if (guardGO.GetComponent<HitReaction>() == null)
                guardGO.AddComponent<HitReaction>();

            // [TEST27-68차] 병사 헤드 UI — 이름 + Lv + HP바 (몬스터 MonsterHeadUI 대응)
            if (guardGO.GetComponent<GuardHeadUI>() == null)
                guardGO.AddComponent<GuardHeadUI>();

            // 시각 바디 (Collider 제거 — 물리 간섭 방지)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = $"{goName}_Visual";
            visual.transform.SetParent(guardGO.transform, false);
            visual.transform.localPosition = new Vector3(0, 1f, 0);
            visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                renderer.material = mat;
            }
            var visCol = visual.GetComponent<Collider>();
            if (visCol != null)
                DestroyImmediate(visCol);

            // 2026-09-10/09-15: 병사 비주얼 — [TEST25-66차] FBX 우선 + GLB 재질 이식.
            // 뿌리(실측): Soldier_*.glb는 임베디드 애니메이션 0개 + 뼈대 중첩(metarig/Root/spine/spine.001/...),
            //   반면 Soldier_*.anim 클립 바인딩은 flat 단일 경로(foot.L, shin.L, shoulder.L, Root 등)라
            //   Unity 제너릭 바인딩이 GLB 중첩 경로와 불일치 → GLB 몸통은 무애니(T포즈).
            //   FBX(fbx/soldier_lv*_rigged)는 flat 계층이라 클립 경로가 일치 → 애니 재생(59차 Editor.log 실측).
            //   → 몸통은 FBX(애니 보장), GLB는 재질 소스(CopyMaterialsFromGlb)로만 사용 — 59차 검증 조합 복원.
            {
                // ① FBX 우선 — SoldierShield_AC + HumanoidClipDriver(Soldier) + GLB 재질 이식.
                string fbxKey = level >= 40
                    ? "Models/UserProvided/fbx/soldier_lv40-50_rigged"
                    : level >= 20 ? "Models/UserProvided/fbx/soldier_lv20-40_rigged"
                    : "Models/UserProvided/fbx/soldier_lv1-20_rigged";
                string matGlbKey = level >= 40
                    ? "Models/UserProvided/Soldier_Lv40-50_Rigged"
                    : level >= 20 ? "Models/UserProvided/Soldier_Lv20-40_Rigged"
                    : "Models/UserProvided/Soldier_Lv1-20_Rigged";
                var fbxPrefab = Resources.Load<GameObject>(fbxKey);
                GameObject attachedBody = null;

                if (fbxPrefab != null)
                {
                    var soldier = Instantiate(fbxPrefab, guardGO.transform);
                    soldier.name = $"{goName}_Body";
                    soldier.transform.localPosition = Vector3.zero;
                    soldier.transform.localRotation = Quaternion.identity;
                    soldier.transform.localScale = Vector3.one;
                    // [51차 사용자 확정 — 키 1.8m 전 씬 통일] 근원(50차까지): NormalizeSoldierScaleToPlayer는
                    //   그 씬의 플레이어(캡슐 1.8m / Heat 1.0m / 폴백 1.9f)를 실측 — 씬마다 기준이 흔들렸다.
                    //   수리: **TerritoryBuilder.NormalizeSoldierHeightFactor(1.8m 고정 = SoldierTargetHeight)**로
                    //   통일 — 테스트10/메인/실내/테스트14 어느 씬이든 병사 키 = 1.8m 고정. 플레이어 비주얼도
                    //   동일 상수 정규화(실내 테스트 씬) — 병사-플레이어 동일 키.
                    //   lv20+ 덩치 배율은 유지(GetSoldierSizeMultiplier — 배율 계약은 별도).
                    float soldierScale = TerritoryBuilder.NormalizeSoldierHeightFactor(soldier, TerritoryBuilder.SoldierTargetHeight);
                    soldierScale *= GuardManager.GetSoldierSizeMultiplier(level);
                    soldier.transform.localScale = Vector3.one * soldierScale;
                    _heightTrackedBodies.Add(soldier);   // [52차] 렌더 안정화 후 최종 키 검증 대상
                    ScaleGuardHitbox(guardGO, level);
                    // 접지 — FBX도 발끝을 실제 지면에 정렬(pos.y는 박스 오프셋+1.0 포함, 발이 뜸).
                    GroundModelToY(soldier, SurfaceY(pos.x, pos.z));

                    // 캡슐 시각 제거 + FBX 자식 콜라이더 제거(루트 BoxCollider만 유지)
                    DestroyImmediate(visual);
                    foreach (var c in soldier.GetComponentsInChildren<Collider>(true))
                        DestroyImmediate(c);

                    // Animator + SoldierShield_AC(방패병사 컨트롤러 — 프로덕션 동일 계열)
                    var anim = soldier.GetComponent<Animator>();
                    if (anim == null) anim = soldier.gameObject.AddComponent<Animator>();
                    var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                    if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    bool avatarOk = anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman;
                    Debug.Log($"[TestTerritoryCombat] 🧍 {goName} avatar={(anim.avatar != null ? anim.avatar.name : "NULL")}"
                        + $" isValid={(anim.avatar != null ? anim.avatar.isValid.ToString() : "-")}"
                        + $" isHuman={(anim.avatar != null ? anim.avatar.isHuman.ToString() : "-")}"
                        + $" controller={(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}");
                    if (!avatarOk)
                        Debug.LogWarning($"[TestTerritoryCombat] ⚠️ {goName} Humanoid avatar 무효 — Soldier_AC 재생 불가(T포즈) 가능성");

                    // 병사 모드 드라이버 — Speed=transform 델타, 공격은 GuardCombatAI→TriggerAttack
                    var driver = guardGO.AddComponent<HumanoidClipDriver>();
                    driver.mode = HumanoidClipDriver.DriveMode.Soldier;
                    HumanoidClipDriver.CopyMaterialsFromGlb(soldier, matGlbKey);

                    Debug.Log($"[TestTerritoryCombat] ✅ 병사 FBX 부착(애니 보장): {goName} ← {fbxKey} + GLB 재질 이식({matGlbKey}) (SoldierShield_AC+드라이버)");
                    attachedBody = soldier;
                }
                else
                {
                    // ② FBX 실패 폴백 → GLB 부착(애니는 클립 경로 불일치로 재생 안 될 수 있음 — 렌더 보장용).
                    string glbName = level >= 40 ? "Soldier_Lv40-50_Rigged"
                                  : level >= 20 ? "Soldier_Lv20-40_Rigged"
                                  : "Soldier_Lv1-20_Rigged";
                    var glbPrefab = Resources.Load<GameObject>($"Models/UserProvided/{glbName}");
                    if (glbPrefab == null)
                        glbPrefab = Resources.Load<GameObject>($"Models/UserProvided/{glbName}.glb");
                    if (glbPrefab != null)
                    {
                        var soldier = Instantiate(glbPrefab, guardGO.transform);
                        soldier.name = $"{goName}_Body";
                        soldier.transform.localPosition = Vector3.zero;
                        soldier.transform.localRotation = Quaternion.identity;
                        soldier.transform.localScale = Vector3.one;
                        // [51차] GLB 폴백 경로도 1.8m 고정으로 통일(씬 무관 — SoldierTargetHeight).
                        float soldierScaleGlb = TerritoryBuilder.NormalizeSoldierHeightFactor(soldier, TerritoryBuilder.SoldierTargetHeight);
                        soldierScaleGlb *= GuardManager.GetSoldierSizeMultiplier(level);
                        soldier.transform.localScale = Vector3.one * soldierScaleGlb;
                        _heightTrackedBodies.Add(soldier);   // [52차] 렌더 안정화 후 최종 키 검증 대상
                        ScaleGuardHitbox(guardGO, level);

                        GroundModelToY(soldier, SurfaceY(pos.x, pos.z));

                        DestroyImmediate(visual);
                        foreach (var c in soldier.GetComponentsInChildren<Collider>(true))
                            DestroyImmediate(c);

                        var anim = soldier.GetComponent<Animator>();
                        if (anim == null) anim = soldier.gameObject.AddComponent<Animator>();
                        var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                        if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                        anim.applyRootMotion = false;
                        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                        var driver = guardGO.AddComponent<HumanoidClipDriver>();
                        driver.mode = HumanoidClipDriver.DriveMode.Soldier;

                        Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 병사 GLB 폴백 부착(FBX 실패 — 애니 바인딩 불일치 가능): {goName} ← Models/UserProvided/{glbName}.glb");
                        attachedBody = soldier;
                    }
                }

                if (attachedBody == null)
                {
                    // ③ FBX·GLB 모두 미로드 — 캡슐(플레이스홀더) 유지 경고(가급적 발생 안 함).
                    Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 병사 GLB/FBX 모두 미로드 — 캡슐 유지: {goName}");

                    // [2026-09-15] 공격 배치 보장: 병사GO에 Rigidbody 중력 off(위 접지 수정) + 모델 없어도
                    // GuardCombatAI 추종 기반 동작은 유지되도록 태그/약어 기록.
                    guardGO.tag = recruited ? "RecruitedSoldier" : "Guard";
                }
            }

            // [2026-09-16] 포섭 여부 태그 — 성공 부착(GLB/FBX) 경로에서도 RecruitedSoldier/Guard 일관 부여.
            //   64차: 프로덕션(GuardManager)은 SetRecruited(true) 시 RecruitedSoldier 태그 부여로 통일했지만,
            //   이 테스트 CreateGuard는 '폐백(GLB/FBX 실패)' 분기에서만 태그를 바꿔 성공 부착 시 "Guard"로 남아
            //   내병사의 RecruitedSoldier 태그(화살 적중/선택·내병사 GLB 회피 방지)가 누락됐다. 전 케이스 일관 부여.
            guardGO.tag = recruited ? "RecruitedSoldier" : "Guard";

            return guardGO;
        }

        /// <summary>
        /// [2026-09-11] GLB/FBX 모델 bounds 기반 접지 정렬 — 전체 렌더러 bounds의 최저점을
        /// targetY(스폰 pos.y, 기존 SurfaceY 계약)에 맞춘다. 모델 피벗이 발끝과 다른 FBX/Glb에서
        /// localPosition.zero 배치만으로는 뜨거나 부유하는 문제를 피벗 오프셋과 무관하게 수리.
        /// Instantiate 직후(부착 직후) 1회 호출. 기존 색/레벨/포섭 로직 무변경.
        /// </summary>
        private static void GroundModelToY(GameObject model, float targetY)
        {
            if (model == null) return;
            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;
            var b = rends[0].bounds;
            foreach (var r in rends)
                b.Encapsulate(r.bounds);
            float bottom = b.min.y;
            model.transform.position += Vector3.up * (targetY - bottom);
        }

        // ================================================================
        // 채집 가능 약초 3종 배치 (2026-09-10 신규 — 기존 셋업 무변경, 신규 배치만 추가)
        // ================================================================
        /// <summary>
        /// Test_10 채집 흐름 점검용 약초 3종(Red/Purple/Green).
        /// E키 채집 → HerbPickup.Harvest() → LootBasket.Create + PlayerStats EXP 3 흐름 검증.
        /// 내 영지 앞(z 18~20)에 배치 — 기존 배치(내병사 z=19, 성 z=20~30)와 이격된 위치.
        /// </summary>
        private void SetupHerbs()
        {
            SetupHerb("Herb_Red", "Models/UserProvided/herb_red", new Vector3(3f, 0f, 20f),
                HerbPickup.HerbType.Red, new Color(0.85f, 0.2f, 0.2f, 1f));
            SetupHerb("Herb_Purple", "Models/UserProvided/herb_purple", new Vector3(-3f, 0f, 20f),
                HerbPickup.HerbType.Purple, new Color(0.7f, 0.2f, 0.8f, 1f));
            SetupHerb("Herb_Green", "Models/UserProvided/herb_green", new Vector3(0f, 0f, 18f),
                HerbPickup.HerbType.Green, new Color(0.2f, 0.7f, 0.2f, 1f));
        }

        private void SetupHerb(string goName, string modelPath, Vector3 xzPos,
            HerbPickup.HerbType herbType, Color fallbackColor)
        {
            // 지표면 계약(SurfaceY = 1 + GetHeightAt) 준수: 약초는 작은 지형 오브젝트 → 표면 + 0.3
            Vector3 pos = new Vector3(xzPos.x, SurfaceY(xzPos.x, xzPos.z) + 0.3f, xzPos.z);

            // 시각 바디: GLB 프리팹 우선, 실패 시 색상 구체 폴백
            // (HerbPickup.Start가 GetComponent<Renderer>()를 사용하므로 시각 바디 필수)
            GameObject go = null;
            GameObject modelPrefab = Resources.Load<GameObject>(modelPath);
            if (modelPrefab != null)
                go = Instantiate(modelPrefab, pos, Quaternion.identity);

            if (go == null)
            {
                Debug.LogWarning($"[TestHerb] ⚠️ '{modelPath}' 로드 실패 — 프리미티브 폴백");
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.5f;
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = fallbackColor;
                    r.material = mat;
                }
            }

            go.name = goName;

            var herbPickup = go.AddComponent<HerbPickup>();

            // _herbType은 private SerializeField — 프로젝트 관례(TestPlayerSetup 등)대로 리플렉션으로 설정
            var hf = typeof(HerbPickup).GetField("_herbType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hf?.SetValue(herbPickup, herbType);

            // Harvest()/Respawn()이 GetComponent<Collider>().enabled를 토글하므로 Collider 보장
            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.5f, 0.5f, 0.5f);
            }

            Debug.Log($"[TestHerb] ✅ {goName} 배치 (type={herbType}, pos={pos})");
        }

        // ================================================================
        // 농장 배치 (2026-09-10 신규 — 기존 셋업 무변경, 신규 배치만 추가)
        // ================================================================
        /// <summary>
        /// 내 소속 영지(East_01 — SetupMyTerritory가 SetOwnership(East,1,PlayerOwned) 등록) 근처 농장 2x2 배치.
        /// FarmPlot [26차]: 굉이질→파종(E 채널) → TimeManager 게임 3일 경과 → Ready → 부착된 HerbPickup이
        /// 기존 E키 채집 흐름(LootBasket.Create + Random yield + EXP)을 그대로 수행. 수확 후 흙땅(Dirt) 복귀.
        /// y 좌표는 이 파일의 SurfaceY(x,z)+0.1 계약 준수(밭이 흙타일로 지면에 박히게 — FarmPlot.Awake에서도 자체 보정).
        /// </summary>
        private void SetupFarm()
        {
            if (FarmingManager.Instance == null)
            {
                var managerGO = new GameObject("FarmingManager");
                managerGO.AddComponent<FarmingManager>();
            }

            // 내 영지 성(0,0,25) 전면 부지 — 약초(x ±3/0, z 18~20)와 내병사(z=19) 사이 여백에 2x2 격자
            const float farmX = 0f, farmZ = 18f;
            Vector3 center = new Vector3(farmX, SurfaceY(farmX, farmZ) + 0.1f, farmZ);

            var plots = FarmingManager.Instance.SpawnPlots(NationType.East, 1, center,
                rows: 2, cols: 2, spacing: 2.5f, HerbPickup.HerbType.Red, 3);   // [26차] 성장 2→3일

            Debug.Log($"[Farm] ✅ 농장 {plots.Count}칸 배치 (내 영지 East_01, center={center})");
        }

        // ================================================================
        // 광질 노드 배치 (신규 — 기존 셋업 무변경, 신규 배치만 추가)
        // ================================================================
        /// <summary>
        /// 내 영지(East_01) 부지 광질용 ResourceNode 3종(Wood/Stone/IronOre) 배치.
        /// SetupFarm과 같은 농장 center(내 영지 성 전면 부지)에서 X/Z 오프셋 지그재그 —
        /// 약초(±3, z 18~20)/성 입구 트리거(0, z 24.5)/내병사(z 26)와 이격된 여백.
        /// ResourceNode.Awake가 GetComponent<Renderer/Collider>() 캐시 → 프리미티브 Cube로
        /// 시각 바디+BoxCollider 동시 제공. _resourceType은 private SerializeField —
        /// HerbPickup._herbType 선례(SetupHerb 리플렉션)대로 NonPublic 인스턴스 필드 설정.
        /// y는 이 파일의 SurfaceY(x,z)+0.3 계약(큐브 반높이 0.3 → 바닥이 표면에 닿고 콜라이더가 지형에 안 박힘).
        /// </summary>
        private void SetupMiningNodes()
        {
            // SetupFarm과 동일한 농장 center(farmX=0, farmZ=18) 기준 오프셋 — 플레이어 접근 동선 공유
            const float farmX = 0f, farmZ = 18f;
            Vector3 center = new Vector3(farmX, 0f, farmZ);

            SetupMiningNode("ResourceNode_Wood", center + new Vector3(-3f, 0f, 3.5f),
                ResourceNode.ResourceType.Wood, new Color(0.45f, 0.3f, 0.15f, 1f));   // 갈색
            SetupMiningNode("ResourceNode_Stone", center + new Vector3(0f, 0f, 2f),
                ResourceNode.ResourceType.Stone, new Color(0.6f, 0.6f, 0.62f, 1f));   // 회색
            SetupMiningNode("ResourceNode_IronOre", center + new Vector3(3f, 0f, 3.5f),
                ResourceNode.ResourceType.IronOre, new Color(0.8f, 0.35f, 0.1f, 1f)); // 주황(철광 테마)

            Debug.Log("[MyTerritory] ⛏️ 광질 노드 배치: Wood/Stone/IronOre (center에서 테스트용)");
        }

        private void SetupMiningNode(string goName, Vector3 xzPos,
            ResourceNode.ResourceType nodeType, Color color)
        {
            // 지표면 계약(SurfaceY = 1 + GetHeightAt) 준수: 표면 + 0.3 — 큐브 반높이 0.3이라
            // 바닥이 정확히 표면에 놓이고 콜라이더가 지형과 겹치지 않는다.
            Vector3 pos = new Vector3(xzPos.x, SurfaceY(xzPos.x, xzPos.z) + 0.3f, xzPos.z);

            // 시각 바디: 프리미티브 Cube(URP Lit 색) — Renderer+BoxCollider 동봉이라
            // ResourceNode.Awake의 GetComponent 캐시 요건을 그대로 충족
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = goName;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                r.material = mat;
            }

            // ResourceNode.TryAutoMine/Respawn이 GetComponent<Collider>().enabled를 토글 —
            // 프리미티브가 보장하지만 HerbPickup 선례(SetupHerb)와 같은 방어 가드 유지
            if (go.GetComponent<Collider>() == null)
                go.AddComponent<BoxCollider>();

            var node = go.AddComponent<ResourceNode>();

            // [34차] ConfigureResourceType — 리플렉션 후설정은 Awake가 계산한 _isWood(Wood 기본)를
            //   못 고쳐 "채광 노드가 벌목으로 발행"됐다(채광 ui.PNG 실측). 공개 세터로 파생상태 동시 설정.
            node.ConfigureResourceType(nodeType);

            Debug.Log($"[MyTerritory] ⛏️ {goName} 배치 (type={nodeType}, pos={pos})");
        }

        // ================================================================
        // UI 전수 + 창고/크래프트 박스 + 전 아이템 시딩 (2026-09-10 신규)
        // [UTK 전환 2026-09-26] GitHub-dark UTK 기준 재작성 — 구식 IMGUI/UGUI 창 생성 제거.
        // ================================================================
        /// <summary>Test_10 UI 전수 테스트 — UTK(GitHub-dark) 기준.
        /// 구식 MinimapUI/UIInventoryHotkey+InventoryWindow/StatusWindowUI 생성부 제거:
        /// UTK 창들은 UIToolkitBootstrap(UIRoot, BeforeSceneLoad) 위에 자가부트(AfterSceneLoad)하므로
        /// 어느 씬이든 자동 표시된다(메인씬과 동일 경로). I키는 본 파일의 UtkInventoryKeyProbe가
        /// UIInventoryHotkey.InventoryToggleRequestedUTK 정적 이벤트를 발화 → UTKWireUp 브리지가
        /// InventoryWindowUTK+ItemDescriptionWindowUTK 쌍 토글(구식 창 컴포넌트 불필요).
        /// 창고/크래프트 박스 배치·시딩은 무수정 — 박스의 TerritoryWarehouse/CraftingStation은
        /// '상호작용 허브'로 UTK 우선 경로 내장(TerritoryWarehouse.OpenWarehouseUI →
        /// InventoryWindowUTK/WarehouseWindowUTK.Open(territoryId)/ItemDescriptionWindowUTK,
        /// CraftingStation.OpenCrafting → WeaponForgeUTK.Open). UTK 루트 부재 시에만 구 IMGUI 폴백.
        /// Systems asmdef은 UI 참조 불가(순환) → UTK도 리플렉션 호출(AttachUiComponent 선례).</summary>
        private void SetupUITestArena()
        {
            var uiAsm = System.Reflection.Assembly.Load("ProjectName.UI");
            if (uiAsm == null) { Debug.LogWarning("[UITest] ⚠️ ProjectName.UI 어셈블리 미발견 — UI 부착 생략"); return; }

            // Test_10은 MainScene의 GameSetup/CoreSystemsBootstrap을 통하지 않는다.
            // 다른 씬에서 실행된 DisableAllUi 상태만 해제하고, UTK root/각 UI 초기화를 명시 보장한다.
            var transition = typeof(ProjectName.Core.UITransitionState);
            var disabledField = transition.GetField("DisableAllUi",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            disabledField?.SetValue(null, false);
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.UIToolkitBootstrap", "Ensure");

            // 애플리케이션 루트가 살아 있는지 확인한 후 각 공용 UTK 화면을 중복 없이 명시 보장.
            var bootType = uiAsm.GetType("ProjectName.UI.Toolkit.UIToolkitBootstrap");
            var rootProp = bootType?.GetProperty("UIRoot", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (rootProp?.GetValue(null) == null)
            {
                Debug.LogWarning("[UITest] ⚠️ UTK root 준비 실패 — 테스트10 UI 셋업 중단");
                return;
            }

            // IMGUI HUD/uGUI 핫바는 생성하지 않는다. 체력+스태미나 원형 게이지는 HUDUTK 소유.
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.MinimapUTK", "Ensure");
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.HUDUTK", "Ensure");
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.HotbarUIUTK", "Ensure");
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.StatusWindowUTK", "Ensure");
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.QuestWindowUTK", "Ensure");
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.QuestJournalUTK", "Ensure");
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.TimeClockGlassUTK", "Ensure");
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.WorldMapWindowUTK", "Ensure"); // [114차] 월드맵 — M키 토글(자체 폴링), Test_10/13/14/15/17 공통 셋업
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.UTKWireUp", "Wire");
            Debug.Log("[UITest] ✅ UTK app boot ensured: Minimap/HUD/Hotbar/Status/Quest/Journal/Clock/WorldMap + wire-up");

            // ② 구식 UIInventoryHotkey+InventoryWindow 생성 제거 — UTK 인벤은 정적 브리지/프로브 경유.
            // UTKWireUp.Wire()는 I키 이벤트에 연결하고 probe가 씬별 입력 이벤트를 발화한다.
            // 구식 UIInventoryHotkey/InventoryWindow 컴포넌트는 생성하지 않는다.
            var probeGO = new GameObject("UTK_InventoryKeyProbe");
            probeGO.AddComponent<UtkInventoryKeyProbe>();
            Debug.Log("[UITest] ✅ I키 → UTKWireUp 브리지(UTK_InventoryKeyProbe 발화) — 구식 인벤/핫키 생성 없음");

            // ③ 구식 StatusWindowUI 생성 제거 — StatusWindowUTK가 자가부트(AfterSceneLoad)+P키 자체
            //    폴링(Updater)이라 명시 호출 불필요.

            // ④⑤ 창고 박스 2개 + 크래프트 박스 1개 + 시딩 — 무수정(2026-09-11 유지).
            //    창고(territoryId별) 시딩(wood 장비+무기 재료+Gold). 박스 상호작용 → UTK 창고.
            SetupWarehouseBox("Warehouse_1", new Vector3(6f, 0f, 14f), new Color(0.5f, 0.45f, 0.35f, 1f), "wh_test");
            SetupWarehouseBox("Warehouse_2", new Vector3(-6f, 0f, 14f), new Color(0.45f, 0.5f, 0.35f, 1f), "wh_test");
            SetupCraftBox(new Vector3(0f, 0f, 16f), new Color(0.55f, 0.4f, 0.2f, 1f));
            SeedAllItemsToWarehouse("wh_test");

            // 플레이어 인벤토리에도 대표 아이템 시딩(인벤 창 표시 검증용)
            SeedPlayerInventory();
            SetupGasVerifyScene();   // [테스트 42] 가스 확인 씬 — 분사기/독·치료 물약/방독면
        }

        /// <summary>[테스트 42] 가스 시스템 확인용 — GameSetup.EnsureGasSystem 미러 배선 + 분사기 장착 + 독/치료 물약·방독면 지급.
        /// 메인씬은 GameSetup이 담당하나 Test_10은 이를 거치지 않으므로 이 씬에서 직접 보장한다.
        /// 분사기 장착은 인벤 ItemData 대신 컨트롤러 테스트 경로(Equip(grade))를 써 즉시 확인 가능하게 한다.</summary>
        private void SetupGasVerifyScene()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) { Debug.LogWarning("[TestTerritoryCombat] Player 없음 — 가스 확인 스킵"); return; }

            // ① 가스 시스템 컴포넌트 배선 (GameSetup.EnsureGasSystem 미러, 멱등)
            if (player.GetComponent<GasSprayerController>() == null) player.AddComponent<GasSprayerController>();
            if (player.GetComponent<GasSprayer>() == null) player.AddComponent<GasSprayer>();
            // GasSprayer owns direct G-held input here; omit the legacy Mouse1 handler so right-click remains RTS input.
            if (player.GetComponent<GasMaskController>() == null) player.AddComponent<GasMaskController>();
            var uiAsm = System.Reflection.Assembly.Load("ProjectName.UI");
            InvokeUtkStatic(uiAsm, "ProjectName.UI.Toolkit.GasSprayUTK", "Ensure");
            GasCloudLauncher.RegisterHook();
            GasMaskEquipmentLink.Register();

            // ② 분사기 즉시 장착 (Wood 등급) — 인벤 ItemData 불필요 테스트 경로
            var ctrl = player.GetComponent<GasSprayerController>();
            if (ctrl != null) ctrl.Equip(GasSprayerGrade.Wood);
            else Debug.LogWarning("[TestTerritoryCombat] 가스 분사기 컨트롤러 미부착 — 장착 생략");

            // ③ 물약/방독면 지급 — ClassifyPotion이 독(Poison_)/치료(Heal_) 접두로 안개 속성을 분류
            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                inv.AddItem(new PlayerInventory.ItemData
                {
                    id = "Poison_TestPotion", displayName = "독 시험 물약",
                    description = "가스 분사기에 장전하면 주변으로 독성 안개를 확산한다.",
                    category = PlayerInventory.ItemCategory.Potion, maxStack = 10
                }, 10);
                if (ctrl != null) GasPotionLoader.LoadPotion(ctrl, "Poison_TestPotion");
                inv.AddItem(new PlayerInventory.ItemData
                {
                    id = "Heal_TestPotion", displayName = "치료 시험 물약",
                    description = "가스 분사기에 장전하면 주변으로 회복 안개를 확산한다.",
                    category = PlayerInventory.ItemCategory.Potion, maxStack = 10
                }, 10);
                inv.AddItem(PlayerInventory.GasMaskWood, 1);   // 나무 방독면 — 인벤 Mask 슬롯 장착 시 면역
                Debug.Log("[TestTerritoryCombat] ✅ 가스 확인 씬 배선 — 분사기(Wood)·독/치료 물약·방독면 지급");
            }
            else Debug.LogWarning("[TestTerritoryCombat] PlayerInventory 미생성 — 물약/방독면 지급 생략");
        }

        /// <summary>UTK 정적 무인자 메서드 리플렉션 호출(멱등 Ensure용) — 실패 시 셋업을 중단하지 않는다
        /// (각 UTK 창의 RuntimeInitializeOnLoadMethod 자가부트가 담보).</summary>
        private static void InvokeUtkStatic(System.Reflection.Assembly uiAsm, string typeName, string methodName)
        {
            var type = uiAsm.GetType(typeName);
            if (type == null) { Debug.LogWarning($"[UITest] ⚠️ {typeName} 미발견 — {methodName} 생략(자가부트 의존)"); return; }
            var mi = type.GetMethod(methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, System.Type.EmptyTypes, null);
            mi?.Invoke(null, null);
        }

        // ================================================================
        // [UTK 전환 2026-09-26] I키 프로브 — 구식 UIInventoryHotkey+InventoryWindow 대체(본 파일 전용)
        // ================================================================
        /// <summary>
        /// 구식 UIInventoryHotkey.Update는 InventoryWindow.Instance(구식 IMGUI 창)가 없으면 I키를 무시하므로
        /// 구식 창 생성을 제거하면 이벤트 발화자가 사라진다. 본 프로브는 동일 정적 이벤트
        /// (UIInventoryHotkey.InventoryToggleRequestedUTK)를 리플렉션으로 Invoke해 UTKWireUp 브리지
        /// (AfterSceneLoad 기구독 → InventoryWindowUTK+ItemDescriptionWindowUTK 쌍 토글)를 재사용한다.
        /// 구독자 부재 시(UTKWireUp 미배선)엔 브리지와 동일한 쌍 동작으로 UTK 창을 직접 토글(폴백).
        /// UTK 루트(UIToolkitBootstrap.UIRoot) 부재 시엔 무시 — Test_10은 UTK 표시가 전제(메인씬 동일).
        /// MinimapUTK.Updater의 nested MonoBehaviour+AddComponent 선례 패턴.
        /// </summary>
        private sealed class UtkInventoryKeyProbe : MonoBehaviour
        {
            private static bool _noRootLogged;

            private void Update()
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null || !kb.iKey.wasPressedThisFrame) return;

                var uiAsm = System.Reflection.Assembly.Load("ProjectName.UI");
                if (uiAsm == null) return;

                // UTK 루트 게이트 — 루트 부재 시 창 열림 없음(구식 폴백 창도 생성하지 않았다)
                var bootType = uiAsm.GetType("ProjectName.UI.Toolkit.UIToolkitBootstrap");
                var rootProp = bootType?.GetProperty("UIRoot",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (rootProp?.GetValue(null) == null)
                {
                    if (!_noRootLogged)
                    {
                        Debug.LogWarning("[UITest] ⚠️ UTK 루트 미준비 — I키 무시(UTK 전환 씬)");
                        _noRootLogged = true;
                    }
                    return;
                }

                // 1순위: 정적 이벤트 Invoke → UTKWireUp 브리지(기구독)가 쌍 토글 처리
                var hotType = uiAsm.GetType("ProjectName.UI.UIInventoryHotkey");
                var evField = hotType?.GetField("InventoryToggleRequestedUTK",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var handler = evField?.GetValue(null) as System.Action;
                if (handler != null)
                {
                    handler.Invoke();
                    Debug.Log("[UITest] I키 → UTKWireUp 브리지 발화 (UTK 인벤+설명 쌍)");
                    return;
                }

                // 2순위 폴백: 브리지 미배선 — UTK 창 직접 토글(UTKWireUp과 동일한 쌍 동작)
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                var invType = uiAsm.GetType("ProjectName.UI.Toolkit.InventoryWindowUTK");
                var descType = uiAsm.GetType("ProjectName.UI.Toolkit.ItemDescriptionWindowUTK");
                if (invType == null) return;
                var instance = invType.GetProperty("Instance", flags)?.GetValue(null);
                bool closing = instance != null
                    && (bool)(invType.GetProperty("IsOpen")?.GetValue(instance) ?? false);
                if (closing)
                {
                    invType.GetMethod("Close")?.Invoke(instance, null);
                    descType?.GetMethod("Hide", flags)?.Invoke(null, null);
                }
                else
                {
                    invType.GetMethod("Open", flags)?.Invoke(null, null);
                    descType?.GetMethod("Open", flags)?.Invoke(null, null);
                }
                Debug.Log($"[UITest] I키 → UTK 인벤 직접 토글 폴백 ({(closing ? "닫힘" : "열림")})");
            }
        }

        private void SetupWarehouseBox(string goName, Vector3 pos, Color color, string territoryId)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = goName;
            box.transform.position = new Vector3(pos.x, SurfaceY(pos.x, pos.z) + 0.55f, pos.z);
            box.transform.localScale = new Vector3(1.4f, 1.1f, 1.4f);
            var mr = box.GetComponent<Renderer>();
            if (mr != null) mr.sharedMaterial.color = color;
            // TerritoryWarehouse(ProjectName.UI) — 리플렉션 부착(asmdef 순환 회피)
            var uiAsm = System.Reflection.Assembly.Load("ProjectName.UI");
            var whType = uiAsm != null ? uiAsm.GetType("ProjectName.UI.TerritoryWarehouse") : null;
            if (whType != null)
            {
                var whComp = box.AddComponent(whType);
                // 2026-09-11: Test_09 선례와 동일 계약 Configure("wh_test_09",64,3f) → Test_10 박스도
                // 시딩 ID 일치 + 64슬롯 상향. AddComponent 직후 Awake가 20슬롯 캐시를 만들므로
                // Configure(런타임 AddComponent 후 외부 초기화용)가 재조정한다.
                var cfg = whType.GetMethod("Configure");
                cfg?.Invoke(whComp, new object[] { territoryId, 64, 3f });
            }
        }

        private void SetupCraftBox(Vector3 pos, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "CraftStation_Box";
            box.transform.position = new Vector3(pos.x, SurfaceY(pos.x, pos.z) + 0.55f, pos.z);
            box.transform.localScale = new Vector3(1.4f, 1.1f, 1.4f);
            var mr = box.GetComponent<Renderer>();
            if (mr != null) mr.sharedMaterial.color = color;
            var uiAsm = System.Reflection.Assembly.Load("ProjectName.UI");
            var csType = uiAsm != null ? uiAsm.GetType("ProjectName.UI.CraftingStation") : null;
            if (csType != null && box.GetComponent(csType) == null) box.AddComponent(csType);
            var col = box.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);   // Raycast 히트 대상에서 제외
        }

        /// <summary>WarehouseSystem 시딩(territoryId="wh_test" 단일 창고) — wood 장비 전종+무기 재료+Gold+화살 3종으로 축소.
        /// 2026-09-13(44차 P6): 화살 3종 시딩 — 활 발사 테스트용.</summary>
        private void SeedAllItemsToWarehouse(string territoryId)
        {
            // 2026-09-11: Test_10 창고 64슬롯 — TestAllInOneSetup 선례 패턴 이식.
            // ① WarehouseSystem은 자동 생성 싱글톤이 아님(Instance get; private set;) → 없으면 생성.
            //    (이전에는 Instance 부재 시 시딩 전체가 무음 스킵됐다)
            // ② _maxSlotsPerTerritory 기본 20은 시딩을 잘라 사용자 실측 증상 →
            //    private [SerializeField]라 리플렉션으로 상향(시딩 전 1회).
            //    2026-09-11: 기존 시딩 ~43슬롯 + 신규 4티어 장비 47종×2(94슬롯) ≈ 137슬롯 → 64→160 상향.
            //    2026-09-11(2): 시딩 축소(wood 장비 13종×2 + 무기 재료/Gold) — 상한 160은 여유로 유지.
            if (WarehouseSystem.Instance == null)
            {
                var wsGO = new GameObject("WarehouseSystem");
                wsGO.AddComponent<WarehouseSystem>();
                Debug.Log("[UITest] ✅ WarehouseSystem 생성");
            }
            var slotsField = typeof(WarehouseSystem).GetField("_maxSlotsPerTerritory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            slotsField?.SetValue(WarehouseSystem.Instance, 160);

            int total = 0;
            void Add(PlayerInventory.ItemData item, int count)
            {
                if (item == null) return;
                if (WarehouseSystem.Instance != null && WarehouseSystem.Instance.AddItem(territoryId, item, count))
                    total += count;
            }

            // 2026-09-11(2): 시딩 축소 — wood 등급 장비 전종 + 무기 재료 2종 + Gold만 유지.
            //  제거: 허브·씨앗·고기·모피/가죽·도구·어류·잠입장비·물약·토큰/증서·비wood(steel/stone/crystal) 장비
            Add(PlayerInventory.BoarTusk, 5);   // 무기 재료 (멧돼지 엄니)
            Add(PlayerInventory.WolfTooth, 5);  // 무기 재료 (늑대 이빨)
            Add(PlayerInventory.Gold, 999);

            // wood 등급 GLB 장비 전종(id에 "wood" 포함) ×2 — steel/stone/crystal은 시딩 제외.
            //  유지 13종: weapon_sword/spear/bow/dagger_wood + wood_armor/helmet/boot_left/boot_right/
            //  glove_left/glove_right + wood_shield + wood_gas_mask + wood_chemical_pack
            foreach (var gear in PlayerInventory.AllTieredGear)
            {
                if (gear != null && !string.IsNullOrEmpty(gear.id) && gear.id.Contains("wood"))
                    Add(gear, 2);
            }

            // 2026-09-13(44차 P6): 화살 3종 시딩 — 활 발사 테스트용.
            //  PlayerInventory에 화살 정적 정의가 없어 ArrowManager.AddArrows(ArrowData 매핑)와
            //  동일한 id/displayName/description으로 인라인 ItemData 생성(category=ItemCategory.Arrow,
            //  maxStack=50). ArrowManager.TryConsumeArrow(RemoveItem by id) 소모 판정 호환.
            Add(new PlayerInventory.ItemData
            {
                id = "arrow_regular",
                displayName = "일반 화살",
                description = "기본 화살. 특별한 효과 없음.",
                category = PlayerInventory.ItemCategory.Arrow,
                rarity = ItemRarity.Common,
                maxStack = 50,
                maxDurability = 0
            }, 20);
            Add(new PlayerInventory.ItemData
            {
                id = "arrow_reinforced",
                displayName = "강화 화살",
                description = "철촉이 달린 강화 화살. +5 데미지.",
                category = PlayerInventory.ItemCategory.Arrow,
                rarity = ItemRarity.Uncommon,
                maxStack = 50,
                maxDurability = 0
            }, 20);
            Add(new PlayerInventory.ItemData
            {
                id = "arrow_magic",
                displayName = "마법 화살",
                description = "마력이 깃든 화살. +15 데미지.",
                category = PlayerInventory.ItemCategory.Arrow,
                rarity = ItemRarity.Rare,
                maxStack = 50,
                maxDurability = 0
            }, 20);

            Debug.Log($"[UITest] ✅ 창고 '{territoryId}'에 시딩 완료 (wood 장비+무기 재료+Gold+화살 3종, {total}개)");
        }

        /// <summary>플레이어 인벤토리 대표 시딩 — 인벤 창(I)/핫바 표시 검증용.</summary>
        private void SeedPlayerInventory()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) { Debug.LogWarning("[UITest] ⚠️ PlayerInventory 미발견 — 시딩 생략"); return; }

            // [2026-10-11] Test_15 슬림화 — 검/가죽갑옷/곡괭이/고기 등 전투·일반 시딩 제외(사용자 요구).
            if (_isLivelihoodScene)
            {
                inv.AddItem(PlayerInventory.Mat_Wood, 6);
                inv.AddItem(PlayerInventory.Mat_Stone, 2);
                inv.AddItem(PlayerInventory.BoarTusk, 1);
                Debug.Log("[Test15Livelihood] ✅ 크래프트 검증 재료 지급 — 통나무×6·석재×2·엄니×1");
                // [149차 P5] 음식점 검증 시딩(Test_15 전용 — Resources 씬 제외) — 일반 요리+마약 요리를
                //   플레이어 인벤토리에 지급. 좌측 "판매 가능한 음식" 그리드 소스 = PlayerInventory Food/Drug(drugged_)
                //   (RestaurantWindowUTK.CollectInventoryFoods) — 기존 시딩은 식재료 0종이라 "판매할 수 있는 음식이
                //   없다"(사용자 실측)가 떴다. id는 VillageDrugState 재고 시딩(foodItemId cook_01/drugged_cook_01)과
                //   일치 — RestockFromPlayer 입고 시 동일 병합 키 슬롯에 합산. 마약 설명에 stage 마커 필수
                //   (RestaurantSystem.ParseDrugStage — "[마약: stage 3]" 파싱, 재고 시딩 stage=3와 병합 정합).
                if (!_isLivelihoodResourcesScene)
                {
                    inv.AddItem(new PlayerInventory.ItemData { id = "cook_01", displayName = "일반 요리", description = "마을 음식점 판매용 일반 요리.", category = PlayerInventory.ItemCategory.Food, rarity = ItemRarity.Common, maxStack = 99, maxDurability = 0, basePrice = 10 }, 10);
                    inv.AddItem(new PlayerInventory.ItemData { id = "drugged_cook_01", displayName = "마약 요리", description = "마약이 담긴 요리. [마약: stage 3]", category = PlayerInventory.ItemCategory.Food, rarity = ItemRarity.Common, maxStack = 99, maxDurability = 0, basePrice = 30 }, 10);
                    Debug.Log("[149차] ✅ 음식점 검증 시딩 — 일반 요리 10 + 마약 요리(3단계) 10 인벤토리 지급");
                }
                return;
            }

            inv.AddItem(PlayerInventory.Herb_Red, 5);
            inv.AddItem(PlayerInventory.RabbitMeat, 5);
            inv.AddItem(PlayerInventory.SwordWood, 1);
            inv.AddItem(PlayerInventory.LeatherArmor, 1);
            inv.AddItem(PlayerInventory.Pickaxe, 1);
            inv.AddItem(PlayerInventory.Fish_Common, 2);
            inv.AddItem(PlayerInventory.Gold, 100);
            // 2026-09-12(41차 P7): 우클릭 복용 테스트용 물약 시딩 — 실존 Potion 정의만 사용
            // (PlayerInventory.StealthPotion=potion_stealth / Sedative=potion_sedative, maxStack 10).
            // PotionUseSystem.Use 매핑 확인: stealth→은신 활성화, sedative→진정(투약 기록) 둘 다 true 반환.
            inv.AddItem(PlayerInventory.StealthPotion, 3);
            inv.AddItem(PlayerInventory.Sedative, 3);

            // [TEST23-FIX] 화살 인벤 시딩 — ArrowManager는 PlayerInventory에서 화살을 소모한다(창고 아님).
            // 창고(wh_test)에도 화살을 시딩했으나 인벤엔 없어 '화살 부족' → 발사 불가. 활 좌클릭 테스트용으로
            // ArrowManager가 소모하는 id(arrow_regular/reinforced/magic)와 동일 ItemData를 인벤에 추가.
            // PlayerInventory에 화살 정적 정의가 없으므로 인라인 ItemData 생성(창고 시딩 선례와 동일).
            inv.AddItem(new PlayerInventory.ItemData { id = "arrow_regular",    displayName = "일반 화살", description = "기본 화살. 특별한 효과 없음.", category = PlayerInventory.ItemCategory.Arrow, rarity = ItemRarity.Common,   maxStack = 50, maxDurability = 0 }, 20);
            inv.AddItem(new PlayerInventory.ItemData { id = "arrow_reinforced", displayName = "강화 화살", description = "철촉이 달린 강화 화살. +5 데미지.", category = PlayerInventory.ItemCategory.Arrow, rarity = ItemRarity.Uncommon, maxStack = 50, maxDurability = 0 }, 20);
            inv.AddItem(new PlayerInventory.ItemData { id = "arrow_magic",      displayName = "마법 화살", description = "마력이 깃든 화살. +15 데미지.", category = PlayerInventory.ItemCategory.Arrow, rarity = ItemRarity.Rare,      maxStack = 50, maxDurability = 0 }, 20);

            Debug.Log("[UITest] ✅ 플레이어 인벤 대표 아이템 시딩 완료 (물약 포함: 은신 물약×3, 진정제×3 — 우클릭 복용 테스트용)");
        }

        /// <summary>[후속17-2] 덩치 병사(level>=20)의 히트용 BoxCollider를 배율만큼 확대(윗몸이 안 맞는 버그 방지).
        ///   보통 병사(배율 1.0)는 기존 상태를 그대로 유지한다.</summary>
        private void ScaleGuardHitbox(GameObject guardGO, int level)
        {
            float mult = GuardManager.GetSoldierSizeMultiplier(level);
            if (mult == 1f) return;
            var hc = guardGO.GetComponent<BoxCollider>();
            if (hc == null) return;
            float w = 0.6f * mult, h = 1.8f * mult, d = 0.6f * mult;
            hc.size = new Vector3(w, h, d);
            hc.center = new Vector3(hc.center.x, h * 0.5f, hc.center.z); // 바닥(0) 기준으로 스팬 — 발이 지면에 닿게
        }
        // ================================================================
        // [62차] Test_17_LordInteraction 전용 — 야외 마을 상호작용(대화 패널) 씬 배치
        // ================================================================
        /// <summary>
        /// Test_17_LordInteraction 전용 런타임 배치 — 실내(구 Test_16 복제 구성) → 야외 마을 지형으로 재구성.
        ///  ① 마을 광장 원판 — PlaceVillageWithNpcs 선례 패턴(Cylinder 12m·Untagged), 위치만 미니지형 중심부 (0,0,10).
        ///  ② 마을 NPC 2종 — SpawnVillageNpc 재사용(좌클릭 → InteractionPanel → 대화하기=NPCDialoguePanelUTK / 상점=ShopWindowUTK).
        ///  ③ 영주 NPC 1명 — SpawnVillageNpc 동일 패턴(isShop=false) 야외 배치. 좌클릭 → "NPC" 패널(상태보기/대화하기/선물주기/퀘스트)
        ///     → 대화하기 → 대화 패널. InvadedLordEncounter는 실내 영주실 앵커(0,0,28)+토폴로지 검증이라 야외 부적합 → 미사용.
        ///     LordFeedTarget/InvadedLordEncounter를 붙이면 호버 분류가 Lord가 되어 대화 액션이 "미지원"로 빠지므로 붙이지 않는다.
        ///  ④ 내 소속 병사 3명(파랑·서측·포섭) + 적군 병사 3명(빨강·동측·적대) — SpawnSoldier(CreateGuard) 경로 + 콜라이더 보강 + 홀드.
        ///  GSM/RTS/Router는 base Awake 자가부트 — 추가 부팅 불필요. IndoorActive는 세팅하지 않음(야외 기본 false).
        /// </summary>
        private void SetupLordInteractionTestScene()
        {
            PlaceLordInteractionPlaza();
            SpawnLordInteractionNpcs();
            SpawnLordInteractionSoldiers();
            Debug.Log("[Test17] ✅ 마을 상호작용 준비 — ①마을 NPC 좌클릭=대화/상점 패널 ②영주 좌클릭=NPC 패널(대화하기)→대화 패널 ③병사 드래그 선택(파랑 서측) ④적군 병사(빨강 동측)");
        }

        /// <summary>마을 광장 원판 — PlaceVillageWithNpcs 선례 복제(위치만 (0,0,10) — 미니지형 중심부).</summary>
        private void PlaceLordInteractionPlaza()
        {
            try
            {
                const float plazaX = 0f, plazaZ = 10f;
                float pY = SurfaceY(plazaX, plazaZ);
                var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                plaza.name = "Test17VillagePlaza";
                plaza.transform.position = new Vector3(plazaX, pY + 0.06f, plazaZ);
                plaza.transform.localScale = new Vector3(12f, 0.06f, 12f);
                plaza.tag = "Untagged";
                var pr = plaza.GetComponent<MeshRenderer>();
                if (pr != null) pr.material = MaterialHelper.CreateLitMaterial(new Color(0.52f, 0.5f, 0.47f), "Test17VillagePlaza_Mat");
                Debug.Log($"[Test17] 광장 원판 배치 @ ({plazaX:F1}, {pY + 0.06f:F2}, {plazaZ:F1})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Test17] 마을 광장 배치 실패: {ex.Message}");
            }
        }

        /// <summary>마을 NPC 2종(상점/주민) + 영주 NPC 1명 — SpawnVillageNpc 재사용.
        ///   본문이 SurfaceY(pos.x, pos.z)로 y를 재계산하므로 pos.y는 0 전달(선례와 동일).</summary>
        private void SpawnLordInteractionNpcs()
        {
            const float plazaX = 0f, plazaZ = 10f;

            // 마을 상점 NPC — 좌클릭 → InteractionPanel "상점" → ShopWindowUTK
            SpawnVillageNpc("test17_shop", "잡화점 상인", true, "어서 오세요, 뭐 필요한가?",
                new Vector3(plazaX - 3f, 0f, plazaZ - 2f));
            // 마을 주민 NPC — 좌클릭 → InteractionPanel "대화하기" → NPCDialoguePanelUTK
            SpawnVillageNpc("test17_villager", "마을 주민 한백", false, "안녕하세요, 여행자님. 최근 서부 영주가 저지르는 일이 걱정돼요.",
                new Vector3(plazaX + 3f, 0f, plazaZ + 2f));
            // 영주 NPC(야외) — 좌클릭 → InteractionPanel "NPC" 패널(상태보기/대화하기/선물주기/퀘스트) → 대화하기 → 대화 패널
            SpawnVillageNpc("test17_lord", "👑 서부 영주 가레트", false, "서부 성에 온 손님이군요. 무슨 일이신가?",
                new Vector3(plazaX, 0f, plazaZ + 5f));
        }

        /// <summary>내 병사 3명(서측 x-6 열 1.5m 간격·파랑·포섭) + 적군 병사 3명(동측 x+6 열 1.5m 간격·빨강·적대).
        ///   CreateGuard가 BoxCollider(0.6×1.8×0.6·+0.9)/kinematic rb/GuardHeadUI를 부여하나, 스폰 후
        ///   EnsureTest17CombatCollider로 누락 없음을 방어적 재보장(LordInteractionTestSetup.EnsureCombatCollider 동일 패턴).
        ///   양측 모두 SetHold(true) — 제자리 고정으로 관측 안정(드래그 선택/명령은 플레이어 수동 검증).</summary>
        private void SpawnLordInteractionSoldiers()
        {
            const float plazaX = 0f, plazaZ = 10f;
            var myColor = new Color(0.25f, 0.5f, 0.9f);      // 내 진영 파랑
            var enemyColor = new Color(0.9f, 0.3f, 0.3f);    // 적 진영 빨강

            int myHeld = 0, enemyHeld = 0;
            for (int i = 0; i < 3; i++)
            {
                float gz = plazaZ - 1.5f + i * 1.5f;   // 열 1.5m 간격(z 방향 라인)

                // 내 소속 병사 — 광장 서측(x-6) 열, 포섭(RecruitedSoldier 태그 → 드래그 선택 대상)
                var myGo = SpawnSoldier($"Test17_MyGuard_{i + 1}", new Vector3(plazaX - 6f, 0f, gz),
                    "내 수행 병사", 8, NationType.East, true, myColor);
                if (myGo != null)
                {
                    EnsureTest17CombatCollider(myGo);
                    var myPh = myGo.GetComponent<GuardPlaceholder>();
                    if (myPh != null) { myPh.SetHold(true); myHeld++; }
                    Debug.Log($"[Test17] 내 병사 배치: {myGo.name} @ {myGo.transform.position} (파랑·홀드)");
                }

                // 적군 병사 — 광장 동측(x+6) 열, 미포섭 + 플레이어 진영 적대
                var enemyGo = SpawnSoldier($"Test17_EnemyGuard_{i + 1}", new Vector3(plazaX + 6f, 0f, gz),
                    "서부 병사", 8, NationType.West, false, enemyColor);
                if (enemyGo != null)
                {
                    EnsureTest17CombatCollider(enemyGo);
                    var enemyPh = enemyGo.GetComponent<GuardPlaceholder>();
                    if (enemyPh != null)
                    {
                        enemyPh.SetHold(true);                    // 제자리 — 관측 안정
                        enemyPh.HostileToPlayerFaction = true;    // 적군 — 플레이어 진영 적대
                        enemyHeld++;
                    }
                    Debug.Log($"[Test17] 적군 병사 배치: {enemyGo.name} @ {enemyGo.transform.position} (빨강·홀드·적대)");
                }
            }
            Debug.Log($"[Test17] ⚔️ 병사 배치 완료 — 내 병사 3명(서측 x-6, 홀드 {myHeld}명) / 적군 병사 3명(동측 x+6, 홀드 {enemyHeld}명) — 국가 상이(East↔West) 서로 적대");
        }

        /// <summary>[62차] Test_17 병사 전투 판정 보강 — LordInteractionTestSetup.EnsureCombatCollider 동일 패턴(멱등).
        ///   CreateGuard가 이미 동일 구성을 부여하지만, 스폰 경로 분기(비주얼 폴백 등)에서의 누락 가능성을 방어적으로 재보장.</summary>
        private static void EnsureTest17CombatCollider(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 1.8f, 0.6f);
                col.center = new Vector3(0f, 0.9f, 0f);
            }
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.mass = 1f;
            if (go.GetComponent<GuardHeadUI>() == null)
                go.AddComponent<GuardHeadUI>();
        }

    }
}