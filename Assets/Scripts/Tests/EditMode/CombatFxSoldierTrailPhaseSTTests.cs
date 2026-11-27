using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 26-ST] 병사 스윙 휘적 리본 계약테스트 — 소스 문자열 계약 6케이스(CombatFxEmissionPipelineTests 템플릿).
    /// 계획서: docs/plans/2026-11-12_combat-vfx-depthfade-soldier-trail-plan.md §Phase ST.
    /// Unity 배치 실행 없음 — EditMode 소스 문자열 검증만(컴파일/Play는 부모 단일 실행·별도 승인).
    /// </summary>
    public class CombatFxSoldierTrailPhaseSTTests
    {
        /// <summary>[ST1/ST5] SoldierSwingTrail 클래스 존재 + AttachTo 시그니처 + 동시 상한 토큰.</summary>
        [Test]
        public void ST1_SoldierSwingTrail_ClassAndAttachToSignature()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/SoldierSwingTrail.cs");
            Assert.IsTrue(src.Contains("public class SoldierSwingTrail : MonoBehaviour"),
                "SoldierSwingTrail 클래스 존재(MonoBehaviour — 인스턴스형)");
            Assert.IsTrue(src.Contains("public static void AttachTo(GameObject weapon, float gripRatio = 0.0f)"),
                "AttachTo 시그니처(WeaponSwingTrail.Attach 선례 준용)");
            Assert.IsTrue(src.Contains("_activeCount"),
                "동시 상한 카운트 토큰(ST5 — 23-X2 선례 인스턴스판)");
            Assert.IsTrue(src.Contains("MaxActive = 16"),
                "활성 리본 동시 상한 16(ST5)");
        }

        /// <summary>[ST1 색 계약] 청백 시작색 (0.75, 0.92, 1.0) — 패망 청 계열(병사 파랑 국가색 무손상).</summary>
        [Test]
        public void ST1_ColorContract_PaleBlue()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/SoldierSwingTrail.cs");
            Assert.IsTrue(src.Contains("(0.75f, 0.92f, 1f"),
                "청백 시작색 존재 — new Color(0.75f, 0.92f, 1f, 1f)");
        }

        /// <summary>[ST2] WeaponSwingTrail 공개 머티리얼 팩토리 위임 + SoldierSwingTrail 재사용(중복 구현 금지).</summary>
        [Test]
        public void ST2_CreateTrailMaterialShared_Delegated()
        {
            var wsSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/WeaponSwingTrail.cs");
            Assert.IsTrue(wsSrc.Contains("public static Material CreateTrailMaterialShared(float emissionIntensity = 1f)"),
                "WeaponSwingTrail 공개 팩토리 위임자 존재(additive 1개)");
            var soldierSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/SoldierSwingTrail.cs");
            Assert.IsTrue(soldierSrc.Contains("WeaponSwingTrail.CreateTrailMaterialShared(1.0f)"),
                "SoldierSwingTrail이 위임자 재사용(머티리얼 사양 동일 보장)");
        }

        /// <summary>[ST3] GuardVisualAttachSystem 배선 — 병사 무기(VisualSlot.Weapon) 부착 직후 트레일 부착.</summary>
        [Test]
        public void ST3_GuardVisualAttachSystem_Wired()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardVisualAttachSystem.cs");
            Assert.IsTrue(src.Contains("SoldierSwingTrail.AttachTo(visual)"),
                "병사 무기 GLB 인스턴스 자식으로 트레일 부착 배선");
            int idxSlot = src.IndexOf("if (slot == VisualSlot.Weapon)");
            int idxAttach = src.IndexOf("SoldierSwingTrail.AttachTo(visual)");
            Assert.IsTrue(idxSlot > 0 && idxAttach > 0 && idxAttach > idxSlot,
                "무기 슬롯 판정(실측 조건) 이후에만 부착 — Shoes/Gloves 등 비무기 슬롯 미부착");
        }

        /// <summary>[ST4] 발화 분기 헬퍼 + DriveMode.Soldier 분기 + 기존 static 경로 문자열 잠금 보존.</summary>
        [Test]
        public void ST4_SetSwingTrailEmitting_Helper_And_LockedString()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.IsTrue(src.Contains("private void SetSwingTrailEmitting(bool on)"),
                "발화 분기 헬퍼 존재");
            Assert.IsTrue(src.Contains("if (mode == DriveMode.Soldier)"),
                "Soldier 모드 분기(인스턴스 트레일 발화)");
            Assert.IsTrue(src.Contains("WeaponSwingTrail.SetEmitting(on);"),
                "기존 static 경로 원 문자열 보존(분기 헬퍼 else 경로 — 계약 문자열 잠금)");
            Assert.IsTrue(src.Contains("SetSwingTrailEmitting(true);") && src.Contains("SetSwingTrailEmitting(false);"),
                "기존 발화/종료 호출부가 헬퍼로 교체됨");
        }

        /// <summary>[ST4] 병사 트레일 캐시 필드 — null이면 조용히 무시(무기 부착 실패 병사 무해).</summary>
        [Test]
        public void ST4_SoldierSwingTrailCache_Field()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.IsTrue(src.Contains("private SoldierSwingTrail _soldierSwingTrail;"),
                "_soldierSwingTrail 캐시 필드 존재");
        }
    }
}
