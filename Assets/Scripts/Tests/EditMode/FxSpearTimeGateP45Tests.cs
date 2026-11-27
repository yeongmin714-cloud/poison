using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 45] 스피어 히트 시간 게이트 + 리본 메시 축소 순서 수리 계약.
    /// 실측 근거(11-15 45차 Play 로그 — 사용자 전달):
    ///  ①[44-SPEAR-DIAG] 13샘플 전부 opener:False/exit:False/followup:False/idle:False + 해시 -125389069 불변
    ///    + normT 루핑 → AttackThrust 상태 미진입 확정(AnyState 47건 전부 트리거 조건 — 이동 강제 이탈 경로 부재).
    ///  ②AttackArcHost:RebuildRibbon 344/345행 — "Mesh.vertices is too small"+"colors out of bounds" 반복
    ///    (축소 시 정점을 삼각형보다 먼저 대입하는 부분 갱신 순서 결함).
    /// </summary>
    public class FxSpearTimeGateP45Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void SpearOpener_TimeGateResolvesHit_WithoutStateMachine()
        {
            string src = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(src, Does.Contain("[무번호 45-SPEAR-TIMEGATE]"), "[45] 시간 게이트 마커.");
            Assert.That(src, Does.Contain("hit resolved (time-gate)"),
                "[45] 시간 게이트 해결 로그 — 상태 미진입과 무관한 타격 보장.");
            Assert.That(src, Does.Contain("_spearOpenerResolveAtTime = Time.time + ResolveSpearOpenerClipLength() * SpearOpenerStrikeNormT"),
                "[45] 발화 시각+클립 길이×마커(0.50) 게이트 산식.");
            Assert.That(src, Does.Contain("Take 001"),
                "[84 진화] 오프너 클립 프래그먼트 매칭 — Player_AC AttackThrust 모션 교체(사용자 제공 플레이어 창 휘두르기.fbx Take 001) 실측.");
            Assert.That(src, Does.Contain("SpearOpenerClipLengthFallback = 1.2f"),
                "[45] 클립 미발견 폴백 길이.");
            // 상태 기반 조기 해결 병행 유지(정상 세션) + 이중 발화 차단 플래그.
            Assert.That(src, Does.Contain("if (!_spearOpenerHitResolved && Time.time >= _spearOpenerResolveAtTime)"),
                "[45] resolved 플래그 가드 — 상태 기반/시간 기반 이중 발화 차단.");
            Assert.That(src, Does.Contain("FireComboTrail(1);"), "[45] 시간 게이트도 1타 스윙 트레일 발화 동기.");
        }

        [Test]
        public void Ribbon_ShrinkAssignsTrianglesBeforeVertices()
        {
            string src = Read("Assets/Scripts/Systems/AttackArcVFX.cs");
            Assert.That(src, Does.Contain("[무번호 45-RIBBON]"), "[45] 리본 수리 마커.");
            Assert.That(src, Does.Contain("if (vOut.Length < _mesh.vertexCount)"),
                "[45] 축소 판정 — 만료 샘플 트림 시 삼각형 선대입 분기.");
            // 축소 분기: triangles → vertices → colors 순서(Unity 부분 갱신 계약).
            int shrinkIdx = src.IndexOf("if (vOut.Length < _mesh.vertexCount)", System.StringComparison.Ordinal);
            Assert.Greater(shrinkIdx, 0, "[45] 축소 분기 존재.");
            int triIdx = src.IndexOf("_mesh.triangles = tOut;", shrinkIdx, System.StringComparison.Ordinal);
            int vertIdx = src.IndexOf("_mesh.vertices = vOut;", shrinkIdx, System.StringComparison.Ordinal);
            int colIdx = src.IndexOf("_mesh.colors = cOut;", shrinkIdx, System.StringComparison.Ordinal);
            Assert.Greater(triIdx, 0, "[45] 축소 분기의 삼각형 대입 존재.");
            Assert.Less(triIdx, vertIdx, "[45] 축소 시 삼각형이 정점보다 먼저(부분 갱신 오류 차단).");
            Assert.Less(vertIdx, colIdx, "[45] 색은 정점 직후(길이 일치 계약).");
        }

        [Test]
        public void Diag_UpgradeCarriesClipAndControllerIdentity()
        {
            string src = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(src, Does.Contain("GetCurrentAnimatorClipInfo(0)"),
                "[44→45] 실제 재생 클립명 기록 — 상태 미진입 근원 식별 축.");
            Assert.That(src, Does.Contain("runtimeAnimatorController"),
                "[45] 런타임 컨트롤러명 동반 기록 — 컨트롤러 교체/오버라이드 판별.");
            Assert.That(src, Does.Contain("[44-SPEAR-DIAG] opener 미해결 감시"),
                "[44] 관측 장치 유지(게이트가 게임플레이를 담당한 뒤에도 근원 추적 지속).");
        }
    }
}
