using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [72차 Phase B] 병사 상호작용 패널 액션 5종 활성화 + 대상 앞 배치 + 이동 명령 링 1s 소멸 + 걷기 클립 교체 계약.
    /// 계획서: docs/2026-10-15_72TH_SOLDIER_STATUS_PANELS_PLAN.md Phase B.
    /// EditMode 소스 어서션 원칙 — SelectionRingFadeTests/GoldEconomyTests 선례(런타임 시뮬 불가 계약).
    /// </summary>
    public class InteractionActionActivationTests
    {
        private const string PanelPath = "Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs";
        private const string InteractPath = "Assets/Scripts/UI/Toolkit/SoldierInteractUTK.cs";
        private const string MarkerPath = "Assets/Scripts/Systems/CommandMarker.cs";
        private const string NewClipGuid = "8b7edae5b7999564bac5c390964fcd7e";
        private const string OldRunBackGuid = "282509bf5e5b14c4ab3fa55034a88be9";
        private const string OldWalkBackGuid = "ba58acae7504290428e85b46edc6096a";

        // ── 1. IsActionSupported — 병사 5종 활성화 ──

        [Test]
        public void IsActionSupported_SoldierFiveActions_Markers()
        {
            var src = System.IO.File.ReadAllText(PanelPath);
            Assert.That(src, Does.Contain("[72차] 병사 액션 5종 활성화"),
                "72차 병사 액션 활성화 마커.");
            // 대화하기 — 병사(GuardPlaceholder) 분기 신설(기존 NPC/영주 로직 보존).
            Assert.That(src, Does.Contain("if (target is GuardPlaceholder talkGuard)"),
                "대화하기 병사 분기.");
            // 뇌물주기 — 적병사 전용(!IsAlly).
            Assert.That(src, Does.Contain("return target is GuardPlaceholder bribeGuard && !bribeGuard.IsAlly;"),
                "뇌물주기는 !IsAlly 가드.");
            // 포섭하기 — 병사 공통.
            Assert.That(src, Does.Contain("return target is GuardPlaceholder recruitGuard;"),
                "포섭하기 병사 분기.");
            // 물약주기/음식주기 — 아군 전용(IsAlly).
            Assert.That(src, Does.Contain("return target is GuardPlaceholder itemGuard && itemGuard.IsAlly;"),
                "물약/음식주기는 IsAlly 가드.");
            // 기존 NPC/영주 로직 무변경 — 대화하기 NPC 분기와 영주 Encounter 분기 잔존.
            Assert.That(src, Does.Contain("target is TerritoryNPCBehaviour npc"),
                "NPC 대화 분기 유지.");
            Assert.That(src, Does.Contain("target is InvadedLordEncounter invadedLord"),
                "영주 대화 분기 유지.");
        }

        // ── 2. OnActionRequested — 병사 액션 라우팅(구창 래퍼 경유) ──

        [Test]
        public void OnActionRequested_SoldierRouting_UsesLegacyWrappers()
        {
            var src = System.IO.File.ReadAllText(PanelPath);
            Assert.That(src, Does.Contain("talkGuard.BeginTalk();"),
                "대화하기 → GuardPlaceholder.BeginTalk().");
            Assert.That(src, Does.Contain("bribeGuard.AttemptBribe();"),
                "뇌물주기 → GuardPlaceholder.AttemptBribe().");
            Assert.That(src, Does.Contain("recruitGuard.BeginRecruit();"),
                "포섭하기 → GuardPlaceholder.BeginRecruit().");
            Assert.That(src, Does.Contain("SoldierInteractUTK.ItemMode.Drug"),
                "물약주기 → 구창 아이템 모드 Drug.");
            Assert.That(src, Does.Contain("SoldierInteractUTK.ItemMode.Food"),
                "음식주기 → 구창 아이템 모드 Food.");
            Assert.That(src, Does.Contain("SoldierInteractUTK.OpenWithItemMode(itemGuard, itemMode);"),
                "물약/음식주기 → SoldierInteractUTK 정적 경유 개방.");

            // 구창 진입점 — public 정적 OpenWithItemMode + public enum ItemMode + ShowItemSection 경유.
            var interact = System.IO.File.ReadAllText(InteractPath);
            Assert.That(interact, Does.Contain("public enum ItemMode { None, Food, Drug, Equipment }"),   // [202차] 장비 착용 모드 추가 — 72차 시그니처 핀 갱신
                "ItemMode public 승격(CS0051 방지 — public 정적 진입점 파라미터).");
            Assert.That(interact, Does.Contain("public static void OpenWithItemMode(GuardPlaceholder guard, ItemMode mode)"),
                "정적 경유 개방 진입점.");
            Assert.That(interact, Does.Contain("_instance.ShowItemSection(mode);"),
                "개방 후 ShowItemSection(음식/약 선택 UI) 즉시 전환.");
        }

        // ── 3. 대상 바로 앞 배치 + 이동 추적 ──

        [Test]
        public void PanelPlacement_AtTarget_WithOneSecondTracking()
        {
            var src = System.IO.File.ReadAllText(PanelPath);
            Assert.That(src, Does.Contain("private void PlaceAtTarget(object target)"),
                "대상 기준 배치 메서드 신설.");
            // [101차 F-1] 본체 단일 앵커 계약 갱신 — [94차 F4] 정면앵커(프록시 경유)는 폐기되고
            //   TryProjectBodyAnchor(렌더러 없는 프록시 Transform 경유)로 치환(bounds 중심 x + Foot 본 y + 1.95m 머리 위).
            Assert.That(src, Does.Contain("if (!TryProjectBodyAnchor(tracked, out Vector2 screen))"),
                "[101차 F-1] 본체 단일 앵커 투영 — PlaceAtTarget → TryProjectBodyAnchor(프록시 Transform 경유 — [94차 F4] 정면앵커 폐기 승계).");
            // 공용 유틸 경유 유지 — 프록시 지점을 TargetScreenProjector.TryProject에 실어 투영
            //   (z>0 가드/카메라 폴백 일원화 계약 승계 — 직접 WorldToScreenPoint 폐기 유지).
            Assert.That(src, Does.Contain("TargetScreenProjector.TryProject(_bodyAnchorProxy, 0f, out screen, out _)"),
                "[101차 F-1] 공용 TargetScreenProjector.TryProject 경유 유지(프록시 Transform 실어 투영).");
            Assert.That(src, Does.Contain("[94차 F4]"), "[94차 F4] 정면 앵커 마커 주석.");
            Assert.That(src, Does.Contain("[101차 F-1]"), "[101차 F-1] 본체 단일 앵커 마커 주석.");
            Assert.That(src, Does.Contain("Vector3.up * 0.9f"),
                "대상 위 0.9m 오프셋 상수(구 Vector3.up * 0.9f 승계).");
            Assert.That(src, Does.Contain("(Screen.height - screen.y)"),
                "y축 반전 산식 유지(165-166행 계약).");
            Assert.That(src, Does.Contain("panelBounds.width * 0.5f"),
                "패널 하단 중앙 정렬(중앙 X 보정).");
            Assert.That(src, Does.Contain("private const long PlacePollMs = 1000L;"),
                "이동 추적 폴링 1000ms.");
            Assert.That(src, Does.Contain("schedule.Execute(PlacementTick).Every(PlacePollMs)"),
                "schedule.Every 폴링 등록.");
            Assert.That(src, Does.Contain("if (tracked is GuardPlaceholder guard && !guard.IsAlive)"),
                "대상 사망 시 패널 닫힘.");

            // 기존 마우스 위치 배치 폐기 — Input.mousePosition 잔존 금지.
            Assert.That(src, Does.Not.Contain("Input.mousePosition"),
                "마우스 위치 배치는 폐기(대상 기준으로 교체).");
        }

        // ── 4. CommandMarker — 이동 명령 링 1s 소멸 ──

        [Test]
        public void CommandMarker_PersistentRing_FadesAfterOneSecond()
        {
            var src = System.IO.File.ReadAllText(MarkerPath);
            Assert.That(src, Does.Contain("private float _life = 1f;"),
                "[72차] 표시 후 1s — 유저 규약.");
            Assert.That(src, Does.Contain("private const float FadeSeconds = 0.3f;"),
                "축소 페이드 0.3s — 67차 SelectionRingController 규약 동일.");
            Assert.That(src, Does.Contain("(_elapsed - _life) / FadeSeconds"),
                "표시(_life) 경과 후 페이드 구간 진입.");
            // 도착 소멸 조건 폐기 — 유저가 "표시 후 1초 뒤" 명시.
            Assert.That(src, Does.Not.Contain("HasCommand"),
                "도착(명령 해제) 소멸 조건은 폐기.");
            // [무번호 19-F2 계약 갱신] _groupOwners는 도착 진행도 "추적"용으로 허용 — 사용자 clarify 확정
            //   ("1초 규약 유지 — 1s 내 병사 접근 시 링 축소(진행도)만 시각화, 도착 소멸은 미적용").
            //   [72차]의 본래 금지 의도 = 그룹 도착 기반 "소멸" — 도착 임계 Destroy 트리거만 계속 금지.
            Assert.That(src, Does.Contain("_groupOwners"),
                "[무번호 19-F2] 도착 진행도 추적 그룹 보존(소멸과 무관 — 1s 규약 단일화 유지).");
            Assert.That(src, Does.Not.Contain("progress >= 1f"),
                "도착 임계 소멸 트리거 금지 — 소멸은 1s 규약 단일화([72차] 유지).");
            Assert.That(src, Does.Contain("도착 소멸은 미적용"),
                "[무번호 19-F2] 도착 소멸 미적용 계약 마커.");
        }

        // ── 5. 3종 컨트롤러 Move 클립 교체(방향/원위치) ──

        [Test]
        public void SoldierControllers_MoveClip_ReplacedWithRunFInPlace()
        {
            string[] controllers =
            {
                "Assets/Resources/Animation/Controllers/SoldierShield_AC.controller",
                "Assets/Resources/Animation/Controllers/SoldierArcher_AC.controller",
                "Assets/Resources/Animation/Controllers/SoldierGreatSword_AC.controller",
            };
            foreach (var path in controllers)
            {
                var yaml = System.IO.File.ReadAllText(path);
                Assert.That(yaml, Does.Contain("guid: " + NewClipGuid),
                    $"{path} — Move state가 OneHand_Up_Run_F_InPlace(전진 러닝 원위치)로 배선되어야 한다.");
                Assert.That(yaml, Does.Not.Contain("guid: " + OldRunBackGuid),
                    $"{path} — 뒤로 러닝 클립(OneHand_Up_Run_B) 잔존 금지.");
                Assert.That(yaml, Does.Not.Contain("guid: " + OldWalkBackGuid),
                    $"{path} — 뒤로 걷기 클립(OneHand_Up_Walk_B) 잔존 금지.");
                Assert.That(yaml, Does.Contain("m_Name: Move"), $"{path} — Move state 유지.");
                Assert.That(yaml, Does.Contain("m_SpeedParameter: MoveRate"),
                    $"{path} — MoveRate 배속 파라미터 무변경(61차 계약).");
            }

            // 교체 대상 클립 진실치 — 메타 guid 대조.
            var clipMetaPath = "Assets/DoubleL/Demo/Anim/OneHand_Up_Run_F_InPlace.anim.meta";
            Assert.That(System.IO.File.Exists(clipMetaPath), Is.True,
                "OneHand_Up_Run_F_InPlace 애셋 존재.");
            var meta = System.IO.File.ReadAllText(clipMetaPath);
            Assert.That(meta, Does.Contain("guid: " + NewClipGuid),
                "교체 guid는 실제 클립 메타의 guid와 일치.");
        }

        // ── 6. [125차 후속9] 대화하기 Test_15 통일 — 아레나 관리인·영주 딸(대화카메라 정면+대화창) ──

        [Test]
        public void DialogueUnification_ArenaManager_And_LordDaughter_UseTest15Pattern()
        {
            var src = System.IO.File.ReadAllText(PanelPath);

            // ① 아레나 베팅 관리인 — 카메라 확대 선행 + OpenForStatic 대화창(구 토스트 전용 경로 폐기).
            Assert.That(src, Does.Contain("DialogueCameraDirector.Begin(arenaMgrTalk, \"아레나 베팅 관리인\");"),
                "[125차 후속9] 관리인 대화카메라 — 제네릭 오버로드(전신 정면, Test_15 동일 계열).");
            Assert.That(src, Does.Contain("NPCDialoguePanelUTK.OpenForStatic(arenaMgrTalk"),
                "[125차 후속9] 관리인 대화창 개방 — OpenForStatic(선택지 3개, 대상 앞 배치).");
            Assert.That(src, Does.Not.Contain("UTKToastService.Show(arenaMgrTalk.Greeting"),
                "[125차 후속9] 관리인 토스트 전용 경로 폐기 — 인사는 대화창 1행으로 승계.");

            // ② 영주 딸 — 동일 3단계(카메라+Close+대화창), 1행=GetGreetingBody(이름 접두사 제외).
            Assert.That(src, Does.Contain("DialogueCameraDirector.Begin(daughterTalk, daughterTalk.DisplayName);"),
                "[125차 후속9] 딸 대화카메라 — 병사·주민·관리인과 동일 계열.");
            Assert.That(src, Does.Contain("NPCDialoguePanelUTK.OpenForStatic(daughterTalk"),
                "[125차 후속9] 딸 대화창 개방 — OpenForStatic.");
            Assert.That(src, Does.Contain("daughterTalk.GetGreetingBody()"),
                "[125차 후속9] 딸 인사 본문 — GetGreetingBody(이름행 중복 방지).");
            Assert.That(src, Does.Not.Contain("UTKToastService.Show(daughterTalk.GetGreetingLine()"),
                "[125차 후속9] 딸 토스트 전용 경로 폐기.");

            // ③ 활성 게이트 — IsActionSupported "대화하기"에 관리인 분기 신설(버튼 SetEnabled(true) 도달 계약).
            Assert.That(src, Does.Contain("[125차 후속9] 아레나 베팅 관리인 — Test_15 통일 경로"),
                "[125차 후속9] IsActionSupported 관리인 대화하기 활성 게이트 마커.");

            // ④ NPCDialoguePanelUTK — 제네릭 정적 NPC 개방 진입점 계약(OpenForGuard/OpenForVillager 계열).
            var panelSrc = System.IO.File.ReadAllText(
                "Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs");
            Assert.That(panelSrc, Does.Contain(
                    "public static void OpenForStatic(Component target, string displayName, string npcLine, string subLine = null)"),
                "[125차 후속9] OpenForStatic — 정적 NPC 대화창 진입점(주민 모드 해제 + 대상 앞 배치).");
            Assert.That(panelSrc, Does.Contain("_instance._villagerNpc = null;   // [125차 후속9]"),
                "[125차 후속9] 정적 경로 주민 모드 해제 — SelectChoice 비주민 계약 유지.");

            // ⑤ LordDaughterNPC — GetGreetingBody 신설(GetGreetingLine 원문 계약 불변).
            var daughterSrc = System.IO.File.ReadAllText(
                "Assets/Scripts/Systems/LordDaughterNPC.cs");
            Assert.That(daughterSrc, Does.Contain("public string GetGreetingBody()"),
                "[125차 후속9] GetGreetingBody — \"이름: \" 접두사 제거 본문(불일치 시 원문 폴백).");
            Assert.That(daughterSrc, Does.Contain("public string GetGreetingLine()"),
                "[126차] GetGreetingLine 원문 계약 유지(접두사 포함 — 기존 소비처 보호).");
        }
    }
}
