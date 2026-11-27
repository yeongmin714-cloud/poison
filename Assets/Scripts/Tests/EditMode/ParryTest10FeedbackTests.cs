using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [패링D] Test_10 패링 체인 발화·인지 계약 — 사용자 보고("Test_10에서 패링 고품질화가 전혀 실행된 게 없다") 대응.
    /// 정적 실측: 체인 본체는 95차 F1~F5+159차 P3로 구현 완료(PlayerCombat.TryParry/FX, AnimalAI/AfterlifeEnemy
    /// 텔레그래프, PlayerHealth.TryParryReflection) — 본 테스트는 ①체인 존재 핀 ②V 개방 즉시 피드백 [패링D1]
    /// ③Test_10 훈련 안내 [패링D2]를 계약한다. 소스 계약(문자열 어서션) 원칙 — InteriorInteractionTests 선례.
    /// </summary>
    public class ParryTest10FeedbackTests
    {
        [Test]
        public void ParryChain_ImplExists_InPlayerCombatAndAi()
        {
            string combat = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(combat, Does.Contain("public bool TryParry()"),
                "패링 판정 진입점(TryParry) 존재 — PlayerHealth.TryParryReflection 반사 경로의 목적지.");
            Assert.That(combat, Does.Contain("HitStopManager.RequestHitStop"),
                "[159차 P3] 패링 히트스톱(HitStopManager) 존재 — 고품질화 구현 핀.");
            Assert.That(combat, Does.Contain("TryParryImpactFX(grade, kbDir)"),
                "성공 FX 실행 경로(히트스톱/지면 링/스파클) 호출 존재.");

            string ai = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(ai, Does.Contain("BeginMeleeTelegraph"),
                "[95차 F5] 적 공격 텔레그래프(예고→타격 2페이즈) 존재 — 패링 타이밍 근거.");
            Assert.That(ai, Does.Not.Contain("AttackTelegraphIndicator.Spawn"),
                "[190차+195차 P1] 텔레그래프 지면 인디케이터(적색 링) 시각은 제거 — 패링 타이밍 근거는 예고 2페이즈 판정(BeginMeleeTelegraph)으로 존속.");

            string health = System.IO.File.ReadAllText("Assets/Scripts/Core/PlayerHealth.cs");
            Assert.That(health, Does.Contain("TryParryReflection"),
                "피해 진입점의 패링 반영 판정(TryParryReflection) 존재.");
        }

        [Test]
        public void ParryWindowOpen_HasImmediateFeedback()
        {
            // [패링D1] V 입력 → 창 개방 즉시 청색 소형 링+로그 — 입력만으로 시스템 생존 판별 가능.
            string combat = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(combat, Does.Contain("패링 창 개방"),
                "[패링D1] 창 개방 로그 마커 존재.");
            int tryStart = combat.IndexOf("private bool TryStartParry()");
            int cue = combat.IndexOf("[패링D1]", System.StringComparison.Ordinal);
            Assert.That(tryStart, Is.GreaterThan(0), "TryStartParry 존재.");
            Assert.That(cue, Is.GreaterThan(tryStart),
                "[패링D1] 피드백 블록이 TryStartParry 내부(성공 경로)에 위치.");
            Assert.That(combat.IndexOf("return true;", tryStart), Is.GreaterThan(cue),
                "피드백 후 성공 반환 — 개방 실패 경로와 무간섭.");
        }

        [Test]
        public void Test10_ParryTrainingGuide_Logged()
        {
            // [패링D2] Test_10 셋업 완료 시 패링 훈련 경로 안내 로그 — 사용자 검증 절차 명시.
            string setup = System.IO.File.ReadAllText("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            Assert.That(setup, Does.Contain("패링 훈련 안내"),
                "[패링D2] Test_10 패링 훈련 안내 로그 존재.");
            Assert.That(setup, Does.Contain("[패링D2]"),
                "[패링D2] 마커 존재.");
        }

        [Test]
        public void Test10_StealthBoot_CallWired_InSetupPlayer()
        {
            // [211차] Test_10 C키 은신 무음 수리 핀 — SetupPlayer가 EnsureStealthSystem을 호출하고,
            // 메서드 본체가 Player에 StealthSystem을 AddComponent하는 배선을 계약한다.
            // 근거: Test_10 씬 StealthSystem 0건(guid 9a0a2d1e53c8dfd46b3c27a40c9964ef) →
            // PlayerMovement.cs:494-495 C키 토글이 Instance null 가드로 무음 스킵되던 실측.
            string setup = System.IO.File.ReadAllText("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");

            int setupPlayerIdx = setup.IndexOf("private void SetupPlayer()", System.StringComparison.Ordinal);
            int callIdx = setup.IndexOf("EnsureStealthSystem();", System.StringComparison.Ordinal);
            int defIdx = setup.IndexOf("private void EnsureStealthSystem()", System.StringComparison.Ordinal);
            Assert.That(setupPlayerIdx, Is.GreaterThan(0), "SetupPlayer 존재.");
            Assert.That(callIdx, Is.GreaterThan(setupPlayerIdx),
                "EnsureStealthSystem() 호출이 SetupPlayer(플레이어 생성 경로) 이후에 위치 — TestAllInOneSetup.cs:85 선례(플레이어 생성 직후).");
            Assert.That(defIdx, Is.GreaterThan(callIdx),
                "private EnsureStealthSystem() 정의 존재(호출부와 별개 토큰 — 정의가 호출 뒤에 배치된 실제 구조).");

            int addCompIdx = setup.IndexOf("player.AddComponent<StealthSystem>();", System.StringComparison.Ordinal);
            Assert.That(addCompIdx, Is.GreaterThan(defIdx),
                "부트 본체가 Player에 StealthSystem을 AddComponent — 중복 가드(Instance null 검사) 후 부착.");
            Assert.That(setup.IndexOf("StealthSystem.Instance != null", System.StringComparison.Ordinal),
                Is.GreaterThan(defIdx),
                "중복 가드 존재 — 씬에 이미 StealthSystem이 있으면 스킵(TestAllInOneSetup.cs:568 선례).");
        }

        [Test]
        public void StealthSystem_BootContract_InstanceAssignedInAwake()
        {
            // [211차] AddComponent만으로 부트되는 계약 — StealthSystem.Awake가 Instance를 할당해야
            // SetupPlayer 중 AddComponent 즉시 C키 토글 경로(PlayerMovement.cs:494)가 활성된다.
            string stealth = System.IO.File.ReadAllText("Assets/Scripts/Systems/StealthSystem.cs");
            Assert.That(stealth, Does.Contain("public static StealthSystem Instance"),
                "싱글턴 Instance 존재 — C키/물약 소비처의 단일 진입점.");
            int awakeIdx = stealth.IndexOf("private void Awake()", System.StringComparison.Ordinal);
            int assignIdx = stealth.IndexOf("Instance = this;", System.StringComparison.Ordinal);
            Assert.That(awakeIdx, Is.GreaterThan(0), "Awake 존재.");
            Assert.That(assignIdx, Is.GreaterThan(awakeIdx),
                "Awake 내 Instance 할당 — AddComponent 즉시 부트(외부 초기화 불요).");

            string movement = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerMovement.cs");
            Assert.That(movement, Does.Contain("StealthSystem.Instance.ToggleStealth();"),
                "C키 소비처 존재 — 시스템 부착만으로 은신 토글 발화(부완성 배선 확인).");
        }
    }
}
