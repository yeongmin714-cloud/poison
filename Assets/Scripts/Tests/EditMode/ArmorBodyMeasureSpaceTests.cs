using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-P4b2] 바디 측정 공간 정합 + 과도기 재피팅 자가교정 — 소스 계약(Source 잠금).
    /// 스펙: /tmp/p4b2_brief.md §2/§3 (ArmorVisualFitGuardTests 관행 복제).
    /// ①SMR 매핑 정합 — 후보A(transform L2W)/후보B(rootBone L2W) 양쪽 계산 후 Unity 자체
    ///   bounds(r.bounds)와 스팬 오차 비교로 채택(동률 시 A 유지 — 기존 동작 보존).
    /// ②과도기 재피팅 자가교정 — fullBody 계약(1.2×SoldierTargetHeight=2.16m) 초과 시
    ///   1.0s 후 슬롯 전체 재피팅 1회 예약(세션당 플레이어 1회 한정, static 키 refit:{scene}).
    /// 대상 구현: Assets/Scripts/Systems/ArmorVisualAttachSystem.cs(구현은 별도 태스크 —
    /// 본 테스트는 브리프 §3 T1~T6 문자열을 그대로 잠금한다).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class ArmorBodyMeasureSpaceTests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");

        // ===== [무번호 28-P4b2] 소스 계약 — ArmorVisualAttachSystem =====

        [Test]
        public void Source_DualMapping()
        {
            string src = ReadSystem();
            // T1: SMR 경로에서 매핑 후보 2개를 모두 계산 — 후보A(transform) / 후보B(rootBone, null 가드 포함).
            Assert.That(src, Does.Contain("r.transform.localToWorldMatrix"),
                "[무번호 28-P4b2] 후보A(기존 transform L2W 근사) 계산이 존재해야 한다 — 동률 시 A 유지(기존 동작 보존).");
            Assert.That(src, Does.Contain("(r.rootBone != null ? r.rootBone : r.transform).localToWorldMatrix"),
                "[무번호 28-P4b2] 후보B(rootBone 우선 L2W, null 가드 포함) 계산이 존재해야 한다 — rootBone=Hips SMR Play 어긋남 수리.");
        }

        [Test]
        public void Source_BoundsReferee()
        {
            string src = ReadSystem();
            // T2: Unity 자체 bounds(r.bounds) 기준 오차 비교로 후보 채택 — 침하감시(r.bounds) 정합 근거.
            Assert.That(src, Does.Contain("r.bounds"),
                "[무번호 28-P4b2] 매핑 채택 심판(refereree)으로 Unity 자체 계산 r.bounds가 사용되어야 한다.");
        }

        [Test]
        public void Source_MapDiagLog()
        {
            string src = ReadSystem();
            // T3: 채택 결과+양 후보 스팬 로그 1줄 + WarnOnce 1회성 키(스팸 가드 관행).
            Assert.That(src, Does.Contain("[29-P5] SMR 매핑 정합"),
                "[무번호 28-P4b2] SMR 매핑 정합 진단 로그(transformSpan/rootBoneSpan/refBounds/채택)가 존재해야 한다.");
            Assert.That(src, Does.Contain("smr-map:"),
                "[무번호 28-P4b2] 매핑 정합 로그 1회성 키(smr-map:{scene})가 존재해야 한다 — WarnOnce 스팸 가드.");
        }

        [Test]
        public void Source_RefitGate()
        {
            string src = ReadSystem();
            // T4: 재피팅 게이트 — fullBody 계약(2.16m) 초과 판정 + 세션당 1회 한정 static 키 + 1.0s 지연 예약.
            Assert.That(src, Does.Contain("2.16m"),
                "[무번호 28-P4b2] 재피팅 게이트가 계약 fullBody 2.16m(1.2×SoldierTargetHeight) 초과 판정을 해야 한다.");
            Assert.That(src, Does.Contain("refit:"),
                "[무번호 28-P4b2] 재피팅 1회 한정 static 키(refit:{scene})가 존재해야 한다 — 세션당 플레이어 1회.");
            Assert.That(src, Does.Contain("1.0s"),
                "[무번호 28-P4b2] 재피팅은 1.0s 지연 예약(코루틴 또는 tick 타이머)이어야 한다.");
        }

        [Test]
        public void Source_RefitLog()
        {
            string src = ReadSystem();
            // T5: 재피팅 예약 로그 — 브리프 §2-2 명시 문구 그대로.
            Assert.That(src, Does.Contain("[ArmorVisual] [28-P4b2] 재피팅"),
                "[무번호 28-P4b2] 재피팅 예약 로그(fullBody 측정값+계약 초과+1회 한정)가 존재해야 한다.");
        }

        [Test]
        public void Source_Marker()
        {
            string src = ReadSystem();
            // T6: 라운드 마커 — 변경 지점 추적용 태그 존재 계약.
            Assert.That(src, Does.Contain("[무번호 28-P4b2]"),
                "[무번호 28-P4b2] 구현 파일에 라운드 마커 [무번호 28-P4b2]가 존재해야 한다.");
        }
    }
}
