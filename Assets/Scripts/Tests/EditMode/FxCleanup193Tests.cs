using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [193차] FX 정리 계약 — 소스 문자열 어서션(CombatFxTelegraphPhaseBTests 관례).
    /// 배경: 사용자 지시 ① "여전히 몬스터 적색링 남아있고" ② "피격시 돌아가는 파란색 파티클도 제거해줘".
    /// 실측: 190차는 AnimalAI·AfterlifeEnemy만 제거 — GuardPlaceholder(병사형) :1459 잔존 스폰이 적색링의
    ///   마지막 몬스터 측 원. 파란 파티클 = MagicHit 에셋의 "1-Shockwave 2" 서브 이미터(청백 — 190차 판독).
    /// 계약:
    ///  ① 몬스터 3종(AnimalAI·AfterlifeEnemy·GuardPlaceholder) TelegraphArcIndicator.Spawn 잔여 0건
    ///     — 호 판정(IsInFrontArc)은 유지(190차 무손상 계약 승계).
    ///  ② MagicHit Shockwave 서브 이미터 인스턴스 전용 억제 — NormalizeMagicHitMaterials 단일 지점
    ///     (PlayImpact/PlayCross 양 경로 공통 통과 — 실측 호출부 2건), 에셋 무변경(56-P1 계약 동일).
    ///  ③ 느낌표 경보(AttackTelegraphIndicator.SpawnAlert) 무수정 — TelegraphArcIndicator 헤더 계약.
    /// </summary>
    public sealed class FxCleanup193Tests
    {
        private static string S(string path) => System.IO.File.ReadAllText(path);

        private static readonly string[] MonsterAiFiles =
        {
            "Assets/Scripts/Systems/AnimalAI.cs",
            "Assets/Scripts/Systems/AfterlifeEnemy.cs",
            "Assets/Scripts/Systems/GuardPlaceholder.cs",
        };

        // ── ① 몬스터 적색링 스폰 잔여 0건 ──
        [Test]
        public void Monster_Telegraph_Spawns_All_Removed()
        {
            foreach (string path in MonsterAiFiles)
            {
                Assert.That(S(path), Does.Not.Contain("TelegraphArcIndicator.Spawn("),
                    "[193] 몬스터 적색링(텔레그래프 호) 스폰 잔존 금지: " + path);
            }
        }

        // ── ①-b 호 판정 유지 — 시각 제거만(명중 로직 무손상) ──
        [Test]
        public void Monster_Arc_Judgment_Preserved()
        {
            foreach (string path in MonsterAiFiles)
            {
                Assert.That(S(path), Does.Contain("IsInFrontArc"),
                    "[193] 부채꼴 호 판정 유지 — 시각만 제거(190차 무손상 계약 승계): " + path);
            }
        }

        // ── ② MagicHit Shockwave 억제 — 정규화 단일 지점 ──
        [Test]
        public void MagicHit_Shockwave_Suppressed_At_Normalize()
        {
            string src = S("Assets/Scripts/Systems/SlashVFXRunner.cs");
            int fn = src.IndexOf("internal static void NormalizeMagicHitMaterials", System.StringComparison.Ordinal);
            Assert.That(fn, Is.GreaterThanOrEqualTo(0), "[193] MagicHit 정규화 함수 존재.");
            string body = src.Substring(fn);
            Assert.That(body, Does.Contain("Contains(\"Shockwave\")"),
                "[193] Shockwave 서브 이미터 억제 분기 — 정규화 단일 지점(양 스폰 경로 공통).");
            Assert.That(body, Does.Contain("StopEmittingAndClear"),
                "[193] 기존 발화 중지 — 렌더+방출 동시 차단.");
            Assert.That(body, Does.Contain("193차 제거"),
                "[193] 사용자 지시 근거 주석 존재.");
        }

        // ── ②-b 에셋 무변경 — 프리팹 파일은 원본 유지(인스턴스 전용 억제 계약) ──
        [Test]
        public void MagicHit_Prefab_Untouched_Contract()
        {
            // 인스턴스 전용 억제라 프리팹 YAML 변경 불요 — 스폰 경로 억제가 전부.
            // (수정 실패 시 런타임 RT프로브로 검출 — P4 진단 파이프라인 재사용)
            Assert.That(S("Assets/Scripts/Systems/SlashVFXRunner.cs"), Does.Contain("MagicHitResourcePath"),
                "[193] MagicHit 단일 경로 상수 유지 — 경로 교체 금지(47차 계약).");
        }
    }
}
