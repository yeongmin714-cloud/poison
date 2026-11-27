using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-10-12] Test_16_IndoorWar 전용 — "내가 타 영지를 침공" 실내 전쟁 테스트씬.
    ///
    /// 목적: **실내 수비병이 내(플레이어 소속) 병사에게 반응을 잘하는지** 실측 검증.
    ///   내가 다른 영지(적 영주 소유)의 성 내부로 침공해 들어온 시나리오를 재현:
    ///   - 적 실내 수비병 3명 = 로비 방어선에서 제자리 수비(홀드) — IsEnemyGuard(국가 상이) 반격.
    ///   - 내 소속 병사 3명 = 남쪽 입구에서 방어선으로 행진 + 공격 명령 — 진입 즉시 교전.
    ///
    /// 검증 포인트 (Play 게이트):
    ///   ① 적 수비병이 내 병사 접근/공격 시 반응(수비병이 되돌려 공격)하는가
    ///   ② 내 병사 침공이 실제 가시 교전(공격 애니·HP 감소)으로 이어지는가
    ///   ③ 적 수비병이 제자리를 지키며 반격만 하는가(추격 없음 — 홀드 반격 계약)
    ///
    /// PlayerCastleInteriorPlaytest 구조를 재사용하되, 플레이어 소유가 아닌 **적 영주 성**을 세우고
    /// 양 진영 병사를 배치한다. 씬은 실내 진입 상태(IndoorActive=true)로 시작해 실내 전투 계약이 적용된다.
    /// </summary>
    public sealed class IndoorWarTestSetup : MonoBehaviour
    {
        public const string TestSceneName = "Test_16_IndoorWar";

        // 침공 대상 적 영지 — 서부(West). 내 병사는 동부(East) → IsEnemyGuard(국가 상이)로 서로 적.
        private const NationType EnemyNation = NationType.West;
        private const NationType MyNation = NationType.East;
        private const string EnemyNationStyle = "Western";

        // ---- Test_16 전용 상태 (Awake에서 기록 → OnDestroy에서 원복) ----
        // 씬 전용 게이트: Awake에서 Test_16일 때만 true. 다른 씬에서는 아무것도 건드리지 않는다.
        private bool _owned;
        private bool _previousBuildPaused;
        private bool _builderPreexisting;          // Awake 시점에 TerritoryBuilder가 이미 있었는지
        private Color _ambientBeforeTest;           // 테스트 진입 전 RenderSettings 값(원복용)
        private Color _baseAmbient;                 // 빌더가 설정한 실내 앰비언트 (보정 기준)
        private DayNightCycle _dnc;                 // 봉쇄한 DayNightCycle 참조
        private bool _dncWasEnabled;
        private readonly List<(Light light, float intensity, LightShadows shadows)> _lightSnapshots = new List<(Light, float, LightShadows)>();
        private bool _lightingApplied;
        private bool _previousSquadModeActive;     // GuardSelectionManager.squadModeActive 진입 전 값(원복용)

        // [Test_16 스폰 교전] 스폰 성공분 참조 수집 — 씬 세팅 직후 1회 자동 교전 지정에 사용.
        private readonly List<GuardPlaceholder> _enemyDefenders = new List<GuardPlaceholder>();
        private readonly List<GuardPlaceholder> _mySquad = new List<GuardPlaceholder>();

        // Test_16 전용 조명 보정 (+25%). 공유 PlayerCastleInteriorBuilder/IndoorLighting 계약은 건드리지 않음.
        private const float TestOnlyAmbientBoost = 1.25f;
        private const int LightingMaxWaitFrames = 300;

        private void Awake()
        {
            if (gameObject.scene.name != TestSceneName)
                return;
            _owned = true;

            // [44차 계약] 실내 진입 상태 — 실내 이동/전투 게이트가 실제 실내처럼 작동.
            UITransitionState.IndoorActive = true;

            // [Test_16 전용] 월드 영지 성 GLB 생성 차단.
            // GameManager.Start → EnsureTerritoryManager가 TerritoryBuilder를 추가하고, 그 Start가
            // 82개 영지 성 GLB를 배치한다. BuildPaused=true는 그 Start(이 씬 로드 프레임 내)에서 읽히므로
            // Awake에서 세우고, OnDestroy에서 이전 값으로 복원한다.
            // (병사 스폰 경로 TerritoryBuilder.SpawnDeployedGuard/CreateGuard는 BuildPaused를 검사하지 않음.)
            _previousBuildPaused = TerritoryBuilder.BuildPaused;
            TerritoryBuilder.BuildPaused = true;
            _builderPreexisting = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include) != null;

            // 테스트 진입 전 조명 상태 기록 (OnDestroy에서 원복).
            _ambientBeforeTest = RenderSettings.ambientLight;

            NeutralizeSceneSunlights();
            EnsureGuardSystems();
            EnsureGameManager();

            // [Test_16 Phase 1A] 기존 RTS 마우스 조작 활성화: 드래그 선택(부대 모드 — Ctrl 없이)
            // + 우클릭 명령(RTSCommandSystem). 진입 전 squadModeActive 값은 OnDestroy에서 원복.
            _previousSquadModeActive = GuardSelectionManager.squadModeActive;
            GuardSelectionManager.squadModeActive = true;
            EnsureRtsControls();

            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(EnemyNationStyle, 0);
            if (room == null)
            {
                Debug.LogError("[IndoorWarTest] PlayerCastleInteriorBuilder returned null.");
                return;
            }

            IndoorMaterialFactory.ApplyToRoom(room,
                PlayerCastleInteriorBuilder.RoomWidth,
                PlayerCastleInteriorBuilder.RoomHeight,
                PlayerCastleInteriorBuilder.RoomDepth);
            HideCeiling(room);

            // 빌더가 설정한 실내 앰비언트를 보정 기준으로 캡처 (DayNightCycle 봉쇄는 코루틴에서 1회).
            _baseAmbient = RenderSettings.ambientLight;
            StartCoroutine(ApplyTestLightingWhenReady());

            // [61차 사용자 요구] Test_16에도 영주실에 영주 NPC 배치 — Test_17과 동일 체계(침략 영주 상호작용).
            // RegisterInvadedLord는 TerritoryState.ownership==LordOwned 게이트 — 소유권 선행세팅 필수.
            // 기존 수비병 3명(SetHold 반격)/내 병사 3명 배치는 그대로 유지 — 영주실(북쪽 z=28)은 로비 방어선(z=4)과
            //   원거리라 교전 불개입(충돌 없음).
            EnsureLordScenarioOwnership();
            var lord = InvadedLordEncounter.SpawnForInvadedTerritory(room, "West_01");
            if (lord == null) Debug.LogError("[IndoorWarTest] 영주 배치 실패 — TerritoryDatabase/키/소유권 확인");
            else
            {
                Debug.Log($"[IndoorWarTest] 👑 영주 배치 — {lord.LordName} @ 영주실 HP={lord.MaxHP}");
                // [69차 P3] 영주 근접 F 프롬프트 — 63차 근접 프롬프트 라벨 패턴 통일.
                EInteractionPromptBridge.RaiseRegister(lord.transform, "F키를 눌러 영주와 대화", 3f);
            }

            // 전투 관찰용 병사 배치
            SpawnEnemyDefenseLine(room);
            SpawnMyMarchingSquad(room);

            // [Test_16 스폰 교전] 씬 세팅 직후 1회 — 내 병사 ↔ 적 수비병 1:1 자동 공격 지정.
            // 이후 플레이어 수동 명령(RTS)이 항상 우선하며 자동 지정은 재주장되지 않는다(자동조종 없음).
            AssignSpawnTimeEngagement(_mySquad, _enemyDefenders);

            EnsurePlayer();
            // [73차 Phase B] 16씬 시드 골드 — 뇌물/포섭 시험 가능(15씬 shop_test_setup 선례).
            //   세이브 gold=0/무시드 세션에서 뇌물이 조용히 실패하던 문제 보완. PlayerStats 부재 시 스킵(null 가드),
            //   500 미만일 때만 시드(재입장 중복 지급 방지).
            if (PlayerStats.Instance != null && PlayerStats.Instance.Gold < 500)
                PlayerStats.Instance.AddGold(500, "test_seed");
            EnsureCamera();
            Debug.Log("[IndoorWarTest] 🧪 실내 전쟁 테스트 준비 — 확인: ①적 수비병 3명 반격 반응 ②내 병사 3명 행진→교전 ③수비병 제자리 수비");
        }

        private void OnDestroy()
        {
            if (!_owned) return;
            _owned = false;

            UITransitionState.IndoorActive = false;
            TerritoryBuilder.BuildPaused = _previousBuildPaused;
            // 부대 모드 임시 설정 원복 (RTS 매니저 자체는 공유 서비스라 파괴하지 않는다).
            GuardSelectionManager.squadModeActive = _previousSquadModeActive;

            // GameManager(DontDestroyOnLoad)가 이 씬 중 만든 TerritoryBuilder는 BuildPaused=true 상태로
            // Start가 끝나 한 번도 빌드하지 않았다. 이 상태로 남기면 다음 씬(메인 등)에서 영지가 생성되지
            // 않으므로, 이 씬이 만든 빌더 컴포넌트만 제거한다. 다음 EnsureTerritoryManager가 다시 추가한다.
            if (!_builderPreexisting)
            {
                var builder = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include);
                if (builder != null) Destroy(builder);
            }

            // 조명 원복: 봉쇄했던 DayNightCycle 복귀, 방향광 원래 값, 앰비언트 원래 값.
            if (_dnc != null) _dnc.enabled = _dncWasEnabled;
            foreach (var snap in _lightSnapshots)
            {
                if (snap.light == null) continue;
                snap.light.intensity = snap.intensity;
                snap.light.shadows = snap.shadows;
            }
            _lightSnapshots.Clear();
            _dnc = null;
            if (_lightingApplied)
            {
                RenderSettings.ambientLight = _ambientBeforeTest;
                _lightingApplied = false;
            }
        }

        /// <summary>
        /// [Test_16 스폰 교전] 스폰 직후 1회 자동 교전 지정 — 내 병사 i ↔ 적 수비병 i (인덱스 1:1, 결정적).
        /// 각 내 병사에 SetExplicitAttackTarget(enemy, enemy.position)을 **한 번만** 호출한다(재발행
        /// 자동조종 없음). 명시 타겟 사망 시 기존 GuardPlaceholder 계약(명령 유지 + 주변 2.5m 재탐색)이
        /// 그대로 처리한다. 이후 RTS 수동 명령(SetCommandTarget/SetExplicitAttackTarget)이 항상 우선하며,
        /// SetCommandTarget이 명시 포커스를 해제하므로 자동 지정은 절대 재주장되지 않는다.
        /// 테스트 직접 호출용 internal static — InternalsVisibleTo(ProjectName.Tests.EditMode).
        /// </summary>
        internal static void AssignSpawnTimeEngagement(IReadOnlyList<GuardPlaceholder> mySquad, IReadOnlyList<GuardPlaceholder> enemyDefenders)
        {
            if (mySquad == null || enemyDefenders == null) return;
            int pairs = Mathf.Min(mySquad.Count, enemyDefenders.Count);
            int assigned = 0;
            for (int i = 0; i < pairs; i++)
            {
                GuardPlaceholder enemy = enemyDefenders[i];
                GuardPlaceholder friend = mySquad[i];
                if (enemy == null || friend == null || enemy.IsDead || friend.IsDead) continue;
                friend.SetExplicitAttackTarget(enemy, enemy.transform.position);
                assigned++;
            }
            if (assigned > 0)
                Debug.Log($"[IndoorWarTest] ⚔️ 스폰 자동 교전 — 내 병사 {assigned}명 → 적 수비병 1:1 지정 (플레이어 수동 명령 시 해제·재주장 없음)");
        }

        /// <summary>
        /// [Test_16 전용] GameManager.Start가 만드는 DayNightCycle은 매 프레임 RenderSettings.ambientLight를
        /// 덮어쓰고 MoonLight(방향광)를 켠다. DayNightCycle이 나타나면 1회 봉쇄(enabled=false)하고,
        /// 방향광을 끈 뒤 기준 앰비언트 ×boost를 1회 적용한다. OnDestroy에서 모두 원복한다.
        /// 별도 컴포넌트 없이 이 씬 오브젝트의 코루틴으로 수행 (씬 언로드 시 자동 중단).
        /// </summary>
        private IEnumerator ApplyTestLightingWhenReady()
        {
            for (int frame = 0; frame < LightingMaxWaitFrames; frame++)
            {
                var dnc = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
                if (dnc != null)
                {
                    // 1) 주기 조명 봉쇄. 이전 상태를 기록해 OnDestroy에서 복원.
                    _dnc = dnc;
                    _dncWasEnabled = dnc.enabled;
                    dnc.enabled = false;

                    // 2) 방향광(MoonLight 등) 끄기 — 원래 값 스냅샷 후 제거.
                    foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (light == null || light.type != LightType.Directional) continue;
                        _lightSnapshots.Add((light, light.intensity, light.shadows));
                        light.intensity = 0f;
                        light.shadows = LightShadows.None;
                    }

                    // 3) 기준 앰비언트 ×boost 1회 적용.
                    RenderSettings.ambientLight = new Color(
                        _baseAmbient.r * TestOnlyAmbientBoost,
                        _baseAmbient.g * TestOnlyAmbientBoost,
                        _baseAmbient.b * TestOnlyAmbientBoost);
                    _lightingApplied = true;

                    Debug.Log($"[IndoorWarTest] 조명 보정 적용 — DayNightCycle 봉쇄(원복 예정), 방향광 {_lightSnapshots.Count}개 끔, 앰비언트 ×{TestOnlyAmbientBoost:F2} → {RenderSettings.ambientLight}");
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning("[IndoorWarTest] DayNightCycle을 찾지 못해 테스트 조명 보정을 건너뜀.");
        }

        private static void NeutralizeSceneSunlights()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light == null || light.type != LightType.Directional) continue;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>전투 판정/오버랩스피어에 필요한 콜라이더+kinematic rb를 보장한다.
        /// TerritoryBuilder.CreateGuard(FBX 경로)는 콜라이더를 추가하지 않으므로
        /// ResolveAttackTarget/TryHoldEngage의 OverlapSphere가 쌍방을 감지하도록 여기서 보강.</summary>
        private static void EnsureCombatCollider(GameObject go)
        {
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

        private static void HideCeiling(GameObject room)
        {
            Transform ceiling = room.transform.Find("Ceiling");
            var renderer = ceiling != null ? ceiling.GetComponent<MeshRenderer>() : null;
            if (renderer != null) renderer.enabled = false;
        }

        /// <summary>
        /// [61차 Test_16 전용] 침략 영주 시나리오 소유권 선행세팅 — LordInteractionTestSetup.EnsureLordScenarioOwnership
        /// (Test_17 선례) 복제. 서부(West_01)를 LordOwned로 세팅해 RegisterInvadedLord의 소유권 게이트를 통과시킨다.
        /// DB 미초기화 등 실패가 테스트씬 세팅 전체를 중단하지 않도록 try-catch 격리.
        /// </summary>
        private static void EnsureLordScenarioOwnership()
        {
            try
            {
                var db = TerritoryDatabase.Instance;
                if (db == null)
                {
                    Debug.LogError("[IndoorWarTest] TerritoryDatabase.Instance가 null — 소유권 세팅 불가");
                    return;
                }
                db.SetOwnership(new TerritoryId(NationType.West, 1), TerritoryOwnership.LordOwned);
                Debug.Log("[IndoorWarTest] ✅ 영지 소유 선행세팅 — West_01 → LordOwned (RegisterInvadedLord 게이트 통과용)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[IndoorWarTest] 영지 DB 소유권 세팅 실패: {ex.Message}");
            }
        }

        /// <summary>적 실내 수비병 3명 — 로비(지휘책상 인근) 방어선에서 제자리 홀드.
        /// SpawnInteriorDefenseGuards 계약(SetInCombat+HostileToPlayerFaction)을 따르되
        /// 반격이 실제로 발동되도록 SetHold(true) 부여 — 국가 상이 내 병사에 TryHoldEngage 반격.</summary>
        private void SpawnEnemyDefenseLine(GameObject room)
        {
            Vector3 basePos = room.transform.position;
            float floorY = basePos.y;
            // 로비 중심 + 지휘책상(CommandDesk local (0,0,15)) 앞 방어선 — 적이 지키는 핵심 구역.
            Vector3 defenseCenter = new Vector3(basePos.x, floorY, basePos.z + 4f);
            Color enemyColor = new Color(0.8f, 0.25f, 0.25f);

            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = new Vector3(defenseCenter.x - 2.4f + i * 2.4f, floorY, defenseCenter.z);
                SpawnDefenseGuard($"IndoorEnemyDefender_{i + 1}", pos, $"적 실내 수비병 {i + 1}", 9, enemyColor);
            }
            Debug.Log($"[IndoorWarTest] 🛡️ 적 실내 수비병 3명 배치 @ {defenseCenter} (홀드 — 제자리 반격)");
        }

        private void SpawnDefenseGuard(string goName, Vector3 pos, string guardName, int level, Color color)
        {
            try
            {
                // 프로덕션 병사 생성 경로 재사용 (FBX/애니/높이 1.8m 보장).
                var go = TerritoryBuilder.SpawnDeployedGuard(goName, level, EnemyNation, new Vector3(pos.x, pos.z));
                if (go == null) return;
                // 실내 바닥 y 재고정 (월드 지형 높이 오염 방지).
                go.transform.position = new Vector3(pos.x, 0f, pos.z);

                EnsureCombatCollider(go);
                GuardPlaceholder ph = go.GetComponent<GuardPlaceholder>();
                if (ph == null) ph = go.AddComponent<GuardPlaceholder>();
                ph.SetGuardInfo(guardName, level, EnemyNation);
                ph.SetRecruited(false);
                ph.HostileToPlayerFaction = true;   // 실내 침입자(플레이어/그 병사) 공격 대상
                ph.SetInCombat(true);
                ph.SetHold(true);                   // 제자리 수비 + IsEnemyGuard 근접 반격
                ColorBody(go, color);
                go.tag = "Guard";
                _enemyDefenders.Add(ph);            // [Test_16 스폰 교전] 자동 지정 후보 수집
                Debug.Log($"[IndoorWarTest] 적 수비병 스폰 — {goName} (Lv.{level}, 홀드 반격)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[IndoorWarTest] 적 수비병 배치 실패({goName}): {ex.Message}");
            }
        }

        /// <summary>내 소속 병사 3명 — 남쪽 입구에 대기(자동 공격 명령 없음). 플레이어가 드래그 선택 후
        /// 우클릭 명령(RTSCommandSystem)으로 지시한다. 실내 이동(IndoorMoveExempt)을 허용해 명령 이동이 실내에서 작동.</summary>
        private void SpawnMyMarchingSquad(GameObject room)
        {
            Vector3 basePos = room.transform.position;
            float floorY = basePos.y;
            // 남쪽 입구(EntryHall 중앙) 대기 위치.
            Vector3 startCenter = new Vector3(basePos.x, floorY, basePos.z - 8f);
            Color myColor = new Color(0.25f, 0.5f, 0.9f);

            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = new Vector3(startCenter.x - 2.4f + i * 2.4f, floorY, startCenter.z);
                SpawnMySoldier($"IndoorMySoldier_{i + 1}", pos, $"내 침공 병사 {i + 1}", 8, myColor);
            }
            Debug.Log($"[IndoorWarTest] ⚔️ 내 소속 병사 3명 대기 @ {startCenter} — 드래그 선택 후 우클릭으로 명령");
        }

        private void SpawnMySoldier(string goName, Vector3 pos, string guardName, int level, Color color)
        {
            try
            {
                var go = TerritoryBuilder.SpawnDeployedGuard(goName, level, MyNation, new Vector3(pos.x, pos.z));
                if (go == null) return;
                go.transform.position = new Vector3(pos.x, 0f, pos.z);

                EnsureCombatCollider(go);
                GuardPlaceholder ph = go.GetComponent<GuardPlaceholder>();
                if (ph == null) ph = go.AddComponent<GuardPlaceholder>();
                ph.SetGuardInfo(guardName, level, MyNation);
                ph.SetRecruited(true);
                ph.HostileToPlayerFaction = false;  // 내 병사 — 플레이어 진영
                ph.IndoorMoveExempt = true;         // 실내 이동 게이트 예외 (침공 보행)
                ColorBody(go, color);
                go.tag = "RecruitedSoldier";
                _mySquad.Add(ph);                   // [Test_16 스폰 교전] 자동 지정 후보 수집
                Debug.Log($"[IndoorWarTest] 내 병사 스폰 — {goName} (Lv.{level}, 대기)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[IndoorWarTest] 내 병사 배치 실패({goName}): {ex.Message}");
            }
        }

        private static void ColorBody(GameObject go, Color color)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r != null && (r is SkinnedMeshRenderer || r is MeshRenderer))
                    r.material.color = color;
        }

        private static void EnsureGuardSystems()
        {
            if (GuardManager.Instance == null)
            {
                var gmGO = new GameObject("GuardManager");
                gmGO.AddComponent<GuardManager>();
            }
            GuardTaskSystem.Ensure();
        }

        /// <summary>[Test_16 Phase 1A] 기존 RTS 마우스 조작 시스템 보장 — 있으면 재사용, 없을 때만 생성.
        /// 생성 시 전용 GO + DontDestroyOnLoad (TestTerritoryCombatSetup 선례). 파괴/교체는 하지 않는다.</summary>
        private static void EnsureRtsControls()
        {
            if (Object.FindAnyObjectByType<GuardSelectionManager>(FindObjectsInactive.Include) == null)
            {
                var gsmGO = new GameObject("GuardSelectionManager");
                DontDestroyOnLoad(gsmGO);
                gsmGO.AddComponent<GuardSelectionManager>();
            }
            if (Object.FindAnyObjectByType<RTSCommandSystem>(FindObjectsInactive.Include) == null)
            {
                var rtsGO = new GameObject("RTSCommandSystem");
                DontDestroyOnLoad(rtsGO);
                rtsGO.AddComponent<RTSCommandSystem>();
            }
        }

        private static void EnsureGameManager()
        {
            GameObject gm = GameManager.Instance != null ? GameManager.Instance.gameObject : new GameObject("GameManager");
            if (GameManager.Instance == null)
                gm.AddComponent<GameManager>();
            if (Object.FindAnyObjectByType<GuardHostilitySystem>(FindObjectsInactive.Include) == null)
                gm.AddComponent<GuardHostilitySystem>();
            if (Object.FindAnyObjectByType<GuardVisualAttachSystem>(FindObjectsInactive.Include) == null)
                gm.AddComponent<GuardVisualAttachSystem>();
        }

        private static void EnsurePlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player") { tag = "Player" };
                player.transform.position = new Vector3(0f, 0f, -12f);
            }
            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f); cc.stepOffset = 0.3f;
            }
            // [이동통일 B1] 단순 WASD 전용 컨트롤러 폐기 — Test_10 계약 PlayerMovement로 통일
            //   (위 CharacterController 계수 유지 + PM RequireComponent 자동 부착).
            if (player.GetComponent<PlayerMovement>() == null)
                player.AddComponent<PlayerMovement>();
            // [이동통일 B3] 구 컨트롤러 발지 클램프 승계 — 성 외부 이탈 방지(벽 콜라이더 부재 씬).
            if (player.GetComponent<IndoorPlayBoundsClamp>() == null)
                player.AddComponent<IndoorPlayBoundsClamp>();
            if (PlayerInventory.Instance == null && player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();
            EnsurePlayerVisual(player);
        }

        // [Test_16 전용] 플레이어 가시화 — PlayerCastleInteriorTestPlaytest와 동일 비주얼(Player_Rigged_Heat, 1.8m 정규화),
        // FBX 부재 시 캡슐 폴백. 멱등: 보이는 Renderer가 이미 있으면 아무것도 하지 않아 중복 생성하지 않는다.
        private static void EnsurePlayerVisual(GameObject player)
        {
            var renderers = player.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                    return;   // 이미 가시 렌더러 존재
            }

            // 이전에 만들어 두었지만 비활성인 비주얼이 있으면 재사용(중복 생성 방지).
            Transform existing = player.transform.Find("PlayerModel") ?? player.transform.Find("PlayerTestCapsule");
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                foreach (var r in existing.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                return;
            }

            GameObject visual;
            var playerFbx = Resources.Load<GameObject>("Models/UserProvided/fbx/Player_Rigged_Heat");
            if (playerFbx != null)
            {
                visual = Object.Instantiate(playerFbx);
                visual.name = "PlayerModel";
                visual.transform.SetParent(player.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                TerritoryBuilder.NormalizeSoldierHeight(visual, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: false);
                if (visual.GetComponent<Animator>() == null)
                    visual.AddComponent<Animator>();
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "PlayerTestCapsule";
                visual.transform.SetParent(player.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                visual.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f);
                Collider visualCollider = visual.GetComponent<Collider>();
                if (visualCollider != null)
                    Object.Destroy(visualCollider);
            }
        }

        private static void EnsureCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
            }
            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null) cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            // [카메라통일] 직교 폐기 → Test_10식 원근(PM 오빗 57° 단일 기록자 계약).
            cam.orthographic = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;   // farClip 500 계약 통일(Test_10 선례 — 기존 120 상향)
            // [카메라통일] 잔존 IndoorCameraFollow 제거 — PM 원근 단일 기록자(투어 DDOL 카메라 재사용 대비).
            var staleFollow = camGO.GetComponent<IndoorCameraFollow>();
            if (staleFollow != null) Object.Destroy(staleFollow);
            if (camGO.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();
        }
    }
}
