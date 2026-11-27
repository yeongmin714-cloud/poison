using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-P5] 측정 신뢰성(후보 C + r.bounds 교차검증 게이트) — 소스 계약(Source 잠금).
    /// 스펙: /tmp/p5_code_brief.md §2/§3/§4 (ArmorBodyMeasureSpaceTests 관행 복제).
    /// ①측정 후보 C — SMR 이중 베이크(기존 무스케일 BakeMesh(tmp) 유지 + 신규 BakeMesh(tmpS, true))
    ///   + TRS 무스케일 행렬(position+rotation, Vector3.one) → 3후보 referee로 확장.
    /// ②r.bounds 교차검증 게이트 — 채택 매핑의 전신 스팬이 Unity 자체 r.bounds 전신과 ±10% 초과 괴차면
    ///   정점 측정 자체 무효(fullValid=false + LastVertexUnreliable) → 폴백 상수 경로 발동.
    /// ③재피팅 게이트 연동 — LastVertexUnreliable 소비 시 재피팅 스케줄 스킵(refit-skip: 키).
    /// 대상 구현: Assets/Scripts/Systems/ArmorVisualAttachSystem.cs(구현은 별도 태스크 —
    /// 본 테스트는 브리프 §2/§3/§4 대상 문자열을 그대로 잠금한다).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class ArmorMeasureReliabilityP5Tests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");

        // ===== [무번호 29-P5] 소스 계약 — ArmorVisualAttachSystem =====

        [Test]
        public void Source_CandidateC()
        {
            string src = ReadSystem();
            // T1: SMR 이중 베이크 — 기존 무스케일 BakeMesh(tmp) 유지 + 신규 useScale BakeMesh(tmpS, true) 추가.
            Assert.That(src, Does.Contain("BakeMesh(tmp)"),
                "[무번호 29-P5] 기존 무스케일 베이크(smr.BakeMesh(tmp))가 유지되어야 한다 — 이중 베이크의 전제(기존 경로 무수정).");
            Assert.That(src, Does.Contain("BakeMesh(tmpS, true)"),
                "[무번호 29-P5] 후보 C용 스케일 내장 베이크(smr.BakeMesh(tmpS, true))가 존재해야 한다 — useScale 베이크.");
            Assert.That(src, Does.Contain("Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one)"),
                "[무번호 29-P5] 후보 C 행렬(TRS 무스케일 — position+rotation만, Vector3.one)이 존재해야 한다.");
        }

        [Test]
        public void Source_Referee3()
        {
            string src = ReadSystem();
            // T2: 3후보 referee — 채택 판정이 기존 errA/errB에 errC를 추가한 3후보 r.bounds 스팬 오차 비교여야 한다.
            Assert.That(src, Does.Contain("errA"),
                "[무번호 29-P5] 후보A 스팬 오차(errA)가 존재해야 한다 — 기존 28-P4b2 referee 심볼 유지.");
            Assert.That(src, Does.Contain("errB"),
                "[무번호 29-P5] 후보B 스팬 오차(errB)가 존재해야 한다 — 기존 28-P4b2 referee 심볼 유지.");
            Assert.That(src, Does.Contain("errC"),
                "[무번호 29-P5] 후보C 스팬 오차(errC)가 존재해야 한다 — 3후보 referee 확장의 새 심볼.");
            Assert.That(src, Does.Contain("scaleBakeSpan"),
                "[무번호 29-P5] 매핑 정합 로그에 scaleBakeSpan(후보C 스팬)이 포함되어야 한다 — 브리프 §2-4 로그 확장 형식.");
        }

        [Test]
        public void Source_CrossGate()
        {
            string src = ReadSystem();
            // T3: r.bounds 교차검증 게이트 — ±10% 괴차 판정(>0.10f * rSpan) + fullValid=false + LastVertexUnreliable 설정.
            Assert.That(src, Does.Contain("0.10f * rSpan"),
                "[무번호 29-P5] 교차검증 게이트가 ±10% 괴차 판정(Mathf.Abs(vSpan - rSpan) > 0.10f * rSpan)을 해야 한다.");
            Assert.That(src, Does.Contain("fullValid = false"),
                "[무번호 29-P5] 괴차 초과 시 fullValid=false 재판정이 존재해야 한다 — 폴백 상수 경로 발동(기존 bool 선언 유지 + 후반 재판정).");
            Assert.That(src, Does.Contain("LastVertexUnreliable = true"),
                "[무번호 29-P5] 괴차 초과 시 LastVertexUnreliable=true 설정이 존재해야 한다 — 재피팅 판정 소비용.");
        }

        [Test]
        public void Source_UnreliableLog()
        {
            string src = ReadSystem();
            // T4: 무효 판정 로그 — 브리프 §3-2 명시 문구 + WarnOnce 1회성 키.
            Assert.That(src, Does.Contain("[29-P5] 정점 측정 무효"),
                "[무번호 29-P5] 정점 측정 무효 로그(vertexSpan vs refBounds 괴차 %)가 존재해야 한다.");
            Assert.That(src, Does.Contain("measure-unreliable:"),
                "[무번호 29-P5] 무효 판정 로그 1회성 키(measure-unreliable:{scene})가 존재해야 한다 — WarnOnce 스팸 가드.");
        }

        [Test]
        public void Source_RefitSkip()
        {
            string src = ReadSystem();
            // T5: 재피팅 게이트 연동 — LastVertexUnreliable 소비 + 재피팅 스킵 로그 + 1회성 키.
            Assert.That(src, Does.Contain("PlayerBodyMeasure.LastVertexUnreliable"),
                "[무번호 29-P5] 재피팅 판정이 PlayerBodyMeasure.LastVertexUnreliable를 소비해야 한다 — 정점 측정 무효 시 재피팅 스킵.");
            Assert.That(src, Does.Contain("[29-P5] 재피팅 스킵"),
                "[무번호 29-P5] 재피팅 스킵 로그(폴백 경로 부착 중 — 재피팅 무의미)가 존재해야 한다.");
            Assert.That(src, Does.Contain("refit-skip:"),
                "[무번호 29-P5] 재피팅 스킵 로그 1회성 키(refit-skip:{scene})가 존재해야 한다 — WarnOnce 스팸 가드.");
        }

        [Test]
        public void Source_Marker()
        {
            string src = ReadSystem();
            // T6: 라운드 마커 — 신규 마커 존재 + 기존 [28-P4b2] smr-map 키 무손상.
            Assert.That(src, Does.Contain("[무번호 29-P5]"),
                "[무번호 29-P5] 구현 파일에 라운드 마커 [무번호 29-P5]가 존재해야 한다.");
            Assert.That(src, Does.Contain("smr-map:"),
                "[무번호 29-P5] 기존 [28-P4b2] 매핑 정합 로그 1회성 키(smr-map:{scene})가 무손상으로 유지되어야 한다.");
        }
    }
}
