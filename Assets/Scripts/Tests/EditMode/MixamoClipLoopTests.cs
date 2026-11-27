using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [97차 F1] 믹사모 클립 루프화 실측 테스트 — Villagers_Mixamo/{Walking,Idle}.
    /// 근거: docs/97_CONSTRUCTION_WINDOW_BISECT_AND_ANIM_FREEZE_SELF_HEAL_PLAN.md 결함 1 —
    /// .meta clipAnimations: [] = m_LoopTime 기본 0 = 클립 1사이클(≈1.1s) 후 마지막 프레임 정지 =
    /// "전원 애니 프리징"의 단일 뿌리. 수리 = 에디터 배치 재임포트(MixamoClipLoopImporter —
    /// defaultClipAnimations 팝업 → loopTime=true → SaveAndReimport, 수기 .meta 편집 금지).
    /// 본 테스트는 ①리소스 로드 클립의 isLooping 실측 ②.meta 재임포트 산출물(loopTime: 1 —
    /// ClipAnimationDescription YAML 필드, 프로젝트 실측 형식) 이중 실측.
    /// Combat_Mixamo 2종은 원샷 유지 계약 — loopTime 진입이 없음을 함께 실측(부수 실측).
    /// </summary>
    public class MixamoClipLoopTests
    {
        private static readonly string[] VillagerClipResources =
        {
            "Animations/Villagers_Mixamo/Walking",
            "Animations/Villagers_Mixamo/Idle",
        };

        private static readonly string[] VillagerMetaPaths =
        {
            "Assets/Resources/Animations/Villagers_Mixamo/Walking.fbx.meta",
            "Assets/Resources/Animations/Villagers_Mixamo/Idle.fbx.meta",
        };

        /// <summary>[97차 F1] 리소스 로드 실측 — 두 클립 모두 isLooping==true(루프화 재임포트 산출).</summary>
        [Test]
        public void MixamoClipLoop_VillagerClips_AreLooping_97F1()
        {
            foreach (var resPath in VillagerClipResources)
            {
                var clips = Resources.LoadAll<AnimationClip>(resPath);
                Assert.That(clips, Is.Not.Empty, $"[97차 F1] 클립 리소스 로드 — {resPath}.");
                foreach (var clip in clips)
                    Assert.That(clip.isLooping, Is.True,
                        $"[97차 F1] 클립 루프 실측 — {resPath}/{clip.name} (loopTime 미설정 = 애니 프리징 단일 뿌리 회귀).");
            }
        }

        /// <summary>
        /// [97차 F1] .meta 실측 — Walking/Idle fbx.meta의 clipAnimations에 loopTime: 1 존재
        /// (재임포트 산출물). Combat_Mixamo 2종은 원샷 유지 — loopTime 진입 없음(clipAnimations: [] 유지) 실측.
        /// </summary>
        [Test]
        public void MixamoClipLoop_MetaLoopTime_AndCombatOneShot_97F1()
        {
            foreach (var metaPath in VillagerMetaPaths)
            {
                var meta = System.IO.File.ReadAllText(metaPath);
                Assert.That(meta, Does.Contain("clipAnimations:"),
                    $"[97차 F1] {metaPath} — clipAnimations 블록 존재(빈 배열 폐기 = 재임포트 산출).");
                Assert.That(meta, Does.Contain("loopTime: 1"),
                    $"[97차 F1] {metaPath} — loopTime: 1 실측(수기 편집 아님 — ModelImporter 재임포트 산출물).");
            }

            // Combat 원샷 유지(부수 실측 — 건드리지 않음 계약): 루프 진입 없음.
            var combatBlock = System.IO.File.ReadAllText(
                "Assets/Resources/Animations/Combat_Mixamo/Sword And Shield Block.fbx.meta");
            Assert.That(combatBlock, Does.Contain("clipAnimations: []"),
                "[97차 F1] Combat Block — 원샷 유지(clipAnimations: [] — 루프화 대상 아님).");
        }

        /// <summary>
        /// [98차 F1] F1-1 객체 동일성 강제 검증 — 파일별 LoadAll 산출 walk/idle 클립 참조 비동일 +
        /// 해석 로그 요소(instanceID/isLooping/length) 소스 단정. 근거: Editor.log 2775행 —
        /// walk='mixamo.com' idle='mixamo.com'(테이크명 동일 = 로그만 판별 불가)로 폴더 LoadAll 키워드
        /// 매칭이 동일 객체를 돌려 Walk 상태가 Idle 클립을 재생(서있는 포즈+이동 증상) 유력. 수리 =
        /// VillageNpcSpawner.ResolveMixamoOverrideController에서 null/참조 동일(_mixamoWalk == _mixamoIdle)이면
        /// 폴더 LoadAll 결과 폐기 → Walking/Idle 파일별 LoadAll 재해석으로 확정. _mixamoWalk/_mixamoIdle
        /// 멤버는 private — 94차 계약진화 선례처럼 소스 경로 문자열 단정 + 리소스 실측 이중 검증.
        /// </summary>
        [Test]
        public void MixamoClipWalkIdle_ReferenceDissimilar_AndResolutionLog_98F1F1()
        {
            // ① 리소스 실측 — 파일별 LoadAll 산출물: walk/idle 서로 다른 객체 + isLooping/length(해석 로그 단정 근거치).
            var walkClips = Resources.LoadAll<AnimationClip>("Animations/Villagers_Mixamo/Walking");
            var idleClips = Resources.LoadAll<AnimationClip>("Animations/Villagers_Mixamo/Idle");
            Assert.That(walkClips, Is.Not.Empty, "[98차 F1] F1-1 Walking 파일별 LoadAll 실측.");
            Assert.That(idleClips, Is.Not.Empty, "[98차 F1] F1-1 Idle 파일별 LoadAll 실측.");
            Assert.That(walkClips[0], Is.Not.SameAs(idleClips[0]),
                "[98차 F1] F1-1 walk/idle 참조 비동일 실측(테이크명 'mixamo.com' 동일과 무관 — 객체 동일성).");
            foreach (var clip in walkClips)
                Assert.That(clip.isLooping, Is.True,
                    $"[98차 F1] F1-1 walk isLooping 실측 — {clip.name}(루프 미설정 회귀 = 애니 프리징 재발).");
            foreach (var clip in idleClips)
                Assert.That(clip.isLooping, Is.True,
                    $"[98차 F1] F1-1 idle isLooping 실측 — {clip.name}(루프 미설정 회귀 = 애니 프리징 재발).");
            Assert.That(walkClips[0].length, Is.GreaterThan(0f),
                "[98차 F1] F1-1 walk length 양성 실측(해석 로그 walkLen 근거치 — 무효 클립 폐기 판별용).");
            Assert.That(idleClips[0].length, Is.GreaterThan(0f),
                "[98차 F1] F1-1 idle length 양성 실측(해석 로그 idleLen 근거치 — 무효 클립 폐기 판별용).");

            // ② 소스 경로 문자열 단정 — 멤버 private(94차 계약진화 선례 준용): 동일객체 가드 + 파일별 재해석 경로 +
            //   해석 로그(instanceID/isLooping/length 포함)가 VillageNpcSpawner 소스에 존재.
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/VillageNpcSpawner.cs");
            Assert.That(src, Does.Contain("[98차 F1]"), "[98차 F1] F1-1 마커 주석.");
            Assert.That(src, Does.Contain("if (_mixamoWalk == null || _mixamoIdle == null || _mixamoWalk == _mixamoIdle)"),
                "[98차 F1] F1-1 동일 객체(null/모호 포함) 가드 존재.");
            Assert.That(src, Does.Contain("Resources.LoadAll<AnimationClip>(\"Animations/Villagers_Mixamo/Walking\")"),
                "[98차 F1] F1-1 Walking 파일별 LoadAll 재해석 경로.");
            Assert.That(src, Does.Contain("Resources.LoadAll<AnimationClip>(\"Animations/Villagers_Mixamo/Idle\")"),
                "[98차 F1] F1-1 Idle 파일별 LoadAll 재해석 경로.");
            Assert.That(src, Does.Contain("if (_mixamoWalk != null && _mixamoWalk == _mixamoIdle)"),
                "[98차 F1] F1-1 재해석 후에도 참조 동일 → 오버라이드 폐기 가드(Player_AC 폴백 승계).");
            // 해석 로그 단정 — instanceID + isLooping + length 포함(스펙 §2 F1-1: walk/idle 서로 다른 객체 + isLooping=true 실측).
            Assert.That(src, Does.Contain("walkID={_mixamoWalk.GetInstanceID()} idleID={_mixamoIdle.GetInstanceID()}"),
                "[98차 F1] F1-1 해석 로그 instanceID 필드 포함.");
            Assert.That(src, Does.Contain("walkLoop={_mixamoWalk.isLooping} idleLoop={_mixamoIdle.isLooping}"),
                "[98차 F1] F1-1 해석 로그 isLooping 필드 포함.");
            Assert.That(src, Does.Contain("walkLen={_mixamoWalk.length:F2}s idleLen={_mixamoIdle.length:F2}s"),
                "[98차 F1] F1-1 해석 로그 length 필드 포함.");
        }
    }
}
