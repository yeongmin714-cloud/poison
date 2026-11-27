using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [128-2 P3] Npc2026 자산 계약 테스트 — 24종 rigged 모델 로드 실측 + walk/idle/talk 클립 존재·루프 계약.
    /// 진실 출처: TestOutput/npc_audit_128_2_report.json(감사 실측) + Npc2026 리소스 임포트 실측.
    ///  · 24종 = Npc_Boy_1..5 / Npc_Girl_1..6 / Npc_Man_1..5 / Npc_OldMan_1..3 / Npc_OldWoman_1..5
    ///    (폴더명 = 파일 스템. 리소스 베이스 = Models/UserProvided/Npc2026/{스템}/{스템}_rigged — 모델,
    ///    {스텝}_walk/{스템}_idle/{스템}_talk — 클립(Resources.LoadAll)).
    ///  · 감사 진실치(빈 클립): walk = Npc_Man_5 / idle = Npc_Girl_5 각 1종, talk = Npc_OldWoman_1(빈 스택)
    ///    + Npc_OldWoman_3(talk.fbx 누락) 2종 — 나머지 68종 비-empty(23+23+22 = 68 비-empty 클립 경로).
    ///  · loopTime 계약: walk/idle 전원 isLooping == true(루프), talk 전원 isLooping == false(원샷).
    ///  · EditMode 규약: Object.Destroy 불가 → 인스턴스화 금지 — Resources 로드만 수행.
    ///  · Resources 부재 환경 안전: alias/폴백 계약은 런타임 호출이 아니라 소스계약(File.ReadAllText 문자열 단정)
    ///    으로 검증(MixamoClipLoopTests 98차 소스계약 선례 준용).
    /// </summary>
    public class Npc2026AssetContractTests
    {
        private const string ResourceBase = "Models/UserProvided/Npc2026";

        /// <summary>24종 캐릭터 스템(폴더명 = 파일 스템) — 5+6+5+3+5 = 24.</summary>
        private static readonly string[] Npc2026Stems =
        {
            "Npc_Boy_1", "Npc_Boy_2", "Npc_Boy_3", "Npc_Boy_4", "Npc_Boy_5",
            "Npc_Girl_1", "Npc_Girl_2", "Npc_Girl_3", "Npc_Girl_4", "Npc_Girl_5", "Npc_Girl_6",
            "Npc_Man_1", "Npc_Man_2", "Npc_Man_3", "Npc_Man_4", "Npc_Man_5",
            "Npc_OldMan_1", "Npc_OldMan_2", "Npc_OldMan_3",
            "Npc_OldWoman_1", "Npc_OldWoman_2", "Npc_OldWoman_3", "Npc_OldWoman_4", "Npc_OldWoman_5",
        };

        /// <summary>스포너 키 계약 24종(man1~girl6) — VillageNpcSpawner.VillagerKeys와 1:1 대응.</summary>
        private static readonly string[] VillagerKeyContract =
        {
            "man1", "man2", "man3", "man4", "man5",
            "girl1", "girl2", "girl3", "girl4", "girl5", "girl6",
            "boy1", "boy2", "boy3", "boy4", "boy5",
            "oldman1", "oldman2", "oldman3",
            "oldwoman1", "oldwoman2", "oldwoman3", "oldwoman4", "oldwoman5",
        };

        /// <summary>감사 진실치 — 빈 클립(clipAnimations 미산출) 스텝: walk/idle 1종씩.
        /// [128-2 P3 정정] talk는 2종 — Npc_OldWoman_1(파일 존재·스택 0) + Npc_OldWoman_3(talk.fbx 원래 누락).
        /// 1차 EditMode 런 실측으로 지시서 오기(23종) 정정 — 진실 출처 TestOutput/npc_audit_128_2_report.json.</summary>
        private static readonly Dictionary<string, string> EmptyClipTruth = new Dictionary<string, string>
        {
            { "walk", "Npc_Man_5" },      // 감사: Npc_Man_5/walk — stack=0, moving=0, 평평한커브
            { "idle", "Npc_Girl_5" },     // 감사: Npc_Girl_5/idle — stack=0, moving=0, 평평한커브
            { "talk", "Npc_OldWoman_1" }, // 감사: Npc_OldWoman_1/talk — 파일 있으나 stack=0(스포너 idle폴백 대상)
        };

        /// <summary>[128-2 P3 정정] talk 빈 클립 판정 2종 — OW_1(빈 스택)·OW_3(talk.fbx 누락). 런타임 폴백(talk→자기 idle)은 VillageNpcSpawner.BuildVillagerOverride가 공통 커버.</summary>
        private static bool IsTalkEmpty(string stem) => stem == "Npc_OldWoman_1" || stem == "Npc_OldWoman_3";

        private static string ModelPath(string stem) => $"{ResourceBase}/{stem}/{stem}_rigged";
        private static string ClipPath(string stem, string kind) => $"{ResourceBase}/{stem}/{stem}_{kind}";
        private static bool IsEmpty(AnimationClip[] clips) => clips == null || clips.Length == 0;

        /// <summary>문자열 내 비중첩 출현 횟수(소스계약 건수 단정용).</summary>
        private static int CountOccurrences(string text, string token)
        {
            int count = 0;
            int idx = text.IndexOf(token, System.StringComparison.Ordinal);
            while (idx >= 0)
            {
                count++;
                idx = text.IndexOf(token, idx + token.Length, System.StringComparison.Ordinal);
            }
            return count;
        }

        /// <summary>
        /// [128-2 P3] 계약 1 — alias 소스계약: VillageNpcSpawner 소스에 VillagerKeys 24키(man1~girl6)가
        /// 따옴표 문자열 리터럴로 존재, RuntimeModelLoader 소스에 "Npc2026/" alias 24건 존재
        /// (Resources 부재 환경 안전 — 런타임 호출 없이 소스 문자열 단정).
        /// </summary>
        [Test]
        public void AliasMap_24VillagerKeys_SourceContract_128_2P3()
        {
            var spawnerSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");
            foreach (var key in VillagerKeyContract)
                Assert.That(spawnerSrc, Does.Contain("\"" + key + "\""),
                    $"[128-2 P3] VillagerKeys 소스계약 — 키 '{key}' 리터럴 존재.");

            var loaderSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/RuntimeModelLoader.cs");
            int aliasCount = CountOccurrences(loaderSrc, "\"Npc2026/");
            Assert.That(aliasCount, Is.GreaterThanOrEqualTo(24),
                $"[128-2 P3] RuntimeModelLoader Npc2026 alias 실측 — 24건 필요, 실측 {aliasCount}건.");

            // alias → 폴더 레이아웃 1:1 대응(스텝별 rigged 풀경로 alias 존재).
            foreach (var stem in Npc2026Stems)
                Assert.That(loaderSrc, Does.Contain("\"Npc2026/" + stem + "/" + stem + "_rigged\""),
                    $"[128-2 P3] alias 소스계약 — '{stem}' rigged alias 존재.");
        }

        /// <summary>
        /// [128-2 P3] 계약 2 — VillageNpcSpawner.VillagerKeys 리플렉션 길이 24 단정
        /// (private static readonly string[] — SaveManager 리플렉션 선례 패턴).
        /// </summary>
        [Test]
        public void VillagerKeys_Count24_Reflection_128_2P3()
        {
            var field = typeof(ProjectName.Systems.VillageNpcSpawner)
                .GetField("VillagerKeys", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null,
                "[128-2 P3] VillagerKeys 필드 존재(private static readonly string[]).");

            var keys = field.GetValue(null) as string[];
            Assert.That(keys, Is.Not.Null, "[128-2 P3] VillagerKeys 배열 실체(string[]).");
            Assert.That(keys.Length, Is.EqualTo(24),
                $"[128-2 P3] VillagerKeys 24종 계약 — 실측 {keys.Length}종.");
        }

        /// <summary>
        /// [128-2 P3] 계약 3 — 24종 rigged 모델 전원 Resources.Load 성공 실측
        /// (Models/UserProvided/Npc2026/{스텝}/{스템}_rigged — GameObject 로드, 인스턴스화 없음).
        /// </summary>
        [Test]
        public void RiggedModels_Loadable_24_128_2P3()
        {
            foreach (var stem in Npc2026Stems)
            {
                var model = Resources.Load<GameObject>(ModelPath(stem));
                Assert.That(model, Is.Not.Null,
                    $"[128-2 P3] rigged 모델 로드 실측 — {ModelPath(stem)}.");
            }
        }

        /// <summary>
        /// [128-2 P3] 계약 4 — walk 클립 존재: 23종 non-empty, Npc_Man_5만 empty(감사 진실치 —
        /// stack=0, moving=0, 평평한커브 → 임포트 클립 미산출).
        /// </summary>
        [Test]
        public void WalkClips_Presence_128_2P3()
        {
            foreach (var stem in Npc2026Stems)
            {
                var clips = Resources.LoadAll<AnimationClip>(ClipPath(stem, "walk"));
                if (stem == EmptyClipTruth["walk"])
                    Assert.That(IsEmpty(clips), Is.True,
                        $"[128-2 P3] 감사 진실치 — {stem} walk 빈 클립 계약(폴백표 근거).");
                else
                    Assert.That(IsEmpty(clips), Is.False,
                        $"[128-2 P3] walk 클립 로드 실측 — {ClipPath(stem, "walk")}.");
            }
        }

        /// <summary>
        /// [128-2 P3] 계약 5 — idle 클립 존재: 23종 non-empty, Npc_Girl_5만 empty(감사 진실치 —
        /// stack=0, moving=0, 평평한커브 → 스포너 idle 폴백 근거).
        /// </summary>
        [Test]
        public void IdleClips_Presence_128_2P3()
        {
            foreach (var stem in Npc2026Stems)
            {
                var clips = Resources.LoadAll<AnimationClip>(ClipPath(stem, "idle"));
                if (stem == EmptyClipTruth["idle"])
                    Assert.That(IsEmpty(clips), Is.True,
                        $"[128-2 P3] 감사 진실치 — {stem} idle 빈 클립 계약(Girl_1 idle 폴백 근거).");
                else
                    Assert.That(IsEmpty(clips), Is.False,
                        $"[128-2 P3] idle 클립 로드 실측 — {ClipPath(stem, "idle")}.");
            }
        }

        /// [128-2 P3 정정] 계약 6 — talk 클립 존재: 22종 non-empty, empty 2종(Npc_OldWoman_1 빈 스택 +
        /// Npc_OldWoman_3 talk.fbx 원래 누락 — 감사 리포트 진실치).
        /// </summary>
        [Test]
        public void TalkClips_Presence_128_2P3()
        {
            foreach (var stem in Npc2026Stems)
            {
                var clips = Resources.LoadAll<AnimationClip>(ClipPath(stem, "talk"));
                if (IsTalkEmpty(stem))
                    Assert.That(IsEmpty(clips), Is.True,
                        $"[128-2 P3] 감사 진실치 — {stem} talk 빈 클립 계약.");
                else
                    Assert.That(IsEmpty(clips), Is.False,
                        $"[128-2 P3] talk 클립 로드 실측 — {ClipPath(stem, "talk")}.");
            }
        }

        /// <summary>
        /// [128-2 P3] 계약 7 — walk/idle 루프 계약: 로드된 walk/idle 클립 전원 isLooping == true
        /// (임포트 loopTime 계약 — MixamoClipLoopTests 97차 F1 선례의 Npc2026 확장).
        /// 하한 46 = 비-empty walk 23 + idle 23(클립 1개/파일 기준 최소치).
        /// </summary>
        [Test]
        public void WalkIdleClips_Loop_128_2P3()
        {
            int checkedClips = 0;
            foreach (var stem in Npc2026Stems)
            foreach (var kind in new[] { "walk", "idle" })
            {
                var clips = Resources.LoadAll<AnimationClip>(ClipPath(stem, kind));
                if (IsEmpty(clips)) continue; // 감사 진실치 빈 클립 스텝(계약 4/5) — 루프 단정 대상 아님
                foreach (var clip in clips)
                {
                    Assert.That(clip.isLooping, Is.True,
                        $"[128-2 P3] {kind} 루프 계약(loopTime=1) — {stem}/{clip.name}.");
                    checkedClips++;
                }
            }
            Assert.That(checkedClips, Is.GreaterThanOrEqualTo(46),
                $"[128-2 P3] walk/idle 루프 실측 하한 46(23+23) — 실측 {checkedClips}클립.");
        }

        /// <summary>
        /// [128-2 P3 정정] 계약 8 — talk 원샷 계약: 로드된 talk 클립 전원 isLooping == false
        /// (대화 제스처 원샷 — 루프 진입 금지). 하한 22 = 비-empty talk 22(empty 2종 제외).
        /// </summary>
        [Test]
        public void TalkClips_OneShot_128_2P3()
        {
            int checkedClips = 0;
            foreach (var stem in Npc2026Stems)
            {
                var clips = Resources.LoadAll<AnimationClip>(ClipPath(stem, "talk"));
                if (IsEmpty(clips)) continue; // 감사 진실치 빈 클립 스텝(계약 6 — OW_1/OW_3)
                foreach (var clip in clips)
                {
                    Assert.That(clip.isLooping, Is.False,
                        $"[128-2 P3] talk 원샷 계약(isLooping==false) — {stem}/{clip.name}.");
                    checkedClips++;
                }
            }
            Assert.That(checkedClips, Is.GreaterThanOrEqualTo(22),
                $"[128-2 P3] talk 원샷 실측 하한 22 — 실측 {checkedClips}클립.");
        }

        /// <summary>
        /// [128-2 P3] 계약 9 — 폴백표 소스계약: VillageNpcSpawner 소스에 Npc_Girl_5→Npc_Girl_1 idle 폴백과
        /// Npc_Man_5→Npc_Man_1 walk 폴백 분기 존재("Npc_Girl_5"/"Npc_Man_5" 각 1건 이상 +
        /// "Npc_Girl_1"/"Npc_Man_1" 폴백 리소스 참조). 진실 출처 = TestOutput/npc_audit_128_2_report.json.
        /// </summary>
        [Test]
        public void FallbackTable_SourceContract_128_2P3()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");

            // idle 폴백 분기 — Npc_Girl_5(idle 빈 클립) → Npc_Girl_1 idle
            Assert.That(CountOccurrences(src, "Npc_Girl_5"), Is.GreaterThanOrEqualTo(1),
                "[128-2 P3] 폴백 분기 대상 'Npc_Girl_5' 문자열 1건 이상.");
            Assert.That(src, Does.Contain("idle == null && stem == \"Npc_Girl_5\""),
                "[128-2 P3] idle 폴백 분기 조건 존재 — Npc_Girl_5.");
            Assert.That(src, Does.Contain("Models/UserProvided/Npc2026/Npc_Girl_1/Npc_Girl_1_idle"),
                "[128-2 P3] 폴백 참조 — Npc_Girl_1 idle 리소스 경로.");

            // walk 폴백 분기 — Npc_Man_5(walk 빈 클립) → Npc_Man_1 walk
            Assert.That(CountOccurrences(src, "Npc_Man_5"), Is.GreaterThanOrEqualTo(1),
                "[128-2 P3] 폴백 분기 대상 'Npc_Man_5' 문자열 1건 이상.");
            Assert.That(src, Does.Contain("walk == null && stem == \"Npc_Man_5\""),
                "[128-2 P3] walk 폴백 분기 조건 존재 — Npc_Man_5.");
            Assert.That(src, Does.Contain("Models/UserProvided/Npc2026/Npc_Man_1/Npc_Man_1_walk"),
                "[128-2 P3] 폴백 참조 — Npc_Man_1 walk 리소스 경로.");

            // 폴백표 진실 출처 고정 주석(감사 리포트 연결) 유지 계약.
            Assert.That(src, Does.Contain("감사 실측 폴백표"),
                "[128-2 P3] 폴백표 진실 출처 주석(감사 실측 폴백표) 유지.");
        }

        /// <summary>
        /// [128-2 R] 계약 10 — run 클립 존재: 23종 non-empty, Npc_OldWoman_3만 empty(감사 진실치 —
        /// run 스택 0 → 러너 배선에서 OW_1 run 폴백 사용. OW_1 run은 정상 존재).
        /// </summary>
        [Test]
        public void RunClips_Presence_128_2R()
        {
            foreach (var stem in Npc2026Stems)
            {
                var clips = Resources.LoadAll<AnimationClip>(ClipPath(stem, "run"));
                if (stem == "Npc_OldWoman_3")
                    Assert.That(IsEmpty(clips), Is.True,
                        "[128-2 R] 감사 진실치 — Npc_OldWoman_3 run 빈 클립 계약(OW_1 run 폴백 근거).");
                else
                    Assert.That(IsEmpty(clips), Is.False,
                        $"[128-2 R] run 클립 로드 실측 — {ClipPath(stem, "run")}.");
            }
        }

        /// <summary>
        /// [128-2 R] 계약 11 — 러너 롤 결정론: IsVillagerRunner("girl1","V_1") 2회 동일값 + 빈 인자 false
        /// + 200개 합성 이름("V_{i}") 샘플에서 true 비율이 10~40% 범위(결정론 25% 롤 정합 — hash & 3 == 0).
        /// </summary>
        [Test]
        public void VillagerRunner_RollDeterministic_128_2R()
        {
            // 1) 같은 주민(modelKey + npcName)은 항상 동일 판정 — 저장 불요 결정론 계약.
            bool first = ProjectName.Systems.VillageNpcSpawner.IsVillagerRunner("girl1", "V_1");
            bool second = ProjectName.Systems.VillageNpcSpawner.IsVillagerRunner("girl1", "V_1");
            Assert.That(second, Is.EqualTo(first),
                "[128-2 R] 러너 롤 결정론 — 같은 주민은 항상 동일 판정.");

            // 2) 빈 인자 방어 — modelKey/npcName 공백 시 false.
            Assert.That(ProjectName.Systems.VillageNpcSpawner.IsVillagerRunner("", "V_1"), Is.False,
                "[128-2 R] 빈 modelKey → false 방어.");
            Assert.That(ProjectName.Systems.VillageNpcSpawner.IsVillagerRunner("girl1", ""), Is.False,
                "[128-2 R] 빈 npcName → false 방어.");

            // 3) 25% 롤 정합 — 200개 합성 이름 샘플에서 true 비율이 10~40% 범위(25% 기대 ±편차 허용).
            int trueCount = 0;
            for (int i = 0; i < 200; i++)
                if (ProjectName.Systems.VillageNpcSpawner.IsVillagerRunner("girl1", "V_" + i))
                    trueCount++;
            double ratio = trueCount / 200.0;
            Assert.That(ratio, Is.GreaterThanOrEqualTo(0.10).And.LessThanOrEqualTo(0.40),
                $"[128-2 R] 러너 롤 25% 정합 — 200 샘플 실측 비율 {ratio:P0}(true {trueCount}/200).");
        }

        /// <summary>
        /// [128-2 R] 계약 12 — 러너 배선 소스계약: VillagerWanderer.cs 소스에 "SetRunner"/"RunnerWalkSpeed"
        /// 존재, VillageNpcSpawner.cs 소스에 "SetRunner(runner)"·"ccForRunner.run" 존재
        /// (File.ReadAllText 소스계약 — 기존 계약 9 케이스 패턴 준용).
        /// </summary>
        [Test]
        public void RunWiring_SourceContract_128_2R()
        {
            var wandererSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/VillagerWanderer.cs");
            Assert.That(wandererSrc, Does.Contain("SetRunner"),
                "[128-2 R] VillagerWanderer 러너 배선 소스계약 — SetRunner 메서드 존재.");
            Assert.That(wandererSrc, Does.Contain("RunnerWalkSpeed"),
                "[128-2 R] VillagerWanderer 러너 속도 상수 소스계약 — RunnerWalkSpeed 존재.");

            var spawnerSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");
            Assert.That(spawnerSrc, Does.Contain("SetRunner(runner)"),
                "[128-2 R] VillageNpcSpawner 러너 배선 소스계약 — SetRunner(runner) 호출 존재.");
            Assert.That(spawnerSrc, Does.Contain("ccForRunner.run"),
                "[128-2 R] VillageNpcSpawner run 클립 부재 해제 소스계약 — ccForRunner.run 존재.");
        }
    }
}
