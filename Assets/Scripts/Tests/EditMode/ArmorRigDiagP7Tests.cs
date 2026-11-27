using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-P7] 리그 정합 진단 + 재정규화 가드 인스턴스 단위 수리 — 소스 계약(Source 잠금).
    /// P6 세션 1회 가드의 결함(재피팅 재부착 후 무보정 — 11-13 12:59 Play 실측) 수리 + 
    /// "장비 작음/부착 불가" 3차 보고의 판별 데이터 1줄(본 스케일/본 y/r.bounds/렌더 스팬).
    /// </summary>
    public class ArmorRigDiagP7Tests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");

        [Test]
        public void Source_InstanceKeyGuard()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("$\"resize:{scene}:{slot}:{v.GetInstanceID()}\""),
                "[무번호 29-P7] 재정규화 가드 키가 인스턴스 단위여야 한다(재피팅 재부착=새 인스턴스=재보정 허용).");
            // [무번호 29-P12 갱신] 가드 의미 = 인스턴스별 쿨다운 지속 추적(1회 소진형 폐기 — 히트 변동 대응).
            Assert.That(src, Does.Contain("인스턴스별 쿨다운"),
                "[무번호 29-P12] 가드 의미 주석(인스턴스별 쿨다운 지속 추적)이 유지되어야 한다.");
        }

        [Test]
        public void Source_RigDiagLog()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("[29-P7] 리그 정합"),
                "[무번호 29-P7] 리그 정합 진단 로그가 존재해야 한다.");
            Assert.That(src, Does.Contain("rig-diag:"),
                "[무번호 29-P7] 진단 로그 1회성 키(rig-diag:{scene})가 존재해야 한다.");
            Assert.That(src, Does.Contain("headScale="),
                "[무번호 29-P7] 본 스케일(Head lossyScale) 판별 데이터가 포함되어야 한다.");
            Assert.That(src, Does.Contain("bodyBounds="),
                "[무번호 29-P7] 바디 SMR r.bounds min/max(본 봉투)가 포함되어야 한다.");
            Assert.That(src, Does.Contain("renderSpan="),
                "[무번호 29-P7] BakeMesh 렌더 스팬(실제 피부)가 포함되어야 한다.");
        }

        [Test]
        public void Source_DiagUsesBoneAccess()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("placeAnim.GetBoneTransform(HumanBodyBones.Head)"),
                "[무번호 29-P7] 진단은 휴머노이드 본 접근(검증된 데이터 소스)을 사용해야 한다.");
            Assert.That(src, Does.Contain("GetComponentInChildren<SkinnedMeshRenderer>()"),
                "[무번호 29-P7] 바디 SMR 탐색이 존재해야 한다.");
        }

        [Test]
        public void Source_Marker_And_Deterministic()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("[무번호 29-P7]"), "[무번호 29-P7] 마커 존재.");
            int idx = src.IndexOf("[29-P7] 리그 정합");
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            string block = src.Substring(System.Math.Max(0, idx - 1200));
            Assert.That(block.Substring(0, System.Math.Min(block.Length, 2400)), Does.Not.Contain("UnityEngine.Random"),
                "[무번호 29-P7] 진단 블록 결정론 — 난수 API 0건.");
        }
    }
}
