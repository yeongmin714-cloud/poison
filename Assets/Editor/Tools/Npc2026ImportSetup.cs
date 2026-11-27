using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [128-2 Phase 0.4] Npc2026 FBX 119 임포트 설정 일괄 배치 (수기 .meta 편집 금지 계약 [113차] — 전부 ModelImporter 경유).
    /// 근거: docs/128_2_VILLAGE_NPC_REPLACEMENT_PLAN.md §3-Phase 0.4 + 전수 파서 리포트
    /// (TestOutput/npc_audit_128_2_report.json — 24캐릭터/119파일, Girl_2=핑거 추가 리그 53본 휴머노이드 호환,
    ///  빈 클립 4건은 런타임 폴백 계약으로 처리 — 본 스크립트와 무관).
    /// 설정:
    ///  ① animationType = Humanoid(3) + avatarSetup = CreateFromThisModel(1)
    ///     — 기존 병사 FBX .meta 실측 패턴 복제(soldier_lv1-20_rigged.fbx.meta L427~429).
    ///  ② loopTime — idle/walk = true([97차 F1] 계약 승계: 루프 미설정 = 1사이클 후 프리징 단일 뿌리),
    ///     talk = 원샷(대화 제스처 1회 — Combat Block/Slash 원샷 선례), run = 원샷(Q2 미배선 — 향후 배선 시 재설정).
    /// 멱등: 이미 목표 설정이면 SaveAndReimport 스킵(반복 실행 무영향).
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.Npc2026ImportSetup.ConfigureAll
    /// 완료 마커: "[128-2 P0.4] DONE humanoid=N loop=N oneshot=N failed=N verifyOK=.."
    /// </summary>
    public static class Npc2026ImportSetup
    {
        private const string Root = "Assets/Resources/Models/UserProvided/Npc2026";

        [MenuItem("Tools/Anim/Configure Npc2026 Import")]
        public static void ConfigureAll()
        {
            AssetDatabase.Refresh();

            var guids = AssetDatabase.FindAssets("t:Model", new[] { Root });
            int configured = 0, looped = 0, oneshot = 0, failed = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx")) continue;

                if (AssetImporter.GetAtPath(path) is not ModelImporter mi)
                {
                    failed++;
                    Debug.LogError($"[128-2 P0.4] ModelImporter 부재 — {path}");
                    continue;
                }

                bool changed = false;
                if (mi.animationType != ModelImporterAnimationType.Human)
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    changed = true;
                }
                if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    changed = true;
                }

                // [97차 F1] 승계 — defaultClipAnimations는 clipAnimations가 비어 있어도 기본 클립을 팝업한다.
                bool loop = path.EndsWith("_idle.fbx") || path.EndsWith("_walk.fbx");
                var clips = mi.defaultClipAnimations;
                if (clips != null && clips.Length > 0 && clips.Any(c => c.loopTime != loop))
                {
                    foreach (var c in clips) c.loopTime = loop;
                    mi.clipAnimations = clips;
                    changed = true;
                }

                if (changed)
                {
                    mi.SaveAndReimport();
                    configured++;
                    if (loop) looped++; else oneshot++;
                }

                if ((configured + failed) % 20 == 0)
                    Debug.Log($"[128-2 P0.4] 진행 {configured + failed}/{guids.Length} — {path}");
            }

            // ── 검증: 재조회 실측 (Humanoid + loopTime 기대 일치 전수) ──
            int vHumanoid = 0, vLoopOk = 0, vBad = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx")) continue;
                if (AssetImporter.GetAtPath(path) is not ModelImporter mi) { vBad++; continue; }
                if (mi.animationType != ModelImporterAnimationType.Human) { vBad++; Debug.LogError($"[128-2 P0.4] 검증 실패(Humanoid 아님) — {path}"); continue; }
                vHumanoid++;
                bool loop = path.EndsWith("_idle.fbx") || path.EndsWith("_walk.fbx");
                var clips = mi.clipAnimations;
                bool ok = clips != null && clips.Length > 0 && clips.All(c => c.loopTime == loop);
                if (ok) vLoopOk++;
                else { vBad++; Debug.LogError($"[128-2 P0.4] 검증 실패(loopTime={loop} 기대 불일치) — {path}"); }
            }

            Debug.Log($"[128-2 P0.4] DONE humanoid재설정={configured} loop대상={looped} oneshot대상={oneshot} failed={failed} " +
                      $"검증: humanoid={vHumanoid} loopTime정합={vLoopOk} 불일치={vBad}");
        }
    }
}
