using NUnit.Framework;
using System.IO;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 57] 아크 전두엽 불투명 판 상한 계약 — 공격씬 10 실측(8.25s 크롭): strike 지점 아크 리본이
    /// "지면에 깔린 불투명 흰 다이아몬드"로 렌더(코어 알파 1.0×백 틴트 — 알파블렌드에서도 단색 판).
    /// 수리 = 코어 알파 상한 0.6 + 슬래시 텍스처 전단 소프트 캡 + FX 스폰 레지스트리(일시 근원 특정).
    /// </summary>
    public class AttackArcDiamondCap_57Tests
    {
        private static string ReadSource(string fileName)
        {
            string path = "Assets/Scripts/Systems/" + fileName;
            Assert.IsTrue(File.Exists(path), $"소스 파일 존재: {path}");
            return File.ReadAllText(path);
        }

        // ── ① 코어 패스 알파 상한 0.6 ────────────────────────────────

        [Test]
        public void Arc_CorePassAlpha_Capped()
        {
            string src = ReadSource("AttackArcVFX.cs");
            Assert.That(src, Does.Contain("internal const float CorePassAlphaMul = 0.6f;"),
                "[57-P1] 코어 알파 상한 0.6 — 1.0은 밝은 지면 위 불투명 흰 판으로 렌더(실측).");
            Assert.That(src, Does.Contain("EmitPass(now, trailSec, halfW, 1.0f, CorePassAlphaMul"),
                "[57-P1] 코어 EmitPass가 상한 상수 경유.");
            Assert.That(src, Does.Not.Contain("EmitPass(now, trailSec, halfW, 1.0f, 1.0f"),
                "[57-P1] 구 코어 α1.0 직접 대입 폐기.");
        }

        // ── ② 슬래시 텍스처 전단 소프트 캡 ────────────────────────────

        [Test]
        public void SlashTexture_FrontSoftCap()
        {
            string src = ReadSource("AttackArcVFX.cs");
            Assert.That(src, Does.Contain("internal const float AlphaCap = 0.62f;"),
                "[57-P1] 텍스처 구조적 최대 알파 0.62 — 페더+코어 합(1.85) Clamp01 포화가 불투명 판의 뿌리(실측).");
            Assert.That(src, Does.Contain("Mathf.Min(baseA, AlphaCap * Mathf.Lerp(1f, 0.85f, frontT)"),
                "[57-P1] 전단 8% 추가 감쇠 — 팁 하드 벽(다이아몬드 모서리) 방지.");

            // 픽셀 실측 — 구조적 최대가 캡되는지: 중심 최대 ≤ 0.62, 선단 중심 ≤ 0.55, 꼬리 페이드 유지.
            var px = new Color[AttackArcVFX.SlashTexWidth * AttackArcVFX.SlashTexHeight];
            AttackArcVFX.FillSlashPixels(px, AttackArcVFX.SlashTexWidth, AttackArcVFX.SlashTexHeight);
            int w = AttackArcVFX.SlashTexWidth, h = AttackArcVFX.SlashTexHeight;
            float maxA = 0f;
            foreach (var c in px) maxA = Mathf.Max(maxA, c.a);
            float frontCenter = px[(h / 2) * w + (w - 1)].a;
            float midCenter = px[(h / 2) * w + (w / 2)].a;
            float tailCenter = px[(h / 2) * w + (w / 10)].a;
            Assert.That(maxA, Is.LessThan(0.65f), "[57-P1] 텍스처 최대 알파 ≤ 0.62캡(+여유) — 불투명 판 근원 차단.");
            Assert.That(frontCenter, Is.LessThan(0.55f), "[57-P1] 선단 중심 ≤ 0.55 — 팁 소프트 소멸.");
            Assert.That(midCenter, Is.GreaterThan(tailCenter), "[53-P2 승계] 중심 > 꼬리 — 길이 페이드 유지.");
        }

        // ── ③ FX 스폰 레지스트리 — 일시 근원 특정 계약 ────────────────

        [Test]
        public void FxSpawnRegistry_Wired()
        {
            Assert.That(ReadSource("MagentaSentinel.cs"), Does.Contain("internal static void RegisterFxSource(string kind, Vector3 pos)"),
                "[57-P2] 레지스트리 진입점.");
            Assert.That(ReadSource("MagentaSentinel.cs"), Does.Contain("DescribeNearbyFx(center)"),
                "[57-P2] 클러스터 보고에 근접 FX 나열 결합 — 일시 FX(수명<스캔주기) 근원 특정.");

            string shock = ReadSource("ShockwaveRingFX.cs");
            Assert.That(shock, Does.Contain("RegisterFxSource(\"Ring\""), "[57-P2] 링 등록.");
            Assert.That(shock, Does.Contain("RegisterFxSource(\"Spark\""), "[57-P2] 스파크 등록.");
            Assert.That(shock, Does.Contain("RegisterFxSource(\"SwingSweep\""), "[57-P2] 스윙 파동 등록.");

            string slash = ReadSource("SlashVFXRunner.cs");
            Assert.That(slash, Does.Contain("RegisterFxSource(\"MagicHit\""), "[57-P2] 임팩트 등록.");
            Assert.That(slash, Does.Contain("RegisterFxSource(\"MagicHitCross\""), "[57-P2] 크로스 등록.");

            Assert.That(ReadSource("DeathVFX.cs"), Does.Contain("RegisterFxSource(\"DeathVFX\""), "[57-P2] 사망 버스트 등록.");
            Assert.That(ReadSource("AttackArcVFX.cs"), Does.Contain("RegisterFxSource(\"AttackArc\""), "[57-P2] 아크 등록.");
        }
    }
}
