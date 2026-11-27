using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 87] 아레나 맨손 격투 계약 — SoldierFist_AC 배선(상태 9종·Humanoid 리타깃 클립·모션 승계) +
    /// 듀엘 결합 소스 계약(결정론 클립 선정·수비 반응 개편·폴백 경로). 데미지/베팅 수치 불변 — 애니 레이어만.
    /// </summary>
    public class ArenaFistContractTests
    {
        const string FistCtrlPath = "Assets/Resources/Animation/Controllers/SoldierFist_AC.controller";
        const string ShieldCtrlPath = "Assets/Resources/Animation/Controllers/SoldierShield_AC.controller";
        static string Read(string p) => System.IO.File.ReadAllText(p);

        static AnimatorController Fist() =>
            AssetDatabaseLoad(FistCtrlPath);
        static AnimatorController Shield() =>
            AssetDatabaseLoad(ShieldCtrlPath);
        static AnimatorController AssetDatabaseLoad(string path) =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

        [Test]
        public void SoldierFist_AC_Wired_9States()
        {
            var ctrl = Fist();
            Assert.That(ctrl, Is.Not.Null, "[87] SoldierFist_AC 존재");
            var sm = ctrl.layers[0].stateMachine;
            var names = sm.states.Select(s => s.state?.name).ToHashSet();
            foreach (var n in new[] { "Idle", "Move", "Hit", "Death", "Punch", "Kick", "Headbutt", "PunchKick", "Block" })
                Assert.That(names, Does.Contain(n), "[87] 상태 존재: " + n);

            var pnames = ctrl.parameters.Select(p => p.name).ToHashSet();
            foreach (var n in new[] { "Speed", "MoveRate", "Hit", "Death", "Block" })
                Assert.That(pnames, Does.Contain(n), "[87] 파라미터 존재: " + n);

            Assert.That(sm.defaultState, Is.Not.Null, "[87] 기본 상태 존재");
            Assert.That(sm.defaultState.name, Is.EqualTo("Idle"), "[87] 기본 상태 = Idle");
        }

        [Test]
        public void FistAttackClips_AreHumanoidRetargetable()
        {
            var sm = Fist().layers[0].stateMachine;
            foreach (var n in new[] { "Punch", "Kick", "Headbutt", "PunchKick", "Block" })
            {
                var st = sm.states.Select(s => s.state).First(s => s != null && s.name == n);
                Assert.That(st.motion, Is.Not.Null, "[87] 모션 배선: " + n);
                var clip = st.motion as AnimationClip;
                Assert.That(clip, Is.Not.Null, "[87] 모션이 클립: " + n);
                Assert.That(clip.humanMotion, Is.True, "[87] Humanoid 리타깃 클립(머슬 공간) — 병사 avatar 바인딩 보장: " + n);
            }
        }

        [Test]
        public void WalkTransitions_ThresholdsInherited()
        {
            var sm = Fist().layers[0].stateMachine;
            var idle = sm.states.Select(s => s.state).First(s => s.name == "Idle");
            var move = sm.states.Select(s => s.state).First(s => s.name == "Move");
            var toMove = idle.transitions.FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Move");
            var toIdle = move.transitions.FirstOrDefault(t => t.destinationState != null && t.destinationState.name == "Idle");
            Assert.That(toMove, Is.Not.Null, "[87] Idle→Move 전이");
            Assert.That(toIdle, Is.Not.Null, "[87] Move→Idle 전이");
            Assert.That(toMove.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Greater && System.Math.Abs(c.threshold - 0.5f) < 0.001f),
                Is.True, "[87] Idle→Move Speed>0.50 임계 승계(SoldierShield_AC 실측값)");
            Assert.That(toIdle.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Less && System.Math.Abs(c.threshold - 0.1f) < 0.001f),
                Is.True, "[87] Move→Idle Speed<0.10 임계 승계(SoldierShield_AC 실측값)");
        }

        [Test]
        public void HitDeath_Motions_InheritedFromSoldierShield()
        {
            var fist = Fist().layers[0].stateMachine;
            var shield = Shield().layers[0].stateMachine;
            Assert.That(shield, Is.Not.Null, "[87] SoldierShield_AC 존재(공유 컨트롤러 — 무수정 원칙의 승계 원본)");
            foreach (var n in new[] { "Hit", "Death" })
            {
                var f = fist.states.Select(s => s.state).First(s => s.name == n).motion;
                var g = shield.states.Select(s => s.state).First(s => s.name == n).motion;
                Assert.That(f, Is.Not.Null, "[87] 모션 승계: " + n);
                Assert.That(g, Is.Not.Null, "[87] 소스 모션: " + n);
                Assert.That(f.name, Is.EqualTo(g.name), "[87] Hit/Death 모션 승계 일치: " + n);
            }
        }

        [Test]
        public void ArenaSystem_SourceContract_FistWiring()
        {
            string src = Read("Assets/Scripts/Systems/ArenaBettingSystem.cs");
            Assert.That(src, Does.Contain("[무번호 87]"), "[87] 마커 주석");
            Assert.That(src, Does.Contain("FistAcKey"), "[87] 맨손 컨트롤러 키");
            Assert.That(src, Does.Contain("SoldierShield_AC"), "[87] 폴백 경로 보존");
            Assert.That(src, Does.Contain("ReplayAction"), "[87] 드라이버 무수정 Play 경로");
            Assert.That(src, Does.Contain("FistAttackCycle"), "[87] 결정론 공격 사이클");
            Assert.That(src, Does.Contain("FistComboState"), "[87] 강타 콤보 상태");
            Assert.That(src, Does.Contain("TriggerDeath()"), "[87] KO 쓰러짐");
            Assert.That(src, Does.Contain("TriggerBlock()"), "[87] 약타 막기");
            string drv = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(drv, Does.Contain("public void TriggerBlock()"), "[87] 드라이버 TriggerBlock API");
        }
    }
}
