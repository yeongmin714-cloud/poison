using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [97차 F1] 믹사모 클립 루프화 — Villagers_Mixamo/{Walking,Idle}.fbx 배치 재임포트.
    /// 근거: docs/97_CONSTRUCTION_WINDOW_BISECT_AND_ANIM_FREEZE_SELF_HEAL_PLAN.md 결함 1 —
    /// .meta 전수 실측에서 clipAnimations: [] (빈 배열) = 클립별 임포트 설정 부재 = m_LoopTime 기본 0 →
    /// 클립 1사이클(≈1.1s) 재생 후 마지막 프레임 정지 = "전원 애니 프리징"의 단일 뿌리.
    /// 수리 = ModelImporter 경유 배치(수기 .meta 편집 금지 계약): defaultClipAnimations 팝업 →
    /// 각 항목 loopTime = true → clipAnimations 재할당 → SaveAndReimport().
    /// 대상은 Villagers_Mixamo 2종 한정 — Combat_Mixamo/{Block,Slash}은 원샷 유지(루프 불필요 —
    /// Block 창 유지 모션/Slash 반격 1회 — CrossFade 복원 계약과 일치, 건드리지 않음).
    /// 멱등: 이미 loopTime=true인 항목만 있으면 재할당/재임포트 skip(반복 실행 무영향).
    /// 실행: 에디터 메뉴 Tools/Anim/Reimport Villager Mixamo Clips Looped 또는 batchmode
    /// -executeMethod ProjectName.EditorTools.MixamoClipLoopImporter.ReimportVillagerClipsLooped.
    /// </summary>
    public static class MixamoClipLoopImporter
    {
        /// <summary>[97차 F1] 루프화 대상 — 주민 걷기/대기 2종(Resources 런타임 로드 계약 경로).</summary>
        private static readonly string[] LoopedClipPaths =
        {
            "Assets/Resources/Animations/Villagers_Mixamo/Walking.fbx",
            "Assets/Resources/Animations/Villagers_Mixamo/Idle.fbx",
        };

        [MenuItem("Tools/Anim/Reimport Villager Mixamo Clips Looped")]
        public static void ReimportVillagerClipsLooped()
        {
            int looped = 0, skipped = 0, failed = 0;
            foreach (var path in LoopedClipPaths)
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    failed++;
                    Debug.LogError($"[97차 F1] ModelImporter 부재 — {path} (경로/임포트 타입 확인)");
                    continue;
                }

                // defaultClipAnimations — clipAnimations가 비어 있어도 기본 클립 설정을 팝업해 준다
                //   (97차 결함 1의 실측 상태 — 빈 배열에서 출발하는 경로).
                var clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    failed++;
                    Debug.LogError($"[97차 F1] 클립 팝업 실패(빈 배열) — {path} (애니메이션 임포트 상태 확인)");
                    continue;
                }

                bool changed = false;
                foreach (var clip in clips)
                {
                    if (clip.loopTime) continue;   // 멱등 — 이미 루프면 유지
                    clip.loopTime = true;
                    changed = true;
                }

                if (!changed)
                {
                    skipped++;
                    Debug.Log($"[97차 F1] skip(이미 loopTime) — {path} ({clips.Length} clips) — 재임포트 없음");
                    continue;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                looped++;
                Debug.Log($"[97차 F1] loopTime=true 재임포트 — {path} ({clips.Length} clips)");
            }

            Debug.Log($"[97차 F1] 믹사모 클립 루프화 배치 완료 — looped={looped} skipped={skipped} failed={failed} (대상 2종 — Combat 원샷 무영향)");
        }
    }
}
