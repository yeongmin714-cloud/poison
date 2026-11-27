using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 85-P1] 말 에셋 수용 계약 — 리소스 GLB 3종(백마/적토마/황마) + 말 컨트롤러 2종
    /// (백마_AC/적토마_AC — 황마는 애니 FBX 미비로 계획 v2 제외) 4상태 모션 배선.
    /// 에셋 실측: 동일 40노드 리그(쿼드러페드)·임베디드 애니 0·애니 FBX Generic 직결(11-20 파싱).
    /// </summary>
    public class MountAssetContractTests
    {
        const string ResRoot = "Models/UserProvided/말/";

        [Test]
        public void HorseResources_Loadable()
        {
            foreach (var name in new[] { "백마", "적토마", "황마" })   // [166-A] 갈색말 파생 GLB 삭제 — 황마 GLB 사용(갈색말 breed 유지)
            {
                var go = Resources.Load<GameObject>(ResRoot + name);
                Assert.That(go, Is.Not.Null, "[85-P1] 말 리소스 로드: " + name);
            }
        }

        [Test]
        public void HorseControllers_4StatesWired()
        {
            foreach (var ctrlName in new[] { "백마_AC", "적토마_AC", "갈색말_AC" })   // [무번호 91] 갈색말 가산
            {
                var ctrl = AssetDatabaseController(ctrlName);
                Assert.That(ctrl, Is.Not.Null, "[85-P1] 컨트롤러 존재: " + ctrlName);
                var root = ctrl.layers[0].stateMachine;
                foreach (var stateName in new[] { "Idle", "Walk", "Gallop", "Rear" })
                {
                    var st = root.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == stateName);
                    Assert.That(st, Is.Not.Null, "[85-P1] 상태 존재: " + ctrlName + "." + stateName);
                    Assert.That(st.motion, Is.Not.Null, "[85-P1] 모션 배선: " + ctrlName + "." + stateName);
                }
                Assert.That(root.defaultState != null && root.defaultState.name == "Idle",
                    Is.True, "[85-P1] 기본 상태 = Idle: " + ctrlName);
            }
        }

        [Test]
        public void HorseControllers_SpeedTransitionsPaired()
        {
            // §10-4 복귀 전이 쌍 계약 — Idle↔Walk, Walk↔Gallop 양방향 전이 존재
            foreach (var ctrlName in new[] { "백마_AC", "적토마_AC", "갈색말_AC" })   // [무번호 91] 갈색말 가산
            {
                var ctrl = AssetDatabaseController(ctrlName);
                var root = ctrl.layers[0].stateMachine;
                Assert.That(HasTransition(root, "Idle", "Walk") && HasTransition(root, "Walk", "Idle"),
                    Is.True, "[85-P1] Idle↔Walk 전이 쌍: " + ctrlName);
                Assert.That(HasTransition(root, "Walk", "Gallop") && HasTransition(root, "Gallop", "Walk"),
                    Is.True, "[85-P1] Walk↔Gallop 전이 쌍: " + ctrlName);
            }
        }

        static AnimatorController AssetDatabaseController(string name)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/Resources/Animation/Controllers/" + name + ".controller");
        }

        [Test]
        public void HorseBreedSource_BrownMapsToGoldenGlb()
        {
            // [166-A] 갈색말 breed의 resPath=황마 매핑 + 파생 GLB 삭제 소스 계약.
            //   ctrlPath(갈색말_AC)/breed 문자열은 세이브 호환을 위해 유지(166차 Wave A 계획).
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/HorseMountSystem.cs");
            Assert.That(src, Does.Contain("[166-A]"), "166-A 마커 주석");
            Assert.That(src, Does.Contain("Models/UserProvided/말/황마"), "갈색말 breed resPath=황마");
            Assert.That(src, Does.Not.Contain("Models/UserProvided/말/갈색말"), "파생 갈색말 GLB 경로 잔존 금지");
            Assert.That(src, Does.Contain("Animation/Controllers/갈색말_AC"), "컨트롤러 경로 유지(세이브 호환)");
            Assert.That(src, Does.Contain("breed = \"갈색말\""), "breed 문자열 유지(세이브 호환)");

            // 파생 GLB 실삭제 확인(에셋 시스템 부재 시에도 파일 시스템 기준으로 판정).
            Assert.That(System.IO.File.Exists("Assets/Resources/Models/UserProvided/말/갈색말.glb"), Is.False,
                "[166-A] 파생 갈색말 GLB 삭제");
        }

        static bool HasTransition(AnimatorStateMachine root, string from, string to)
        {
            var s = root.states.Select(x => x.state).FirstOrDefault(x => x != null && x.name == from);
            if (s == null) return false;
            return s.transitions.Any(t => t.destinationState != null && t.destinationState.name == to);
        }
    }
}
