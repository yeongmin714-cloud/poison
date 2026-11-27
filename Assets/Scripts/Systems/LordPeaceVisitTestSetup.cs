using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// [128차] Test_19_LordPeaceVisit 전용 — 병사 포섭 → 생존 영주 성 평화 방문 테스트씬.
    ///
    /// 시나리오(사용자 요구): "병사를 포섭해서 영지에 진입했을 때 전쟁으로 이어지지 않고
    /// 영주와, 영주 딸과 상호작용할 수 있는" — 용어계약 [126차]: 포섭=병사 포섭(점령 아님),
    /// 생존 영주(비PlayerOwned) 성 방문 시나리오.
    ///
    /// 구성(직접 구축형 — Test_17_LordInteraction 선례, EnterBuilding 미호출):
    ///   - 실경로(IndoorSceneTransition castle 비PlayerOwned 분기)는 실내 수비병을 적대 상태로
    ///     스폰한다(SetInCombat+적대 = 전쟁 경로). 본 씬은 그 경로를 호출하지 않고
    ///     평화 상태를 직접 구축한다(실경로 코드 무수정 — zero-touch).
    ///   - 영주 = InvadedLordEncounter.SpawnForInvadedTerritory(실제 본경로) — 공격 버튼을
    ///     눌러야만 교전 시작(평화 기본값). 이 함수 말미의 영주딸 스폰 훅이 결정론 롤을 수행한다.
    ///   - 영주딸 = 결정론 등장(HasLordDaughter FNV-1a 40%) — West_01..20 중 롤을 통과하는
    ///     첫 영지키(사전 검증: West_7, roll 32)를 채택해 훅 경로로 확정 등장(강제 스폰 없음).
    ///   - 포섭 대상 병사 1기 = 남쪽 로비, 중립(HostileToPlayerFaction=false)+호감도 75
    ///     (GuardRecruitSystem.LOYALTY_NORMAL 70 이상 — 포섭 결정론 성공, 랜덤 분기 미진입).
    ///   - 시드: 골드 5,000(입단비) + 들꽃 3개(선물) + 딸 호감도 70(선물→80→프로포즈 사다리).
    ///
    /// 검증 포인트 (Play 게이트):
    ///   ① F키 병사 → SoldierInteractUTK 포섭하기 → "포섭 완료" 토스트 + 입단비 차감
    ///   ② 영주 좌클릭/F키 → 패널(대화하기/공격하기) → 대화하기 → InvadedLordDialogueUTK 개방
    ///      (공격하지 않는 한 전투 없음 — EncounterState.NotStarted 유지)
    ///   ③ 딸 좌클릭 → 패널 3버튼(대화하기/들꽃 선물/프로포즈) — 선물 1회 후 호감도 80 → 프로포즈 수락
    ///   ④ 적대 병사 0건 — 전투 미발생(씬 Awake 검증 로그)
    ///   ⑤ 딸은 좌클릭 전용(F키 체인은 병사&gt;영주까지만 — 본씬과 동일 계약)
    /// </summary>
    public sealed class LordPeaceVisitTestSetup : MonoBehaviour
    {
        public const string TestSceneName = "Test_19_LordPeaceVisit";

        // 방문 대상 국가/스타일 — 서부(West). Test_17과 동일 적성 구조(내 병사=동부와 달리 본 씬은 포섭 대상이 서부 병사).
        private const NationType VisitNation = NationType.West;
        private const string VisitNationStyle = "Western";
        private const int DaughterKeyScanRange = 20;   // West_01..West_20 결정론 스캔

        // 포섭 대상 병사 — 중립 + 호감도 75(≥ LOYALTY_NORMAL 70) = 포섭 결정론 성공.
        private const int RecruitSoldierLevel = 5;
        private const float RecruitLoyaltySeed = 75f;

        // 시드 — 골드(입단비)/들꽃(선물)/딸 호감도(선물→프로포즈 사다리).
        private const int SeedGoldAmount = 5000;
        private const int SeedFlowerCount = 3;
        private const int SeedDaughterAffection = 70;

        // ---- Test_19 전용 상태 (Awake에서 기록 → OnDestroy에서 원복) ----
        private bool _owned;
        private bool _previousBuildPaused;
        private bool _builderPreexisting;
        private Color _ambientBeforeTest;
        private Color _baseAmbient;
        private DayNightCycle _dnc;
        private bool _dncWasEnabled;
        private readonly List<(Light light, float intensity, LightShadows shadows)> _lightSnapshots = new List<(Light, float, LightShadows)>();
        private bool _lightingApplied;
        private TerritoryOwnership _previousOwnership;
        private bool _ownershipRecorded;
        private string _visitKey = "West_7";

        // Test_19 전용 조명 보정 — Test_17 동일 배율. 공유 빌더/IndoorLighting 계약 무수정.
        private const float TestOnlyAmbientBoost = 1.25f;
        private const int LightingMaxWaitFrames = 300;

        private void Awake()
        {
            if (gameObject.scene.name != TestSceneName)
                return;
            _owned = true;

            // [128차] 실내 진입 상태 — 실내 이동/전투 게이트가 실제 실내처럼 작동(Test_17 계약 동일).
            UITransitionState.IndoorActive = true;

            // [128차] 월드 영지 성 GLB 생성 차단 — Test_17 선례( Awk에서 세우고 OnDestroy에서 원복).
            _previousBuildPaused = TerritoryBuilder.BuildPaused;
            TerritoryBuilder.BuildPaused = true;
            _builderPreexisting = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include) != null;

            _ambientBeforeTest = RenderSettings.ambientLight;
            NeutralizeSceneSunlights();
            EnsureGuardSystems();
            EnsureGameManager();

            // [128차] 결정론 딸 등장 영지키 — West_01..20 중 HasLordDaughter 통과 첫 키(예상 West_7).
            _visitKey = ResolveDaughterTerritoryKey();

            // [128차] 소유권 선행세팅 — RegisterInvadedLord의 LordOwned 게이트 통과(Test_17 선례).
            //   생존 영주 시나리오 유지 — 점령(PlayerOwned)로 만들지 않는다(용어계약).
            EnsureVisitOwnership(_visitKey);

            ContextCommandRouter.Ensure();   // 좌클릭 NPC 패널 라우팅(영주/딸) — InteractionPanelUTK는 자가부트(AfterSceneLoad).

            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(VisitNationStyle, 0);
            if (room == null)
            {
                Debug.LogError("[PeaceVisit] PlayerCastleInteriorBuilder returned null.");
                return;
            }
            IndoorMaterialFactory.ApplyToRoom(room,
                PlayerCastleInteriorBuilder.RoomWidth,
                PlayerCastleInteriorBuilder.RoomHeight,
                PlayerCastleInteriorBuilder.RoomDepth);
            HideCeiling(room);
            _baseAmbient = RenderSettings.ambientLight;
            StartCoroutine(ApplyTestLightingWhenReady());

            // [128차] 영주 NPC — 실제 본경로 스폰(내부 훅에서 딸까지 함께). 공격 버튼을 눌러야만 교전.
            var lord = InvadedLordEncounter.SpawnForInvadedTerritory(room, _visitKey);
            if (lord == null)
                Debug.LogError("[PeaceVisit] 영주 스폰 실패 — TerritoryDatabase/키/소유권 확인");
            else
            {
                Debug.Log($"[PeaceVisit] 👑 생존 영주 배치 — {lord.LordName} @ 영주실 HP={lord.MaxHP} (평화 상태 — 공격 전까지 교전 없음)");
                EInteractionPromptBridge.RaiseRegister(lord.transform, "F키를 눌러 영주와 대화", 3f);
            }

            // [128차] 영주딸 등장 확인 — 훅 경로 산물. 강제 스폰하지 않는다(훅 경로 자체가 검증 대상).
            var daughter = Object.FindAnyObjectByType<LordDaughterNPC>(FindObjectsInactive.Include);
            if (daughter != null)
                Debug.Log($"[PeaceVisit] 🌸 영주딸 등장 확인 — {daughter.DisplayName} (호감도 {daughter.Affection}/100, 훅 경로)");
            else
                Debug.LogError("[PeaceVisit] 영주딸 미등장 — 롤 통과 키인데도 훅이 스폰하지 않았다(계약 위반 후보 — Play 판정 단서).");

            // [128차] 포섭 대상 병사 1기 — 남쪽 로비, 중립(적대 아님), 호감도 75.
            SpawnRecruitableSoldier(room);

            // [128차] 플레이어 + 시드(골드/들꽃/딸 호감도) + 카메라.
            EnsurePlayer();
            SeedTestAssets(_visitKey);
            EnsureCamera();

            // [128차] 전쟁 요소 부재 증명 — 적대 병사 0건(Play 판정 단서).
            ValidateNoHostileGuards();

            Debug.Log("[PeaceVisit] 🧪 평화 방문 테스트 준비 — ①F키 병사→포섭하기(입단비 차감) ②영주 좌클릭→대화하기/먹이기 ③딸 좌클릭→대화/들꽃 선물/프로포즈(선물 1회 후 80) ④전투 미발생 확인");
        }

        private void OnDestroy()
        {
            if (!_owned) return;
            _owned = false;

            UITransitionState.IndoorActive = false;
            TerritoryBuilder.BuildPaused = _previousBuildPaused;

            // [128차] 소유권 원복 — 진입 전 값 기록분 복원 + 영주 등록 전역 오염 해제(InvadedLordEncounterTests 선례).
            if (_ownershipRecorded)
            {
                var db = TerritoryDatabase.Instance;
                if (db != null && TryParseVisitKey(_visitKey, out TerritoryId visitId))
                    db.SetOwnership(visitId, _previousOwnership);
                _ownershipRecorded = false;
            }
            LordSurrenderSystem.ResetAll();

            // GameManager(DontDestroyOnLoad)가 이 씬 중 만든 TerritoryBuilder 제거 — Test_17 선례.
            if (!_builderPreexisting)
            {
                var builder = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include);
                if (builder != null) Destroy(builder);
            }

            // 조명 원복 — Test_17 선례(DNC 복원/방향광 원값/앰비언트 원값).
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

        // ================================================================
        // 결정론 영지키 / 소유권
        // ================================================================

        /// <summary>
        /// [128차] 딸 등장 확정 영지키 — West_01..West_20 순회, HasLordDaughter(결정론 FNV-1a 40%)
        /// 통과 첫 키 채택(사전 검증: West_7). 통과 키 부재 시 West_1 폴백 + 경고(훅이 스폰 안 함 — 로그로 판별).
        /// </summary>
        private static string ResolveDaughterTerritoryKey()
        {
            for (int i = 1; i <= DaughterKeyScanRange; i++)
            {
                string key = $"West_{i}";
                if (!LordDaughterNPC.HasLordDaughter(key)) continue;
                Debug.Log($"[PeaceVisit] 딸 등장 확정 영지키 — {key} (HasLordDaughter=결정론 통과, {i}번째 스캔)");
                return key;
            }
            Debug.LogWarning("[PeaceVisit] 롤 통과 영지키 부재 — West_1 폴백(딸 미등장 예상 — 영지 풀 점검 필요).");
            return "West_1";
        }

        /// <summary>[128차] 방문 영지 소유권 선행세팅 — LordOwned(생존 영주) 유지가 핵심. 진입 전 값 기록.</summary>
        private void EnsureVisitOwnership(string visitKey)
        {
            try
            {
                var db = TerritoryDatabase.Instance;
                if (db == null)
                {
                    Debug.LogError("[PeaceVisit] TerritoryDatabase.Instance가 null — 소유권 세팅 불가");
                    return;
                }
                if (!TryParseVisitKey(visitKey, out TerritoryId visitId))
                {
                    Debug.LogError($"[PeaceVisit] 영지 키 파싱 실패: '{visitKey}'");
                    return;
                }
                var state = db.GetState(visitKey);
                if (state != null)
                {
                    _previousOwnership = state.ownership;
                    _ownershipRecorded = true;
                }
                db.SetOwnership(visitId, TerritoryOwnership.LordOwned);
                Debug.Log($"[PeaceVisit] ✅ 영지 소유 선행세팅 — {visitKey} → LordOwned (생존 영주 — RegisterInvadedLord 게이트 통과용)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PeaceVisit] 영지 DB 소유권 세팅 실패: {ex.Message}");
            }
        }

        private static bool TryParseVisitKey(string key, out TerritoryId id)
        {
            id = default;
            if (string.IsNullOrWhiteSpace(key)) return false;
            string[] parts = key.Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int index)) return false;
            if (!System.Enum.TryParse(parts[0], true, out NationType nation)) return false;
            id = new TerritoryId(nation, index);
            return true;
        }

        // ================================================================
        // 포섭 대상 병사
        // ================================================================

        /// <summary>
        /// [128차] 포섭 대상 병사 1기 — 남쪽 로비 대기. 미영입(SetRecruited(false))+중립
        /// (HostileToPlayerFaction=false — EnemyGuard 분류여도 공격 안 함)+호감도 75(포섭 결정론 성공).
        /// 포섭 경로 = F키 → SoldierInteractUTK 포섭하기(좌클릭은 공격 과녁 — 안내문 표기).
        /// </summary>
        private void SpawnRecruitableSoldier(GameObject room)
        {
            Vector3 basePos = room.transform.position;
            float floorY = basePos.y;
            Vector3 pos = new Vector3(basePos.x + 2.4f, floorY, basePos.z - 8f);   // Test_17 남로비 라인
            try
            {
                var go = TerritoryBuilder.SpawnDeployedGuard("PeaceVisitRecruit_1", RecruitSoldierLevel, VisitNation, new Vector3(pos.x, pos.z));
                if (go == null)
                {
                    Debug.LogError("[PeaceVisit] 포섭 대상 병사 스폰 실패 — SpawnDeployedGuard null");
                    return;
                }
                go.transform.position = new Vector3(pos.x, 0f, pos.z);   // 실내 바닥 y 고정(Test_17 계약)

                EnsureCombatCollider(go);
                GuardPlaceholder ph = go.GetComponent<GuardPlaceholder>();
                if (ph == null) ph = go.AddComponent<GuardPlaceholder>();
                ph.SetGuardInfo("수비병 다모", RecruitSoldierLevel, VisitNation);
                ph.SetRecruited(false);                 // 미영입 — 포섭 대상 상태 유지
                ph.HostileToPlayerFaction = false;      // 중립 — 적대 아님(전쟁 요소 배제)
                ph.IndoorMoveExempt = true;             // 실내 이동 게이트 예외(Test_17 선례)
                ph.Loyalty = RecruitLoyaltySeed;        // 75 ≥ LOYALTY_NORMAL 70 — 포섭 결정론 성공
                EInteractionPromptBridge.RaiseRegister(go.transform, "F키를 눌러 병사와 대화", 3f);
                Debug.Log($"[PeaceVisit] 🤝 포섭 대상 병사 스폰 — {go.name} (Lv.{RecruitSoldierLevel}, 중립·호감도 {RecruitLoyaltySeed:F0} — F키→포섭하기)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PeaceVisit] 포섭 대상 병사 배치 실패: {ex.Message}");
            }
        }

        /// <summary>[128차] 전쟁 요소 부재 증명 — 적대(SetInCombat/HostileToPlayerFaction) 병사 0건 실측 로그.</summary>
        private static void ValidateNoHostileGuards()
        {
            int hostile = 0;
            foreach (var ph in Object.FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None))
            {
                if (ph != null && ph.HostileToPlayerFaction) hostile++;
            }
            if (hostile == 0)
                Debug.Log("[PeaceVisit] ✅ 전쟁 요소 없음 — HostileToPlayerFaction 병사 0건 (평화 방문 상태)");
            else
                Debug.LogError($"[PeaceVisit] ⚔️ 적대 병사 {hostile}건 발견 — 평화 계약 위반(전쟁 경로 유입 의심)");
        }

        // ================================================================
        // 플레이어 / 시드 / 카메라 (ArenaTestSetup·Test_17 선례)
        // ================================================================

        private static void EnsurePlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }
            if (player.GetComponent<PlayerMovement>() == null) player.AddComponent<PlayerMovement>();
            if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            }
            if (player.GetComponent<PlayerCombat>() == null) player.AddComponent<PlayerCombat>();
            if (player.GetComponent<PlayerHealth>() == null) player.AddComponent<PlayerHealth>();
            if (player.GetComponent<PlayerInventory>() == null) player.AddComponent<PlayerInventory>();
            // [128차] 플레이어 시각+애니 통일 — ArenaTestSetup.SetupPlayer 선례(GLB+ForceBiped, 1.8m 정규화 내장).
            if (player.GetComponent<PlayerPlaceholder>() == null) player.AddComponent<PlayerPlaceholder>();
            player.transform.position = new Vector3(0f, 1.5f, -12f);   // 남쪽 로비 — 포섭 대상 병사(x+2.4, z-8) 근처
        }

        /// <summary>[128차] 테스트 자산 시드 — 골드 5,000(입단비)+들꽃 3개(선물)+딸 호감도 70(사다리).</summary>
        private static void SeedTestAssets(string visitKey)
        {
            // 골드 — ArenaTestSetup.EnsureLobbyGold 선례(멱등: 미달분만 보충).
            var stats = PlayerStats.Instance ?? Object.FindAnyObjectByType<PlayerStats>();
            if (stats == null)
            {
                var go = new GameObject("PlayerStats");
                stats = go.AddComponent<PlayerStats>();   // 실내 씬에 인스턴스 부재 시 신설(Awake가 Instance 등록)
            }
            if (stats.Gold < SeedGoldAmount)
            {
                stats.AddGold(SeedGoldAmount - stats.Gold, "peace_visit_seed");
                Debug.Log($"[PeaceVisit] 골드 시드 — {stats.Gold}G (입단비 검증용, 미달분 보충)");
            }

            // 들꽃 — 딸 선물 재료(3회 선물 가능).
            var player = GameObject.FindGameObjectWithTag("Player");
            var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inventory != null)
            {
                int before = inventory.GetItemCount(PlayerInventory.Flower_Wild.id);
                if (before < SeedFlowerCount)
                    inventory.AddItem(PlayerInventory.Flower_Wild, SeedFlowerCount - before);
                Debug.Log($"[PeaceVisit] 🌸 들꽃 시드 — {inventory.GetItemCount(PlayerInventory.Flower_Wild.id)}개 (들꽃 선물 검증용)");
            }

            // 딸 호감도 70 — 선물 1회(→80) 후 프로포즈 수락 사다리 검증.
            var state = TerritoryDatabase.Instance != null ? TerritoryDatabase.Instance.GetState(visitKey) : null;
            if (state != null)
            {
                state.daughterAffection = SeedDaughterAffection;
                Debug.Log($"[PeaceVisit] 딸 호감도 시드 — {visitKey} {state.daughterAffection}/100 (선물→80→프로포즈 사다리)");
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
            cam.farClipPlane = 500f;   // farClip 500 계약 유지(Test_10 선례)
            // [카메라통일] 잔존 IndoorCameraFollow 제거 — PM 원근 단일 기록자(투어 DDOL 카메라 재사용 대비).
            var staleFollow = camGO.GetComponent<IndoorCameraFollow>();
            if (staleFollow != null) Object.Destroy(staleFollow);
            if (camGO.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();
        }

        // ================================================================
        // 공용 셋업 헬퍼 (Test_17 선례 복사)
        // ================================================================

        private static void EnsureGuardSystems()
        {
            if (GuardManager.Instance == null)
            {
                var gmGO = new GameObject("GuardManager");
                gmGO.AddComponent<GuardManager>();
            }
            GuardTaskSystem.Ensure();
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

        /// <summary>전투 판정/오버랩스피어에 필요한 콜라이더+kinematic rb 보장 — Test_17 선례.</summary>
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

        /// <summary>
        /// [128차] 테스트 조명 보정 — Test_17 ApplyTestLightingWhenReady 선례(DNC 봉쇄+방향광 off+앰비언트 ×boost).
        /// OnDestroy 원복. 코루틴 무음사망 계약 회피(Update 폴링 코루틴 — 기존 계약 준용).
        /// </summary>
        private IEnumerator ApplyTestLightingWhenReady()
        {
            for (int frame = 0; frame < LightingMaxWaitFrames; frame++)
            {
                var dnc = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
                if (dnc != null)
                {
                    _dnc = dnc;
                    _dncWasEnabled = dnc.enabled;
                    dnc.enabled = false;
                    foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (light == null || light.type != LightType.Directional) continue;
                        _lightSnapshots.Add((light, light.intensity, light.shadows));
                        light.intensity = 0f;
                        light.shadows = LightShadows.None;
                    }
                    RenderSettings.ambientLight = new Color(
                        _baseAmbient.r * TestOnlyAmbientBoost,
                        _baseAmbient.g * TestOnlyAmbientBoost,
                        _baseAmbient.b * TestOnlyAmbientBoost);
                    _lightingApplied = true;
                    Debug.Log($"[PeaceVisit] 조명 보정 적용 — DNC 봉쇄(원복 예정), 방향광 {_lightSnapshots.Count}개 off, 앰비언트 ×{TestOnlyAmbientBoost:F2}");
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning("[PeaceVisit] DayNightCycle 미발견 — 테스트 조명 보정 건너뜀.");
        }

        /// <summary>[128차] 화면 안내문 — ArenaTestSetup OnGUI 선례.</summary>
        private void OnGUI()
        {
            if (!_owned) return;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            GUI.Label(new Rect(12f, 12f, 560f, 118f),
                "[Test_19] 병사 포섭 → 생존 영주 성 평화 방문\n" +
                $"방문 영지: {_visitKey} (LordOwned — 결정론 딸 등장 키)\n" +
                "① 남쪽 수비병 F키 → 포섭하기 (입단비 차감 — 좌클릭은 공격이니 주의)\n" +
                "② 영주(👑) 좌클릭/F키 → 대화하기·공격하기·먹이기 (공격 전까지 교전 없음)\n" +
                "③ 영주딸(🌸) 좌클릭 → 대화하기·들꽃 선물·프로포즈 (선물 1회 후 호감도 80)");
        }
    }
}
