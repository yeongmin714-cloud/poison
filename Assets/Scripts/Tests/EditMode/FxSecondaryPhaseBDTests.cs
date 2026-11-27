using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 33-B/D] 블룸 펄스 + 2차 파티클(데브리/불티) 계약 — 포스트 계층 2와 물리감 파티클.
    /// 결정론/유한 수명(누출 금지)/예산 가드/글로벌 블룸 회복/훅 배선/기존 경로 무손상 검증.
    /// </summary>
    public class FxSecondaryPhaseBDTests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── B1: 블룸 펄스 — 볼륨 인스턴스 패턴 승계 + 글로벌 회복 ──
        [Test]
        public void Source_33B1_BloomPulse()
        {
            string src = Read("Assets/Scripts/Systems/ScreenBloomFX.cs");
            Assert.That(src, Does.Contain("[무번호 33-B1]"), "[33-B1] 마커.");
            Assert.That(src, Does.Contain("public static void Spike(Camera cam, float peakIntensity, float totalDuration)"),
                "[33-B1] 스파이크 시그니처 — 31-V3 동일 형상(패턴 승계).");
            Assert.That(src, Does.Contain("if (!Application.isPlaying || cam == null) return;"),
                "[33-B1] EditMode 무동작 가드 + null 카메라 가드.");
            Assert.That(src, Does.Contain("_profile.Add<Bloom>(true)"),
                "[33-B1] Bloom 오버라이드만 — 별도 프로필(CA 볼륨과 파라미터 충돌 없음).");
            Assert.That(src, Does.Contain("_volume.isGlobal = true;"), "[33-B1] 글로벌 볼륨.");
            Assert.That(src, Does.Contain("_bloom.active = false;   // 글로벌 블룸 회복 — 잔존 억제(31-V3 0 복귀 계약 승계)"),
                "[33-B1] 종료 시 오버라이드 비활성 — 글로벌 블룸(PhaseG2) 완전 회복.");
            Assert.That(src, Does.Contain("if (_profile != null) Object.Destroy(_profile);"),
                "[33-B1] OnDestroy 프로필 파괴 — 누출 금지.");
            Assert.That(src, Does.Contain("Time.unscaledDeltaTime"), "[33-B1] unscaled 시간.");
            Assert.That(src, Does.Not.Contain("Random"), "[33-B1] 결정론 — 외부 난수 0건(신규 파일 전문).");
        }

        // ── B1 훅: PERFECT 패망(수차·골드 라이트와 동기) + 크리 한정 ──
        [Test]
        public void Source_33B1_Hooks()
        {
            string combat = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(combat, Does.Contain("ScreenBloomFX.Spike(vc, 2.2f, 0.12f);"),
                "[33-B1] 패망 PERFECT 블룸 펄스.");
            int iLight = combat.IndexOf("[무번호 33-A2] 패망 PERFECT 골드 라이트");
            int iBloom = combat.IndexOf("[무번호 33-B1] 블룸 임팩트 펄스");
            Assert.That(iBloom, Is.GreaterThan(iLight), "[33-B1] 골드 라이트 직후 발화(순서 계약).");

            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            // [무번호 39-T3 갱신] 크리 블룸 앰프 2.2→2.8(위계 체감 — 패망 PERFECT 2.2 별개 경로 유지).
            Assert.That(gate, Does.Contain("if (isCrit) ScreenBloomFX.Spike(Camera.main, 2.8f, 0.12f);"),
                "[33-B1] 크리 한정 블룸 펄스(39-T3 앰프 승계).");
        }

        // ── D1: 파편 데브리 — 결정론/예산/비주얼 전용/누출 금지 ──
        [Test]
        public void Source_33D1_Debris()
        {
            string src = Read("Assets/Scripts/Systems/FxDebrisSpawner.cs");
            Assert.That(src, Does.Contain("[무번호 33-D1]"), "[33-D1] 마커.");
            Assert.That(src, Does.Contain("public const int MaxConcurrentBursts = 4;"),
                "[33-D1] 동시 버스트 예산 상한 4.");
            Assert.That(src, Does.Contain("InteriorQualityBaseline.StableHash($\"debris_{seed}_{i}\")"),
                "[33-D1] 결정론 시드 — 외부 난수 미사용(StableHash 단일 소스).");
            Assert.That(src, Does.Contain("Object.Destroy(go.GetComponent<Collider>());"),
                "[33-D1] 비주얼 전용 — 충돌 부착 금지.");
            // [무번호 41-a 갱신 79-R20] 흰 정육면체 폐기 → 워먼 석편 톤(영상 실측 "흰 사각형" 근원 수리). AttackPrimary 경유 단일 소스.
            Assert.That(src, Does.Contain("new Color(FXPalette.AttackPrimary.r * 0.7f, FXPalette.AttackPrimary.g * 0.5f, FXPalette.AttackPrimary.b * 0.4f, 1f);"),
                "[33-D1/41-a 갱신 79-R20] 워먼 석편 톤(AttackPrimary 경유 — 공격 이펙트 통일색).");
            Assert.That(src, Does.Contain("_vel.y = -_vel.y * 0.45f;"),
                "[33-D1] 지면 1회 바운스 — 감쇠 반사.");
            Assert.That(src, Does.Contain("Time.unscaledDeltaTime"), "[33-D1] unscaled 시간 — 히트스톱 중 재생.");
            Assert.That(src, Does.Contain("if (_mat != null) Object.Destroy(_mat);"),
                "[33-D1] 머티리얼 인스턴스 파괴 — 누출 금지.");
            Assert.That(src, Does.Contain("if (remain < 0.25f) transform.localScale = _baseScale * Mathf.Max(0.01f, remain / 0.25f);"),
                "[33-D1] 말기 축소 소멸.");
            Assert.That(src, Does.Not.Contain("Random"), "[33-D1] 결정론 — 외부 난수 0건(신규 파일 전문).");
        }

        // ── D2: 소멸 불티 — FXPalette 단일 소스 + 유한 수명 ──
        [Test]
        public void Source_33D2_EmberRise()
        {
            string src = Read("Assets/Scripts/Systems/FxDebrisSpawner.cs");
            Assert.That(src, Does.Contain("main.startColor = new ParticleSystem.MinMaxGradient(FXPalette.Edge);"),
                "[33-D2] 불티 색=Edge 단일 소스.");
            Assert.That(src, Does.Contain("FXPalette.ApplyTo(rnd);"),
                "[33-D2] 정규화 머티리얼 단일 소스(45차 계약).");
            Assert.That(src, Does.Contain("main.gravityModifier = -0.12f;"),
                "[33-D2] 음수 중력 — 불티 상승.");
            Assert.That(src, Does.Contain("em.rateOverTime = new ParticleSystem.MinMaxCurve(0f);"),
                "[33-D2] 버스트 전용 — 연속 방출 없음(에미터 예산).");
            Assert.That(src, Does.Contain("Object.Destroy(go, 1.2f);"),
                "[33-D2] 유한 수명 자가 파괴 — 누출 금지.");
            Assert.That(src, Does.Not.Contain("Random"), "[33-D2] 결정론 — 에미터 상수 전용.");
        }

        // ── 훅: CombatFXGate D1 + AnimalAI D2(디졸브 동기 블록 내부) ──
        [Test]
        public void Source_33BD_Hooks()
        {
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(gate, Does.Contain("if (isCrit || damage >= 15f) FxDebrisSpawner.Burst(position, damageInt);"),
                "[33-D1] 크리/Heavy 한정 파편.");
            int iLight = gate.IndexOf("[무번호 33-A1]");
            int iBloom = gate.IndexOf("[무번호 33-B1]");
            int iDebris = gate.IndexOf("[무번호 33-D1]");
            Assert.That(iLight, Is.GreaterThan(0), "[33] A1 훅 존재.");
            Assert.That(iBloom, Is.GreaterThan(iLight), "[33] 라이트→블룸 순서.");
            Assert.That(iDebris, Is.GreaterThan(iBloom), "[33] 블룸→파편 순서.");

            string ai = Read("Assets/Scripts/Systems/AnimalAI.cs");
            int iDissolve = ai.IndexOf("if (dissolveBegan)");
            int iEmber = ai.IndexOf("[무번호 33-D2] 소멸 불티");
            int iSeq = ai.IndexOf("StartCoroutine(DeathDownRoutine(respawnDelay));");
            Assert.That(iEmber, Is.GreaterThan(iDissolve), "[33-D2] 불티는 디졸브 성공 블록 내부.");
            Assert.That(iEmber, Is.LessThan(iSeq), "[33-D2] 사망 시퀀스 시작 전 발화(D-4 순서 계약 승계).");
        }
    }
}
