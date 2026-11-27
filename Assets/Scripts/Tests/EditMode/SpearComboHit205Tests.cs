using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [205차 배치B-1] 창 2연타 두 번째 휘두르기 타격 수리 계약 — MonitorSpearSequence 세그먼트2 데미지 발화 핀.
    /// 근원(11-26 Editor.log 실측): 스피어 쿨다운(WeaponData.Spear.attackSpeed=1.5s — PlayerCombat.CanAttack)이
    /// 구 melee 공용 팔로업 클릭 윈도우(0.85s)보다 길어 2클릭 팔로업이 구조적으로 도달 불능이었다.
    /// ① 0.85s 내 클릭2 = PlayerCombat 거부(세션 로그 "attack rejected ... spearFollowup=True" 14건).
    /// ② 0.85s 경과 클릭 = 만료 재시작(새 오프너 → 스윙1 데미지만). 세션 전체에서 "Spear segment accepted=2"와
    ///   "[Spear] WeaponCombo follow-up hit resolved" 0건 — 세그먼트2 데미지 경로 단 한 번도 발화 불능.
    /// 수리: 스피어 전용 윈도우 상수(SpearFollowupClickWindow=1.8s) — 수용/만료/재시작/홀드 4개 스피어 경로 일치 소비.
    /// 계약: ①세그먼트별 타격 1회(이중발화 방지) ②VFX 조기발화 소유 구조 유지 ③[176차] 1클릭=1타·2클릭=2타.
    /// </summary>
    public class SpearComboHit205Tests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private const string DriverPath = "Assets/Scripts/Systems/HumanoidClipDriver.cs";
        private const string CombatPath = "Assets/Scripts/Systems/PlayerCombat.cs";

        // ── ① 세그먼트2 데미지 발화 경로 존재 핀(수리 결과 반영) ──

        [Test]
        public void SpearFollowupWindow_ExceedsSpearWeaponCooldown()
        {
            float window = GetSpearFollowupClickWindow();
            Assert.That(window, Is.GreaterThan(WeaponData.Spear.attackSpeed),
                "[205차 B-1] 스피어 팔로업 윈도우는 스피어 쿨다운(1.5s)보다 길어야 한다 — 윈도우 ≤ 쿨다운이면 " +
                "쿨다운 개시 후 접수되는 클릭2가 이미 만료로 판정되어 세그먼트2 데미지가 영구 미발화된다(11-26 세션 근원).");
            Assert.That(window, Is.EqualTo(1.8f).Within(0.0001f),
                "윈도우 = 쿨다운 1.5s + 0.3s 수용 밴드 — 1.8s 경과 클릭은 기존 만료 재시작 경로 유지.");
        }

        [Test]
        public void FollowupAcceptance_UsesSpearWindow_AtCooldownOpenBoundary()
        {
            var accept = typeof(HumanoidClipDriver).GetMethod("EvaluateSpearFollowupAcceptance", PrivateStatic);
            Assert.That(accept, Is.Not.Null);
            float window = GetSpearFollowupClickWindow();

            // 오프너 활성(clicks=1, pending=0) — 쿨다운 개시 시점(now=공속 1.5s)의 클릭2는 윈도우 내 → 팔로업 승인
            Assert.That((bool)accept.Invoke(null,
                new object[] { true, 1, 0L, 0f, WeaponData.Spear.attackSpeed, window }), Is.True,
                "쿨다운(1.5s) 개시 직후 접수된 클릭2는 스피어 윈도우(1.8s) 내 — 팔로업 승인. " +
                "수리 전 0.85s 공용 윈도우에서는 같은 클릭이 만료로 거부됐다(근원).");
            // 윈도우 경과 클릭 = 팔로업 아님 → 만료 재시작(새 오프너)로 분류 — 기존 계약 유지
            Assert.That((bool)accept.Invoke(null,
                new object[] { true, 1, 0L, 0f, window + 0.01f, window }), Is.False,
                "1.8s 경과 클릭은 팔로업 승인 대상이 아니다 — 만료 재시작 경계 유지.");
        }

        [Test]
        public void DispatchExpiry_UsesSameSpearWindow_AsFollowupAcceptance()
        {
            var expired = typeof(HumanoidClipDriver).GetMethod("IsSpearSequenceExpired", PrivateStatic);
            Assert.That(expired, Is.Not.Null);
            float window = GetSpearFollowupClickWindow();

            Assert.That((bool)expired.Invoke(null,
                new object[] { true, 0f, WeaponData.Spear.attackSpeed, window }), Is.False,
                "쿨다운 개시(1.5s) 시점 클릭2 — 디스패치 만료 아님: ShouldAuthorizeSpearFollowupAtDispatch " +
                "팔로업 승인 경로에 도달해야 한다.");
            Assert.That((bool)expired.Invoke(null,
                new object[] { true, 0f, window, window }), Is.True,
                "윈도우 경과 클릭은 만료 재시작 — 수용 게이트와 만료 게이트가 동일 상수를 소비해야 경계에 갭이 없다.");
        }

        [Test]
        public void Segment2DamagePath_IsWiredThroughFollowupGate()
        {
            var src = System.IO.File.ReadAllText(DriverPath);
            Assert.That(src, Does.Contain("_combat?.ResolveComboSwingHit(2)"),
                "세그먼트2(팔로업 스윙2) 데미지 발화 경로 존재 핀.");
            Assert.That(src, Does.Contain("ShouldResolveSpearFollowupHit(true, normalizedTime, _spearFollowupHitResolved)"),
                "세그먼트2 데미지는 2클릭 승인 팔로업 게이트(단일 소스)로만 발화된다 — [176차] 1클릭=1타 계약.");
            Assert.That(src, Does.Contain("ShouldResolveSpearOpenerHit(true, Mathf.Max(0f, state.normalizedTime), _spearOpenerHitResolved)"),
                "세그먼트1(오프너 스윙1) 데미지 게이트 존재 핀.");
            Assert.That(src, Does.Not.Contain("ShouldResolveSpearOpenerSwing2(Mathf.Max"),
                "[176차] 오프너 내장 스윙2 자동 게이트 철수 상태 유지 — 1클릭만으로 2타 발화 금지.");
        }

        [Test]
        public void MeleeComboDamageChain_1and2_ResolveThroughDriverGateIntoPlayerCombat()
        {
            var driver = System.IO.File.ReadAllText(DriverPath);
            Assert.That(driver, Does.Contain("if (!TryResolveMeleeComboHit(segment, normalizedTime, _stageHitResolved)) continue;"),
                "[206차] WeaponCombo 1·2단 데미지는 TryResolveMeleeComboHit 게이트(세그먼트별 1회 가드)로만 발화된다 — 이중발화 방지.");
            Assert.That(driver, Does.Contain("_combat?.ResolveComboSwingHit(segment);"),
                "게이트 통과 세그먼트는 PlayerCombat.ResolveComboSwingHit로 1·2단 데미지 체인이 이어진다.");
            var combat = System.IO.File.ReadAllText(CombatPath);
            Assert.That(combat, Does.Contain("public void ResolveComboSwingHit(int comboStage)"),
                "PlayerCombat 수신단 존재 핀 — 1·2단 데미지 체인의 종착점.");
        }

        // ── ② 스피어 경로 윈도우 일치 핀(수리 무효화 방지) ──

        [Test]
        public void SpearPaths_ConsumeSpearWindow_NotMeleeWindow()
        {
            var src = System.IO.File.ReadAllText(DriverPath);
            Assert.That(src, Does.Contain("openerTime, Time.time, SpearFollowupClickWindow)"),
                "CanAcceptSpearFollowup 수용 게이트 — 스피어 전용 윈도우 소비.");
            Assert.That(src, Does.Contain("_spearOpenerAcceptedTime, acceptedInputTime, SpearFollowupClickWindow)"),
                "디스패치 만료 판정 — 스피어 전용 윈도우 소비.");
            Assert.That(src, Does.Contain("Time.time - _spearOpenerAcceptedTime >= SpearFollowupClickWindow"),
                "만료 재시작 게이트(CanRestartExpiredSpearSequence) — 스피어 전용 윈도우 소비.");
            Assert.That(src, Does.Not.Contain("openerTime, Time.time, MeleeComboClickWindow)"),
                "구 공용 윈도우가 스피어 수용 게이트에 잔존하면 수리가 무효화된다.");
            Assert.That(src, Does.Not.Contain("_spearOpenerAcceptedTime, acceptedInputTime, MeleeComboClickWindow"),
                "구 공용 윈도우가 스피어 디스패치 만료에 잔존하면 쿨다운 개시 클릭2가 만료로 오분류된다.");
        }

        [Test]
        public void MeleeComboClickWindow_StaysAt168P1Contract()
        {
            var field = typeof(HumanoidClipDriver).GetField("MeleeComboClickWindow", PrivateStatic);
            Assert.That(field, Is.Not.Null);
            Assert.That((float)field.GetRawConstantValue(), Is.EqualTo(0.85f).Within(0.0001f),
                "[168-P1] 주먹/검 melee 콤보 윈도우 0.85s 계약 무수정 — 스피어 수리가 melee 콤보 타이밍을 오염하지 않는다.");
        }

        // ── ③ 이중발화 방지 계약 문자열 유지 핀 ──

        [Test]
        public void DoubleFirePrevention_ContractStringsMaintained()
        {
            var src = System.IO.File.ReadAllText(DriverPath);
            Assert.That(src, Does.Contain("MonitorSpearSequence가 스윙별 소유(ResolveComboSwingHit 재사용 — 계획 §B-2) — 이중 발화 차단"),
                "[166-B] 레거시 경로 이중 발화 차단 계약 유지.");
            Assert.That(src, Does.Contain("if (st.IsName(SpearOpenerStateName)) continue;   // 오프너는 MonitorSpearSequence 전담 — 이중 방출 차단"),
                "레거시 Attack* 스윙 루프의 오프너 이중 방출 차단 가드 유지.");
            Assert.That(src, Does.Contain("_spearOpenerHitResolved = true;"),
                "스윙1 1회 가드 플래그 — 세그먼트별 타격 1회 보장.");
            Assert.That(src, Does.Contain("_spearFollowupHitResolved = true;"),
                "스윙2(세그먼트2) 1회 가드 플래그 — 세그먼트별 타격 1회 보장.");
            Assert.That(src, Does.Contain("[176차] 오프너 내장 2스윙 자동 해결 게이트 철수"),
                "1클릭=1타 계약 주석 유지 — 자동 스윙2 게이트 재활성 금지.");
        }

        private static float GetSpearFollowupClickWindow()
        {
            var field = typeof(HumanoidClipDriver).GetField("SpearFollowupClickWindow", PrivateStatic);
            Assert.That(field, Is.Not.Null, "[205차 B-1] 스피어 전용 팔로업 클릭 윈도우 상수가 존재해야 한다.");
            return (float)field.GetRawConstantValue();
        }
    }
}
