// ============================================================================
// [무번호 63-B] 상태이상 시스템 EditMode 테스트 (2026-11-16 계획서 §5-1 이행)
//  · 신규 단일 파일 — Assets/Scripts/Tests/EditMode/StatusEffectSystemTests.cs
//  · EditMode 제약(§5-1): Time.deltaTime=0 / AddComponent-Awake 미호출 →
//    모든 행위 테스트는 정적 순수 훅 TickCore(float dt) 경유(internal — IVT 핀).
//  · DOT/스탯/부트 계약은 소스 어서션(ParryGauge_61Tests 선례) — 런타임 행위는 Play 게이트(별도).
// ============================================================================

using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [63-B] StatusEffectSystemTests — §5-1 ①~⑧ 전 항목.
    /// ①AddEffect 멱등 갱신 ②만료 자동 해제(TickCore 순수 경로) ③RemoveEffect/Cure* 매핑
    /// ④HasEffect/GetRemaining/GetActive 순서 ⑤OnChanged 발화 카운트 ⑥다중 최소값·합산·clamp
    /// ⑦14종 enum 무결성 ⑧DOT 틱 — "status" weaponType 더블 가드(소스 어서션).
    /// </summary>
    public static class StatusEffectSystemTests
    {
        private static string Read(string path)
        {
            var full = Path.Combine(Application.dataPath, path.Replace("Assets/", ""));
            return File.Exists(full) ? File.ReadAllText(full) : "";
        }

        // ── ① AddEffect 멱등 갱신(중복 Add → duration 리셋, 목록 1개) ──

        [Test]
        public static void AddEffect_IsIdempotent_RefreshResetsDuration()
        {
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Slowness, 5f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Slowness, 3f); // 중복 Add — 중첩 아님

            Assert.That(PlayerStatusEffectSystem.GetActiveEffects(), Has.Count.EqualTo(1),
                "[63-B] 멱등 — 목록 1개(중첩 없음).");
            Assert.That(PlayerStatusEffectSystem.GetRemainingSeconds(StatusEffectId.Slowness),
                Is.EqualTo(3f).Within(1e-4), "[63-B] remaining=duration 갱신.");
            Assert.That(PlayerStatusEffectSystem.GetDurationSeconds(StatusEffectId.Slowness),
                Is.EqualTo(3f).Within(1e-4), "[63-B] UI 분모(Duration)도 리셋.");
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Slowness), Is.True,
                "[63-B] 갱신 후에도 활성 유지.");
        }

        // ── ② 만료 자동 해제 — Update 우회 순수 경로: TickCore(float dt) ──

        [Test]
        public static void TickCore_Expire_AutoRemoves_AndFiresOnce()
        {
            PlayerStatusEffectSystem.ResetForTest();
            int changed = 0;
            System.Action handler = () => changed++;
            PlayerStatusEffectSystem.OnChanged += handler;

            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Daze, 8f);
            Assert.That(changed, Is.EqualTo(1), "[63-B] 추가 시 1회 발화.");

            PlayerStatusEffectSystem.TickCore(3f); // §5-1 — Time.deltaTime=0 우회 순수 훅
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Daze), Is.True,
                "[63-B] 3s 경과 — 아직 활성.");
            Assert.That(PlayerStatusEffectSystem.GetRemainingSeconds(StatusEffectId.Daze),
                Is.EqualTo(5f).Within(1e-4), "[63-B] remaining 감소.");

            PlayerStatusEffectSystem.TickCore(5f);
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Daze), Is.False,
                "[63-B] 0 도달 — 자동 해제.");
            Assert.That(PlayerStatusEffectSystem.GetRemainingSeconds(StatusEffectId.Daze),
                Is.EqualTo(0f), "[63-B] 비활성 시 0.");
            Assert.That(changed, Is.EqualTo(2), "[63-B] 만료 시 1회 발화.");

            PlayerStatusEffectSystem.TickCore(10f); // 이미 비활성 — 추가 발화/예외 없음
            Assert.That(changed, Is.EqualTo(2), "[63-B] 만료 발화는 1회만.");

            PlayerStatusEffectSystem.OnChanged -= handler;
            PlayerStatusEffectSystem.ResetForTest();
        }

        // ── ③ RemoveEffect/Cure* 매핑 (§1 치료 채널 규약) ──

        [Test]
        public static void CureMethods_MapCorrectEffects()
        {
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Bleeding, 10f);
            PlayerStatusEffectSystem.CureBleeding();
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Bleeding), Is.False, "[63-B] 붕대→Bleeding 제거.");

            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Poisoning, 12f);
            PlayerStatusEffectSystem.CurePoisoning();
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Poisoning), Is.False, "[63-B] 해독제→Poisoning 제거.");

            // 수면 — Exhaustion/Fracture/Hypothermia/Terror/Infection 제거 + Hunger remaining 60% 완화(제거 아님)
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Exhaustion, 20f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Fracture, 90f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Hypothermia, 15f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Terror, 6f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Infection, 30f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Hunger, 600f);
            PlayerStatusEffectSystem.CureSleep();
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Exhaustion), Is.False, "[63-B] 수면→Exhaustion 제거.");
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Fracture), Is.False, "[63-B] 수면→Fracture 제거.");
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Hypothermia), Is.False, "[63-B] 수면→Hypothermia 제거.");
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Terror), Is.False, "[63-B] 수면→Terror 제거.");
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Infection), Is.False, "[63-B] 수면→Infection 제거.");
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Hunger), Is.True, "[63-B] 수면은 Hunger 제거 아님(해제=식사).");
            Assert.That(PlayerStatusEffectSystem.GetRemainingSeconds(StatusEffectId.Hunger),
                Is.EqualTo(360f).Within(1e-3), "[63-B] 수면 — Hunger remaining 60%로 절반.");

            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Hunger, 600f);
            PlayerStatusEffectSystem.CureMeal();
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Hunger), Is.False, "[63-B] 식사→Hunger 제거.");

            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Hypothermia, 15f);
            PlayerStatusEffectSystem.CureWarm();
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Hypothermia), Is.False, "[63-B] 실내/모닥불→Hypothermia 제거.");

            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Terror, 6f);
            PlayerStatusEffectSystem.CureCalm();
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Terror), Is.False, "[63-B] 진정제→Terror 제거.");

            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Burn, 4f);
            PlayerStatusEffectSystem.Extinguish();
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Burn), Is.False, "[63-B] 물 접촉(수영)→Burn 즉시 제거.");

            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Fracture, 90f);
            PlayerStatusEffectSystem.RemoveEffect(StatusEffectId.Fracture);
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Fracture), Is.False, "[63-B] RemoveEffect 직접 해제.");

            // 전체 해제 — §4 훅#7④("상태이상 방지") 소비 경로
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Slowness, 5f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Weaken, 8f);
            PlayerStatusEffectSystem.RemoveAllEffects();
            Assert.That(PlayerStatusEffectSystem.GetActiveEffects(), Has.Count.EqualTo(0), "[63-B] 전체 해제.");
        }

        // ── ④ HasEffect/GetRemaining/GetActive 순서 ──

        [Test]
        public static void ActiveOrder_IsAddOrder_AndQueriesReflect()
        {
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Bleeding, 10f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Poisoning, 12f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Daze, 8f);

            Assert.That(PlayerStatusEffectSystem.GetActiveEffects(),
                Is.EqualTo(new[] { StatusEffectId.Bleeding, StatusEffectId.Poisoning, StatusEffectId.Daze }),
                "[63-B] UI 표시 순서 = 추가 순.");

            PlayerStatusEffectSystem.TickCore(2f);
            Assert.That(PlayerStatusEffectSystem.GetRemainingSeconds(StatusEffectId.Bleeding), Is.EqualTo(8f).Within(1e-4), "[63-B] remaining 감소 1.");
            Assert.That(PlayerStatusEffectSystem.GetRemainingSeconds(StatusEffectId.Poisoning), Is.EqualTo(10f).Within(1e-4), "[63-B] remaining 감소 2.");
            Assert.That(PlayerStatusEffectSystem.GetRemainingSeconds(StatusEffectId.Daze), Is.EqualTo(6f).Within(1e-4), "[63-B] remaining 감소 3.");
            Assert.That(PlayerStatusEffectSystem.GetDurationSeconds(StatusEffectId.Bleeding), Is.EqualTo(10f).Within(1e-4), "[63-B] 분모 불변.");
            Assert.That(PlayerStatusEffectSystem.GetRemainingSeconds(StatusEffectId.Hunger), Is.EqualTo(0f), "[63-B] 비활성 remaining=0.");
            Assert.That(PlayerStatusEffectSystem.GetDurationSeconds(StatusEffectId.Hunger), Is.EqualTo(0f), "[63-B] 비활성 duration=0.");

            // 제거 후 재추가 — 목록 끝에 재배치(추가 순 규약 유지)
            PlayerStatusEffectSystem.RemoveEffect(StatusEffectId.Poisoning);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Poisoning, 12f);
            Assert.That(PlayerStatusEffectSystem.GetActiveEffects(),
                Is.EqualTo(new[] { StatusEffectId.Bleeding, StatusEffectId.Daze, StatusEffectId.Poisoning }),
                "[63-B] 재추가는 목록 말미(추가 순).");
            Assert.That(PlayerStatusEffectSystem.HasEffect(StatusEffectId.Poisoning), Is.True, "[63-B] 재추가 활성.");
        }

        // ── ⑤ OnChanged 발화 카운트 (추가/제거/만료 1회 — 멱등 갱신 미발화) ──

        [Test]
        public static void OnChanged_FiresOnAddRemoveExpire_NotOnRefresh()
        {
            PlayerStatusEffectSystem.ResetForTest();
            int changed = 0;
            System.Action handler = () => changed++;
            PlayerStatusEffectSystem.OnChanged += handler;

            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Bleeding, 10f); // 추가 → 1
            Assert.That(changed, Is.EqualTo(1), "[63-B] 추가 발화.");

            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Bleeding, 5f);  // 멱등 갱신 → 미발화
            Assert.That(changed, Is.EqualTo(1), "[63-B] 갱신은 추가/제거/만료 아님 — 미발화.");

            PlayerStatusEffectSystem.RemoveEffect(StatusEffectId.Bleeding);  // 제거 → 2
            Assert.That(changed, Is.EqualTo(2), "[63-B] 제거 발화.");

            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Daze, 1f);     // 3
            PlayerStatusEffectSystem.TickCore(1f);                           // 만료 → 4
            Assert.That(changed, Is.EqualTo(4), "[63-B] 만료 발화.");

            PlayerStatusEffectSystem.OnChanged -= handler;
            PlayerStatusEffectSystem.ResetForTest();
        }

        // ── ⑥ HealMultiplier/StaminaRegenMultiplier/ComboMissChance 다중 최소값·합산 ──

        [Test]
        public static void MultiplierQueries_MinSumClamp()
        {
            // 기저 상태
            PlayerStatusEffectSystem.ResetForTest();
            Assert.That(PlayerStatusEffectSystem.HealMultiplier(), Is.EqualTo(1f), "[63-B] 비활성 힐 배율 1.");
            Assert.That(PlayerStatusEffectSystem.ApplyHealMultiplier(10f), Is.EqualTo(10f).Within(1e-4), "[63-B] Heal 절반 경로 — 무효 시 통과.");
            Assert.That(PlayerStatusEffectSystem.StaminaRegenMultiplier(), Is.EqualTo(1f), "[63-B] 비활성 스태 회복 1.");
            Assert.That(PlayerStatusEffectSystem.ComboMissChance(), Is.EqualTo(0f), "[63-B] 비활성 누락 0.");
            Assert.That(PlayerStatusEffectSystem.BlocksDash(), Is.False, "[63-B] 대쉬 잠금 없음.");
            Assert.That(PlayerStatusEffectSystem.BlocksRoll(), Is.False, "[63-B] 구르기 잠금 없음.");
            Assert.That(PlayerStatusEffectSystem.BlocksHeal(), Is.False, "[63-B] 힐 정지 없음.");
            Assert.That(PlayerStatusEffectSystem.MaxStaminaMultiplier(), Is.EqualTo(1f), "[63-B] 스태 최대 배율 1.");

            // Poisoning — 회복×0.5(정지 아님)
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Poisoning, 12f);
            Assert.That(PlayerStatusEffectSystem.HealMultiplier(), Is.EqualTo(0.5f), "[63-B] Poisoning 회복×0.5.");
            Assert.That(PlayerStatusEffectSystem.ApplyHealMultiplier(10f), Is.EqualTo(5f).Within(1e-4), "[63-B] Heal 절반 경로.");
            Assert.That(PlayerStatusEffectSystem.BlocksHeal(), Is.False, "[63-B] Poisoning은 힐 정지 아님.");

            // Infection — 힐 정지(0)
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Infection, 30f);
            Assert.That(PlayerStatusEffectSystem.HealMultiplier(), Is.EqualTo(0f), "[63-B] Infection 힐 정지.");
            Assert.That(PlayerStatusEffectSystem.BlocksHeal(), Is.True, "[63-B] Infection 힐 완전 정지.");

            // 다중 최소값 — Poisoning+Infection → 0
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Poisoning, 12f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Infection, 30f);
            Assert.That(PlayerStatusEffectSystem.HealMultiplier(), Is.EqualTo(0f), "[63-B] 힐 배율 다중 최소값.");

            // Hunger — 힐 정지(자연회복만) + 스태 최대 −30%
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Hunger, 600f);
            Assert.That(PlayerStatusEffectSystem.HealMultiplier(), Is.EqualTo(0f), "[63-B] Hunger 자연회복 정지.");
            Assert.That(PlayerStatusEffectSystem.BlocksHeal(), Is.True, "[63-B] Hunger 힐 완전 정지.");
            Assert.That(PlayerStatusEffectSystem.MaxStaminaMultiplier(), Is.EqualTo(0.7f), "[63-B] Hunger 스태 최대 −30%.");

            // 스태 회복 — Exhaustion 0.5 / Daze 0.7 / Hypothermia 0.6, 다중 최소값
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Exhaustion, 20f);
            Assert.That(PlayerStatusEffectSystem.StaminaRegenMultiplier(), Is.EqualTo(0.5f), "[63-B] Exhaustion 회복 −50%.");
            Assert.That(PlayerStatusEffectSystem.BlocksDash(), Is.True, "[63-B] Exhaustion 대쉬 잠금.");
            Assert.That(PlayerStatusEffectSystem.BlocksRoll(), Is.False, "[63-B] Exhaustion은 구르기 허용.");
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Daze, 8f);
            Assert.That(PlayerStatusEffectSystem.StaminaRegenMultiplier(), Is.EqualTo(0.5f), "[63-B] 다중 최소값(0.5<0.7).");
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Hypothermia, 15f);
            Assert.That(PlayerStatusEffectSystem.StaminaRegenMultiplier(), Is.EqualTo(0.5f), "[63-B] 다중 최소값(0.5<0.6).");

            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Daze, 8f);
            Assert.That(PlayerStatusEffectSystem.StaminaRegenMultiplier(), Is.EqualTo(0.7f), "[63-B] Daze 회복 −30%.");

            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Hypothermia, 15f);
            Assert.That(PlayerStatusEffectSystem.StaminaRegenMultiplier(), Is.EqualTo(0.6f), "[63-B] Hypothermia 회복 −40%.");

            // 콤보 누락 — Terror 0.3 / Daze 0.15 / Drunkenness 0.1 합산
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Terror, 6f);
            Assert.That(PlayerStatusEffectSystem.ComboMissChance(), Is.EqualTo(0.3f).Within(1e-4), "[63-B] Terror 30%.");
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Daze, 8f);
            Assert.That(PlayerStatusEffectSystem.ComboMissChance(), Is.EqualTo(0.15f).Within(1e-4), "[63-B] Daze 15%.");
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Drunkenness, 25f);
            Assert.That(PlayerStatusEffectSystem.ComboMissChance(), Is.EqualTo(0.1f).Within(1e-4), "[63-B] Drunkenness 10%.");
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Terror, 6f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Daze, 8f);
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Drunkenness, 25f);
            Assert.That(PlayerStatusEffectSystem.ComboMissChance(), Is.EqualTo(0.55f).Within(1e-4), "[63-B] 합산 0.3+0.15+0.1.");

            // 구르기 잠금 — Fracture 전용
            PlayerStatusEffectSystem.ResetForTest();
            PlayerStatusEffectSystem.AddEffect(StatusEffectId.Fracture, 90f);
            Assert.That(PlayerStatusEffectSystem.BlocksRoll(), Is.True, "[63-B] Fracture 구르기 잠금.");
            Assert.That(PlayerStatusEffectSystem.BlocksDash(), Is.True, "[63-B] Fracture 대쉬 잠금.");
        }

        // ── ⑦ 14종 enum 무결성 (§1 정의와 1:1 순서) ──

        [Test]
        public static void Enum_Has14Kinds_ExactOrder()
        {
            var names = System.Enum.GetNames(typeof(StatusEffectId));
            Assert.That(names.Length, Is.EqualTo(14), "[63-B] 14종 전부 채택.");
            CollectionAssert.AreEqual(new[]
            {
                "Bleeding", "Poisoning", "Slowness", "Exhaustion", "Fracture", "Daze",
                "Hunger", "Burn", "Hypothermia", "Terror", "Infection", "Weaken",
                "Drunkenness", "BrittleDefense"
            }, names, "[63-B] §1 enum 선언 순서 1:1.");
        }

        // ── ⑧ DOT 틱 — Tick(dt) 경유 PlayerHealth 더블 가드(status weaponType 미재진입 — 소스 어서션) ──

        [Test]
        public static void DotTick_SourceContract_StatusWeaponAndGuards()
        {
            string src = Read("Assets/Scripts/Systems/PlayerStatusEffectSystem.cs");
            Assert.That(src, Does.Contain("private const string DotWeaponType = \"status\""),
                "[63-B] DOT 자기재귀 가드 핀 — §4 훅#1이 \"status\"로 DOT 식별(피격 발동 미재진입).");
            Assert.That(src, Does.Contain("health.TakeDamage(dps, Vector3.zero, DotWeaponType)"),
                "[63-B] DOT은 3인자 오버로드 경유 — hitDirection=zero(넉백 분기 미진입, 순수 대미지).");
            Assert.That(src, Does.Contain("if (health == null) return;"),
                "[63-B] 더블 가드 ① — PlayerHealth 인스턴스 없음(EditMode/부팅 전) 안전.");
            Assert.That(src, Does.Contain("private const float BleedingDps = 2f"), "[63-B] 출혈 2 HP/s.");
            Assert.That(src, Does.Contain("private const float PoisoningDps = 1.5f"), "[63-B] 중독 1.5 HP/s.");
            Assert.That(src, Does.Contain("private const float BurnDps = 5f"), "[63-B] 화상 5 HP/s.");
            Assert.That(src, Does.Contain("private const float InfectionDps = 1f"), "[63-B] 감염 1 HP/s.");
            Assert.That(src, Does.Contain("internal void Tick(float dt)"),
                "[63-B] §5-1 순수 훅 핀 — internal Tick(float dt) 존재.");
            Assert.That(src, Does.Contain("internal static void TickCore(float dt)"),
                "[63-B] §5-1 정적 경로 — 인스턴스 무관 순수 훅.");
            Assert.That(src, Does.Contain("Update() => Tick(Time.deltaTime)"),
                "[63-B] Update가 유일한 시간 주입부(Time.deltaTime).");
            Assert.That(src, Does.Contain("while (state.DotTimer >= 1f)"),
                "[63-B] 1초 주기 강틱 — HP/s 규약.");
        }

        // ── 보충 소스 어서션: 스탯 가감 배선(BuffManager 동일 산식) ──

        [Test]
        public static void StatEdits_SourceContract_BuffManagerStyle()
        {
            string src = Read("Assets/Scripts/Systems/PlayerStatusEffectSystem.cs");
            Assert.That(src, Does.Contain("stats.MoveSpeedBase -= SlownessSpeedPenalty"),
                "[63-B] Slowness — 기존 몬스터 값 −0.5 유지.");
            Assert.That(src, Does.Contain("stats.MoveSpeedBase -= FractureSpeedPenalty"),
                "[63-B] Fracture — 기본 이동속도 기준 −20% 상수 산식.");
            Assert.That(src, Does.Contain("stats.MoveSpeedBase -= HypothermiaSpeedPenalty"),
                "[63-B] Hypothermia — 이속 −1.5.");
            Assert.That(src, Does.Contain("stats.AttackDamageBase -= WeakenAttackPenalty"),
                "[63-B] Weaken — 기본 공격력 10f 실측 ×30% 산식.");
            Assert.That(src, Does.Contain("stats.AttackDamageBase += DrunkennessAttackBonus"),
                "[63-B] Drunkenness — 공격 +10%.");
            Assert.That(src, Does.Contain("stats.DefenseBase -= BrittleDefenseDefensePenalty"),
                "[63-B] BrittleDefense — 방어력 −30% 상수 산식.");
            Assert.That(src, Does.Contain("ReverseStats(id)"),
                "[63-B] 해제 시 정확히 역가감 — BuffManager Apply/ReverseBuff 동일 산식.");
            Assert.That(src, Does.Contain("if (stats == null) return;"),
                "[63-B] PlayerStats 미존재(EditMode) 가드.");
            Assert.That(src, Does.Not.Contain("BuffManager.Instance"),
                "[63-B] BuffManager 무수정 계약 — 참조 없음.");
        }

        // ── 보충 소스 어서션: §1 API 시그니처 불변 + 자가부트 ──

        [Test]
        public static void ApiSurface_AndSelfBoot_SourceContract()
        {
            string src = Read("Assets/Scripts/Systems/PlayerStatusEffectSystem.cs");
            // 교차 시그니처 불변(§1) — UI(StatusEffectBarUTK)가 소비하는 핀
            Assert.That(src, Does.Contain("public static void AddEffect(StatusEffectId id, float duration)"), "[63-B] API 핀.");
            Assert.That(src, Does.Contain("public static void RemoveEffect(StatusEffectId id)"), "[63-B] API 핀.");
            Assert.That(src, Does.Contain("public static bool HasEffect(StatusEffectId id)"), "[63-B] API 핀.");
            Assert.That(src, Does.Contain("public static float GetRemainingSeconds(StatusEffectId id)"), "[63-B] API 핀.");
            Assert.That(src, Does.Contain("public static float GetDurationSeconds(StatusEffectId id)"), "[63-B] API 핀.");
            Assert.That(src, Does.Contain("public static IReadOnlyList<StatusEffectId> GetActiveEffects()"), "[63-B] API 핀.");
            Assert.That(src, Does.Contain("public static event Action OnChanged;"), "[63-B] API 핀.");
            Assert.That(src, Does.Contain("public static void CureBleeding()"), "[63-B] 치료 채널 핀.");
            Assert.That(src, Does.Contain("public static void CurePoisoning()"), "[63-B] 치료 채널 핀.");
            Assert.That(src, Does.Contain("public static void CureSleep()"), "[63-B] 치료 채널 핀.");
            Assert.That(src, Does.Contain("public static void CureMeal()"), "[63-B] 치료 채널 핀.");
            Assert.That(src, Does.Contain("public static void CureWarm()"), "[63-B] 치료 채널 핀.");
            Assert.That(src, Does.Contain("public static void CureCalm()"), "[63-B] 치료 채널 핀.");
            Assert.That(src, Does.Contain("public static void Extinguish()"), "[63-B] 치료 채널 핀.");
            // 행동 잠금/배율 조회(§1 — 반드시 포함)
            Assert.That(src, Does.Contain("public static bool BlocksDash()"), "[63-B] 조회 API 핀.");
            Assert.That(src, Does.Contain("public static bool BlocksRoll()"), "[63-B] 조회 API 핀.");
            Assert.That(src, Does.Contain("public static bool BlocksHeal()"), "[63-B] 조회 API 핀.");
            Assert.That(src, Does.Contain("public static float HealMultiplier()"), "[63-B] 조회 API 핀.");
            Assert.That(src, Does.Contain("public static float StaminaRegenMultiplier()"), "[63-B] 조회 API 핀.");
            Assert.That(src, Does.Contain("public static float ComboMissChance()"), "[63-B] 조회 API 핀.");
            // 자가부트 — AfterSceneLoad 멱등 + DDOL(§1)
            Assert.That(src, Does.Contain("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]"),
                "[63-B] 자가부트 핀 — AfterSceneLoad.");
            Assert.That(src, Does.Contain("DontDestroyOnLoad"), "[63-B] DDOL — 씬 전환 유지.");
            Assert.That(src, Does.Contain("new GameObject(\"PlayerStatusEffectSystem\")"),
                "[63-B] 부트 GameObject 명명 핀.");
            Assert.That(src, Does.Contain("public class PlayerStatusEffectSystem : MonoBehaviour"),
                "[63-B] 클래스 형태 핀.");
            Assert.That(src, Does.Contain("namespace ProjectName.Systems"), "[63-B] 네임스페이스 핀.");
        }
    }
}
