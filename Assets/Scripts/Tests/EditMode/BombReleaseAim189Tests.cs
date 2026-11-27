using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [189차] 폭탄 릴리즈 투척 + 궤적 조준 프리뷰 계약테스트 — 실제 프리뷰/투척은 Play 영역, EditMode는 소스계약만.
    /// 근거: press 즉발 투척은 조준 실수를 그대로 반영(즉발) → 활 드로→릴리즈 선례와 동일한
    /// press=조준(궤적 프리뷰) / release=투척 패턴으로 전환.
    ///  ① PlayerCombat — 무장 press 분기에서 BeginAim(즉발 금지), 릴리즈 블록(wasReleasedThisFrame)에서 ThrowTowardCursor
    ///  ② AttackSystem — 무장 분기 GetKeyDown 즉발 투척 제거(이중 투척 방지, PlayerCombat 단일 소유)
    ///  ③ BombArmController — IsAiming/BeginAim/CancelAim API + Disarm 연동 프리뷰 정리
    ///  ④ 궤적 시뮬레이션 — throwForce/upBoost/Physics.gravity 반영, 착지 링 = explosionRadius(폭발 범위 시각화)
    ///  ⑤ 기존 투척 흐름 보존 — _isThrowPending 연타 금지 + ThrowReleaseDelaySeconds(0.35s) 지연 릴리스 유지
    /// </summary>
    public class BombReleaseAim189Tests
    {
        private const string CombatPath = "Assets/Scripts/Systems/PlayerCombat.cs";
        private const string AttackPath = "Assets/Scripts/Systems/AttackSystem.cs";
        private const string BombArmPath = "Assets/Scripts/Systems/BombArmController.cs";

        [Test]
        public void PlayerCombat_ArmedPressBeginsAim_NotInstantThrow()
        {
            string src = System.IO.File.ReadAllText(CombatPath);
            Assert.That(src, Does.Contain("BombArmController.Instance.BeginAim();"),
                "무장 상태 좌클릭 press = 조준 시작 — ThrowTowardCursor 즉발 대신 BeginAim 호출.");
            string pressBranch = Slice(src, "[폭탄][189차] 무장 상태 좌클릭 press", "[무번호 86-P3] 승마 중");
            Assert.That(pressBranch, Is.Not.Empty, "무장 press 분기 존재([189차] 주석 마커).");
            Assert.That(pressBranch, Does.Not.Contain("ThrowTowardCursor()"),
                "press 분기 즉발 투척 금지 — 투척은 릴리즈 프레임으로 이관.");
        }

        [Test]
        public void PlayerCombat_ReleaseFrameThrows_OnlyWhileAiming()
        {
            string src = System.IO.File.ReadAllText(CombatPath);
            Assert.That(src, Does.Contain("BombArmController.Instance.IsAiming && Mouse.current.leftButton.wasReleasedThisFrame"),
                "릴리즈 게이트 — IsAiming 중 좌클릭 해제 프레임에만 투척(활 드로→릴리즈 선례 패턴).");
            string releaseBlock = Slice(src,
                "BombArmController.Instance.IsAiming && Mouse.current.leftButton.wasReleasedThisFrame",
                "좌클릭 감지 (InputSystem)");
            Assert.That(releaseBlock, Does.Contain("BombArmController.Instance.ThrowTowardCursor();"),
                "릴리즈 블록 내 ThrowTowardCursor 호출 — press=조준/release=투척 완결.");
        }

        [Test]
        public void AttackSystem_ArmedBranch_DelegatesThrowToPlayerCombat()
        {
            string src = System.IO.File.ReadAllText(AttackPath);
            string armedBranch = Slice(src,
                "if (BombArmController.Instance != null && BombArmController.Instance.IsArmed)",
                "HorseMountSystem.IsMounted");
            Assert.That(armedBranch, Is.Not.Empty, "AttackSystem 무장 분기 존재.");
            Assert.That(armedBranch, Does.Not.Contain("BombArmController.Instance.ThrowTowardCursor();"),
                "무장 분기 GetKeyDown 즉발 투척 제거 — 이중 투척 방지(PlayerCombat 릴리즈 경로 단일 소유).");
            Assert.That(armedBranch, Does.Contain("return;"),
                "무장 중 일반 공격 차단 유지 — 분기 자체는 존속.");
        }

        [Test]
        public void BombArmController_AimStateApi_AndDisarmCleanup()
        {
            string src = System.IO.File.ReadAllText(BombArmPath);
            Assert.That(src, Does.Contain("public bool IsAiming"), "조준 상태 노출 — 릴리즈 게이트/프리뷰 판정용.");
            Assert.That(src, Does.Contain("public void BeginAim()"), "조준 시작 API — press에서 호출.");
            Assert.That(src, Does.Contain("public void CancelAim()"), "조준 취소 API — 프리뷰 정리.");
            string cancelBody = Slice(src, "public void CancelAim()", "private void EnsureAimPreviewObjects()");
            Assert.That(cancelBody, Does.Contain("DestroyAimPreviewObjects();"),
                "CancelAim = 조준 해제 + 프리뷰 오브젝트 파괴.");
            string disarmBody = Slice(src, "public void Disarm()", "public void ThrowTowardCursor()");
            Assert.That(disarmBody, Does.Contain("CancelAim();"),
                "Disarm 연동 — 해제 시 조준 프리뷰 즉시 파괴(프리뷰 잔류 방지).");
        }

        [Test]
        public void BombArmController_TrajectoryUsesThrowPhysics_AndExplosionRadiusRing()
        {
            string src = System.IO.File.ReadAllText(BombArmPath);
            string sampler = Slice(src, "private void UpdateAimPreview()", "/// <summary>[189차] 착지 링");
            Assert.That(sampler, Is.Not.Empty, "궤적 샘플러 존재.");
            Assert.That(sampler, Does.Contain("throwForce"), "궤적 초속 — 실제 투척(ReleaseBombNow)과 동일 throwForce 반영.");
            Assert.That(sampler, Does.Contain("upBoost"), "상향 성분 — upBoost 반영(실제 투척 초기 조건과 동일).");
            Assert.That(sampler, Does.Contain("Physics.gravity"), "중력 — Physics.gravity 포물선 시뮬레이션.");
            string ring = Slice(src, "private void UpdateLandingRing(", "private void DestroyAimPreviewObjects()");
            Assert.That(ring, Does.Contain("explosionRadius"),
                "착지 링 반경 = explosionRadius — 폭발 범위를 조준 단계에서 시각 전달.");
            Assert.That(src, Does.Contain("BombAimPreview_Arc"), "궤적 프리뷰 오브젝트 명명 계약.");
            Assert.That(src, Does.Contain("BombAimPreview_Landing"), "착지 링 프리뷰 오브젝트 명명 계약.");
            Assert.That(src, Does.Contain("Universal Render Pipeline/Unlit"),
                "URP 언릿 셰이더 우선 — 투명 궤적 라인.");
            Assert.That(src, Does.Contain("Sprites/Default"),
                "셰이더 폴백 — URP 부재 시 Sprites/Default(null 가드 포함).");
        }

        [Test]
        public void BombArmController_ThrowFlowIntact_PendingGateAndDelayedRelease()
        {
            string src = System.IO.File.ReadAllText(BombArmPath);
            string throwBody = Slice(src, "public void ThrowTowardCursor()",
                "private System.Collections.IEnumerator ThrowReleaseRoutine()");
            Assert.That(throwBody, Does.Contain("_isThrowPending = true;"),
                "기존 연타 금지 게이트 유지 — 투척 애니 진행 중 재발화 금지.");
            Assert.That(throwBody, Does.Contain("StartCoroutine(ThrowReleaseRoutine())"),
                "지연 릴리스 흐름 유지 — 애니 윈드업 후 폭탄 릴리즈(타이밍 변경 금지).");
            string routine = Slice(src, "private System.Collections.IEnumerator ThrowReleaseRoutine()",
                "private void ReleaseBombNow()");
            Assert.That(routine, Does.Contain("WaitForSeconds(ThrowReleaseDelaySeconds)"),
                "ThrowReleaseDelaySeconds(0.35f) 대기 유지 — 기존 릴리즈 타이밍 계약.");
        }

        /// <summary>구간 추출 헬퍼 — 미발견 시 빈 문자열.</summary>
        private static string Slice(string src, string startMarker, string endMarker)
        {
            int start = src.IndexOf(startMarker, System.StringComparison.Ordinal);
            int end = start >= 0 ? src.IndexOf(endMarker, start, System.StringComparison.Ordinal) : -1;
            return start >= 0 && end > start ? src.Substring(start, end - start) : string.Empty;
        }
    }
}
