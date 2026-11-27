using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 59] 지면 데칼 지면 스냅 계약 — 11-16 실측 근거: HitVfxTelegramProbe 배치 런에서
    /// Test_10 미니지형(원점 표면 y=-0.37, 121x121 정점 높이 변동)에서 SpawnDecal 생산 호출은
    /// 정상 스폰·머티리얼 계약 이행했으나 3.0s 컷 지면에서 데칼 미확인. 원인: 호출부 고정 y=0.02
    /// 전달 → 지형 표면보다 낮아 파묻힘. 대조: FxGroundCrackFX.Burst 하향 레이캐스트 스냅 경로는
    /// 같은 컷 0.3~1.0s에서 지면에 정상 관측.
    /// 수리 = SpawnDecal 내부 하향 레이캐스트 지면 스냅(XZ 보존·Y만 스냅) + 기존 +0.02 리프트 유지.
    /// EditMode 규약: 런타임 인스턴스화 없이 File.ReadAllText 소스계약 문자열 단정만 수행.
    /// 주의(100차 3회 재발 함정): 전문 부정 잠금(Does.Not.Contain)은 자기 파일 주석 오탐 — 본 파일은 전부 긍정 잠금만 사용.
    /// </summary>
    public class DecalGroundSnap_59Tests
    {
        private static string ReadSource()
        {
            string path = "Assets/Scripts/Systems/DecalSpawner.cs";
            Assert.IsTrue(File.Exists(path), $"소스 파일 존재: {path}");
            return File.ReadAllText(path);
        }

        // ── ① 하향 레이캐스트 지면 스냅 존재 ────────────────────────

        [Test]
        public void DecalSpawner_GroundSnap_RaycastDown_59()
        {
            string src = ReadSource();
            Assert.That(src, Does.Contain("Physics.Raycast(position + Vector3.up * 0.2f, Vector3.down"),
                "[무번호 59] 하향 레이캐스트 지면 스냅 — FxGroundCrackFX.Burst 선례 1:1 (11-16 실측: 고정 y 데칼 파묻힘).");
            Assert.That(src, Does.Contain("[무번호 59]"),
                "[무번호 59] 마커 존재 — 계약 근거 주석 식별.");
            Assert.That(src, Does.Contain("QueryTriggerInteraction.Ignore"),
                "[무번호 59] 트리거 무시 — 트리거 콜라이더 위 오스냅 방지.");
        }

        // ── ② XZ 보존 계약(Y만 스냅) ────────────────────────────────

        [Test]
        public void DecalSpawner_GroundSnap_XZPreserved_59()
        {
            string src = ReadSource();
            Assert.That(src, Does.Contain("new Vector3(position.x, hit.point.y, position.z)"),
                "[무번호 59] XZ는 호출부 값 유지, Y만 스냅 — FxGroundCrackFX의 hit.point 전체 채택과 구별되는 본 계약의 핵심(데칼은 호출부 XZ가 정답 위치).");
        }

        // ── ③ 기존 +0.02 리프트 무손상 ─────────────────────────────

        [Test]
        public void DecalSpawner_LiftContract_Unchanged_59()
        {
            string src = ReadSource();
            Assert.That(src, Does.Contain("position + Vector3.up * 0.02f"),
                "[무번호 59] 기존 +0.02 리프트 계약 유지 — 스냅된 y에 리프트가 자동 적용(58-P2 지면 언더 렌더 방지 무손상).");
        }
    }
}
