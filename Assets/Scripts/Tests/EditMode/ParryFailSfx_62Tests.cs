using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 62] 패링 실패/빈 최소 SFX 계약 — 36-PW2 의사결정 갱신(성공 위계와 대비하는 낮은 짧은 신호음).
    /// </summary>
    public class ParryFailSfx_62Tests
    {
        private static string Read(string path) => System.IO.File.ReadAllText(path);

        [Test]
        public void Source_62_ParryFailSfx()
        {
            // 1) SFX 위계 — Parry_Fail enum 멤버 + 클립명 매핑(성공 위계와 대비)
            string sfx = Read("Assets/Scripts/Systems/SoundEffectManager.cs");
            Assert.That(sfx, Does.Contain("Parry_Fail,"), "[62] SFXType.Parry_Fail 멤버(성공 위계 Parry_Perfect/Good과 대비).");
            Assert.That(sfx, Does.Contain("SFXType.Parry_Fail => \"SFX_Parry_Fail\""), "[62] 클립명 매핑 — Resources/Audio/SFX 클립명 규약.");

            // 2) 발화 경로 — EndParryWindow 내 Application.isPlaying 가드(성공 경로 배선 선례 1:1)
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            int iEnd = src.IndexOf("private void EndParryWindow()");
            int iNext = src.IndexOf("private void ResumeQueuedParryAction()");
            string body = src.Substring(iEnd, iNext - iEnd);
            Assert.That(body, Does.Contain("SFXType.Parry_Fail, transform.position)"), "[62] 최소 실패 SFX 발화(플레이어 위치 — 성공 경로 p 전달 선례).");
            Assert.That(body, Does.Contain("var failSfx = SoundEffectManager.Instance;"), "[62] 싱글턴 인스턴스 경로(95차 SFX 배선 선례).");
            Assert.That(body, Does.Contain("if (Application.isPlaying)"), "[62] Play 가드 — EditMode 무발화.");

            // 3) 36-PW2 최소 신호 유지 — 히트스톱/시각 FX 무추가(수렴 링만)
            Assert.That(body, Does.Contain("ShockwaveRingFX.SpawnConvergingRing(cp, 1.2f,"), "[36-PW2] 만료 수렴 링 유지.");
            Assert.That(body, Does.Not.Contain("Time.timeScale"), "[36-PW2] 히트스톱 무추가(최소 신호 유지).");
        }
    }
}
