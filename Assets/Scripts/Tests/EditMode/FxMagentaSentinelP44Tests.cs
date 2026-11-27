using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 44] 마젠타 근원 자동 확정 + 몬스터 무피해 사각지대 진단 장치 계약.
    /// 실측 근거(11-15 21:00 세션 — Editor-prev.log):
    ///  ① 공격 2회 모두 드라이버 수락("[Combo] Spear segment accepted")까지 갔으나 "[Spear] AttackThrust
    ///     hit resolved" 0건 → 몬스터 무피해 → 사망 없음 → 전리품 없음(사용자 보고 3건의 단일 사슬).
    ///  ② 센티널(43)은 렌더러 셰이더 파손만 감시 — "셰이더 정상 + 내용물(텍스처) 마젠타"와 VFX Graph 경로 미커버.
    /// 본 라운드는 수리 없음(확정 전 수리금지 원칙) — 관측 장치의 존재와 판정식만 잠근다.
    /// </summary>
    public class FxMagentaSentinelP44Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void Sentinel_TextureProbe_And_VfxInventory_And_Capture()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            Assert.That(src, Does.Contain("[무번호 44-SENTINEL]"), "[44] 확장 마커.");
            Assert.That(src, Does.Contain("using UnityEngine.VFX;"),
                "[44] VFX Graph 경로 — Renderer 밖 감시 커버(인벤토리 로그).");
            Assert.That(src, Does.Contain("마젠타 텍스처 감지"),
                "[44] 셰이더 정상+텍스처 마젠타 케이스 감지 로그(보고 전용).");
            Assert.That(src, Does.Contain("ScreenCapture.CaptureScreenshot"),
                "[44] 감지 직후 근원 시각 증거 캡처.");
            Assert.That(src, Does.Contain("_nextCapture = Time.unscaledTime + 10f"),
                "[44] 캡처 쿨다운 10s — 스팸 방지.");
            Assert.That(src, Does.Contain("probeBudget"),
                "[44] 텍스처 프로브 예산(스윕당 8) — 스윕 비용 상한.");
            // 43 계약 보존 확인(승계 무손상).
            Assert.That(src, Does.Contain("Hidden/InternalErrorShader"), "[43] 파손 셰이더 감지 유지.");
            Assert.That(src, Does.Contain("MagentaSentinel_Fallback"), "[43] 폴백 수리 유지.");
        }

        [Test]
        public void Sentinel_PureMagentaPredicate_DistinguishesIntendedColors()
        {
            // 순수 마젠타(#FF00FF — InternalErrorShader/마젠타 채움 텍스처 공통)만 양성.
            Assert.IsTrue(MagentaSentinel.IsPureMagenta(new Color(1f, 0f, 1f)), "순수 마젠타 양성.");
            Assert.IsTrue(MagentaSentinel.IsPureMagenta(new Color(0.95f, 0.02f, 0.95f)), "실측 마젠타(243,5,241) 양성.");
            // 의도 색 오탐 방지 — 41-b 딥 퍼플(0.45,0.25,0.85)은 양성이 아니어야 한다.
            Assert.IsFalse(MagentaSentinel.IsPureMagenta(new Color(0.45f, 0.25f, 0.85f)), "딥 퍼플 FX 오탐 금지.");
            Assert.IsFalse(MagentaSentinel.IsPureMagenta(Color.white), "화이트 오탐 금지.");
            Assert.IsFalse(MagentaSentinel.IsPureMagenta(new Color(0.2f, 0.8f, 0.3f)), "녹색(슬라임) 오탐 금지.");
            Assert.IsFalse(MagentaSentinel.IsPureMagenta(new Color(0.55f, 0.36f, 0.53f)), "과노출 핑크(40-b 실측) 오탐 금지.");
        }

        [Test]
        public void PlayerCombat_MissDiagnosis_And_InputDeviceGate()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 44-AIM-DIAG]"),
                "[44] 콤보 경로 미스 1급 진단 — 3중 조건 전부 실패 시에도 관측 가능.");
            Assert.That(src, Does.Contain("LogMeleeMissDiagnosis()"),
                "[44] ResolveComboSwingHit 미스 분기에서 진단 호출.");
            Assert.That(src, Does.Contain("Physics.OverlapSphere(transform.position, sweepRange, _targetLayers)"),
                "[44] 주변 콜라이더 실측(레이어·IDamageable·거리) — 21:00 세션 미스 원인 판별 축.");
            Assert.That(src, Does.Contain("[무번호 44-INPUT-DIAG]"),
                "[44] 입력 디바이스 상태 세션 1회 관측 — 클릭 무음 소비 판별 게이트.");
            Assert.That(src, Does.Contain("_nextMissDiagLog = Time.unscaledTime + 3f"),
                "[44] 미스 진단 스로틀 3s — 스팸 방지.");
        }

        [Test]
        public void HumanoidClipDriver_SpearOpenerStateSampling()
        {
            string src = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(src, Does.Contain("[44-SPEAR-DIAG] opener 미해결 감시"),
                "[44] 오프너 상태/진행도 샘플 — 21:00 세션 '[Spear] hit resolved 0건' 근원 판별 축.");
            Assert.That(src, Does.Contain("_spearDiagSamples < 6"),
                "[44] 클릭당 샘플 상한 6회 — 로그 스팸 방지.");
            Assert.That(src, Does.Contain("_spearDiagNextLog = 0f;"),
                "[44] 클릭 즉시 샘플 개시(리셋 배선).");
        }
    }
}
