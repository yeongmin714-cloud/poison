using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 58] 센티널 스캔 완전성 계약 — 57차까지 흰(≥246)+마젠타만 스캔해 ①검은 덩어리 ②준백(230~245)
    /// 덩어리가 완전 사각지대였다(11:30 세션 [51-SCREEN] 0건 + 사용자 잔존 보고 병존). 58차: 검정 감지기 신설
    /// + 준백 티어 + 스캔 RT 원본 저장(오버레이 UI 없는 센티널 시점).
    /// </summary>
    public class ScannerCoverage_58Tests
    {
        [Test]
        public void Sentinel_CoversAllFourTiers()
        {
            string path = "Assets/Scripts/Systems/MagentaSentinel.cs";
            Assert.IsTrue(File.Exists(path), "센티널 소스 존재.");
            string src = File.ReadAllText(path);

            // 4티어 판정 존재 — 마젠타/순백(기존) + 준백/검정(58 신설).
            Assert.That(src, Does.Contain("IsPureMagenta"), "[기존] 마젠타 판정 유지.");
            Assert.That(src, Does.Contain("c.r >= 246 && c.g >= 246 && c.b >= 246"), "[기존] 순백 임계 유지.");
            Assert.That(src, Does.Contain("c.r >= 232 && c.g >= 232 && c.b >= 232"),
                "[58-P1] 준백 티어 — 230~245대 덩어리 커버(기존 ≥246 누락분).");
            Assert.That(src, Does.Contain("c.r <= 14 && c.g <= 14 && c.b <= 14"),
                "[58-P1] 검정 클러스터 감지기 신설 — 검은 덩어리 사각지대 해소.");
            Assert.That(src, Does.Contain("SemiWhiteClusterPixels = 250"), "[58-P1] 준백 임계 핀.");
            Assert.That(src, Does.Contain("BlackClusterPixels = 400"), "[58-P1] 검정 임계 핀(이름판 HP바 ≈250~350px 상회).");

            // 보고 일관성 — 4티어 전부 근접 FX 나열 결합(일시 근원 특정).
            int nearCount = System.Text.RegularExpressions.Regex.Matches(src, "근접 FX").Count;
            Assert.That(nearCount, Is.GreaterThanOrEqualTo(4), "[57-P2/58-P1] 4티어 전부 근접 FX 필드 보유.");

            // 스캔 RT 원본 저장 — 오버레이 UI 없는 센티널 시점 증거.
            Assert.That(src, Does.Contain("58_scan_"), "[58-P1] 스캔 RT PNG 저장.");
            Assert.That(src, Does.Contain("_nextScanSave"), "[58-P1] 스캔 RT 별도 쿨다운(스크린 캡처와 분개).");
        }
    }
}
