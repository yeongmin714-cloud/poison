using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [166-D] 몬스터 피격 효과음 계약 — 발화 훅 소스 어서션.
    ///  · 데미지 실제 적용 공통 종착(target.TakeDamage 직후) 2곳 훅:
    ///    AttackTarget(레거시/콤보/자동조준/스윕 전 경로 종착) + TryChargeAttack(충전 강공).
    ///  · SFX_Combat_Hit 토큰 + 무기별 프로필 승계(AttackSoundLayerManager.ProfileOf) + 랜덤 피치 지터 상수.
    /// </summary>
    public class MonsterHitSfxTests
    {
        static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void MonsterHitSfx_SourceContract_HookAndToken()
        {
            string sfx = Read("Assets/Scripts/Systems/MonsterHitSfx.cs");
            Assert.That(sfx, Does.Contain("[166-D]"), "166-D 마커 주석");
            Assert.That(sfx, Does.Contain("Audio/SFX/SFX_Combat_Hit"), "[166-D] SFX 토큰(Resources)");
            Assert.That(sfx, Does.Contain("AttackSoundLayerManager.ProfileOf"), "[166-D] 무기별 pitch/volume 프로필 승계");
            Assert.That(sfx, Does.Contain("PitchJitterAmplitude"), "[166-D] 랜덤 피치 마이크로베리에이션 상수");
            Assert.That(sfx, Does.Contain("public static void PlayMonsterHit"), "[166-D] 공개 발화 API");

            string combat = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            int hookCount = System.Text.RegularExpressions.Regex
                .Matches(combat, "MonsterHitSfx\\.PlayMonsterHit\\(").Count;
            Assert.That(hookCount, Is.EqualTo(2),
                "[166-D] 발화 훅 2곳 — AttackTarget 공통 종착 + TryChargeAttack 강공");
            Assert.That(combat, Does.Contain("[166-D]"), "[166-D] 훅 마커 주석");
        }
    }
}
