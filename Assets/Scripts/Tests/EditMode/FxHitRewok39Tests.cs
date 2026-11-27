using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 39] 히트 리웍 2R+렌더 결함 수리 — ①디졸브 예약어 마젠타 수리 ②투명 머티리얼 표준화
    /// (체브런/눈금/크랙/엣지 — 흰·검 사각 렌더 실측) ③T2 방향 스파이크 ④T3 크리 앰프 ⑤T4 디졸브 티어 위계.
    /// </summary>
    public class FxHitRewok39Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void Source_DissolveReservedKeywordFix()
        {
            string src = Read("Assets/Shaders/FxDissolve.shader");
            Assert.That(src, Does.Contain("[무번호 39] 예약어 수리"), "[39] 수리 마커.");
            Assert.That(src, Does.Contain("half4 baseCol = SAMPLE_TEXTURE2D"), "[39] 변수명 baseCol(예약어 base 회피).");
            Assert.That(src, Does.Not.Contain("half4 base "), "[39] 예약어 `base` 잔존 금지(마젠타 렌더 뿌리).");
        }

        [Test]
        public void Source_UnlitTransparentStandard()
        {
            string lib = Read("Assets/Scripts/Systems/FxMaterialLibrary.cs");
            Assert.That(lib, Does.Contain("public static void EnsureUnlitTransparent(Material mat)"), "[39] 표준화 헬퍼.");
            Assert.That(lib, Does.Contain("if (mat.HasProperty(\"_Surface\")) mat.SetFloat(\"_Surface\", 1f);"), "[39] _Surface=1(URP/Unlit 투명 스위치).");
            foreach (var p in new[] { "Assets/Scripts/Systems/CommandMarker.cs", "Assets/Scripts/Systems/FxGroundCrackFX.cs", "Assets/Scripts/Systems/ScreenEdgeDangerFX.cs" })
            {
                string s = Read(p);
                Assert.That(s, Does.Contain("FxMaterialLibrary.EnsureUnlitTransparent(mat);"), $"[39] {p} 표준화 적용.");
            }
        }

        [Test]
        public void Source_T2_DirectionalSparks()
        {
            string src = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            // [무번호 79 사용자 결정 진화] 히트 방향 스파이크 3본 콘 제거 — "몬스터 피격 시 노란색 스파크 연출이
            //   촌스럽다" 사용자 판정(11-19). 활성 호출 소멸(주석 잔코드 ≠ 활성 계약)을 라인 단위로 검증한다.
            Assert.That(src, Does.Contain("[무번호 39-T2 → 79 사용자 결정]"), "[79 진화] 제거 마커.");
            bool activeCall = false;
            foreach (string raw in src.Split('\n'))
            {
                string line = raw.TrimStart();
                if (line.StartsWith("//")) continue;   // 주석 줄 제외
                if (line.Contains("ShockwaveRingFX.SpawnDirectionalSpark(position"))
                {
                    activeCall = true;
                    break;
                }
            }
            Assert.That(activeCall, Is.False, "[79 진화] 히트 경로 방향 스파이크 활성 호출 0건(PlayHitFXInternal).");
        }

        [Test]
        public void Source_T3_CritAmp()
        {
            string src = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(src, Does.Contain("isCrit ? 4.2f : 2.2f"), "[39-T3] 라이트 크리 앰프 4.2.");
            Assert.That(src, Does.Contain("ScreenBloomFX.Spike(Camera.main, 2.8f, 0.12f)"), "[39-T3] 블룸 앰프 2.8.");
        }

        [Test]
        public void Source_T4_DissolveTier()
        {
            string util = Read("Assets/Scripts/Systems/FxDissolveUtility.cs");
            Assert.That(util, Does.Contain("[무번호 39-T4] 티어 위계 오버로드"), "[39-T4] 오버로드 마커.");
            Assert.That(util, Does.Contain("public static bool TryBeginDissolve(GameObject body, Color edgeColor, float duration, int tier)"), "[39-T4] 4인자 오버로드.");
            Assert.That(util, Does.Contain("if (tier >= 5)"), "[39-T4] 정예 게이트.");
            Assert.That(util, Does.Contain("duration = 1.2f;"), "[39-T4] 정예 창 연장.");
            string ai = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(ai, Does.Contain("TryBeginDissolve(gameObject, FXPalette.MonsterDanger, 0.9f, (int)_tier)"), "[39-T4] 티어 전달 배선.");
        }
    }
}
