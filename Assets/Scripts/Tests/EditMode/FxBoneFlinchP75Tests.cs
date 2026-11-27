using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 75] 피격 본(상체) 플린치 승격 계약테스트.
    /// 계획서: docs/plans/2026-11-18_no75-bone-flinch-upgrade-plan.md
    /// 소스 계약 패턴(FxEnvReactionP73Tests 선례) — EditMode 안전(런타임 쿼리·코루틴·AddComponent 발화 없음,
    /// FxHitFlinchBone.Trigger의 isPlaying 가드 준용 — 소스 계약만 검증).
    /// </summary>
    public class FxBoneFlinchP75Tests
    {
        private static string Read(string path) => System.IO.File.ReadAllText(path);

        private static float ParseConst(string src, string name)
        {
            var m = Regex.Match(src, name + @"\s*=\s*([0-9.]+)f");
            Assert.IsTrue(m.Success, $"{name} 상수 선언 탐지 실패");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        // ── 마커·시그니처·가드 ───────────────────────────────────

        /// <summary>[75] 마커, bool 반환 Trigger 시그니처, EditMode 무동작 가드, 부착형 러너.</summary>
        [Test]
        public void P75_Marker_TriggerSignature_Guards()
        {
            var src = Read("Assets/Scripts/Systems/FxHitFlinchBone.cs");
            Assert.IsTrue(src.Contains("[무번호 75]"), "P75 마커");
            Assert.IsTrue(src.Contains("public static bool Trigger(GameObject target, Vector3 hitDirection, HitSeverity severity)"),
                "bool 반환 Trigger 시그니처(driver 폴백 분기 계약)");
            Assert.IsTrue(src.Contains("if (!Application.isPlaying) return false;"),
                "EditMode 무동작 가드(코루틴·AddComponent 발화 없음 — 소스 계약만)");
            Assert.IsTrue(src.Contains("class FxHitFlinchBoneRunner : MonoBehaviour"), "target 부착형 절차 본 러너");
            Assert.IsTrue(src.Contains("Destroy(this);"), "유한 수명 — 컴포넌트 자가 파괴(누출 금지)");
            Assert.IsTrue(src.Contains("private void OnDestroy()"), "OnDestroy 원복 보장 경로(FxSwayRunner 패턴)");
        }

        // ── 본 패턴 매칭(상체 전용 — Hips 제외) ──────────────────

        /// <summary>[75] 본 탐색 — SMR bones, 상체 패턴(Spine/Chest/Torso), Hips 제외 리스트, 본 0건 폴백.</summary>
        [Test]
        public void P75_BonePattern_UpperBody_HipsExcluded()
        {
            var src = Read("Assets/Scripts/Systems/FxHitFlinchBone.cs");
            Assert.IsTrue(src.Contains("GetComponentsInChildren<SkinnedMeshRenderer>(true)"),
                "본 탐색 — 자식 SMR bones(FxDeathRelax 선례)");
            Assert.IsTrue(src.Contains("NameMatches(boneName, \"hips\")"), "Hips 제외 리스트(하체 루트 — 스킵)");
            Assert.IsTrue(src.Contains("NameMatches(boneName, \"spine\")"), "상체 패턴 — Spine(포함 판정: Spine1/Spine2/SpineChest)");
            Assert.IsTrue(src.Contains("NameMatches(boneName, \"chest\")"), "상체 패턴 — Chest");
            Assert.IsTrue(src.Contains("NameMatches(boneName, \"torso\")"), "상체 패턴 — Torso");
            Assert.IsTrue(src.Contains("new HashSet<Transform>()"), "다중 SMR 공유 본 디듀프(FxDeathRelax 선례)");
            Assert.IsTrue(src.Contains("if (list.Count == 0) return false;"), "본 0건 = false 반환(폴백 계약)");
            Assert.IsTrue(src.Contains("ToLowerInvariant().Contains(token)"),
                "대소문자 무시 판정(OrdinalIgnoreCase 계약 — GLB 소스별 혼용)");
        }

        // ── 각도 위계 3종 + 공통 duration ────────────────────────

        /// <summary>[75] severity 각도 위계 — Light 6° &lt; Heavy 10° &lt; Crit 15°, duration 0.28s 공통(계획서 고정).</summary>
        [Test]
        public void P75_SeverityAngleHierarchy_AndDuration()
        {
            var src = Read("Assets/Scripts/Systems/FxHitFlinchBone.cs");
            float light = ParseConst(src, "LightFlinchDeg");
            float heavy = ParseConst(src, "HeavyFlinchDeg");
            float crit = ParseConst(src, "CritFlinchDeg");
            Assert.AreEqual(6f, light, "Light 루트 각도 6°(계획서 고정)");
            Assert.AreEqual(10f, heavy, "Heavy 루트 각도 10°");
            Assert.AreEqual(15f, crit, "Crit 루트 각도 15°");
            Assert.That(light, Is.LessThan(heavy), "각도 위계 — Light < Heavy");
            Assert.That(heavy, Is.LessThan(crit), "각도 위계 — Heavy < Crit");
            Assert.AreEqual(0.28f, ParseConst(src, "FlinchDurationSec"), "지속시간 0.28s 공통(severity 무관)");
        }

        // ── 이징 재사용·체인 배분·회전축·원복 ────────────────────

        /// <summary>[75] FXMotion.Progress 재사용, 1/N 균등 배분, Cross(up, -dir) 축, 캡처→Slerp→원복.</summary>
        [Test]
        public void P75_FxMotionReuse_ChainSplit_Axis_Restore()
        {
            var src = Read("Assets/Scripts/Systems/FxHitFlinchBone.cs");
            Assert.IsTrue(src.Contains("FXMotion.Progress"), "이징 재사용(28-U3 재구현 금지 규약)");
            Assert.IsTrue(src.Contains("rootAngleDeg / bones.Length"), "체인 균등 배분 1/N(결정론 — 각도 합 = 루트 각도)");
            Assert.IsTrue(src.Contains("Vector3.Cross(Vector3.up, -dir)"),
                "수평 법선 축 — 맞은 방향 반대로 기움(FxSwayRunner/FxDeathRelax 선례 패턴)");
            Assert.IsTrue(src.Contains("Quaternion.Slerp"), "Slerp 적용(절차 본 회전 — 물리엔진 미사용)");
            Assert.IsTrue(src.Contains("localRotation"), "본 로컬 회전 캡처/적용/원복");
            Assert.IsTrue(src.Contains("RestoreAll();"), "원복 단일 경로(종료 + OnDestroy 공용)");
            Assert.IsTrue(src.Contains("target.GetComponent<FxHitFlinchBoneRunner>()"),
                "러너 중복 방지 — 기존 러너 존재 확인");
            Assert.IsTrue(src.Contains("target.AddComponent<FxHitFlinchBoneRunner>()"),
                "러너 부착 — AddComponent(재발화는 갱신)");
        }

        // ── HitReactionDriver 통합(대체 + 폴백) ──────────────────

        /// <summary>[75] Apply 본문 — 본 반응 시도 → 성공 시 lean 스킵, 실패 시 기존 lean 폴백. 기존 경로 무손상.</summary>
        [Test]
        public void P75_Driver_Integration_FallbackBranch()
        {
            var src = Read("Assets/Scripts/Systems/HitReactionDriver.cs");
            Assert.IsTrue(src.Contains("[무번호 75]"), "driver 통합 마커");
            Assert.IsTrue(src.Contains("FxHitFlinchBone.Trigger(target, dir, severity)"), "Apply → 본 반응 시도");
            Assert.IsTrue(src.Contains("if (!boneFlinch)"), "본 매치 성공 시 transform lean 스킵 분기");
            Assert.IsTrue(src.Contains("runner.PlayFlinch(dir, dur, lean, push, isFatal);"),
                "폴백 — 기존 transform lean 보존(본 0건 대비)");
            Assert.IsTrue(src.Contains("if (target.CompareTag(\"Player\"))"), "Player 태그 스킵 계약 유지(플레이어 반응 금지)");
            Assert.IsTrue(src.Contains("TryAnimatorTrigger(target, isFatal ? \"Hit\" : \"HitLight\")"),
                "애니 트리거 경로 무손상");
            Assert.IsTrue(src.Contains("ApplyKnockback(runner, target, dir, mag)"), "넉백 경로 무손상");
            Assert.IsTrue(src.Contains("if (!target.activeInHierarchy) return;"),
                "inactive 가드 무손상(사망 처리중 스킵 — 72-W2 본 경합 방지)");
        }

        /// <summary>[75] driver 기존 상수 9종 선언 무손상(문자열 카운트 보존 — FxQuality 계열 테스트 회귀 가드).</summary>
        [Test]
        public void P75_Driver_Constants_Preserved()
        {
            var src = Read("Assets/Scripts/Systems/HitReactionDriver.cs");
            string[] decls =
            {
                "private const float LightDuration = 0.18f;",
                "private const float HeavyDuration = 0.26f;",
                "private const float CritDuration = 0.34f;",
                "private const float LightLeanDeg = 9f;",
                "private const float HeavyLeanDeg = 16f;",
                "private const float CritLeanDeg = 22f;",
                "private const float LightPush = 0.09f;",
                "private const float HeavyPush = 0.22f;",
                "private const float CritPush = 0.32f;",
            };
            foreach (var d in decls)
                Assert.IsTrue(src.Contains(d), $"기존 상수 무손상 — {d}");
        }

        // ── 결정론 + 잠금 파일 무접촉 ────────────────────────────

        /// <summary>[75] 결정론 — Random 0건 / Rigidbody 0건(절차 본 회전, FxDeathRelax W2 동일 계약).</summary>
        [Test]
        public void P75_Deterministic_NoRandom_NoRigidbody()
        {
            var src = Read("Assets/Scripts/Systems/FxHitFlinchBone.cs");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "결정론 — 난수 0건");
            Assert.That(src, Does.Not.Contain("System.Random"), "결정론 — 난수 0건");
            Assert.That(src, Does.Not.Contain("Random.Range"), "결정론 — 난수 0건");
            Assert.That(src, Does.Not.Contain("Rigidbody"), "물리엔진 미사용(절차 본 회전 — FxDeathRelax W2 동일)");
            Assert.That(src, Does.Not.Contain("AddForce"), "물리엔진 미사용 — 힘 경로 0건");
            var driver = Read("Assets/Scripts/Systems/HitReactionDriver.cs");
            Assert.That(driver, Does.Not.Contain("UnityEngine.Random"), "driver 결정론 계약 보존(기존 — 무손상)");
        }

        /// <summary>[75] 잠금 파일 무접촉 — HitReaction.cs(KinematicJolt·ScalePulse·Stun 계약 유지), FxDeathRelax 본 경합 없음.</summary>
        [Test]
        public void P75_LockedFiles_Untouched()
        {
            var hit = Read("Assets/Scripts/Systems/HitReaction.cs");
            Assert.IsTrue(hit.Contains("KinematicJolt"), "KinematicJolt 계약 유지(무접촉)");
            Assert.IsTrue(hit.Contains("ScalePulse"), "ScalePulse 계약 유지(무접촉)");
            Assert.IsTrue(hit.Contains("Stun"), "Stun 계약 유지(무접촉)");
            Assert.That(hit, Does.Not.Contain("FxHitFlinchBone"), "P75 무접촉 — HitReaction.cs 참조 0건");
            Assert.That(hit, Does.Not.Contain("[무번호 75]"), "P75 무접촉 — HitReaction.cs 마커 0건");

            var relax = Read("Assets/Scripts/Systems/FxDeathRelax.cs");
            Assert.That(relax, Does.Not.Contain("FxHitFlinchBone"), "FxDeathRelax 무접촉(팔/다리 본 — Spine 미매치, 본 경합 없음)");

            var driver = Read("Assets/Scripts/Systems/HitReactionDriver.cs");
            Assert.That(driver, Does.Not.Contain("HitReaction."), "HitReaction.cs 정적 참조 추가 없음(무접촉)");
        }
    }
}
