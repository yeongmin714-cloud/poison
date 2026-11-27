using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [69차+이동통일] 실내 플레이어 F/E 상호작용 스캔 계약 — [이동통일 B1/B4]로 PlayerMovement 단일 경로 통일 +
    /// 월드 플레이어(PlayerMovement) F스캔 Lord 감지 통일 + 영주 근접 F 프롬프트 등록.
    /// OverlapSphere 시뮬 불가 → 소스 계약(문자열 어서션) 원칙 — SelectionRingFadeTests/HudBottomAnchorTests 선례.
    /// </summary>
    public class InteriorInteractionTests
    {
        private const string PlayerMovementPath = "Assets/Scripts/Systems/PlayerMovement.cs";
        private const string IndoorWarSetupPath = "Assets/Scripts/Systems/IndoorWarTestSetup.cs";
        private const string LordInteractionSetupPath = "Assets/Scripts/Systems/LordInteractionTestSetup.cs";

        [Test]
        public void IndoorSetups_InteractionUnify_UsePlayerMovement()
        {
            // [이동통일 B1/B4] 구 PlayerCastleInteriorTestMovement 폐기 — 실내 3셋업(Test_16/17·성 플레이테스트)
            //   전부 PlayerMovement 통일. E(침대)/F(병사·영주) 상호작용은 PM.HandleInteraction 동일 계약으로 승계.
            string pmSrc = System.IO.File.ReadAllText(PlayerMovementPath);
            Assert.That(pmSrc, Does.Contain("private void HandleInteraction()"),
                "PM 상호작용 스캔 진입점(HandleInteraction) — 69차 TestMovement 계약 승계.");
            Assert.That(pmSrc, Does.Contain("bed.OnInteract()"),
                "E키 침대 감지 → Bed.OnInteract()(잠자기/세이브 체인) 계약.");
            Assert.That(pmSrc, Does.Contain("SoldierInteractBridge.RaiseTargetInteraction"),
                "Systems는 UI 참조 불가 → 브리지 이벤트 경유 계약(SoldierInteractBridge).");
            string[] setups =
            {
                "Assets/Scripts/Systems/PlayerCastleInteriorPlaytest.cs",
                IndoorWarSetupPath,
                LordInteractionSetupPath,
            };
            foreach (var path in setups)
            {
                string src = System.IO.File.ReadAllText(path);
                Assert.That(src, Does.Contain("AddComponent<PlayerMovement>()"),
                    $"{path} — [이동통일 B1] PlayerMovement 통일.");
                Assert.That(src, Does.Not.Contain("PlayerCastleInteriorTestMovement"),
                    $"{path} — 구 전용 컨트롤러 잔존 금지(폐기).");
            }
        }

        [Test]
        public void PlayerMovement_69thP2_FScanDetectsLord()
        {
            // [69차 P2] 월드 플레이어 F스캔에도 영주(InvadedLordEncounter) 감지 — 좌클릭과 F키 통일.
            string src = System.IO.File.ReadAllText(PlayerMovementPath);
            Assert.That(src, Does.Contain("[69차 P2]"),
                "PlayerMovement F스캔 Lord 감지 블록 마커.");
            Assert.That(src, Does.Contain("TargetKind.Lord, encounter"),
                "병사 미발견 분기 → 영주 최근접 RaiseTargetInteraction(Lord, encounter) 계약.");
            Assert.That(src, Does.Contain("GetComponentInParent<InvadedLordEncounter>()"),
                "영주 감지는 InvadedLordEncounter 부모 체인 검색(직접 참조 — 동일 어셈블리).");
        }

        [Test]
        public void LordPrompts_69thP3_RegisteredInBothIndoorSetups()
        {
            // [69차 P3] Test_16/17 영주 배치 직후 근접 F 프롬프트 등록 — 63차 라벨 패턴 통일.
            string indoorWar = System.IO.File.ReadAllText(IndoorWarSetupPath);
            Assert.That(indoorWar, Does.Contain("F키를 눌러 영주와 대화"),
                "IndoorWarTestSetup(Test_16) 영주 배치 → 근접 F 프롬프트 등록.");
            Assert.That(indoorWar, Does.Contain("EInteractionPromptBridge.RaiseRegister(lord.transform"),
                "프롬프트 앵커는 인스턴스화된 영주 Transform(lord.transform)이어야 한다.");

            string lordInteraction = System.IO.File.ReadAllText(LordInteractionSetupPath);
            Assert.That(lordInteraction, Does.Contain("F키를 눌러 영주와 대화"),
                "LordInteractionTestSetup(Test_17) 영주 배치 → 근접 F 프롬프트 등록.");
            Assert.That(lordInteraction, Does.Contain("EInteractionPromptBridge.RaiseRegister(lord.transform"),
                "프롬프트 앵커는 인스턴스화된 영주 Transform(lord.transform)이어야 한다.");
        }

        [Test]
        public void PlayerMovement_KeysContract_UsesWasPressedThisFrame()
        {
            // [이동통일 B1] 눌린 순간 1회만 반응 — PM HandleInteraction 키 계약(wasPressedThisFrame).
            //   구 TestMovement 계약 승계(실내 3셋업도 PM 단일 경로).
            string src = System.IO.File.ReadAllText(PlayerMovementPath);
            Assert.That(src, Does.Contain("eKey.wasPressedThisFrame"),
                "E키(침대 세이브)는 눌린 순간 1회만 반응해야 한다.");
            Assert.That(src, Does.Contain("fKey.wasPressedThisFrame"),
                "F키(영주/병사/NPC 패널)는 눌린 순간 1회만 반응해야 한다.");
        }
    }
}
