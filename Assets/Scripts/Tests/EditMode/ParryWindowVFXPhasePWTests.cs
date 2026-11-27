using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 36] 패망 창 시각 언어 + 임팩트 정합 계약 — PW1 방어 림(닫히는 시계) / PW2 만료 수렴 링 /
    /// PW3 PERFECT 힌트(95차 판정 상수 공유) / PI1 접촉점 정합(공격자 방향 흡수점).
    /// </summary>
    public class ParryWindowVFXPhasePWTests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── PW1: 방어 림 부착/갱신/해제(멱등) ──
        [Test]
        public void Source_36PW1_GuardRim()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 36-PW1]"), "[36-PW1] 마커.");
            Assert.That(src, Does.Contain("private GameObject _parryGuardRim;"), "[36-PW1] 림 참조 필드.");
            Assert.That(src, Does.Contain("private const float ParryGuardRimIntensity = 1.6f;"),
                "[36-PW1] 29-P1-2 무기 림 선례 강도.");
            Assert.That(src, Does.Contain("_parryGuardRim = FxRimOverlay.AttachRimStatic(visual, FXPalette.ParryWhite, 2.5f, ParryGuardRimIntensity);"),
                "[36-PW1] 부착 — WeaponEquipManager.CurrentVisual 기반(105차 F1), 색=ParryWhite 단일 소스([171차] 흰 단일).");
            Assert.That(src, Does.Contain("ReleaseParryGuardRim();   // 멱등 — 재입력 이중 부착 방지"),
                "[36-PW1] 부착 멱등 가드 — 이중 부착 금지.");
            Assert.That(src, Does.Contain("FxRimOverlay.SetRimIntensity(_parryGuardRim, ParryGuardRimIntensity * remaining * parryPulse);"),
                "[36-PW1] 잔여 시간 비례 감쇠 — 닫히는 시계([171차] GOOD 진입 맥동 배율 합산).");
            Assert.That(src, Does.Contain("if (_parryActive) UpdateParryGuardRim();"),
                "[36-PW1] Update 갱신 배선.");
            Assert.That(src, Does.Contain("ReleaseParryGuardRim();   // [무번호 36-PW1] 성공 — 방어 림 즉시 해제(28-P1 플래시 림이 이어받음 — 이중 림 방지)"),
                "[36-PW1] 성공 시 즉시 해제 — 28-P1 플래시 이중 림 방지.");
        }

        // ── PW2: 만료 수렴 링(자연 만료 전용 — 성공 경로 미발화) ──
        [Test]
        public void Source_36PW2_ExpiryCue()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            int iEnd = src.IndexOf("private void EndParryWindow()");
            int iNext = src.IndexOf("private void ResumeQueuedParryAction()");
            string body = src.Substring(iEnd, iNext - iEnd);
            Assert.That(body, Does.Contain("ShockwaveRingFX.SpawnConvergingRing(cp, 1.2f,"),
                "[36-PW2] 만료 수렴 링(개방 확산의 역방향 — 21-L1 수렴 계열 재사용).");
            Assert.That(body, Does.Contain("if (_parryGuardRim != null && Application.isPlaying)"),
                "[36-PW2] 자연 만료 전용 — 성공 경로(림 선행 해제) 미발화 가드.");
            Assert.That(body, Does.Contain("ReleaseParryGuardRim();"), "[36-PW2] 만료 시 림 해제.");
        }

        // ── PW3: PERFECT 힌트 — 95차 판정 상수 공유(이원화 금지) ──
        [Test]
        public void Source_36PW3_PerfectHint()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.5f)"),
                "[36-PW3] 개방 링 = 흰([171차] 흰 단일).");
            Assert.That(src, Does.Contain("elapsed > ParryPerfectWindow"),
                "[36-PW3] 경계 판정 = 95차 F1 판정 상수 공유(이원화 금지).");
            Assert.That(src, Does.Not.Contain("ParryGoodCueBlue"),
                "[171차] 청 큐 폐기(흰 단일).");
            Assert.That(src, Does.Contain("_parryGoodPulseUntil"),
                "[171차] GOOD 진입 림 맥동.");
            Assert.That(src, Does.Contain("ShockwaveRingFX.SpawnConvergingRing(cp, 1.2f, new Color(FXPalette.ParryWhite"),
                "[171차] 흰 수렴링.");
            Assert.That(src, Does.Contain("_parryGoodTickDone = false;   // [무번호 36-PW3] 경계 틱 1회 플래그 리셋"),
                "[36-PW3] 틱 1회 플래그 리셋.");
        }

        // ── PI1: 접촉점 정합 — 방향 선행 + 흡수점 공격자 방향 + 폴백 보존 ──
        [Test]
        public void Source_36PI1_ContactPoint()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 36-PI1]"), "[36-PI1] 마커.");
            int iDir = src.IndexOf("Vector3 sparkDir = -kbDir;   // kbDir 역방향 = 공격자 방향. 폴백은 전방.");
            int iPoint = src.IndexOf("Vector3 p = transform.position + sparkDir * 1f + Vector3.up * 0.9f;");
            Assert.That(iDir, Is.GreaterThanOrEqualTo(0), "[36-PI1] 방향 계산 존재.");
            Assert.That(iPoint, Is.GreaterThan(iDir), "[36-PI1] 방향 계산 선행 — 흡수점이 공격자 방향을 따라감.");
            Assert.That(src, Does.Contain("if (sparkDir.sqrMagnitude < 0.0001f) sparkDir = transform.forward;"),
                "[36-PI1] 폴백 보존(kbDir 영벡터 = 기존 전방 공식 동일 결과).");
            // [176차 갱신] 리플렉 궤적·등급 슬롯 패막 배선 철수 — 방사형 산란 제거(원형 링만 유지).
            //   PI1 방향 계산(sparkDir)/흡수점(p) 계산 자체는 유지 — 궤적 소비처 소멸만 검증.
            Assert.That(src.Split(new[] { "ShockwaveRingFX.SpawnParryReflectWisps(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 리플렉 궤적 호출 0건 — 방사형 산란 제거");
            Assert.That(src.Split(new[] { "SetNextReflectPerfect(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 등급 슬롯 배선 철수 — 방사형 산란 제거");
        }

        // ── FxRimOverlay 정적 지원 확장(SetRimColor/SetRimIntensity — PW1/PW3 전제) ──
        [Test]
        public void Source_36_RimOverlayStaticSupport()
        {
            string rim = Read("Assets/Scripts/Systems/FxRimOverlay.cs");
            Assert.That(rim, Does.Contain("[무번호 36-PW3] 정적 림 오버로드 대응"),
                "[36] SetRimColor 정적 경로 — 방어 림(AttachRimStatic) 색 전환 전제.");
            Assert.That(rim, Does.Contain("public static GameObject AttachRimStatic(GameObject sourceVisual"),
                "[36] 기존 29-P1-2 잠금 무손상.");
        }

        // ── [171차] 흰 단일 통합 — 패막 경로 잔존 청/금 0건 ──
        [Test]
        public void P171_Palette_Unified_White()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Not.Contain("ParryGold"),
                "[171차] 패막 경로 ParryGold 잔존 0건 — 흰 단일.");
            Assert.That(src, Does.Not.Contain("ParryCyan"),
                "[171차] 패막 경로 ParryCyan 잔존 0건 — 흰 단일.");
            Assert.That(src, Does.Not.Contain("ParryGoodCueBlue"),
                "[171차] ParryGoodCueBlue 폐기 — 흰 단일.");
            Assert.That(src, Does.Contain("FXPalette.ParryWhite"),
                "[171차] 패막 색 = FXPalette.ParryWhite 단일 소스.");
        }

        // ── [171차] 보유 에셋 FX 배선 — Resources/VFX/Parry 소비 ──
        [Test]
        public void P171_AssetFX_Wired()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src.Split(new[] { "ParryAssetFX.Spawn" }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1),
                "[171차] ParryAssetFX.Spawn 호출 1건.");
            Assert.That(src, Does.Contain("ParryAssetFX.Spawn(grade == ParryGrade.Perfect, p);"),
                "[171차] 등급 인자 배선 — Spawn(bool, Vector3).");

            string fx = Read("Assets/Scripts/Systems/ParryAssetFX.cs");
            Assert.That(fx, Does.Contain("public static void Spawn(bool isPerfect, Vector3 contactPoint)"),
                "[171차] Spawn(bool, Vector3) 시그니처.");
            Assert.That(fx, Does.Contain("\"VFX/Parry/Guard_01\""),
                "[171차] Resources/VFX/Parry/Guard_01 경로.");
            Assert.That(fx, Does.Contain("if (!Application.isPlaying) return;"),
                "[171차] EditMode 무동작 가드.");
            Assert.That(fx, Does.Contain("Object.Destroy(go, LifetimeSeconds);"),
                "[171차] 수명 2s 자동 파괴(LifetimeSeconds = 2f).");
        }

        // ── [171차 C1] PERFECT 반격 스텝인 ──
        [Test]
        public void P171_CounterStepIn()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            int iDef = src.IndexOf("private System.Collections.IEnumerator ParryCounterStepInRoutine(MonoBehaviour attacker)");
            Assert.That(iDef, Is.GreaterThanOrEqualTo(0),
                "[171차 C1] 스텝인 코루틴 정의.");
            Assert.That(src, Does.Contain("StartCoroutine(ParryCounterStepInRoutine(attacker));"),
                "[171차 C1] 호출부 배선.");
            Assert.That(src, Does.Contain("Vector3 target = start + toAttacker * 0.4f;"),
                "[171차 C1] 공격자 방향 0.4m 전진.");
            Assert.That(src.IndexOf("Time.unscaledDeltaTime", iDef), Is.GreaterThan(iDef),
                "[171차 C1] unscaled 시간 진행 — 히트스톱 무간섭.");
        }

        // ── [171차] 패막 SFX 클립 3종 수용 ──
        [Test]
        public void P171_SfxClips_Accepted()
        {
            string sfxDir = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources/Audio/SFX");
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(sfxDir, "SFX_Parry_Perfect.ogg")), Is.True,
                "[171차] SFX_Parry_Perfect.ogg 수용.");
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(sfxDir, "SFX_Parry_Good.ogg")), Is.True,
                "[171차] SFX_Parry_Good.ogg 수용.");
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(sfxDir, "SFX_Parry_Fail.ogg")), Is.True,
                "[171차] SFX_Parry_Fail.ogg 수용.");
        }

        // ── [171차] 텔레그래프 임팩트 마커 — 수축 링 자식 ──
        [Test]
        public void P171_Telegraph_ImpactMarker()
        {
            string src = Read("Assets/Scripts/Systems/TelegraphArcIndicator.cs");
            Assert.That(src, Does.Contain("TelegraphArcImpactRing"),
                "[171차] 수축 임팩트 링 자식 오브젝트.");
            Assert.That(src, Does.Contain("public static GameObject Spawn(Vector3 pos, float radius, float duration, Vector3 direction, bool isElite = false)"),
                "[171차] Spawn 시그니처 유지.");
        }
    }
}
