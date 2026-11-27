using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [91차 F1/F2] 토글키 단일화 + 건설 창 재부착 경화 계약 (소스 문자열 어서션 — [91차] 마커,
    /// DialogueFeedbackTests 선례).
    ///
    /// 진단(마을씬 실측): U키가 GameStatsUTK/ConstructionWindowUTK 양쪽에서 토글 — GameStats가 1회라도
    /// 열린 뒤 U = 두 창 동시 개방 → GameStats가 위에 떠 "건설창 안뜸" 체감. 재부착 경로(parent==null 가드)는
    /// stale detach에 취약(재개방 미표시 실측).
    ///
    ///  · GameStatsUTK = O키(단일화) / ConstructionWindowUTK = U키 전용
    ///  · Ensure/Open/Show 3곳 재부착 가드(parent.panel == null) + 개방 로그 부착 상태 진단
    /// </summary>
    public class GameStatsKeyTests
    {
        // ── 1. GameStatsUTK — O키 단일화 ──

        [Test]
        public void GameStatsUTK_ToggleKey_IsO_NoMoreUKey()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/GameStatsUTK.cs");
            Assert.That(src, Does.Contain("[91차 F1]"), "[91차 F1] 마커 주석.");
            Assert.That(src, Does.Contain("kb.oKey.wasPressedThisFrame"),
                "[91차 F1] GameStatsUTK 토글키 = O(단일화).");
            Assert.That(src, Does.Not.Contain("kb.uKey"),
                "[91차 F1] 구 uKey 잔존 금지 — 건설 창과 충돌 원인 제거.");
            Assert.That(src, Does.Contain("O키로 토글"), "인스턴스 생성 로그 — O키 문구.");
            Assert.That(src, Does.Contain("통계 창 열림 (키: O)"), "개방 로그 — O키 문구.");
        }

        // ── 2. ConstructionWindowUTK — U키 전용 + 재부착 경화 ──

        [Test]
        public void ConstructionWindowUTK_UKeyExclusive_And_ReattachHardened()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/ConstructionWindowUTK.cs");
            Assert.That(src, Does.Contain("[91차 F2]"), "[91차 F2] 마커 주석.");
            Assert.That(src, Does.Contain("kb.uKey.wasPressedThisFrame"),
                "[91차 F1] U키 = 건설 관리 창 전용(기존 계약 유지).");

            // [91차 F2] stale detach 재부착 가드 — Ensure/Open/Show 3곳 동일 적용.
            int guards = Regex.Matches(src, @"parent\.panel == null").Count;
            Assert.That(guards, Is.GreaterThanOrEqualTo(3),
                $"[91차 F2] parent.panel == null 가드 3곳 이상(Ensure/Open/Show) — 실제={guards}.");

            // [91차 F2] 개방 로그에 부착 상태 포함 — 다음 Play 재개방 미표시 판별용.
            Assert.That(src, Does.Contain("건설 관리 창 열림 (키: U, attached="),
                "[91차 F2] 개방 로그 부착 상태 진단.");
            Assert.That(src, Does.Contain("[91차 F1] 건설 관리 창 전용 단일화"),
                "Updater 주석 — U키 전용화 명시.");
        }
    }
}
