using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 70] 공격 VFX 업그레이드 계약 — U1 히트 틴트 가시화 / U2 스윙 무기 발광 펄스 / U3 크리 플립북 색 위계.
    /// 소스 문자열 계약 방식(FxQualityRoadmapP37Tests 스타일). 부정 잠금은 코드형 토큰만 사용(단어형 금지 — 자기 주석 오염 주의).
    /// </summary>
    public class FxAttackUpgradeP70Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        // ── U1: 히트 틴트 가시화(38-T1 이월) — 전 슬롯/인스턴스 클론/원복 ──
        [Test]
        public void Source_U1_HitTintAllSlots_InstanceRestore()
        {
            string src = Read("Assets/Scripts/Systems/CombatVFXController.cs");
            Assert.That(src, Does.Contain("[무번호 70-U1]"), "[70-U1] 마커.");
            // ① 전 슬롯 판독 — sharedMaterials 직접 판독(무인스턴스화).
            Assert.That(src, Does.Contain("try { shared = r.sharedMaterials; } catch (System.Exception) { shared = null; }"),
                "[70-U1] 전 슬롯 sharedMaterials 직접 판독(49-T1 게터 금지 승계).");
            // ② 인스턴스 클론 + 47-a 단일 관문 대입 + 종료 시 원본 배열 원복.
            Assert.That(src, Does.Contain("var inst = Object.Instantiate(m);"), "[70-U1] 인스턴스 클론 — shared 무손상.");
            Assert.That(src, Does.Contain("FxMaterialLibrary.TryAssignSharedMaterials(r, instArr, \"HitFlashTint70\")"),
                "[70-U1] 대입 47-a 단일 관문 경유.");
            Assert.That(src, Does.Contain("FxMaterialLibrary.TryAssignSharedMaterials(kv.Key, kv.Value, \"HitFlashRestore70\")"),
                "[70-U1] 종료 시 원본 배열 원복(47-a 관문).");
            Assert.That(src, Does.Contain("Object.Destroy(im);"), "[70-U1] 대입 실패 시 미소유 인스턴스 즉시 정리(누출 금지).");
            Assert.That(src, Does.Contain("foreach (var m in _mats) if (m != null) Object.Destroy(m);"),
                "[70-U1] 플래시 종료 시 인스턴스 파괴(누출 금지).");
            // ③ 지속 0.10s([무번호 80-P7] — 0.08s(38-T1)→림 실루엣(FxHitSilhouette 0.10s) 창 합일, no80 갭① 종결).
            Assert.That(src, Does.Contain("const float FlashDuration = 0.10f;"), "[80-P7] 플래시 지속 0.08→0.10s — 림 창 합일.");
            Assert.That(src, Does.Contain("[무번호 80-P7]"), "[80-P7] 마커.");
            // 기존 레지스트리 구조 유지 계약 — begin/end 쌍.
            Assert.That(src, Does.Contain("private static void FlashMaterialBegin(Material m)"), "[70-U1] FlashMaterialBegin 레지스트리 구조 유지.");
            Assert.That(src, Does.Contain("private static void FlashMaterialEnd(Material m)"), "[70-U1] FlashMaterialEnd 레지스트리 구조 유지.");
            // PlayHitFlash 메서드 범위 잠금(부정) — 코드형 토큰만.
            int s = src.IndexOf("public static void PlayHitFlash", System.StringComparison.Ordinal);
            int e = src.IndexOf("private static readonly", s, System.StringComparison.Ordinal);
            Assert.That(s, Is.GreaterThanOrEqualTo(0), "[70-U1] PlayHitFlash 존재.");
            Assert.That(e, Is.GreaterThan(s), "[70-U1] 메서드 범위 판정 가능.");
            string method = src.Substring(s, e - s);
            Assert.That(method, Does.Not.Contain("r.sharedMaterial;"), "[70-U1] 슬롯 0 한정 판독(구 r.sharedMaterial;) 폐기 — 전 슬롯 대상.");
            Assert.That(method, Does.Not.Contain(".material"), "[70-U1] 게터 금지(49-T1 승계) — sharedMaterials 직접 판독만.");
        }

        // ── U2: 스윙 발화 무기 발광 펄스 ──
        [Test]
        public void Source_U2_WeaponGlowPulse_Wired()
        {
            string glow = Read("Assets/Scripts/Systems/FxWeaponGlow.cs");
            Assert.That(glow, Does.Contain("[무번호 70-U2]"), "[70-U2] 마커.");
            Assert.That(glow, Does.Contain("public static void Pulse(GameObject visual, Color color, float duration)"),
                "[70-U2] Pulse static 유틸 시그니처.");
            Assert.That(glow, Does.Contain("if (!Application.isPlaying) return;"), "[70-U2] EditMode 무동작 가드.");
            Assert.That(glow, Does.Contain("if (visual == null || duration <= 0f) return;"),
                "[70-U2] 주먹/미장착(CurrentVisual null) 조용 스킵.");
            Assert.That(glow, Does.Contain("FxMaterialLibrary.TryAssignSharedMaterials(rend, instArr, \"FxWeaponGlow\")"),
                "[70-U2] 대입 47-a 단일 관문 경유.");
            Assert.That(glow, Does.Contain("FxMaterialLibrary.TryAssignSharedMaterials(p.Rend, p.Originals, \"FxWeaponGlowRestore\")"),
                "[70-U2] 종료 시 원본 배열 원복(캡처/원복 계약 승계).");
            Assert.That(glow, Does.Contain("Object.Destroy(m);"), "[70-U2] 인스턴스 머티리얼 파괴(누출 금지 — 유한 수명).");
            Assert.That(glow, Does.Contain("private void OnDestroy() { Restore(); }"),
                "[70-U2] 유한 수명 러너 단일 복원 경로(FxImpactLight 감산 선례).");
            // 부정 잠금 — 코드형 토큰만(결정론).
            Assert.That(glow, Does.Not.Contain("UnityEngine.Random"), "[70-U2] 결정론 — 난수 경로 0건.");

            string driver = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(driver, Does.Contain("[무번호 70-U2]"), "[70-U2] 배선부 마커.");
            Assert.That(driver, Does.Contain("FxWeaponGlow.Pulse(WeaponEquipManager.CurrentVisual, FXPalette.AttackPrimary, 0.15f);"),
                "[70-U2 갱신 79-R20] FireComboSlash Swing 호출 지점 인접 배선 — 색=FXPalette.AttackPrimary(공격 이펙트 통일색), 0.15s.");
        }

        // ── U3: 크리 슬래시 플립북 색 위계 ──
        [Test]
        public void Source_U3_CritFlipbook_GoldHierarchy()
        {
            string flash = Read("Assets/Scripts/Systems/FxSlashFlipbookFlash.cs");
            Assert.That(flash, Does.Contain("[무번호 70-U3]"), "[70-U3] 마커.");
            Assert.That(flash, Does.Contain("public static void Play(Vector3 position, float scale, bool isCrit = false)"),
                "[70-U3] 시그니처 확장(기본값으로 구 2인자 호출 호환).");
            Assert.That(flash, Does.Contain("main.startColor = new Color(FXPalette.AttackPrimary.r, FXPalette.AttackPrimary.g, FXPalette.AttackPrimary.b, 1f);"),
                "[70-U3 갱신 79-R20] 일반/크리 통일색 = FXPalette.AttackPrimary(크리 구분은 크기 앰프+블룸 라이트 위계 — 색 통일).");
            // 부정 잠금 — 코드형 토큰만(결정론 유지).
            Assert.That(flash, Does.Not.Contain("UnityEngine.Random"), "[70-U3] 결정론 유지.");

            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(gate, Does.Contain("FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f, isCrit);"),
                "[70-U3] 갱신된 호출부 — isCrit 전달.");
        }

        // ── [무번호 80-P3] 트레일 층 — 칼날 팁 즉발 잔상(U2 진화) ──
        [Test]
        public void Source_U2_WeaponTrailLayer_P3_Contract()
        {
            string glow = Read("Assets/Scripts/Systems/FxWeaponGlow.cs");
            Assert.That(glow, Does.Contain("[무번호 80-P3]"), "[80-P3] 마커.");
            Assert.That(glow, Does.Contain("TrailBurst(color);"), "[80-P3] Pulse 발화점 트레일 동시 발화(신규 키/파라미터 없음 — Task 3.3).");
            Assert.That(glow, Does.Contain("AddComponent<TrailRenderer>()"), "[80-P3] 트레일 컴포넌트(Task 3.1).");
            Assert.That(glow, Does.Contain("AttackArcVFX.AlphaCap"), "[80-P3] 알파 캡 0.62 계약 승계(53~60차).");
            Assert.That(glow, Does.Contain("AttackArcVFX.RefTipSpeed"), "[80-P3] RefTipSpeed 속도 게이트(기준 속도 미만 미발화 — Task 3.1).");
            Assert.That(glow, Does.Contain("WeaponSwingTrail.CreateTrailMaterialShared(1f)"), "[80-P3] ST2 중복 구현 금지 — 공개 팩토리 위임.");
            Assert.That(glow, Does.Contain("WeaponSwingTrail.GetTrailTipAnchor()"), "[80-P3] 팁 앵커 승계(손 본 추적 공짜 동기).");
            Assert.That(glow, Does.Contain("const float TrailLifetime = 0.18f"), "[80-P3] 수명 창 0.12~0.18s 상한(Task 3.2).");
            Assert.That(glow, Does.Not.Contain("UnityEngine.Random"), "[80-P3] 결정론 — 난수 0건.");
            // 배선부 무변동 — 기존 호출 지점 그대로(신규 키/파라미터 없음 계약).
            string driver = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(driver, Does.Contain("FxWeaponGlow.Pulse(WeaponEquipManager.CurrentVisual, FXPalette.AttackPrimary, 0.15f);"),
                "[80-P3] 기존 호출 지점 무변동 — Pulse 내부 동시 발화.");
            Assert.That(driver, Does.Contain("[무번호 80-P3]"), "[80-P3] 배선부 마커.");
        }
    }
}
