using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-M] 사망/피격 피드백 Phase M — 소스 계약(Source 잠금).
    /// 스펙: /tmp/m_b_brief.md §2/§3/§4 (ArmorBodyMeasureSpaceTests.cs 패턴 복제).
    /// ① M3 — DeathVFX 지면 링 잔광(ShockwaveRingFX.SpawnImpactRing 추가 — MaxBurstsPerSecond
    ///   예산 게이트 안, 생략 경로 공유, EA2 계약 무손상, 색은 FXPalette.MonsterAccent).
    /// ② M2 — CombatCameraEffects 피격 리코일(PlayDamageRecoil + ScreenVignetteFX 23-V1 선례
    ///   RuntimeInitializeOnLoadMethod 부트스트랩 구독 — FOV/fieldOfView 금지, useImpulseSource=false).
    /// 대상 구현: Assets/Scripts/Systems/DeathVFX.cs, Assets/Scripts/Systems/CombatCameraEffects.cs.
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class DeathFeedbackPhaseMTests
    {
        private static string ReadDeathVFX() => System.IO.File.ReadAllText("Assets/Scripts/Systems/DeathVFX.cs");
        private static string ReadCameraFx() => System.IO.File.ReadAllText("Assets/Scripts/Systems/CombatCameraEffects.cs");

        // ===== [무번호 28-M] 소스 계약 =====

        [Test]
        public void Source_DeathRing()
        {
            string src = ReadDeathVFX();
            // T1: 지면 링 1겹 추가 — SpawnImpactRing 호출 + 몬스터 액센트 팔레트 참조(로컬 리터럴 부재).
            Assert.That(src, Does.Contain("ShockwaveRingFX.SpawnImpactRing("),
                "[무번호 28-M3] 사망 지면 링 잔광 — ShockwaveRingFX.SpawnImpactRing 호출이 존재해야 한다(AnimalAI L974 선례 시그니처).");
            Assert.That(src, Does.Contain("FXPalette.MonsterAccent"),
                "[무번호 28-M3] 링 색은 몬스터 액센트 팔레트(25-IN 계승) 참조여야 한다.");
            Assert.That(src, Does.Not.Contain("new Color("),
                "[무번호 28-M3] 로컬 리터럴 금지 — DeathVFX에 new Color( 리터럴이 남아 있으면 안 된다.");
        }

        [Test]
        public void Source_DeathRingBudget()
        {
            string src = ReadDeathVFX();
            // T2: 링 삽입이 예산 게이트 논리 안(생략 경로 공유) — 구현 주석 키워드 잠금 + 구조 순서 검증.
            Assert.That(src, Does.Contain("MaxBurstsPerSecond 게이트"),
                "[무번호 28-M3] 링은 MaxBurstsPerSecond 예산 게이트 내부에 두어야 한다(주석 명시 계약).");
            Assert.That(src, Does.Contain("생략 경로 공유"),
                "[무번호 28-M3] 예산 초과 스킵 시 링도 함께 생략(생략 경로 공유)이 주석으로 명시되어야 한다 — 파티클 없이 링만 남는 불일치 방지.");
            int gate = src.IndexOf("TryConsumeBudget");
            int ring = src.IndexOf("SpawnImpactRing");
            Assert.That(gate, Is.GreaterThanOrEqualTo(0),
                "[무번호 28-M3] 예산 게이트 TryConsumeBudget 호출이 존재해야 한다(기존 로직 무손상).");
            Assert.That(ring, Is.GreaterThan(gate),
                "[무번호 28-M3] SpawnImpactRing 호출은 예산 게이트(TryConsumeBudget) 이후(=게이트 통과 경로 안)에 있어야 한다 — 스폰 생략 경로 공유.");
        }

        [Test]
        public void Source_RecoilApi()
        {
            string src = ReadCameraFx();
            // T3: 피격 리코일 static API — 시그니처 + useImpulseSource=false 고정 + FOV 부정 잠금.
            Assert.That(src, Does.Contain("public static void PlayDamageRecoil(Vector3"),
                "[무번호 28-M2] 피격 리코일 static API PlayDamageRecoil(Vector3, float)가 존재해야 한다.");
            Assert.That(src, Does.Contain("useImpulseSource = false"),
                "[무번호 28-M2] Cinemachine Impulse 미사용 — useImpulseSource = false 고정이어야 한다(기존 ShakeRoutine 경로만).");
            Assert.That(src, Does.Not.Contain("fieldOfView"),
                "[무번호 28-M2] FOV 킥 제외 계약 — CombatCameraEffects에 fieldOfView 문자열이 존재하면 안 된다.");
        }

        [Test]
        public void Source_RecoilBootstrap()
        {
            string src = ReadCameraFx();
            // T4: 부트스트랩 구독 — ScreenVignetteFX L94-99 선례(RIAOL + Core 무수정 + 이중 구독 방지 가드).
            Assert.That(src, Does.Contain("[RuntimeInitializeOnLoadMethod"),
                "[무번호 28-M2] 피격 리코일 부트스트랩은 RuntimeInitializeOnLoadMethod(23-V1 선례)여야 한다.");
            Assert.That(src, Does.Contain("ProjectName.Core.PlayerHealth.OnPlayerDamaged"),
                "[무번호 28-M2] Core 무수정 계약 — ProjectName.Core.PlayerHealth.OnPlayerDamaged 구독만으로 배선해야 한다.");
            Assert.That(src, Does.Contain("if (_damageRecoilHooked) return;"),
                "[무번호 28-M2] 이중 구독 방지 정적 가드(_damageRecoilHooked — ScreenVignetteFX _damageHooked 선례)가 존재해야 한다.");
        }

        [Test]
        public void Source_ExistingUnharmed()
        {
            string src = ReadCameraFx();
            // T5: 기존 PlayHit()/PlayCrit() 본문 리터럴 잠금 유지(부주의 수정 방지 — 대표 문자열 2개).
            Assert.That(src, Does.Contain("Instance.PlayHitShake(Instance._hitShakeIntensity);"),
                "[무번호 28-M2] 기존 PlayHit() 본문(PlayHitShake(_hitShakeIntensity))이 무손상이어야 한다.");
            Assert.That(src, Does.Contain("BulletTime.Get().Apply(0.4f, 0.08f, 0.10f);"),
                "[무번호 28-M2] 기존 PlayCrit() 본문(BulletTime.Apply(0.4f, 0.08f, 0.10f))이 무손상이어야 한다.");
        }

        [Test]
        public void Source_Marker()
        {
            // T6: 라운드 마커 + 결정론 계약 — 마커 존재, 신규 코드에 UnityEngine.Random 부재.
            string deathVfx = ReadDeathVFX();
            string cameraFx = ReadCameraFx();
            Assert.That(deathVfx, Does.Contain("[무번호 28-M3]"),
                "[무번호 28-M3] DeathVFX 변경 지점 마커가 존재해야 한다.");
            Assert.That(cameraFx, Does.Contain("[무번호 28-M2]"),
                "[무번호 28-M2] CombatCameraEffects 변경 지점 마커가 존재해야 한다.");
            Assert.That(deathVfx, Does.Not.Contain("UnityEngine.Random"),
                "[무번호 28-M] 결정론 계약 — DeathVFX에 UnityEngine.Random이 존재하면 안 된다.");
            Assert.That(cameraFx, Does.Not.Contain("UnityEngine.Random"),
                "[무번호 28-M] 결정론 계약 — CombatCameraEffects에 UnityEngine.Random이 존재하면 안 된다.");
        }
    }
}
