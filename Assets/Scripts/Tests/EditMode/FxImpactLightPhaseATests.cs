using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 33-A] 임팩트 실시간 라이트 계약 — 전투 FX 첫 동적 광원 계층.
    /// 예산 가드/유한 수명(누출 금지)/결정론/훅 배선/기존 경로 무손상 검증.
    /// </summary>
    public class FxImpactLightPhaseATests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── A: 스파이크 발화부 — 가드/예산/선형 소멸 ──
        [Test]
        public void Source_33A_SpikeEntry()
        {
            string src = Read("Assets/Scripts/Systems/FxImpactLight.cs");
            Assert.That(src, Does.Contain("[무번호 33-A]"), "[33-A] 마커.");
            Assert.That(src, Does.Contain("public static void Spike(Vector3 position, Color color, float intensity, float range, float duration)"),
                "[33-A] 스파이크 시그니처 계약.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"),
                "[33-A] EditMode 무동작 가드.");
            Assert.That(src, Does.Contain("public const int MaxConcurrentLights = 3;"),
                "[33-A] 동시 라이트 예산 상한 3.");
            Assert.That(src, Does.Contain("if (_activeCount >= MaxConcurrentLights) return;"),
                "[33-A] 예산 가드 배선 — 초과 조용 스킵(23-X2 선례).");
            Assert.That(src, Does.Contain("catch (System.Exception) { }"),
                "[33-A] 시각 보조 try/catch — 전투 방해 금지(23-V 선례).");
            Assert.That(src, Does.Not.Contain("Random"), "[33-A] 결정론 — Random 0건(신규 파일 전문).");
        }

        // ── A: 러너 유한 수명 — 누출 금지 + 카운트 정합 ──
        [Test]
        public void Source_33A_RunnerLifecycle()
        {
            string src = Read("Assets/Scripts/Systems/FxImpactLight.cs");
            Assert.That(src, Does.Contain("_light.type = LightType.Point;"), "[33-A] 포인트 라이트.");
            Assert.That(src, Does.Contain("_light.shadows = LightShadows.None;"),
                "[33-A] 단명 라이트 섀도우 비용 방지(LootSpawnFX 선례).");
            Assert.That(src, Does.Contain("Time.unscaledDeltaTime"),
                "[33-A] unscaled 시간 — 히트스톱 중에도 재생(VignettePulse 선례).");
            Assert.That(src, Does.Contain("_light.intensity = _base * (1f - Mathf.Clamp01(_t / _duration));"),
                "[33-A] 즉시 peak → 선형 소멸.");
            Assert.That(src, Does.Contain("Destroy(gameObject); return;"),
                "[33-A] 유한 수명 — 자가 파괴(누출 금지).");
            Assert.That(src, Does.Contain("FxImpactLight.Release();   // 유한 수명 종료/조기 소멸 공용 — 카운트 정합(단일 감산 경로)"),
                "[33-A] 소멸 카운트 단일 경로(OnDestroy) — 이중 감산 방지.");
        }

        // ── A1: 히트 임팩트 훅(CombatFXGate 단일 통로) ──
        [Test]
        public void Source_33A_HitHook()
        {
            string src = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            // [무번호 38 갱신 79-R20] 강도/반경/지속 승격(영상 판독 — 히트 광 미체감 수리). 색=AttackPrimary(공격 이펙트 통일색) 유지.
            Assert.That(src, Does.Contain("FxImpactLight.Spike(position, FXPalette.AttackPrimary, isCrit ? 4.2f : 2.2f, 3.8f, 0.18f);"),
                "[33-A1 갱신 79-R20] 히트 지점 라이트(색=AttackPrimary 통일색 — 39-T3 크리 앰프 승계 4.2).");
            int hookCount = System.Text.RegularExpressions.Regex.Matches(src, "FxImpactLight\\.Spike").Count;
            Assert.That(hookCount, Is.EqualTo(1), "[33-A1] 발화처 1곳 한정 — Internal 단일 통로.");
            Assert.That(src, Does.Contain("SlashVFXRunner.PlayImpactMulti(position, type, isCrit ? 1.5f : 1f);"),
                "[33-A1] 기존 Guz 임팩트 경로 무손상.");
        }

        // ── A2: 패망 PERFECT 흰 라이트(시퀀서 wave0 연동) ──
        [Test]
        public void Source_33A_ParryHook()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("FXPalette.ParryWhite, 2.4f, 4f, 0.3f);"),
                "[33-A2] 패망 흰 라이트(색=ParryGold 단일 소스, 0.3s).");
            // [176차 갱신] 크로매틱 수차 스파이크 패막 경로 철수 — RGB 프린지(청백 왜곡 원인) 제거(순백 계약).
            //   라이트는 ParryWhite 무채색 유지 — 순백 화면 계층 잔존 요소.
            int hookCount = System.Text.RegularExpressions.Regex.Matches(src, "ScreenChromaticFX\\.Spike").Count;
            Assert.That(hookCount, Is.EqualTo(0), "[176차] 패막 경로 크로매틱 스파이크 발화 0건.");
        }

        // ── A3: 사망 소멸 광(디졸브 동기 — 폴백 시 스킵) ──
        [Test]
        public void Source_33A_DeathHook()
        {
            string src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            // [무번호 39-T4 갱신] 디졸브 티어 위계 오버로드 배선 승계(정예=엣지 강화+창 연장).
            Assert.That(src, Does.Contain("FxDissolveUtility.TryBeginDissolve(gameObject, FXPalette.MonsterDanger, 0.9f, (int)_tier)"),
                "[33-A3] 30-D4 디졸브 호출 문자열(티어 위계 승계) 무손상.");
            Assert.That(src, Does.Contain("bool dissolveBegan ="),
                "[33-A3] 디졸브 성공 여부 변수화 — 폴백 시 라이트 스킵 계약.");
            Assert.That(src, Does.Contain("FXPalette.MonsterDanger, 1.6f, 5f, 0.9f);"),
                "[33-A3] 소멸 광(색=MonsterDanger 단일 소스, 0.9s 램프 동기).");
            int iSpike = src.IndexOf("[무번호 33-A3] 사망 소멸 광");
            int iSeq = src.IndexOf("StartCoroutine(DeathDownRoutine(respawnDelay));");
            Assert.That(iSpike, Is.GreaterThan(0), "[33-A3] 소멸 광 훅 존재.");
            Assert.That(iSpike, Is.LessThan(iSeq), "[33-A3] 소멸 광은 사망 시퀀스 시작 전 발화(D-4 순서 계약 승계).");
            int iBegin = src.IndexOf("if (dissolveBegan)");
            Assert.That(iSpike, Is.GreaterThan(iBegin), "[33-A3] 소멸 광은 디졸브 성공 블록 내부(동기 계약).");
        }
    }
}
