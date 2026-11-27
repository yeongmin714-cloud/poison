using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [91차 F6] NPC 휴머노이드 아바타 부트스트랩 계약 (소스 문자열 어서션 — [91차] 마커,
    /// DialogueFeedbackTests/VillageNpcFKeyTests 선례).
    ///
    /// 진단(마을씬 실측): girl 계열 3명 T-pose — SpawnNpc가 루트 전용 GetComponent&lt;Animator&gt;() →
    /// 부재 시 AddComponent(루트) = 아바타 없는 Animator = 휴머노이드 클립 리타겟 불가 = T-pose.
    ///
    ///  ① 매핑 테이블 — 22슬롯(척추 6 + 어깨 2 + 양팔 8 + 양다리/발가락 8)×Rigify 본명(GLB 실측 단일소스)
    ///  ② GetComponentInChildren 재사용 + 아바타 무효 시 AvatarBuilder.BuildHumanAvatar 폴백 순서
    ///  ③ NPC별 진단 로그 문구("애니 부착: ... animator=... avatar=... isHuman=... controller=...")
    ///  ④ Player_AC 경로 유지 + SoldierShield_AC 폴백 유지([89차] 계약)
    ///
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class NpcAnimationAvatarTests
    {
        private static string Src() => System.IO.File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");

        // ── 1. RigifyHumanMap — 22슬롯 × Rigify 본명 (GLB 실측 단일소스) ──

        [Test]
        public void RigifyHumanMap_AllSlots_MeasuredRigifyBoneNames()
        {
            var src = Src();
            Assert.That(src, Does.Contain("[91차 F6]"), "[91차 F6] 마커 주석.");
            Assert.That(src, Does.Contain("static readonly (string humanSlot, string boneName)[] RigifyHumanMap"),
                "[91차 F6] 매핑 테이블 — static readonly 튜플 배열(GLB 7종 공용 단일소스).");

            // 척추 체인 — Hips=spine → Head=spine.005(GLB 실측)
            Assert.That(src, Does.Contain("(\"Hips\", \"spine\")"), "Hips=spine.");
            Assert.That(src, Does.Contain("(\"Spine\", \"spine.001\")"), "Spine=spine.001.");
            Assert.That(src, Does.Contain("(\"Chest\", \"spine.002\")"), "Chest=spine.002.");
            Assert.That(src, Does.Contain("(\"UpperChest\", \"spine.003\")"), "UpperChest=spine.003.");
            Assert.That(src, Does.Contain("(\"Neck\", \"spine.004\")"), "Neck=spine.004.");
            Assert.That(src, Does.Contain("(\"Head\", \"spine.005\")"), "Head=spine.005.");

            // 어깨/양팔 — shoulder/upper_arm/forearm/hand(.L/.R)
            Assert.That(src, Does.Contain("(\"LeftShoulder\", \"shoulder.L\")"), "LeftShoulder=shoulder.L.");
            Assert.That(src, Does.Contain("(\"RightShoulder\", \"shoulder.R\")"), "RightShoulder=shoulder.R.");
            Assert.That(src, Does.Contain("(\"LeftUpperArm\", \"upper_arm.L\")"), "LeftUpperArm=upper_arm.L.");
            Assert.That(src, Does.Contain("(\"RightUpperArm\", \"upper_arm.R\")"), "RightUpperArm=upper_arm.R.");
            Assert.That(src, Does.Contain("(\"LeftLowerArm\", \"forearm.L\")"), "LeftLowerArm=forearm.L.");
            Assert.That(src, Does.Contain("(\"RightLowerArm\", \"forearm.R\")"), "RightLowerArm=forearm.R.");
            Assert.That(src, Does.Contain("(\"LeftHand\", \"hand.L\")"), "LeftHand=hand.L.");
            Assert.That(src, Does.Contain("(\"RightHand\", \"hand.R\")"), "RightHand=hand.R.");

            // 양다리/발가락 — thigh/shin/foot/toe(.L/.R)
            Assert.That(src, Does.Contain("(\"LeftUpperLeg\", \"thigh.L\")"), "LeftUpperLeg=thigh.L.");
            Assert.That(src, Does.Contain("(\"RightUpperLeg\", \"thigh.R\")"), "RightUpperLeg=thigh.R.");
            Assert.That(src, Does.Contain("(\"LeftLowerLeg\", \"shin.L\")"), "LeftLowerLeg=shin.L.");
            Assert.That(src, Does.Contain("(\"RightLowerLeg\", \"shin.R\")"), "RightLowerLeg=shin.R.");
            Assert.That(src, Does.Contain("(\"LeftFoot\", \"foot.L\")"), "LeftFoot=foot.L.");
            Assert.That(src, Does.Contain("(\"RightFoot\", \"foot.R\")"), "RightFoot=foot.R.");
            Assert.That(src, Does.Contain("(\"LeftToes\", \"toe.L\")"), "LeftToes=toe.L.");
            Assert.That(src, Does.Contain("(\"RightToes\", \"toe.R\")"), "RightToes=toe.R.");
        }

        // ── 2. GetComponentInChildren 재사용 + BuildHumanAvatar 폴백 순서 ──

        [Test]
        public void AvatarBootstrap_ReuseChildAnimator_BuildHumanAvatarFallbackOrder()
        {
            var src = Src();
            int reuse = src.IndexOf("npc.GetComponentInChildren<Animator>()");
            int addRoot = src.IndexOf("npc.AddComponent<Animator>()");
            int build = src.IndexOf("AvatarBuilder.BuildHumanAvatar(npc");
            int rebind = src.IndexOf("anim.Rebind()");

            Assert.That(reuse, Is.GreaterThan(0),
                "[91차 F6] ① 프리팹 내 Animator 재사용(GetComponentInChildren — 루트 전용 폐기).");
            Assert.That(addRoot, Is.GreaterThan(reuse),
                "[91차 F6] ③ 루트 Animator 확보(부재 시 AddComponent) — 재사용 판정 이후.");
            Assert.That(build, Is.GreaterThan(addRoot),
                "[91차 F6] ③ AvatarBuilder.BuildHumanAvatar — 아바타 무효 폴백.");
            Assert.That(rebind, Is.GreaterThan(build),
                "[91차 F6] 빌드 아바타 부착 후 Rebind(휴머노이드 리타겟 활성화).");

            // 유효 판정 — avatar.isValid && avatar.isHuman(재사용/빌드 양쪽 경로)
            Assert.That(src, Does.Contain("anim.avatar.isValid").Or.Contain("avatar.isValid"),
                "[91차 F6] ② 아바타 유효 판정(isValid).");
            Assert.That(src, Does.Contain("isHuman"), "[91차 F6] ② 아바타 휴머노이드 판정(isHuman).");

            // HumanDescription — human=HumanBone[](boneName→humanName) / skeleton=SkeletonBone[](전 Transform 로컬 TRS)
            Assert.That(src, Does.Contain("private static HumanDescription CreateRigifyHumanDescription"),
                "[91차 F6] HumanDescription 빌더 — RigifyHumanMap 소비.");
            Assert.That(src, Does.Contain("boneName = RigifyHumanMap[i].boneName"),
                "HumanBone.boneName = Rigify 본명.");
            Assert.That(src, Does.Contain("humanName = RigifyHumanMap[i].humanSlot"),
                "HumanBone.humanName = 휴머노이드 슬롯.");
            Assert.That(src, Does.Contain("position = t.localPosition"),
                "SkeletonBone = 전 Transform 현재 로컬 TRS(position).");
            Assert.That(src, Does.Contain("rotation = t.localRotation"),
                "SkeletonBone = 전 Transform 현재 로컬 TRS(rotation).");
            Assert.That(src, Does.Contain("scale = t.localScale"),
                "SkeletonBone = 전 Transform 현재 로컬 TRS(scale).");
        }

        // ── 3. NPC별 진단 로그 ──

        [Test]
        public void AnimationAttach_Log_OnePerNpc_WithAvatarDiagnostics()
        {
            var src = Src();
            Assert.That(src, Does.Contain("[VillageNpcSpawner] 애니 부착: "),
                "[91차 F6] ④ NPC별 진단 로그 1회 — girls 수리 증명.");
            Assert.That(src, Does.Contain("animator={animatorSrc}"),
                "진단 로그 — animator=root|child.");
            Assert.That(src, Does.Contain("avatar={avatarLabel}").Or.Contain("신규빌드"),
                "진단 로그 — avatar=avatarName|신규빌드.");
            Assert.That(src, Does.Contain("isHuman="), "진단 로그 — isHuman.");
            Assert.That(src, Does.Contain("controller="), "진단 로그 — controller.");
        }

        // ── 4. Player_AC 경로 유지 + SoldierShield 폴백([89차] 계약) ──

        [Test]
        public void PlayerAc_ControllerPath_And_SoldierFallback_Kept()
        {
            var src = Src();
            Assert.That(src, Does.Contain("[89차]"), "[89차] 마커 주석 유지.");
            Assert.That(src, Does.Contain("Resources.Load<RuntimeAnimatorController>(\"Animation/Controllers/Player_AC\")"),
                "[89차] Player_AC 로드 경로 유지.");
            Assert.That(src, Does.Contain("SoldierShield_AC"), "[89차] SoldierShield_AC 폴백 유지.");
            Assert.That(src, Does.Contain("Player_AC/SoldierShield_AC 컨트롤러 로드 실패"),
                "[89차] 양쪽 로드 실패 경고 로그 유지.");
        }
    }
}
