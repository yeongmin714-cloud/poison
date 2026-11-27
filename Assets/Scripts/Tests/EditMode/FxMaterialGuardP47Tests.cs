using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 47] 렌더러 머티리얼 가드 경로 + 스윙 중 아크 원칙 계약.
    /// 실측 근거(11-15 46차 Play — Editor.log + 캡처 3건 + 공격씬 테스트 6.mp4 픽셀 스캔):
    ///  ①FxEmberRise 파티클 null 머티리얼(FXPalette.ApplyTo 단일 세터 NRE 무음 삼킴) → 마젠타 빌보드.
    ///  ②Monster_slime/input SMR null — FxDissolveUtility .material 세터 NRE 부분 갱신 → 마젠타 시체,
    ///    센티널 웜그레이 수리 후 "흰 덩어리 시체"(영상 t=6.5~9.5 흰 스파이크와 처치 시점 일치).
    ///  ③흰 아크 지속 — 스피어 시퀀스 은퇴 시 OFF 부재(상태 미진입 경로에서 EndCombo/레거시 탈출 미발화).
    /// </summary>
    public class FxMaterialGuardP47Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void MaterialGuard_SingleGate_WithOriginalRestore()
        {
            string src = Read("Assets/Scripts/Systems/FxMaterialLibrary.cs");
            Assert.That(src, Does.Contain("[무번호 47-a]"), "[47] 가드 마커.");
            Assert.That(src, Does.Contain("public static bool TryAssignSharedMaterials(Renderer rend, Material[] mats, string context)"),
                "[47] 배열 대입 단일 관문.");
            Assert.That(src, Does.Contain("rend.sharedMaterials = original;"),
                "[47] 실패 시 원본 복원 — null화 금지(마젠타 시체 원천 차단).");
            Assert.That(src, Does.Contain("[47-MATGUARD] 머티리얼 대입 실패·원본 복원"),
                "[47] 실패 1급 로그(context 동반).");
            Assert.That(src, Does.Contain("public static bool TryAssignSlot0(Renderer rend, Material slot0, string context)"),
                "[47] .material 세터 승계용 슬롯 0 오버로드.");
            Assert.That(src, Does.Contain("재시도 없음"),
                "[47] 세션 파손 결정론 — 무의미한 재시도 금지 근거 명시.");
        }

        [Test]
        public void AllRuntimeAssignPaths_RoutedThroughGuard()
        {
            string palette = Read("Assets/Scripts/Systems/FXPalette.cs");
            Assert.That(palette, Does.Contain("FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, \"FXPalette.ApplyTo\")"),
                "[47-b] ApplyTo 가드화 — FxEmberRise 마젠타 무음 삼킴 제거.");

            string dissolve = Read("Assets/Scripts/Systems/FxDissolveUtility.cs");
            Assert.That(dissolve, Does.Contain("FxMaterialLibrary.TryAssignSlot0(rend, inst, \"FxDissolve\")"),
                "[47-b] 디졸브 교체 가드화 — 슬롯 null 시체 방지.");
            Assert.That(dissolve, Does.Contain("Object.Destroy(inst);   // 미소유 인스턴스 즉시 정리"),
                "[47-b] 실패 시 미소유 인스턴스 누출 금지.");
            Assert.That(dissolve, Does.Contain("sharedMaterials 직접 판독"),
                "[47-b→49-T1] 램프 루프 격리 승계 — .material 게터 자체를 제거(클론 슬롯 대입 부작용 원천 차단).");

            string death = Read("Assets/Scripts/Systems/DeathVFX.cs");
            Assert.That(death, Does.Contain("FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, \"DeathFlipbookSmoke\")"),
                "[47-b] 46차 배열 대입의 가드 승계(46-T1 폴백 계약 유지).");

            string flash = Read("Assets/Scripts/Systems/FxSlashFlipbookFlash.cs");
            Assert.That(flash, Does.Contain("FxMaterialLibrary.TryAssignSharedMaterials(rnd, new Material[] { mat }, \"FxSlashFlipbookFlash\")"),
                "[47-b] 슬래시 플립북 가드화.");
        }

        [Test]
        public void SwingArc_ExistsOnlyDuringSwing()
        {
            string src = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(src, Does.Contain("[무번호 47-P1] 시퀀스 은퇴 = 스윙 종료"),
                "[47-P1] 스피어 은퇴 시 아크 OFF — 상태 미진입 경로의 OFF 누락 차단.");
            Assert.That(src, Does.Contain("SetSwingTrailEmitting(false);\n            AttackArcVFX.Settle();"),
                "[47-P1] 은퇴 지점 트레일 종료+아크 세틀 쌍.");
            Assert.That(src, Does.Contain("private const float SwingEmitMaxSeconds = 2.0f"),
                "[47-P1] 방출 상한 2.0s — 콤보 1스테이지보다 긴 안전 상한.");
            Assert.That(src, Does.Contain("[47-ARC] 스윙 방출 안전왕치"),
                "[47-P1] 상한 초과 강제 종료 — 어떤 OFF 누락에도 아크 잔존 금지.");
            Assert.That(src, Does.Contain("WatchSwingEmitDeadline();"),
                "[47-P1] UpdatePlayer 매 프레임 감시 배선.");
        }
    }
}
