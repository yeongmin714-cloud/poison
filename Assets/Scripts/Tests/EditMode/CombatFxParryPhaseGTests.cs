using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 19-Phase G] 패링 VFX 보강 계약테스트 — G1 화이트 프레임 실카메라 캡처 + G2 스파클 버스트 배선.
    /// 판독 근거(참조 영상 3컷 — 4s/10s/40s): 흡수점 중심 방사 스파이크 8~16방향 존재 확정.
    /// G3(임팩트 방향 스파크) = [95차 F4]로 기존 구현 존재(중복 방지 — 신규 구현 금지).
    /// G4(반격 히트 금색 플래시) = 참조 3컷 전부 미표출 → 미채택(위조 배선 금지).
    /// [176차 갱신] 방사형 산란 제거(사용자 지시: 원형 링만 유지) — G2 버스트/G3 코어 글린트 패막 경로 호출 철수.
    /// </summary>
    public class CombatFxParryPhaseGTests
    {
        /// <summary>[G2 → 176차 갱신] 흡수점 스파클 버스트 — [176차] 방사형 산란 제거로 패막 경로 호출 철수.
        ///   SpawnCritBurst 정의/타 경로(CombatVFXController)는 무손상 — 호출부 소멸만 검증.</summary>
        [Test]
        public void G2_CritBurst_UnwiredFromParry_176()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.IsTrue(src.Split(new[] { "CombatVFXController.SpawnCritBurst(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 SpawnCritBurst(12방향 파편 버스트) 호출 0건 — 방사형 산란 제거(원형 링만 유지)");
        }

        /// <summary>[G2] 스파클 버스트 0.5s 강화 — 참조의 긴 줄기 스파이크 지속(기존 0.15~0.25s → 0.5s).
        ///   [무번호 22 교훈] 파일 단위 Contains는 동일 상수 오배선을 놓쳤다 — **메서드 범위 검증**으로 강화.</summary>
        [Test]
        public void G2_CritBurst_Lifetime_HalfSecond()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CombatVFXController.cs");
            int sig = src.IndexOf("public static void SpawnCritBurst");
            int end = src.IndexOf("public static void", sig + 10);
            string method = src.Substring(sig, end - sig);
            Assert.IsTrue(method.Contains("main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f);"),
                "SpawnCritBurst **메서드 내부** 수명 0.5s 강화 계약(무번호 19-G2 — 오배선 방지 범위 검증)");
        }

        /// <summary>[G2 + 무번호 20-J2 + 172차 P2] 등급 파라미터 시그니처 — 기본 12방향(참조 8~16 범위), 등급별 전달 계약.
        ///   [172차 P2] 색 파라미터화 — Color? tint = null 후미 옵션(기본 null=AttackPrimary, 패막 경로 ParryWhite 주입).</summary>
        [Test]
        public void G2_CritBurst_TwelveDirections()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CombatVFXController.cs");
            Assert.IsTrue(src.Contains("public static void SpawnCritBurst(Vector3 position, int sparkCount = 12, float speedScale = 1f, float sizeScale = 1f, Color? tint = null)"),
                "SpawnCritBurst 등급 파라미터 시그니처 — 기본 12방향(참조 8~16방향 범위 내) + 172차 P2 색 파라미터");
            Assert.IsTrue(src.Contains("Color burstColor = tint ?? FXPalette.AttackPrimary;"),
                "[172차 P2] 기본 색 = 기존 AttackPrimary(null 무손상 — 기존 호출부 무영향)");
        }

        /// <summary>[G1] 화이트 프레임 실카메라 직접 캡처 컷 — 폴백 복제 카메라 뷰 밖 문제의 절차 수리.</summary>
        [Test]
        public void G1_Probe_RealCamera_CaptureCut()
        {
            var src = System.IO.File.ReadAllText("Assets/Editor/CombatFxRealCamProbe.cs");
            Assert.IsTrue(src.Contains("cfx_17_whiteframe_realcam"),
                "[G1] 실카메라 직접 캡처 컷 존재");
            Assert.IsTrue(src.Contains("child.name == \"ParryWhiteFrame\""),
                "플래시 부모 실카메라 탐색 배선(A3 계약 — FX가 렌더되는 카메라 뷰로 판정)");
        }

        /// <summary>[G3 176차 갱신] 공격자 방향 코어 — [176차] 십자 글린트(SpawnParryCoreGlint)도 방사형 산란으로
        ///   패막 경로 호출 철수. [95차 F4] 방향성 스파크 0건 계약·[172차 P3-L3] 팬 철수 계약은 승계 유지
        ///   (정의/타 경로 무손상 — 호출부 소멸만 검증).</summary>
        [Test]
        public void G3_CoreGlint_UnwiredFromParry_176()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.IsTrue(src.Split(new[] { "ShockwaveRingFX.SpawnParryCoreGlint(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 코어 글린트(4방향 십자 플레어) 호출 0건 — 방사형 산란 제거");
            // 패막 경로 방향성 스파크 활성 호출 0건 — [95차 F4] 스파크는 패막 레이어에서 철수(정의 무손상)
            Assert.IsTrue(src.Split(new[] { "SpawnDirectionalSpark(p, sparkDir.normalized" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "방향성 스파크 패막 경로 호출 0건 — [172차 P3-L1] 코어 글린트 교체");
            Assert.IsTrue(src.Contains("ShockwaveRingFX.SpawnParryDeflectFan") == false,
                "[172차 P3-L3] 디플렉트 팬 패막 경로 호출 철수");
        }
    }
}
