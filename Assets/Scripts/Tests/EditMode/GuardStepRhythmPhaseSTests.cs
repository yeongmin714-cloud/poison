using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-S] 스텝 리듬 먼지(S1) + 이동 시작 킥(S2) — 소스 계약(Source 잠금).
    /// 스펙: /tmp/s_a_brief.md §2/§3 (ArmorBodyMeasureSpaceTests 패턴 복제).
    /// S1 — StepToward 게이팅 블록: 발자국 좌우 교차 오프셋(결정론 _stepFlip, 스폰 위치만 변조 —
    ///   0.6m 거리 게이팅(TrailDustGapSq)·2레이어 구조·도착 간격 리셋 무손상, 스폰 수 증가 없음).
    /// S2 — SetCommandTarget 이동 시작 킥: 이동 명령 전용(a==false) + 1.5m 거리 게이트 발밑 퍼프 1회
    ///   (도착 퍼프 20-I2와 대칭). [무번호 22-Q2] 기존 배선 원형 보존 위에 [무번호 28-S2] 마커 부여.
    /// 대상 구현: Assets/Scripts/Systems/GuardPlaceholder.cs
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class GuardStepRhythmPhaseSTests
    {
        private static string ReadGuard() => System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");

        // ===== [무번호 28-S1] 소스 계약 — GuardPlaceholder.StepToward =====

        [Test]
        public void Source_StepFlip()
        {
            string src = ReadGuard();
            // T1: 스텝 좌우 교차 — _stepFlip 필드 + 횡축 오프셋 수식(-dirXZ.z / dirXZ.x, 0.12f 교차 진폭).
            Assert.That(src, Does.Contain("private bool _stepFlip;"),
                "[무번호 28-S1] 스텝 좌우 교차 필드(_stepFlip)가 존재해야 한다 — 발자국 리듬(결정론).");
            Assert.That(src, Does.Contain("Vector3 stepSide = new Vector3(-dirXZ.z, 0f, dirXZ.x) * (_stepFlip ? 0.12f : -0.12f);"),
                "[무번호 28-S1] 이동 방향의 횡축 오프셋 수식(좌우 교차 0.12m)이 존재해야 한다.");
            Assert.That(src, Does.Contain("_stepFlip = !_stepFlip;"),
                "[무번호 28-S1] 스텝마다 교차 플립 배선이 존재해야 한다 — 좌/우 리듬 유지.");
        }

        [Test]
        public void Source_GatingUnharmed()
        {
            string src = ReadGuard();
            // T2: 기존 계약 리터럴 무손상 — 0.6m 거리 게이팅 + 도착 간격 리셋 + 2레이어 스폰 수 불변(위치 변조만).
            Assert.That(src, Does.Contain("(next - _lastTrailDustPos).sqrMagnitude >= TrailDustGapSq"),
                "[무번호 28-S1] TrailDustGapSq(0.6m) 거리 게이팅 리터럴이 무손상이어야 한다 — 스폰 수 증가 금지.");
            Assert.That(src, Does.Contain("_hasTrailDustPos = false"),
                "[무번호 28-S1] 도착 분기 간격 플래그 리셋(_hasTrailDustPos = false)이 무손상이어야 한다.");
            Assert.That(src, Does.Contain("ShockwaveRingFX.SpawnMoveTrailDust(new Vector3(next.x, next.y + 0.02f, next.z) + stepSide)"),
                "[무번호 28-S1] 지면 링(2레이어 1층) 스폰 유지 + stepSide 가산(스폰 위치만 변조)이어야 한다.");
            Assert.That(src, Does.Contain("ShockwaveRingFX.SpawnMoveTrailPuff(new Vector3(next.x, next.y + 0.06f, next.z) + stepSide)"),
                "[무번호 28-S1] 공기 퍼프(2레이어 2층) 스폰 유지 + stepSide 가산(스폰 위치만 변조)이어야 한다.");
        }

        // ===== [무번호 28-S2] 소스 계약 — GuardPlaceholder.SetCommandTarget =====

        [Test]
        public void Source_StartKick()
        {
            string src = ReadGuard();
            // T3: 이동 시작 킥 — 1.5m 거리 게이트(1.5f * 1.5f, 제곱 거리 2.25f) + 발밑 퍼프 존재.
            Assert.That(src, Does.Contain("1.5f * 1.5f"),
                "[무번호 28-S2] 1.5m 거리 게이트(1.5f * 1.5f = 2.25f 제곱 거리) 계약이 시작 킥에 존재해야 한다 — 제자리 재명령 스팸 방지.");
            Assert.That(src, Does.Contain("(t - here).sqrMagnitude >= 2.25f"),
                "[무번호 28-S2] 실제 게이트 배선((t - here).sqrMagnitude >= 2.25f)이 유지되어야 한다 — [무번호 22-Q2] 원형 보존.");
            Assert.That(src, Does.Contain("ShockwaveRingFX.SpawnMoveTrailPuff(new Vector3(gp.x, gp.y + 0.06f, gp.z))"),
                "[무번호 28-S2] 시작 킥 발밑 퍼프 배선이 존재해야 한다(도착 퍼프 20-I2와 대칭 — 출발/도착 프레임 완성).");
        }

        [Test]
        public void Source_KickMoveOnly()
        {
            string src = ReadGuard();
            // T4: 킥은 이동 명령 전용 — !a 게이트 안(공격 명령 제외) 배선 순서 계약.
            int idxCmd = src.IndexOf("public void SetCommandTarget");
            int idxGate = src.IndexOf("if (!a)");
            int idxKickGate = src.IndexOf("(t - here).sqrMagnitude >= 2.25f");
            int idxPuff = src.IndexOf("ShockwaveRingFX.SpawnMoveTrailPuff(new Vector3(gp.x, gp.y + 0.06f, gp.z))");
            Assert.That(idxCmd > 0 && idxGate > idxCmd,
                "[무번호 28-S2] !a 이동 명령 게이트(if (!a))가 SetCommandTarget 내부에 존재해야 한다.");
            Assert.That(idxKickGate > idxGate && idxPuff > idxGate,
                "[무번호 28-S2] 시작 킥(1.5m 게이트 + 발밑 퍼프)은 !a 게이트 안(이동 명령 전용)에 배선되어야 한다 — 공격 명령에서는 미발화.");
        }

        [Test]
        public void Source_Deterministic()
        {
            string src = ReadGuard();
            // T5: 신규 블록 결정론 — [28-S1] 스텝 리듬 블록과 [28-S2] 시작 킥 블록에 Random 부재.
            //   (파일 전체 Random 부재가 아닌 "신규 블록 무 Random" 계약 — 기존 루팅 Random.Range/value는 원형 보존.)
            int s1 = src.IndexOf("[무번호 28-S1]");
            int s1End = src.IndexOf("SpawnMoveTrailPuff", s1);
            Assert.That(s1, Is.GreaterThan(0),
                "[무번호 28-S1] 마커가 존재해야 한다 — 결정론 블록 범위 산출 기준.");
            Assert.That(s1End, Is.GreaterThan(s1),
                "[무번호 28-S1] 스텝 리듬 블록 범위(마커 → 퍼프 스폰)가 성립해야 한다.");
            Assert.That(src.Substring(s1, s1End - s1), Does.Not.Contain("Random"),
                "[무번호 28-S1] 스텝 리듬 블록은 결정론(_stepFlip 교차) — Random 사용 금지.");

            int s2 = src.IndexOf("[무번호 28-S2]");
            int s2End = src.IndexOf("SpawnMoveTrailPuff(new Vector3(gp.x", s2);
            Assert.That(s2, Is.GreaterThan(0),
                "[무번호 28-S2] 마커가 존재해야 한다 — 결정론 블록 범위 산출 기준.");
            Assert.That(s2End, Is.GreaterThan(s2),
                "[무번호 28-S2] 시작 킥 블록 범위(마커 → 퍼프 스폰)가 성립해야 한다.");
            Assert.That(src.Substring(s2, s2End - s2), Does.Not.Contain("Random"),
                "[무번호 28-S2] 시작 킥 블록은 결정론(거리 게이트) — Random 사용 금지.");
        }

        [Test]
        public void Source_Marker()
        {
            string src = ReadGuard();
            // T6: 라운드 마커 — 변경 지점 추적용 태그 존재 계약.
            Assert.That(src, Does.Contain("[무번호 28-S1]"),
                "[무번호 28-S1] 스텝 리듬 마커가 존재해야 한다(필드 + StepToward 블록).");
            Assert.That(src, Does.Contain("[무번호 28-S2]"),
                "[무번호 28-S2] 이동 시작 킥 마커가 존재해야 한다(SetCommandTarget 진입부).");
        }
    }
}
