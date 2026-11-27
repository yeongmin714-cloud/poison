using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 35] 전투 VFX 품질 도약 1라운드 계약 — S1 프리미엄 참격 리본 기본 활성 +
    /// S2 히트 실루엣 화이트아웃(FxRimOverlay 재사용) + R1 이동 경로 체브런(72차 규약 동기).
    /// </summary>
    public class FxCombatLeapS1S2R1Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── S1: 프리미엄 참격 리본 기본 ON(F6 토글 유지) ──
        [Test]
        public void Source_35S1_PremiumArcDefaultOn()
        {
            string src = Read("Assets/Scripts/Systems/AttackArcVFX.cs");
            Assert.That(src, Does.Contain("[무번호 35-S1]"), "[35-S1] 마커.");
            Assert.That(src, Does.Contain("public static bool PremiumArcEnabled = true;"),
                "[35-S1] 리본 기본 활성 — 기존 공격 연출 위 프리미엄 계층.");
            string driver = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(driver, Does.Contain("private bool _premiumArcEnabled = true;"),
                "[35-S1] 드라이버 기본값 동기 — 토글 경로와 진실원 일치.");
            Assert.That(driver, Does.Contain("KeyCode.F6"),
                "[35-S1] F6 A/B 토글 유지(기존 이력 보존 계약).");
        }

        // ── S2: 실루엣 화이트아웃 발화부 ──
        [Test]
        public void Source_35S2_HitSilhouette()
        {
            string src = Read("Assets/Scripts/Systems/FxHitSilhouette.cs");
            Assert.That(src, Does.Contain("[무번호 35-S2]"), "[35-S2] 마커.");
            Assert.That(src, Does.Contain("public static void Flash(GameObject target)"),
                "[35-S2] 플래시 시그니처 계약.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying || target == null) return;"),
                "[35-S2] EditMode 무동작 가드 + null 가드.");
            Assert.That(src, Does.Contain("public const int MaxConcurrentFlashes = 6;"),
                "[35-S2] 동시 플래시 예산 상한 6(33-A1 선례).");
            Assert.That(src, Does.Contain("FxRimOverlay.AttachRim(smr, FXPalette.AttackPrimary, 2.0f, PeakIntensity)"),
                "[35-S2 갱신 79-R20] SMR 경로 — 공격 이펙트 통일색 림(FXPalette.AttackPrimary 단일 소스).");
            Assert.That(src, Does.Contain("FxRimOverlay.AttachRimStatic(mr.gameObject, FXPalette.AttackPrimary, 2.0f, PeakIntensity)"),
                "[35-S2 갱신 79-R20] 정적 메시 폴백 경로(통일색).");
            Assert.That(src, Does.Contain("if (overlay == null) return;   // 셰이더 부재/메시 부재 폴백 — 기존 틴트 경로만(29-D3 승계)"),
                "[35-S2] 폴백 계약 — 기존 틴트 경로 유지.");
            Assert.That(src, Does.Contain("Time.unscaledDeltaTime"), "[35-S2] unscaled 시간.");
            Assert.That(src, Does.Not.Contain("Random"), "[35-S2] 결정론 — 외부 난수 0건(신규 파일 전문).");
        }

        // ── S2: 훅 2오버로드 + FxRimOverlay 정적 지원 확장 ──
        [Test]
        public void Source_35S2_HooksAndRimStatic()
        {
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            int hooks = System.Text.RegularExpressions.Regex.Matches(gate, "FxHitSilhouette\\.Flash\\(target\\);").Count;
            Assert.That(hooks, Is.EqualTo(2), "[35-S2] 두 진입 오버로드 모두 배선.");

            string rim = Read("Assets/Scripts/Systems/FxRimOverlay.cs");
            Assert.That(rim, Does.Contain("[무번호 35-S2] 정적 림 오버로드 대응(무기/정적 메시)"),
                "[35-S2] SetRimIntensity 정적 경로 — 미지원 시 플래시가 소멸 없이 잔존하는 결함 방지.");
            Assert.That(rim, Does.Contain("[무번호 35-S2] 정적 오버로드(AttachRimStatic — MeshRenderer) 머티리얼도 함께 파괴(누출 금지)"),
                "[35-S2] Release 정적 머티리얼 파괴 — 누출 금지.");
            Assert.That(rim, Does.Contain("public static GameObject AttachRimStatic(GameObject sourceVisual"),
                "[35-S2] 기존 29-P1-2 잠금 무손상.");
        }

        // ── R1: 이동 경로 체브런(72차 규약 동기) ──
        [Test]
        public void Source_35R1_MoveChevrons()
        {
            string src = Read("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.That(src, Does.Contain("[무번호 35-R1]"), "[35-R1] 마커.");
            Assert.That(src, Does.Contain("private void TrySpawnMoveChevrons()"), "[35-R1] 체브런 배선 진입점.");
            Assert.That(src, Does.Contain("if (!_persistent || _groupOwners == null || _groupOwners.Count == 0) return;"),
                "[35-R1] 지속형(이동/집결) 전용 — 공격 페이드형 미발화.");
            Assert.That(src, Does.Contain("if (dir.sqrMagnitude < 4f) return;   // 2m 미만 — 방향 무의미"),
                "[35-R1] 근거리 미발화 가드.");
            Assert.That(src, Does.Contain("TrySpawnMoveChevrons();   // [무번호 35-R1] 이동 명령 경로 체브런(방향 감각)"),
                "[35-R1] Start 배선.");
            Assert.That(src, Does.Contain("private const float ChevronLifeSeconds = 1f;"),
                "[35-R1] 72차 1s 규약 수명 동기.");
            Assert.That(src, Does.Contain("transform.localScale = _full * (1f - 0.45f * fadeT);   // 마커 축소 페이드와 동일 계수"),
                "[35-R1] 소멸 계수 = 마커 페이드 패리티(0.45).");
            Assert.That(src, Does.Contain("mat.color = CommandRingPresentation.MoveTargetColor;"),
                "[35-R1] 색 단일 소스(골드 팀색).");
            Assert.That(src, Does.Contain("private const float FadeSeconds = 0.3f;"),
                "[35-R1] 기존 72차 규약 상수 무손상.");
        }

        // ── [무번호 82] 이동 경로 체브런 발화 해제 — 목적지 링만(사용자 요구 11-20) ──
        [Test]
        public void Source_82_MoveChevronSuspension()
        {
            string src = Read("Assets/Scripts/Systems/CommandMarker.cs");
            bool active = false;
            foreach (string raw in src.Split('\n'))
            {
                string line = raw.TrimStart();
                if (line.StartsWith("//")) continue;
                if (line.Contains("TrySpawnMoveChevrons();")) { active = true; break; }
            }
            Assert.That(active, Is.False, "[82 보류] 이동 경로 체브런 활성 발화 0건(목적지 링만 표시).");
            Assert.That(src, Does.Contain("private void TrySpawnMoveChevrons()"),
                "[82] 체브런 본체·공유 머티리얼 유지(예고 수렴 눈금 재사용 — AttackTelegraphIndicator SharedChevronMaterial 계약).");
        }
    }
}
