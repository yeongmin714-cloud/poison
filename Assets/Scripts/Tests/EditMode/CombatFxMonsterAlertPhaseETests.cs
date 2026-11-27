using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 19-Phase E] 몬스터 발견 VFX 계약테스트 — E1 발견 ! 링 + E2 발견 SFX + E3 응시 애니.
    /// </summary>
    public class CombatFxMonsterAlertPhaseETests
    {
        /// <summary>[E1] SpawnAlert — 발견 ! 링 스폰 시그니처 존재(3인자, 부채꼴 미사용).</summary>
        [Test]
        public void E1_SpawnAlert_SignatureExists()
        {
            var mi = typeof(AttackTelegraphIndicator).GetMethod("SpawnAlert",
                new[] { typeof(Vector3), typeof(float), typeof(float) });
            Assert.IsNotNull(mi, "SpawnAlert(Vector3, float, float) 오버로드가 존재해야 합니다(무번호 19-E1)");
        }

        /// <summary>[E1] SpawnAlert — 기존 Spawn 경로(공격 텔레그래프) 시그니처 불변 계약 유지.</summary>
        [Test]
        public void E1_ExistingSpawn_SignatureUnchanged()
        {
            var mi3 = typeof(AttackTelegraphIndicator).GetMethod("Spawn",
                new[] { typeof(Vector3), typeof(float), typeof(float) });
            var mi6 = typeof(AttackTelegraphIndicator).GetMethod("Spawn",
                new[] { typeof(Vector3), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(Vector3) });
            Assert.IsNotNull(mi3, "Spawn(Vector3, float, float) — 스폰 시그니처 불변 계약");
            Assert.IsNotNull(mi6, "Spawn(Vector3, float, float, bool, bool, Vector3) — 부채꼴 오버로드 불변 계약");
        }

        /// <summary>[E2] SFXType.Monster_Alert — 열거형 존재(Alert 진입 경계음).</summary>
        [Test]
        public void E2_SFXType_MonsterAlert_Exists()
        {
            var mi = typeof(SoundEffectManager).GetMethod("GetSFXName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mi == null)
            {
                // GetSFXName이 non-public이면 문자열 매핑 근거로 검증 — SFX_Monster_Alert 클립명 매핑
                var src = System.IO.File.ReadAllText(
                    "Assets/Scripts/Systems/SoundEffectManager.cs");
                Assert.IsTrue(src.Contains("SFXType.Monster_Alert => \"SFX_Monster_Alert\""),
                    "SFXType.Monster_Alert → SFX_Monster_Alert 클립명 매핑이 존재해야 합니다(무번호 19-E2)");
            }
            else
            {
                // 열거형 존재 검증 — SFXType.Monster_Alert
                var sfxType = typeof(SoundEffectManager).GetNestedType("SFXType");
                Assert.IsNotNull(sfxType, "SFXType 열거형이 SoundEffectManager 안에 있어야 합니다");
                Assert.IsTrue(System.Enum.IsDefined(sfxType, "Monster_Alert"),
                    "SFXType.Monster_Alert가 존재해야 합니다(무번호 19-E2)");
            }
        }

        /// <summary>[E2] SFX_Monster_Alert.wav — Resources/Audio/SFX에 클립 에셋 존재.</summary>
        [Test]
        public void E2_SFX_MonsterAlert_WavExists()
        {
            var path = "Assets/Resources/Audio/SFX/SFX_Monster_Alert.wav";
            Assert.IsTrue(System.IO.File.Exists(path), "SFX_Monster_Alert.wav가 존재해야 합니다(무번호 19-E2)");
        }

        /// <summary>[E1/E2] AnimalAI — Alert 진입 시 SpawnAlertSignals 호출 배선 존재 + [195차 P1] 링/SFX 호출 철거.
        /// 시그니처 존재 어서션(E1_SpawnAlert_SignatureExists 등)은 별도 유지 — 본 테스트는 호출부 철거 계약만 핀한다.</summary>
        [Test]
        public void E1_AnimalAI_AlertSignals_Wired()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(src.Contains("SpawnAlertSignals()"), "AnimalAI.SetAggroTarget에서 SpawnAlertSignals 호출 배선");
            Assert.That(src, Does.Not.Contain("AttackTelegraphIndicator.SpawnAlert"),
                "[195차 P1] 발견 적색 링(청록 회전 체브런 포함) 스폰 호출 철거 — 메서드 시그니처는 존재(무번호 19-E1)");
            Assert.That(src, Does.Not.Contain("SFXType.Monster_Alert"),
                "[195차 P1] 발견 SFX 호출도 링 스폰과 함께 철거 — 에셋/열거형 존재는 E2 어서션이 핀");
        }

        /// <summary>[E1] AnimalAI — Alert 진입 시그널이 Idle→Alert 전이 지점에서만 발화.</summary>
        [Test]
        public void E1_AnimalAI_AlertSignal_OnTransition()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            int idx = src.IndexOf("SpawnAlertSignals()");
            int idxTransition = src.IndexOf("_aggroState = AggroState.Alert;");
            Assert.IsTrue(idx > 0 && idxTransition > 0 && idx > idxTransition,
                "SpawnAlertSignals가 Idle→Alert 전이 지점에서 호출되어야 합니다(무번호 19-E1)");
        }
    }
}
