using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 76] 시트애니 스파크 + 충격 먼지 컬럼 + CombatFXGate 배선 계약 테스트.
    /// 소스 문자열 계약 방식(FxCombatResidueP71Tests 스타일). 부정 잠금은 코드형 토큰만 사용
    /// (단어형 금지 — 자기 주석 오염 주의). 계획서: docs/plans/2026-11-18_no76-impact-sheet-spark-dust-column-plan.md.
    /// </summary>
    public class FxGpuQualityP76Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        // ── S1: 시트애니 스파크 — 절차 시트 결정론 + 단발 스폰 + 예산 ──
        [Test]
        public void Source_S1_ImpactSheetSpark_Contract()
        {
            string src = Read("Assets/Scripts/Systems/FxImpactSheetSpark.cs");
            Assert.That(src, Does.Contain("[무번호 76-S1]"), "[76-S1] 마커.");
            Assert.That(src, Does.Contain("public static void Burst(Vector3 position, bool isCrit)"), "[76-S1] Burst static 진입점.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"), "[76-S1] EditMode 무동작 가드(스폰 0건).");
            // 절차 시트 256×512(4열×2행 8프레임) — 캐시 1회(결정론).
            Assert.That(src, Does.Contain("new Texture2D(256, 512, TextureFormat.RGBA32, false)"), "[76-S1] 시트 해상도 256×512(pow2, mipChain=false).");
            Assert.That(src, Does.Contain("sheet.numTilesX = 4;"), "[76-S1] 시트애니 4열.");
            Assert.That(src, Does.Contain("sheet.numTilesY = 2;"), "[76-S1] 시트애니 2행.");
            Assert.That(src, Does.Contain("new ParticleSystem.MinMaxCurve(0f, 1f)"), "[76-S1] frameOverTime 0→1 1패스(FxSlashFlipbookFlash 선례).");
            // 사각 금지 — smoothstep 방사 마스크(결정론 해시 — Random 0건).
            Assert.That(src, Does.Contain("Mathf.SmoothStep(1f, 0f, r01)"), "[76-S1] smoothstep 방사(0프레임 점화 소체).");
            Assert.That(src, Does.Contain("Hash(d, f, Seed) - 0.5f) * 20f"), "[76-S1] 8방향 사선 파편 결정론 해시 각도.");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[76-S1] 결정론 — 난수 경로 0건.");
            // 색 단일 소스 — FXPalette.AttackPrimary(공격 이펙트 통일색 — HDR 0 — LDR 고정 66 계약).
            Assert.That(src, Does.Contain("var uni = FXPalette.AttackPrimary;"), "[76-S1 갱신 79-R20] 통일색 단일 소스.");
            Assert.That(src, Does.Contain("main.startColor = new Color(uni.r, uni.g, uni.b, 1f);"), "[76-S1 갱신 79-R20] 틴트 = AttackPrimary(HDR 0).");
            // 예산 — 동시 상한 6 + 쿨다운 0.12s(정적 필드), 링 예산 문자열 0건.
            Assert.That(src, Does.Contain("private const int MaxConcurrentSparks = 6;"), "[76-S1] 동시 스폰 상한 6.");
            Assert.That(src, Does.Contain("if (_recentBurstTimes.Count >= MaxConcurrentSparks) return;"), "[76-S1] 상한 초과 조용히 스킵.");
            Assert.That(src, Does.Contain("private const float CooldownSec = 0.12f;"), "[76-S1] 쿨다운 0.12s.");
            Assert.That(src, Does.Contain("if (Time.time - _lastBurstTime < CooldownSec) return;"), "[76-S1] 쿨다운 가드(정적 필드 — 결정론).");
            Assert.That(src, Does.Not.Contain("_activeRingCount"), "[76-S1] 링 예산(X2) 미소비.");
            Assert.That(src, Does.Not.Contain("MaxConcurrentRings"), "[76-S1] 링 예산 문자열 0건.");
        }

        // ── S1: 머티리얼 유한 파괴 + isPlaying 가드(FxSlashFlipbookFlash 21-K1 선례 정합) ──
        [Test]
        public void Source_S1_MaterialFiniteDestruction_AndGuard()
        {
            string src = Read("Assets/Scripts/Systems/FxImpactSheetSpark.cs");
            Assert.That(src, Does.Contain("FxMaterialLibrary.CreateSoftParticle(\"ImpactSheetSparkMat\""), "[76-S1] 머티리얼 = FxMaterialLibrary 경로(Sprites/Default·URP 폴백 선례 재사용).");
            Assert.That(src, Does.Contain("FxMaterialLibrary.AlignParticleVertexStreams(rnd);"), "[76-S1] 69-P2 AnimFrame 스트림 정합(시트애니 필수).");
            Assert.That(src, Does.Contain("if (Application.isPlaying) { Object.Destroy(go, Lifetime + 0.1f); Object.Destroy(mat, Lifetime + 0.1f); }"), "[76-S1] 인스턴스 유한 파괴(Play).");
            Assert.That(src, Does.Contain("else { Object.DestroyImmediate(go); Object.DestroyImmediate(mat); }"), "[76-S1] Destroy/DestroyImmediate 분기(21-K1 수리 선례).");
        }

        // ── S2: 충격 먼지 컬럼 — 전용 절차 스트립 + 수직 에미션 ──
        [Test]
        public void Source_S2_ImpactDustColumn_Contract()
        {
            string src = Read("Assets/Scripts/Systems/FxImpactDustColumn.cs");
            Assert.That(src, Does.Contain("[무번호 76-S2]"), "[76-S2] 마커.");
            Assert.That(src, Does.Contain("public static void Spawn(Vector3 position)"), "[76-S2] Spawn static 진입점.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"), "[76-S2] EditMode 무동작 가드.");
            // 전용 절차 텍스처 64×256 세로 스트립 — 캐시 1회, EnsurePuffTexture 무참조(74 private 잠금).
            Assert.That(src, Does.Contain("new Texture2D(w, h, TextureFormat.RGBA32, false)"), "[76-S2] 전용 텍스처 절차 생성.");
            Assert.That(src, Does.Contain("const int w = 64, h = 256;"), "[76-S2] 세로 스트립 64×256.");
            Assert.That(src, Does.Not.Contain("EnsurePuffTexture"), "[76-S2] EnsurePuffTexture 무참조(private 잠금 — 74 확정).");
            // 사각 금지 — smoothstep 그라데이션 + 하단 방사 마스크(결정론).
            Assert.That(src, Does.Contain("Mathf.SmoothStep(1f, 0f, v)"), "[76-S2] 세로 smoothstep 그라데이션(하단 진·상단 투명).");
            Assert.That(src, Does.Contain("Mathf.Exp(-(du * du) / (2f * 0.18f * 0.18f))"), "[76-S2] 하단 방사 마스크(가우시안 — 사각 금지).");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[76-S2] 결정론 — 난수 경로 0건.");
            // 파티클 4개 수직 에미션 — 위로 1.2~1.8m(0.5s), 세로 스트레치, 흙톤.
            Assert.That(src, Does.Contain("private const int ParticleCount = 4;"), "[76-S2] 파티클 4개 수직 에미션.");
            Assert.That(src, Does.Contain("2.4f + 1.2f * Hash(i, 3, Seed)"), "[76-S2] 결정론 벤치 2.4~3.6m/s × 0.5s = 위로 1.2~1.8m.");
            Assert.That(src, Does.Contain("ParticleSystemRenderMode.Stretch"), "[76-S2] 세로 스트레치 빌보드.");
            Assert.That(src, Does.Contain("new Color(0.78f, 0.72f, 0.62f, 0.35f)"), "[76-S2] 흙톤(TrailDustColor 선례 정합 — α 0.35).");
            // 사운드 0건(72-W3 ImpactSoundFX 위계 무손상) + 쿨다운 0.12s 독립 카운터.
            Assert.That(src, Does.Not.Contain("ImpactSoundFX"), "[76-S2] 사운드 0건.");
            Assert.That(src, Does.Contain("private const float CooldownSec = 0.12f;"), "[76-S2] 쿨다운 0.12s(독립 카운터).");
            Assert.That(src, Does.Contain("if (Time.time - _lastSpawnTime < CooldownSec) return;"), "[76-S2] 쿨다운 가드.");
            // 수명 유한 + 머티리얼 유한 파괴(21-K1 분기).
            Assert.That(src, Does.Contain("if (Application.isPlaying) { Object.Destroy(go, Lifetime + 0.1f); Object.Destroy(mat, Lifetime + 0.1f); }"), "[76-S2] 수명 유한 파괴.");
            Assert.That(src, Does.Contain("else { Object.DestroyImmediate(go); Object.DestroyImmediate(mat); }"), "[76-S2] Destroy/DestroyImmediate 분기(21-K1).");
        }

        // ── S3: CombatFXGate 배선 — L144 직후 게이트 밴드 공유 2줄 + 기존 잠금 무손상 ──
        [Test]
        public void Source_S3_GateWiring_GateBandShared()
        {
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(gate, Does.Contain("[무번호 76-S3]"), "[76-S3] 마커.");
            // [무번호 79 사용자 결정 진화] 시트 스파크 게이트 배선 해제 — "노란색 스파크 연출 촌스럽다" 사용자
            //   판정(11-19). 시스템 본체 유지, 게이트 호출 0건(주석 잔코드 ≠ 활성 배선)을 라인 단위 검증.
            Assert.That(gate, Does.Contain("[무번호 79 사용자 결정]"), "[79 진화] 시트 스파크 제거 마커.");
            bool activeSpark = false;
            foreach (string raw in gate.Split('\n'))
            {
                string line = raw.TrimStart();
                if (line.StartsWith("//")) continue;   // 주석 줄 제외
                if (line.Contains("FxImpactSheetSpark.Burst(position"))
                {
                    activeSpark = true;
                    break;
                }
            }
            Assert.That(activeSpark, Is.False, "[79 진화] 시트 스파크 활성 게이트 호출 0건.");
            Assert.That(gate, Does.Contain("if (isCrit || damage >= 15f) FxImpactDustColumn.Spawn(position);"), "[76-S3] 먼지 컬럼 배선 — 강타격 게이트 밴드 공유.");
            // 배치 계약 — 크랙(37-H3) 직후 먼지 컬럼([79 진화]: 스파크 제거로 2단 배치).
            int crack = gate.IndexOf("if (isCrit || damage >= 15f) FxGroundCrackFX.Burst(position);", System.StringComparison.Ordinal);
            int dust = gate.IndexOf("FxImpactDustColumn.Spawn(position);", System.StringComparison.Ordinal);
            Assert.That(crack, Is.GreaterThanOrEqualTo(0), "[76-S3] 크랙 훅 존재.");
            Assert.That(dust, Is.GreaterThan(crack), "[76-S3] 먼지 컬럼은 크랙 직후(계획서 배치 계약).");
            // 기존 잠금 문자열 무손상 — [70-U3]/[71-L2]/46차 축소 주석/37-H3 훅/33-A1 라이트.
            Assert.That(gate, Does.Contain("[무번호 70-U3]"), "[76-S3] [70-U3] 주석 무손상.");
            Assert.That(gate, Does.Contain("[무번호 71-L2]"), "[76-S3] [71-L2] 주석 무손상.");
            Assert.That(gate, Does.Contain("46차 정리: 타격 체인 대폭 축소"), "[76-S3] 46차 축소 주석 무손상.");
            Assert.That(gate, Does.Contain("if (isCrit || damage >= 15f) FxGroundCrackFX.Burst(position);"), "[76-S3] [37-H3] 훅 무손상.");
            Assert.That(gate, Does.Contain("FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f, isCrit);"), "[76-S3] [37-S3]/[70-U3] 호출부 무손상.");
            Assert.That(gate, Does.Contain("FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f);"), "[76-S3] [71-L2] 호출부 무손상.");
        }

        // ── 잠금 무손상 — FxDistortionWave/FxSlashFlipbookFlash/EnsurePuffTexture 보유 파일 무수정(간접 확인) ──
        [Test]
        public void Source_LockedFiles_Unharmed()
        {
            // FxDistortionWave — 본 라운드 접촉 없음(71 마커 보존 + 76 마커 부재로 간접 확인).
            string wave = Read("Assets/Scripts/Systems/FxDistortionWave.cs");
            Assert.That(wave, Does.Contain("[무번호 71-L2]"), "[76] 71-L2 마커 보존.");
            Assert.That(wave, Does.Contain("public static void Burst(Vector3 position, float scale)"), "[76] Burst 시그니처 무손상.");
            Assert.That(wave, Does.Not.Contain("[무번호 76"), "[76] FxDistortionWave 무수정 — 76 마커 부재.");
            // FxSlashFlipbookFlash — 본 라운드 접촉 없음(37-S3 마커 보존 + 패턴 참조만).
            string slash = Read("Assets/Scripts/Systems/FxSlashFlipbookFlash.cs");
            Assert.That(slash, Does.Contain("[무번호 37-S3]"), "[76] 37-S3 마커 보존.");
            Assert.That(slash, Does.Contain("public static void Play(Vector3 position, float scale, bool isCrit = false)"), "[76] Play 시그니처 무손상.");
            Assert.That(slash, Does.Not.Contain("[무번호 76"), "[76] FxSlashFlipbookFlash 무수정 — 76 마커 부재.");
            // ShockwaveRingFX — EnsurePuffTexture private 잠금 보존(74 확정 — 무접촉).
            string ring = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.That(ring, Does.Contain("private static Texture2D EnsurePuffTexture()"), "[76] EnsurePuffTexture private 잠금 보존.");
            Assert.That(ring, Does.Not.Contain("[무번호 76"), "[76] ShockwaveRingFX 무수정 — 76 마커 부재.");
        }

        // ── [무번호 82] 피격 이펙트 Free Hit Impact 단일화 — 보류 레이어 활성 호출 0건 계약 ──
        // 사용자 판정(11-20): "지금처럼 잡다한 것보다 기존의 free hit impact 에셋" — 발화선 해제(시스템 본체 유지).
        // 주석 잔코드 ≠ 활성 배선 — 라인 단위 검증(FxHitRewok39 T2/FxGpuQualityP76 S3 시트 스파크 선례).
        [Test]
        public void Source_82_HitLayerSuspension()
        {
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            foreach (var needle in new[]
            {
                "FxDistortionWave.Burst(position",              // [71-L2]
                "FxSecondaryRipple.Schedule(position",          // [77]
                "ScreenBloomFX.Spike(Camera.main",              // [33-B1]
                "FxDebrisSpawner.Burst(position",               // [33-D1]
                "FxGroundCrackFX.Burst(position",               // [37-H3]
                "FxImpactDustColumn.Spawn(position",            // [76-S3]
                "MeteoriteVFXRunner.Burst(position"             // [78-P1]
            })
            {
                bool active = false;
                foreach (string raw in gate.Split('\n'))
                {
                    string line = raw.TrimStart();
                    if (line.StartsWith("//")) continue;
                    if (line.Contains(needle)) { active = true; break; }
                }
                Assert.That(active, Is.False, $"[82 보류] {needle} 활성 게이트 호출 0건.");
            }
        }
    }
}
