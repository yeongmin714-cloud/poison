using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [156차 P5] 곡갱이질 FBX 수용·연동 계약 테스트 — 에셋계약 + 소스계약.
    /// 진실 출처: docs/plans/2026-11-06_cave-floor-dirt-safety-scale4-pickaxe-fbx-plan.md Phase P5.
    ///  · 계약 1/2: Player_AC "Mine" 파라미터/State 존재 + 모션 소스(곡갱이질 FBX) 단언, Till 모션 승계 단언
    ///    — PlayerRollRunRebindTests(132차) 에셋계약 선례: AssetDatabase 직접 로드, 에셋 패치 후 GREEN
    ///    (에셋 패치 = ProjectName.EditorTools.Player156AnimTools.RunAll 유니티 배치 실행).
    ///  · 계약 3/4: TriggerMine 발화 배선 — Farm153ChannelFeedbackTests/PlayerActionAnimContractTests
    ///    소스계약 선례(File.ReadAllText + Does.Contain + 지역 슬라이스) 준용.
    /// </summary>
    public class Player156PickaxeAnimTests
    {
        private const string ControllerPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
        private const string PickaxeFbxPath = "Assets/플레이어 애니메이션/곡갱이질+망치질.fbx";
        private const string DriverPath = "Assets/Scripts/Systems/HumanoidClipDriver.cs";
        private const string NodePath = "Assets/Scripts/Systems/ResourceNode.cs";

        /// <summary>[156차 P5] 계약 1 — MineState_AssetContract: "Mine" Trigger 파라미터 + "Mine" State 존재,
        /// 모션 소스가 곡갱이질 FBX 내장 클립(GetAssetPath 역조회 — 임시/타 클립 잔존 방지),
        /// AnyState→Mine(If Mine, canTransitionToSelf=false) + Mine→Idle(Exit Time) 배선 존재.</summary>
        [Test]
        public void MineState_AssetContract_156()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null, $"[156차 P5] {ControllerPath} 로드 실패.");

            Assert.That(controller.parameters.Any(p =>
                    p.name == "Mine" && p.type == AnimatorControllerParameterType.Trigger),
                Is.True, "[156차 P5] 'Mine' Trigger 파라미터 부재(에셋 패치(Player156AnimTools.RunAll) 후 GREEN).");

            var root = controller.layers[0].stateMachine;
            var mine = root.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == "Mine");
            Assert.That(mine, Is.Not.Null, "[156차 P5] 'Mine' State 부재(에셋 패치 후 GREEN).");

            // 모션 소스 단언 — 상태 모션 클립의 소유 에셋 = 곡갱이질 FBX.
            var motion = mine.motion as AnimationClip;
            Assert.That(motion, Is.Not.Null, "[156차 P5] 'Mine' State 모션(AnimationClip) 부재.");
            Assert.That(AssetDatabase.GetAssetPath(motion), Is.EqualTo(PickaxeFbxPath),
                "[156차 P5] Mine 모션 소스 = 곡갱이질 FBX 내장 클립이어야 한다.");

            bool anyMine = root.anyStateTransitions.Any(t =>
                t.destinationState != null && t.destinationState.name == "Mine"
                && t.conditions.Any(c => c.mode == AnimatorConditionMode.If && c.parameter == "Mine")
                && !t.canTransitionToSelf);
            Assert.That(anyMine, Is.True,
                "[156차 P5] AnyState→Mine(If Mine, canTransitionToSelf=false) 전이 부재.");

            var idle = root.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == "Idle");
            Assert.That(idle, Is.Not.Null, "[156차 P5] Idle State 부재(선행 계약).");
            bool mineToIdle = mine.transitions.Any(t =>
                t.destinationState != null && t.destinationState.name == "Idle" && t.hasExitTime);
            Assert.That(mineToIdle, Is.True, "[156차 P5] Mine→Idle Exit Time 전이 부재.");
        }

        /// <summary>[156차 P5] 계약 2 — TillMotionSource_AssetContract: Till 상태 모션이 곡갱이질 FBX 소스로
        /// 교체되었는지 단언(153차 임시 클립 Male_Bend_Over_Pick_Up 승계 완료 판정 — 에셋 패치 후 GREEN).</summary>
        [Test]
        public void TillMotionSource_AssetContract_156()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null, $"[156차 P5] {ControllerPath} 로드 실패.");

            var till = controller.layers[0].stateMachine.states
                .Select(s => s.state).FirstOrDefault(s => s != null && s.name == "Till");
            Assert.That(till, Is.Not.Null, "[156차 P5] 'Till' State 부재(153차 계약).");

            var motion = till.motion as AnimationClip;
            Assert.That(motion, Is.Not.Null, "[156차 P5] 'Till' State 모션 부재.");
            Assert.That(AssetDatabase.GetAssetPath(motion), Is.EqualTo(PickaxeFbxPath),
                "[156차 P5] Till 모션 소스 = 곡갱이질 FBX(153차 임시 클립 승계)이어야 한다.");
        }

        /// <summary>[156차 P5] 계약 3 — TriggerMineWiring_SourceContract: 드라이버 TriggerMine 신설 +
        /// ResourceNode 채굴 성공 경로(플레이어 경로) 발화 존재. TriggerTill 테스트 패턴(소스계약) 준용.</summary>
        [Test]
        public void TriggerMineWiring_SourceContract_156()
        {
            var driverSrc = System.IO.File.ReadAllText(DriverPath);
            Assert.That(driverSrc, Does.Contain("public void TriggerMine"),
                "[156차 P5] TriggerMine(파라미터 가드) 존재.");

            // 채굴 성공 경로(HarvestForPlayer 본문) 지역 슬라이스 — HarvestCheer_SourceContract_130 선례.
            var src = System.IO.File.ReadAllText(NodePath);
            int begin = src.IndexOf("public void HarvestForPlayer()");
            Assert.That(begin, Is.GreaterThan(0), "[156차 P5] HarvestForPlayer 존재.");
            int next = src.IndexOf("private void EnsureStump()", begin);
            string body = next > begin ? src.Substring(begin, next - begin) : src.Substring(begin);
            Assert.That(body, Does.Contain("TriggerMine"),
                "[156차 P5] 채굴 성공 경로(플레이어) TriggerMine 발화 존재.");
        }

        /// <summary>[156차 P5] 계약 4 — SoldierAutoMineExcluded_SourceContract: 병사 TryAutoMine 경로는
        /// 곡갱이질 발화(TriggerMine/HumanoidClipDriver) 무배선 — 플레이어 경로 전용 계약(130차 선례).</summary>
        [Test]
        public void SoldierAutoMineExcluded_SourceContract_156()
        {
            var src = System.IO.File.ReadAllText(NodePath);
            int begin = src.IndexOf("public bool TryAutoMine(");
            Assert.That(begin, Is.GreaterThan(0), "[156차 P5] TryAutoMine 존재.");
            int next = src.IndexOf("public bool TryRollRareBonus(", begin);
            string body = next > begin ? src.Substring(begin, next - begin) : src.Substring(begin);
            Assert.That(body, Does.Not.Contain("TriggerMine"),
                "[156차 P5] 병사 TryAutoMine — TriggerMine 무배선(플레이어 경로 전용).");
            Assert.That(body, Does.Not.Contain("HumanoidClipDriver"),
                "[156차 P5] 병사 TryAutoMine — 드라이버 접근 없음.");
        }
    }
}
