using System.IO;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [89차] F키 마을 주민 상호작용 + 플레이어 애니(idle/walk) NPC 부착 + 대화 정면 카메라 확대 —
    /// 소스 문자열 어서션 테스트(DialogueFeedbackTests/RestaurantWindowFigmaTests 선례 — [89차] 마커 + Does.Contain,
    /// Or 체인은 Does.Contain(a).Or.Contain(b) 문법).
    /// 스펙: docs/89_NPC_FKEY_PLAYER_ANIM_PLAN.md §5 ①~⑥.
    ///  ① VillageNpcSpawner — "Animation/Controllers/Player_AC" 부착 + Soldier 폴백 유지
    ///  ② VillagerWanderer — PauseForDialogue/ResumeAfterDialogue + "Player_AC" 계약 주석(>0.55/<0.35)
    ///  ③ PlayerMovement — NPCAmbientDialogue 감지 + RaiseTargetInteraction(TargetKind.NPC + "NPCAmbientDialogue")
    ///  ④ InteractionPanelUTK — IsActionSupported "is NPCAmbientDialogue" + OnTalkRequested 분기(Begin+OpenForVillager+Close)
    ///  ⑤ NPCDialoguePanelUTK — OpenForVillager + PlaceAtAnchor + Talk2
    ///  ⑥ DialogueCameraDirector — Begin(Component, string) 오버로드 + Begin(GuardPlaceholder) 유지(위임)
    ///     + MeasureHeadY/ComputeSightlineDir Transform 시그니처
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class VillageNpcFKeyTests
    {
        // ═══════════════ ① VillageNpcSpawner — 플레이어 애니 부착 ═══════════════

        [Test]
        public void VillageNpcSpawner_PlayerAcAttached_SoldierFallbackKept()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");

            Assert.That(src, Does.Contain("[89차]"), "[89차] 마커 주석.");
            Assert.That(src, Does.Contain("\"Animation/Controllers/Player_AC\""),
                "[89차] 주민 애니 = Resources Player_AC(플레이어와 동일 idle/walk) 로드.");
            Assert.That(src, Does.Contain("Resources.Load<RuntimeAnimatorController>(\"Animation/Controllers/Player_AC\")"),
                "Player_AC RuntimeAnimatorController 로드부.");
            Assert.That(src, Does.Contain("SoldierShield_AC"),
                "로드 실패 폴백 — 기존 SoldierShield_AC 유지(경고 로그).");
            Assert.That(src, Does.Contain("Player_AC/SoldierShield_AC 컨트롤러 로드 실패"),
                "양쪽 로드 실패 경고 로그.");
            Assert.That(src, Does.Contain(">0.55 Walk 진입"), "Player_AC Speed 계약 주석(진입 임계).");
            Assert.That(src, Does.Contain("<0.35 Idle 복귀"), "Player_AC Speed 계약 주석(복귀 임계).");
        }

        // ═══════════════ ② VillagerWanderer — 대화 정지 API + Speed 계약 주석 ═══════════════

        [Test]
        public void VillagerWanderer_PauseResumeDialogue_ApiAndSpeedContract()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/VillagerWanderer.cs");

            Assert.That(src, Does.Contain("[89차]"), "[89차] 마커 주석.");
            Assert.That(src, Does.Contain("public void PauseForDialogue()"),
                "[89차] 대화 정지 API — 배회 정지+Speed 0 즉시.");
            Assert.That(src, Does.Contain("public void ResumeAfterDialogue()"),
                "[89차] 대화 해제 API — 휴식 카운터 재시작.");
            Assert.That(src, Does.Contain("Player_AC"), "Speed 계약 주석 = Player_AC.");
            // Speed 계약 임계 주석 — >0.55 Walk 진입 / <0.35 Idle 복귀(스펙 §1)
            Assert.That(src, Does.Contain(">0.55").Or.Contain("0.55 Walk"),
                "Player_AC Walk 진입 임계(>0.55) 주석.");
            Assert.That(src, Does.Contain("<0.35").Or.Contain("0.35 Idle"),
                "Player_AC Idle 복귀 임계(<0.35) 주석.");
            Assert.That(src, Does.Contain("SetFloat(\"Speed\", 0f)"),
                "PauseForDialogue — Speed 0 즉시(Idle 유지).");
        }

        // ═══════════════ ③ PlayerMovement — F키 주민 감지(P4) ═══════════════

        [Test]
        public void PlayerMovement_FKey_DetectsVillagerAmbientDialogue()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/PlayerMovement.cs");

            Assert.That(src, Does.Contain("[89차 P4]"), "[89차 P4] 마커 주석.");
            Assert.That(src, Does.Contain("GetComponentInParent<NPCAmbientDialogue>()"),
                "P4 — NPCAmbientDialogue 직접 참조 감지(동일 Systems 어셈블리, 리플렉션 불필요).");
            Assert.That(src, Does.Contain("HoverTargetClassifier.TargetKind.NPC, nearestVillager"),
                "RaiseTargetInteraction(TargetKind.NPC, 주민) — 64차 P3 좌클릭 패널과 동일 kind.");
            Assert.That(src, Does.Contain("bool villageNpcRaised = false"),
                "우선순위 계약 — 주민 스캔은 P3(마을NPC) 미발화 시에만(병사>영주>마을NPC>주민).");
            Assert.That(src, Does.Contain("if (!villageNpcRaised)"),
                "P4 게이트 — P3 발화 시 주민 스캔 스킵.");
        }

        // ═══════════════ ④ InteractionPanelUTK — 주민 대화 분기 ═══════════════

        [Test]
        public void InteractionPanelUTK_VillagerTalkBranch_SupportedAndRouted()
        {
            string src = File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");
            string villagerSrc = File.ReadAllText("Assets/Scripts/Systems/NPCAmbientDialogue.cs");

            Assert.That(src, Does.Contain("[89차]"), "[89차] 마커 주석.");
            // IsActionSupported — 대화하기 주민 지원
            Assert.That(src, Does.Contain("target is NPCAmbientDialogue"),
                "IsActionSupported \"대화하기\" — target is NPCAmbientDialogue → true.");
            // OnTalkRequested 주민 분기 — ①Begin ②Close ③BeginDialoguePose ④OpenForVillager
            Assert.That(src, Does.Contain("DialogueCameraDirector.Begin(villager, villager.DisplayName)"),
                "① 정면 카메라 확대 — Begin(npc, 표시명)(영주/병사와 동일 경험).");
            Assert.That(src, Does.Contain("Instance?.Close()"),
                "② 패널 겹침 방지 — 기존 Close 계약.");
            Assert.That(src, Does.Contain("villager.BeginDialoguePose()"),
                "③ 대화 자세 — 배회 정지+Talk 트리거(DialogueEnded → ResumeAfterDialogue).");
            Assert.That(src, Does.Contain("NPCDialoguePanelUTK.OpenForVillager(villager)"),
                "④ NPC 대화 패널 개방(대상 앞 배치).");
            // NPCAmbientDialogue 신설 API 계약
            Assert.That(villagerSrc, Does.Contain("public string DisplayName"),
                "NPCAmbientDialogue.DisplayName — NpcName 우선/objectName 폴백.");
            Assert.That(villagerSrc, Does.Contain("public string GetInteractionLine()"),
                "NPCAmbientDialogue.GetInteractionLine — 데이터/폴백 1행 대사.");
            Assert.That(villagerSrc, Does.Contain("public void BeginDialoguePose()"),
                "NPCAmbientDialogue.BeginDialoguePose — PauseForDialogue+Talk 트리거.");
            Assert.That(villagerSrc, Does.Contain("public void EndDialoguePose()"),
                "NPCAmbientDialogue.EndDialoguePose — 구독 해제+ResumeAfterDialogue.");
            Assert.That(villagerSrc, Does.Contain("DialogueCameraDirector.DialogueEnded -= OnDialogueEndedForPose"),
                "DialogueEnded 구독 전 해제(1회성 계약).");
            Assert.That(villagerSrc, Does.Contain("데이터 미구성 폴백"),
                "GetInteractionLine 폴백 위조 금지 표기 — 데이터 미구성 폴백.");
        }

        // ═══════════════ ⑤ NPCDialoguePanelUTK — OpenForVillager ═══════════════

        [Test]
        public void NPCDialoguePanelUTK_OpenForVillager_ChoicesAndAnchor()
        {
            string src = File.ReadAllText("Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs");

            Assert.That(src, Does.Contain("[89차]"), "[89차] 마커 주석.");
            Assert.That(src, Does.Contain("public static void OpenForVillager(ProjectName.Systems.NPCAmbientDialogue npc)"),
                "OpenForVillager 신설 — OpenForGuard 패턴 준용.");
            Assert.That(src, Does.Contain("_instance.PlaceAtAnchor(npc.transform)"),
                "개방 시 대상 앞 배치(PlaceAtAnchor — OpenForGuard 동일 패턴).");
            Assert.That(src, Does.Contain("\"마을 주민 — 잡담\""),
                "2행 보조행 — \"마을 주민 — 잡담\"(#8B949E).");
            Assert.That(src, Does.Contain("\"근처 소식 있어?\"").Or.Contain("\"잘 지내?\""),
                "주민 선택지 3개 계열(안녕하세요./근처 소식 있어?/잘 지내?).");
            Assert.That(src, Does.Contain("_villagerNpc.SetTalkBTrigger()"),
                "선택지 클릭 → npc SetTalkBTrigger(Animator \"Talk2\" — Player_AC TalkB).");
            Assert.That(src, Does.Contain("_npcLineOne.text = _villagerNpc.GetInteractionLine()"),
                "선택지 클릭 → 1행 응답 갱신(GetInteractionLine 재호출).");
            // 기존 계약 불변
            Assert.That(src, Does.Contain("public static void Open(NPCInstance npc)"),
                "기존 Open(NPCInstance) 계약·시그니처 불변.");
            Assert.That(src, Does.Contain("public static void OpenForGuard(ProjectName.Systems.GuardPlaceholder guard)"),
                "기존 OpenForGuard 계약·시그니처 불변.");
            Assert.That(src, Does.Contain("_instance._villagerNpc = null"),
                "비주민 경로(Open/OpenForGuard) 주민 모드 해제 — 모드 누수 방지.");
        }

        // ═══════════════ ⑥ DialogueCameraDirector — Component 일반화 ═══════════════

        [Test]
        public void DialogueCameraDirector_ComponentOverload_GuardDelegationKept()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");

            Assert.That(src, Does.Contain("[89차]"), "[89차] 마커 주석.");
            Assert.That(src, Does.Contain("public static void Begin(Component target, string displayName)"),
                "Begin(Component, string) 신설 오버로드 — 정면 확대 로직 전이.");
            Assert.That(src, Does.Contain("public static void Begin(GuardPlaceholder target)"),
                "Begin(GuardPlaceholder) 유지 — InteractionPanelUTK \"Begin(talkGuard)\" 문자열 계약.");
            Assert.That(src, Does.Contain("Begin(target, target.GuardName)"),
                "병사 경로 → 일반화 오버로드 위임(로직 단일소스).");
            Assert.That(src, Does.Contain("public static event System.Action<Component> DialogueStarted"),
                "DialogueStarted — Action<Component> 일반화(소비처 0 실측).");
            Assert.That(src, Does.Contain("public static event System.Action<Component> DialogueEnded"),
                "DialogueEnded — Action<Component> 일반화(소비처 0 실측).");
            Assert.That(src, Does.Contain("private static float MeasureHeadY(Transform target)"),
                "MeasureHeadY — Transform 시그니처 일반화(로직 무변경).");
            Assert.That(src, Does.Contain("private static Vector3 ComputeSightlineDir(Transform target, Vector3 pos)"),
                "ComputeSightlineDir — Transform 시그니처 일반화(로직 무변경).");
            Assert.That(src, Does.Contain("public static Component Target"),
                "Target 프로퍼티 — Component 일반화(소비처 0 실측 — 안전).");
            Assert.That(src, Does.Contain("var watchGuard = _target as GuardPlaceholder"),
                "Update 사망 감시 — guard일 때만 IsDead, 그 외 파괴(null) 감시만.");
        }

        // ═══════════════ [92차] 믹사모 FBX 클립 전환 + 모델별 보행 정합 ═══════════════

        /// <summary>
        /// [92차] ①~④ + 선택 클립 로그 — VillageNpcSpawner 믹사모 오버라이드 경로.
        /// 컨트롤러 YAML guid 역조회 실측: Walk 상태 모션 = MeshyUser/Walking.fbx
        /// "Armature|Walking_withSkin", Idle 상태 모션 = MeshyUser/Idle_02.fbx "Armature|Idle_02".
        /// </summary>
        [Test]
        public void VillageNpcSpawner_MixamoOverrideController_LoadMatchAndFallback()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");

            Assert.That(src, Does.Contain("[92차]"), "[92차] 마커 주석.");
            // ② Resources.LoadAll 경로 — Resources/Animations/Villagers_Mixamo 폴더 이동(런타임 로드 계약)
            Assert.That(src, Does.Contain("Resources.LoadAll<AnimationClip>(\"Animations/Villagers_Mixamo\")"),
                "[92차] 믹사모 클립 폴더 레벨 LoadAll 경로.");
            // ③ walk/idle 이름 매칭 코드(lowercase contains — 모호 시 첫 번째 폴백)
            Assert.That(src, Does.Contain("FirstClipNameContaining(clips, \"walk\")"),
                "[92차] walk 키워드 매칭 호출.");
            Assert.That(src, Does.Contain("FirstClipNameContaining(clips, \"idle\")"),
                "[92차] idle 키워드 매칭 호출.");
            // 실측 보강 — 믹사모 테이크명 "mixamo.com" 대응 이차 판정(소스 FBX 파일명 개별 로드)
            Assert.That(src, Does.Contain("Animations/Villagers_Mixamo/Walking"),
                "[92차] mixamo.com 테이크명 이차 판정 — Walking.fbx 경로 로드.");
            Assert.That(src, Does.Contain("Animations/Villagers_Mixamo/Idle"),
                "[92차] mixamo.com 테이크명 이차 판정 — Idle.fbx 경로 로드.");
            // [128-2 P2] 캐릭터별 클립 소스계약 — Npc2026 폴더/스템 파일별 LoadAll(_walk/_idle/_talk)
            Assert.That(src, Does.Contain("Npc2026"),
                "[128-2 P2] 캐릭터별 클립 리소스키 Npc2026 프리픽스 계약.");
            Assert.That(src, Does.Contain("\"_walk\""),
                "[128-2 P2] 캐릭터 walk 클립 — 파일별 LoadAll(stem + _walk) 계약.");
            Assert.That(src, Does.Contain("LoadAll<AnimationClip>"),
                "[128-2 P2] 캐릭터별 클립 — Resources LoadAll<AnimationClip> 소스계약.");
            Assert.That(src, Does.Contain("믹사모 클립 — walk='"),
                "[92차] 선택 클립명 로그 1회 — walk='<이름>' idle='<이름>' 형식.");
            // ① AnimatorOverrideController 생성 코드(런타임 API — 컨트롤러 에셋 무수정)
            Assert.That(src, Does.Contain("new AnimatorOverrideController(baseCtrl)"),
                "[92차] Player_AC 베이스 오버라이드 컨트롤러 런타임 생성.");
            Assert.That(src, Does.Contain("overrideCtrl.GetOverrides(pairs)"),
                "[92차] GetOverrides — 슬롯 페어 조회.");
            Assert.That(src, Does.Contain("overrideCtrl.ApplyOverrides(pairs)"),
                "[92차] ApplyOverrides — Walk/Idle 슬롯 교체 적용.");
            // ④ 폴백 가드 — 클립 둘 중 하나라도 null이면 오버라이드 미사용(기존 Player_AC 그대로)
            Assert.That(src, Does.Contain("if (_mixamoWalk == null || _mixamoIdle == null)"),
                "[92차] 폴백 가드 — null 검사.");
            Assert.That(src, Does.Contain("믹사모 클립 로드 실패"),
                "[92차] 폴백 경고 로그.");
            Assert.That(src, Does.Contain(": playerCtrl"),
                "[92차] 폴백 할당 — 기존 Player_AC 유지.");
        }

        /// <summary>[92차] ⑤⑥ — 슬롯 매칭 2종 한정(TalkA/Talk2 제외)+ walkSpeed 모델 테이블/전달.</summary>
        [Test]
        public void VillageNpcSpawner_MixamoSlotsWalkIdleOnly_TalkKept()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");

            // ⑥ [128-2 P2] TalkA/TalkB 슬롯 오버라이드 적용 — 캐릭터별 Talk/idle로 교체(정책 변경)
            Assert.That(src, Does.Contain("PlayerAcWalkSlotClipName = \"Armature|Walking_withSkin\""),
                "[92차] Walk 슬롯 클립명 실측(Player_AC Walk 상태 모션 — guid 역조회).");
            Assert.That(src, Does.Contain("PlayerAcIdleSlotClipName = \"Armature|Idle_02\""),
                "[92차] Idle 슬롯 클립명 실측(Player_AC Idle 상태 모션 — guid 역조회).");
            Assert.That(src, Does.Contain("TalkA/TalkB 슬롯 오버라이드 적용"),
                "[128-2 P2] Talk 슬롯 오버라이드 적용 마커(캐릭터별 Talk/idle 교체 계약).");
            // 오버라이드는 런타임 스왑 — 에디터 API(에셋 수정 경로) 부재
            Assert.That(src, Does.Not.Contain("AssetDatabase"),
                "[92차] 에셋 수정 경로 부재 — 컨트롤러 에셋 무수정 계약.");
        }

        /// <summary>[92차] ⑤ 모델별 보폭-속도 정합 — walkSpeed 테이블/파라미터 전달/계약 무변경.</summary>
        [Test]
        public void VillagerWanderer_ModelWalkSpeedTable_PassedFromSpawner()
        {
            string wandererSrc = File.ReadAllText("Assets/Scripts/Systems/VillagerWanderer.cs");
            string spawnerSrc = File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");

            Assert.That(wandererSrc, Does.Contain("[92차]"), "[92차] 마커 주석.");
            Assert.That(wandererSrc, Does.Contain("WalkSpeedDefault = 0.95f"),
                "[100차 F-3] 성인 기본값 1.4→0.95 하향(클립 유효 보행속도 실측 0.81~1.05 반영 — 92차 테이블 계약진화).");
            Assert.That(wandererSrc, Does.Contain("WalkSpeedGirl = 0.85f"),
                "[100차 F-3] girl 기본값 1.25→0.85 하향(클립 유효 보행속도 실측 반영 — 92차 테이블 계약진화).");
            Assert.That(wandererSrc, Does.Contain("modelKey.StartsWith(\"girl\")"),
                "[92차] girl 계열 매칭 — 테이블 선택([100차 F-3] 캐시 우선 경로와 병존).");
            Assert.That(wandererSrc, Does.Contain("영상 재실측 후"),
                "[100차 F-3] 초기값 = 영상 재실측 후 확정치(클립 유효 보행속도 실측 반영 주석 동반).");
            Assert.That(wandererSrc, Does.Contain("[VillagerWanderer] 보행 정합 — model="),
                "[92차] 보행 정합 로그 — model=<키> walkSpeed=<값>.");
            // anim.speed는 1.0 유지(필드만 준비) + Speed 파라미터 계약 무변경
            Assert.That(wandererSrc, Does.Contain("_animSpeed = 1.0f"),
                "[92차] anim.speed 예약 필드 1.0 유지.");
            Assert.That(wandererSrc, Does.Contain("_anim.SetFloat(\"Speed\", _walking ? 1f : 0f);"),
                "[92차] Speed 파라미터 계약 무변경(값 1/0 플립 — >0.55 Walk/<0.35 Idle).");
            // 전달 경로 — 스폰 직후 모델키 전달(기존 Init 결정론 시드 계약 문자열 유지)
            Assert.That(spawnerSrc, Does.Contain("wanderer.SetWalkModel(modelKey);"),
                "[92차] 스폰 경로 walkSpeed 모델 키 전달.");
            Assert.That(spawnerSrc, Does.Contain("wanderer.Init(center, villageRadius, SeedBase + GetHash($\"{modelKey}_{npcName}\"));"),
                "[87차] 기존 Init 계약 유지(시드 문자열 변경 없음).");
        }

        // ═══════════════ [92차 후속] 비대 bounds 후속 수리 F-B/F-C/F-D ═══════════════

        /// <summary>
        /// [92차 후속 F-B] 주민 스폰 anim.cullingMode=AlwaysAnimate — 비대 bounds 컬링 오판 근절.
        /// 근거: 기본 CullUpdateTransforms가 렌더러 bounds로 오프스크린을 판정하는데 주민
        /// SkinnedMeshRenderer bounds 비대(center.y≈6.8·size.y≈13m 추정)가 오판 → 애니 스킵·
        /// transform 이동 지속 = 걷기 포즈 동결+슬라이드(영상 10~18s 실측). Player_AC 사용 계약과 동일 수준.
        /// </summary>
        [Test]
        public void VillageNpcSpawner_AlwaysAnimateCulling_92Followup()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");

            Assert.That(src, Does.Contain("[92차 후속 F-B]"), "[92차 후속 F-B] 마커 주석.");
            Assert.That(src, Does.Contain("anim.cullingMode = AnimatorCullingMode.AlwaysAnimate"),
                "[92차 후속 F-B] 주민 스폰 Animator cullingMode=AlwaysAnimate(비대 bounds 컬링 오판 근절).");
            Assert.That(src, Does.Contain("포즈 동결"),
                "[92차 후속 F-B] 근거 주석(걷기 포즈 동결+슬라이드 — 영상 10~18s 실측) 유지.");
            Assert.That(src, Does.Contain("Player_AC 사용 계약"),
                "[92차 후속 F-B] Player_AC 사용 계약(플레이어)과 동일 수준 주석 유지.");
        }

        /// <summary>
        /// [151차 P2] 계약 진화 — 구 [92차 후속 F-C] 대화창 앵커 1.2~2.2m 클램프(비대 bounds 방어)는
        /// 항상 중앙 하단 고정 일원화로 폐기(앵커 투영 경로 소멸 — 클램프/프록시/상수 동반 소멸).
        /// TargetScreenProjector는 무수정(공용 계약) — 구 경로는 폐기 회귀 핀으로 전환(102차 후속 선례).
        /// </summary>
        [Test]
        public void NPCDialoguePanelUTK_AnchorClampPathRetired_AlwaysBottomCenter_151()
        {
            string src = File.ReadAllText("Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs");

            Assert.That(src, Does.Contain("[151차 P2]"), "[151차 P2] 마커 주석.");
            // 구 [92차 후속 F-C] 클램프 경로 폐기 회귀 핀 — 상수/산식/프록시 잔존 금지.
            Assert.That(src, Does.Not.Contain("AnchorClampMinM"),
                "[151차 P2] 구 앵커 클램프 하한 상수 폐기(투영 경로 소멸 — 잔존 금지).");
            Assert.That(src, Does.Not.Contain("AnchorClampMaxM"),
                "[151차 P2] 구 앵커 클램프 상한 상수 폐기(투영 경로 소멸 — 잔존 금지).");
            Assert.That(src, Does.Not.Contain("Mathf.Clamp(rawY"),
                "[151차 P2] 구 bounds 산출 앵커 높이 클램프 폐기.");
            Assert.That(src, Does.Not.Contain("EnsureAnchorProxy"),
                "[151차 P2] 구 클램프 투영 프록시 경로 폐기.");
            // [154차 P1] 항상 중앙 하단 고정 — bottom-up 0.15 입력(위에서 85% = 중앙 하단).
            //   구 [151차 P2] 0.85 입력은 ApplyAnchorLayout의 투영용 y 반전을 이중 통과해 상단 고정을 유발 — 수리.
            Assert.That(src, Does.Contain("Screen.width * 0.5f, Screen.height * 0.15f"),
                "[154차 P1] 배치 산식 — bottom-up 0.15(위에서 85% = 중앙 하단 고정).");
        }

        /// <summary>
        /// [92차 후속 F-D] VillagerWanderer 이동 직전 전방 레이캐스트 회피 — 건물/주민 관통 완화
        /// (transform 직접 이동은 충돌 무시 — 마을씬 테스트 3 포렌식 5~6번 타일 실측).
        /// 레이캐스트 비용 최소화 — 0.25s 주기 캐시(매 프레임 폐기).
        /// </summary>
        [Test]
        public void VillagerWanderer_ForwardRaycastAvoidance_92Followup()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/VillagerWanderer.cs");

            Assert.That(src, Does.Contain("[92차 후속 F-D]"), "[92차 후속 F-D] 마커 주석.");
            Assert.That(src, Does.Contain("Physics.Raycast(transform.position + Vector3.up * AvoidRayHeight"),
                "[92차 후속 F-D] 이동 직전 전방 레이캐스트(pos + up×0.9m, forward).");
            Assert.That(src, Does.Contain("AvoidRayDistance = 1.2f"),
                "[92차 후속 F-D] 회피 판정 전방 거리 1.2m.");
            Assert.That(src, Does.Contain("AvoidRayHeight = 0.9f"),
                "[92차 후속 F-D] 레이 원점 높이 0.9m(흉부 라인).");
            Assert.That(src, Does.Contain("ObstacleMask = 1 << 0"),
                "[92차 후속 F-D] 장애물 레이어 = Default 건물 레이어(ConstructionManager 배치 판정은 데이터 기반 — grep 실측).");
            Assert.That(src, Does.Contain("AvoidRayInterval = 0.25f"),
                "[92차 후속 F-D] 레이캐스트 주기 캐시 0.25s(매 프레임 폐기 — 비용 최소화).");
            Assert.That(src, Does.Contain("RerouteAroundObstacle"),
                "[92차 후속 F-D] 히트 시 목표 방향 재선정(35~65° 좌우 교대 회전 — 반경 밖 시 PickTarget 폴백).");
            Assert.That(src, Does.Contain("관통"),
                "[92차 후속 F-D] 근거 주석(건물/주민 관통 완화) 유지.");
        }

        // ═══════════════ [94차 F4] F키 즉시 정지 브리지 + 정면 앵커 정합 + 로직 테스트 ═══════════════

        /// <summary>
        /// [94차 F4] 소스 어서션 — ①PlayerMovement F키 브리지(정지 API 호출 전달 — Systems 내 캐스트로
        /// VillagerWanderer 접근, 최소 변경) ②EInteractionPromptUTK 정면 앵커 정합(패널과 동일 산식 —
        /// bounds center + forward×0.6m, 높이 클램프 1.2~2.2m; 비NPC 기존 상면 앵커 무변경).
        /// </summary>
        [Test]
        public void PlayerMovement_FKeyPauseBridge_And_EPromptFrontAnchor_94()
        {
            string pmSrc = File.ReadAllText("Assets/Scripts/Systems/PlayerMovement.cs");
            Assert.That(pmSrc, Does.Contain("[94차 F4]"), "[94차 F4] 마커 주석.");
            Assert.That(pmSrc, Does.Contain("private static void PauseTargetWanderForPanel"),
                "[94차 F4] 정지 API 호출 전달 브리지 메서드 존재.");
            Assert.That(pmSrc, Does.Contain("villager.GetComponent<VillagerWanderer>()"),
                "[94차 F4] 캐스트로 VillagerWanderer 접근(Systems 내 직접 — 최소 변경).");
            Assert.That(pmSrc, Does.Contain("wanderer.PauseForDialogue();"),
                "[94차 F4] 정지 API 전달(PauseForDialogue 재사용 — 멱등).");
            // 호출 위치 — F키 주민 분기에서 패널 개방 이벤트 전에 전달(즉시 정지 계약).
            int bridge = pmSrc.IndexOf("PauseTargetWanderForPanel(nearestVillager);");
            int raise = pmSrc.IndexOf("HoverTargetClassifier.TargetKind.NPC, nearestVillager");
            Assert.That(bridge, Is.GreaterThan(0), "[94차 F4] F키 주민 분기 브리지 호출.");
            Assert.That(raise, Is.GreaterThan(bridge), "[94차 F4] 정지 전달이 패널 개방 이벤트 선행(즉시 정지).");
            Assert.That(pmSrc, Does.Contain("배회 로직 없음"),
                "[94차 F4] 마을NPC 실측 주석(TerritoryNPCBehaviour 정적 — 정지 API 불요) 존재.");

            string epSrc = File.ReadAllText("Assets/Scripts/UI/Toolkit/EInteractionPromptUTK.cs");
            Assert.That(epSrc, Does.Contain("[94차 F4]"), "[94차 F4] 마커 주석.");
            Assert.That(epSrc, Does.Contain("FrontOffsetM = 0.6f"), "[94차 F4] 정면 오프셋 0.6m — 패널과 동일 상수.");
            Assert.That(epSrc, Does.Contain("AnchorClampMinM = 1.2f"), "[94차 F4] 높이 클램프 하한 1.2m 정합.");
            Assert.That(epSrc, Does.Contain("AnchorClampMaxM = 2.2f"), "[94차 F4] 높이 클램프 상한 2.2m 정합.");
            Assert.That(epSrc, Does.Contain("IsFrontAnchorCandidate"), "[94차 F4] NPC 계열 정면 앵커 판정.");
            Assert.That(epSrc, Does.Contain("TryProjectFrontAnchor"), "[94차 F4] 정면 앵커 투영 경로.");
            Assert.That(epSrc, Does.Contain("TargetScreenProjector.TryProject(_screenCandidate.tr, AnchorGapM"),
                "기존 상면 앵커 경로 유지(비NPC 회귀 방지 — [94차 F4] NPC 한정 적용).");
        }

        /// <summary>
        /// [94차 F4 → 96차 F2] 로직 테스트 — 주민 대상 패널 정지/재개 경로: InteractionPanelUTK.PauseTargetWander →
        /// VillagerWanderer.PauseForDialogue(_paused=true), EnsureResumeOnAnyClose(96차 수렴 개명) → ResumeAfterDialogue(_paused=false).
        /// 프라이빗 메서드 리플렉션 + 컴포넌트 주입(기존 스타일 — 73차/93차 선례). UI 패널 개방 전체
        /// (RebuildActions/UIRoot 의존) 회피 — 정지/재게 경로만 직접 검증. _rng는 컴포넌트 주입
        /// (ResumeAfterDialogue의 IdleDuration 요구 — Init 경로 대체, EditMode Awake 미호출 규약).
        /// </summary>
        [Test]
        public void InteractionPanelUTK_WanderPauseResume_VillagerState_94()
        {
            var villagerGo = new GameObject("F94_Villager");
            try
            {
                var npc = villagerGo.AddComponent<NPCAmbientDialogue>();   // EditMode — Awake 미호출 규약
                var wanderer = villagerGo.AddComponent<VillagerWanderer>();
                typeof(VillagerWanderer).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(wanderer, new System.Random(7));   // 컴포넌트 주입 — IdleDuration 결정론 시드

                InteractionPanelUTK.Ensure();
                var panel = InteractionPanelUTK.Instance;
                Assert.That(panel, Is.Not.Null, "패널 싱글턴 확보(Ensure — Bootstrap과 동일 경로).");

                var pause = typeof(InteractionPanelUTK).GetMethod("PauseTargetWander",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var resume = typeof(InteractionPanelUTK).GetMethod("EnsureResumeOnAnyClose",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(pause, Is.Not.Null, "[94차 F4] PauseTargetWander 존재.");
                Assert.That(resume, Is.Not.Null, "[94차 F4 → 96차 F2] EnsureResumeOnAnyClose 존재(닫힘 전 경로 수렴 개명).");

                pause.Invoke(panel, new object[] { npc });
                Assert.That(GetWanderPaused(wanderer), Is.True,
                    "[94차 F4] 패널 개방 정지 — VillagerWanderer.PauseForDialogue(_paused=true).");

                resume.Invoke(panel, null);
                Assert.That(GetWanderPaused(wanderer), Is.False,
                    "[94차 F4] 패널 닫힘 재개 — ResumeAfterDialogue(_paused=false).");

                // 멱등 — 재정지/재재개 반복 무해(대화하기 클릭 경로 중복 호출 계약).
                pause.Invoke(panel, new object[] { npc });
                Assert.That(GetWanderPaused(wanderer), Is.True, "[94차 F4] 재정지 멱등(중복 Pause 무해).");
                resume.Invoke(panel, null);
                Assert.That(GetWanderPaused(wanderer), Is.False, "[94차 F4] 재재개 멱등(중복 Resume 무해).");
            }
            finally
            {
                Object.DestroyImmediate(villagerGo);
            }
        }

        /// <summary>
        /// [100차 F-1] 소스 어서션 — F키 상호작용 패널 머리 위 부착 계약. 근거: 패널 하단이 NPC 목/가슴
        /// 라인을 겹쳐 가리는 실측. 수리 = ①머리 위 앵커(rawY = bounds maxY + HeadAnchorUpM 0.25m — 구
        /// bounds center + 15% 산식 폐기, bounds 부재 시 2.05m 폴백, 클램프 1.2~2.2m 유지) ②배치 산식
        /// (패널 하단 중앙 = 앵커 screenPx.y − panelH − HeadAnchorGapPx, x 중앙 정렬 — 화면 클램프 유지)
        /// ③시맨틱 판정(패널하단중앙↔머리앵커 거리 ≤ HeadAttachMaxDistancePx 24px = 부착(성공) — 부착
        /// 검증 로그 병기).
        /// [101차 F-1] 계약진화 — 앵커 소스 교체(정면앵커/재투영 경로 폐기 → 본체 단일 앵커): 구
        /// HeadAnchorUpM 0.25m/rawY 산식/높이 클램프(1.2~2.2m) 단정은 본체 앵커 산식 단정으로 승계
        /// (간격 6px/시맨틱 판정 24px/배치 산식/로그 병기는 유지 — 상세 신규 단정은 101F1 전용 테스트).
        /// </summary>
        [Test]
        public void InteractionPanelUTK_HeadAboveAttach_Contract_100F1()
        {
            string src = File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");

            Assert.That(src, Does.Contain("[100차 F-1]"), "[100차 F-1] 마커 주석.");

            // ① 상수 — [101차 F-1] 계약진화: 머리 앵커 상승분 0.25m(bounds maxY 기준) 폐기 → 본체 앵커
            //   고도 1.95m(Foot 본 y 기준). 간격 6px/시맨틱 부착 판정 24px는 유지.
            Assert.That(src, Does.Contain("BodyAnchorFootUpM = 1.95f"),
                "[101차 F-1] 본체 앵커 고도 1.95m(Foot 본 y 기준 — 머리 위, 구 HeadAnchorUpM 0.25m 계약진화).");
            Assert.That(src, Does.Contain("HeadAnchorGapPx = 6f"),
                "[100차 F-1] 패널 하단 ↔ 머리앵커 투영점 간격 6px.");
            Assert.That(src, Does.Contain("HeadAttachMaxDistancePx = 24f"),
                "[100차 F-1] 시맨틱 부착 판정 거리 24px(≤24px = 부착 성공).");

            // ② 본체 단일 앵커 산식 — [101차 F-1] 계약진화: bounds 중심 x + Foot 본 y + 1.95m(머리 위),
            //   Foot 부재 시 bounds maxY + 0.15m 폴백. 정면 오프셋/높이 클램프(1.2~2.2m) 폐기 — 본체 단일
            //   앵커 계약(패널 경로 한정 — E키 프롬프트 기존 클램프는 무변경).
            Assert.That(src, Does.Contain("Vector3 anchor = new Vector3(baseXZ.x, ComputeBodyAnchorHeight(tracked, b, has), baseXZ.z);"),
                "[101차 F-1] 본체 단일 앵커 — bounds 중심 x + 본체 앵커 고도(Foot 본 y + 1.95m) 합성.");
            Assert.That(src, Does.Contain("return footY + BodyAnchorFootUpM;"),
                "[101차 F-1] 본체 앵커 고도 — Foot 본 y + 1.95m(단일소스 ComputeBodyAnchorHeight).");
            Assert.That(src, Does.Contain("return b.max.y + BodyAnchorBoundsPadM;"),
                "[101차 F-1] Foot 본 부재 폴백 — bounds maxY + 0.15m(스펙 지정).");

            // ③ 배치 산식 — y = 앵커 screenPx.y − panelH − HeadAnchorGapPx(x 중앙 정렬 — 화면 클램프 유지).
            Assert.That(src, Does.Contain("Mathf.Clamp(scaledPoint.y - panelBounds.height - HeadAnchorGapPx"),
                "[100차 F-1] 패널 하단 = 머리앵커 투영점 − panelH − 6px(화면 클램프 유지 산식).");

            // ④ 시맨틱 부착 판정 — 패널하단중앙 ↔ 머리앵커 거리 ≤24px + 부착 검증 로그 병기.
            Assert.That(src, Does.Contain("semantic = headDist <= HeadAttachMaxDistancePx"),
                "[100차 F-1] 시맨틱 판정 — 패널하단중앙↔머리앵커 거리 ≤24px = 부착(성공).");
            Assert.That(src, Does.Contain("{verdict} [100차 F-1] {semantic}{reasonSuffix}"),
                "[100차 F-1] 부착 검증 로그에 시맨틱 판정 병기(머리부착 Npx 성공/실패 — [99차 F-A] 사유 병기 승계).");
        }

        /// <summary>
        /// [101차 F-1] 소스 어서션 — 프롬프트 본체 단일 앵커 + x축 판정 신설. 근거: 테스트씬 15 실측 —
        /// 시맨틱 판정(머리부착)이 배치에 쓴 같은 앵커끼리의 거리라 자기참조 성공(화면은 패널이 NPC 측면).
        /// 수리 = ①정면앵커/재투영 경로 폐기 → 본체 단일 앵커(x = NPC 렌더러 bounds 중심 투영 x, y =
        /// Foot 본 y + 1.95m 투영 y — 머리 위) ②본체 실측 대조 판정(몸중심 x 편차 ≤20px AND
        /// 패널하단↔머리 y ≤24px — 앵커 좌표와 본체 실측 좌표를 별도 산출해 대조).
        /// </summary>
        [Test]
        public void InteractionPanelUTK_BodyAnchor_And_XAxisVerdict_101F1()
        {
            string src = File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");

            Assert.That(src, Does.Contain("[101차 F-1]"), "[101차 F-1] 마커 주석.");

            // ① 본체 단일 앵커 — 정면앵커/재투영 경로 폐기 수렴.
            Assert.That(src, Does.Contain("private static bool TryProjectBodyAnchor(Component tracked, out Vector2 screen)"),
                "[101차 F-1] 본체 단일 앵커 투영 경로(정면앵커/눌러투영 폐기 — 단일 앵커 수렴).");
            Assert.That(src, Does.Contain("BodyAnchorFootUpM = 1.95f"),
                "[101차 F-1] 본체 앵커 고도 1.95m(Foot 본 y 기준 — 머리 위).");
            Assert.That(src, Does.Contain("BodyAnchorBoundsPadM = 0.15f"),
                "[101차 F-1] Foot 본 부재 폴백 — bounds maxY + 0.15m.");
            Assert.That(src, Does.Contain("private static bool TryMeasureFootBoneY(Component target, out float footY)"),
                "[101차 F-1] Foot 본 y 실측(foot/toe 포함 본 최저 y — 폴백 근거 산출).");

            // ② 본체 실측 대조 판정 — x축 판정 신설(자기참조 방지).
            Assert.That(src, Does.Contain("private static bool TryProjectBodyMeasuredPoint(Vector3 world, out Vector2 screen)"),
                "[101차 F-1] 본체 실측 직접 투영 — 앵커 경유와 별도 산출(자기참조 방지).");
            Assert.That(src, Does.Contain("BodyCenterMaxDeviationPx = 20f"),
                "[101차 F-1] 몸중심 x 편차 판정 ≤20px(x축 판정 신설).");
            Assert.That(src, Does.Contain("bool attached = devX <= BodyCenterMaxDeviationPx && devY <= HeadAttachMaxDistancePx;"),
                "[101차 F-1] 본체부착 판정 — x 편차 ≤20px AND y ≤24px(AND 성립 = 성공).");
            Assert.That(src, Does.Contain("ComputeBodyAttachVerdict(measuredTarget, bottomCenterX, bottomCenterY)"),
                "[101차 F-1] 본체 실측 대조 판정 병기 배선(LogPlacementVerify).");
            Assert.That(src, Does.Contain("[101차 F-1] {bodyVerdict}"),
                "[101차 F-1] 부착 검증 로그에 본체 실측 대조 판정 병기.");
        }

        /// <summary>[94차 F4] VillagerWanderer._paused 리플렉션 판독(정지 상태 실측).</summary>
        private static bool GetWanderPaused(VillagerWanderer wanderer) =>
            (bool)typeof(VillagerWanderer).GetField("_paused", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(wanderer);
    }
}
