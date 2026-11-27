using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [129차 P0+P1] 플레이어 액션 애니 3종 임포트 설정 + Player_AC 컨트롤러 확장 배치.
    /// 근거: docs/129_PLAYER_ACTION_CHEER_ANIM_PLAN.md — 채집(6.93s)/크래프트(5.0s)/환호(4.7s) 3종
    /// (자체 파서 실측: 본 23 믹사모 휴머노이드, 스택 1, 실질 커브 61/63).
    /// ① Humanoid 임포트(animationType Human + avatarSetup CreateFromThisModel — 병사 .meta 패턴 복제),
    ///    loopTime은 기본 0(원샷 — 채집/크래프트/환호 모두 1회 재생 후 ExitTime 복귀 계약).
    /// ② Player_AC에 상태 3종(Gather/Craft/Cheer)+트리거 파라미터 4종(Gather/Craft/Cheer/ActionEnd)+전이 9건:
    ///    Idle→각 상태(트리거), Gather→Cheer(트리거 즉시 — 채널형 성공), Craft→Cheer(트리거+ExitTime 0.92 —
    ///    즉시형 성공은 크래프트 애니 마무리 후 환호), Gather/Craft→Idle(ActionEnd — 취소),
    ///    3상태→Idle(ExitTime 0.92 무조건 — TalkA 선례).
    /// 멱등: 상태/파라미터/전이 이미 존재하면 스킵.
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.PlayerActionsAnimSetup.ConfigureAll
    /// 완료 마커: "[129차] DONE"
    /// </summary>
    public static class PlayerActionsAnimSetup
    {
        private const string Root = "Assets/Resources/Animations/PlayerActions2026";
        private const string ControllerPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";

        [MenuItem("Tools/Anim/Configure PlayerActions2026 + Extend Player_AC")]
        public static void ConfigureAll()
        {
            AssetDatabase.Refresh();
            ConfigureImports();
            ExtendController();
            Debug.Log("[129차] DONE");
        }

        private static void ConfigureImports()
        {
            int configured = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx")) continue;
                if (AssetImporter.GetAtPath(path) is not ModelImporter mi)
                {
                    Debug.LogError($"[129차] ModelImporter 부재 — {path}");
                    continue;
                }
                bool changed = false;
                if (mi.animationType != ModelImporterAnimationType.Human)
                { mi.animationType = ModelImporterAnimationType.Human; changed = true; }
                if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; }
                if (changed) { mi.SaveAndReimport(); configured++; }
            }
            // 검증: Humanoid 3/3 실측
            int vHuman = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx")) continue;
                if (AssetImporter.GetAtPath(path) is ModelImporter mi && mi.animationType == ModelImporterAnimationType.Human)
                    vHuman++;
            }
            Debug.Log($"[129차] 임포트 설정 — 재설정={configured} 검증 Humanoid={vHuman}/3");
        }

        private static void ExtendController()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) { Debug.LogError($"[129차] 컨트롤러 부재 — {ControllerPath}"); return; }

            // ── 파라미터 4종(트리거) — 멱등 ──
            string[] triggers = { "Gather", "Craft", "Cheer", "ActionEnd" };
            int addedParams = 0;
            foreach (var name in triggers)
            {
                if (!ctrl.parameters.Any(p => p.name == name))
                {
                    ctrl.AddParameter(name, AnimatorControllerParameterType.Trigger);
                    addedParams++;
                }
            }

            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.states.FirstOrDefault(s => s.state != null && s.state.name == "Idle").state;
            if (idle == null) { Debug.LogError("[129차] Idle 상태 부재 — 전이 앵커 실패"); return; }

            // ── 상태 3종 + 모션(파일별 첫 클립 — 128-2 파일별 로드 계약) ──
            int addedStates = 0;
            var gather = EnsureState(sm, "Gather", FirstClip("플레이어 채집"), ref addedStates);
            var craft = EnsureState(sm, "Craft", FirstClip("플레이어 크래프트"), ref addedStates);
            var cheer = EnsureState(sm, "Cheer", FirstClip("플레이어 환호"), ref addedStates);

            // ── 전이 9건 — 멱등(동일 src/dst/조건 존재 시 스킵) ──
            int addedTrans = 0;
            if (idle != null)
            {
                AddTrigTransition(idle, gather, "Gather", false, ref addedTrans);
                AddTrigTransition(idle, craft, "Craft", false, ref addedTrans);
                AddTrigTransition(idle, cheer, "Cheer", false, ref addedTrans);
            }
            // Gather→Cheer 즉시(채널형 성공) — ExitTime 없음
            AddTrigTransition(gather, cheer, "Cheer", false, ref addedTrans);
            // Craft→Cheer — ExitTime 0.92+트리거(즉시형 성공은 애니 마무리 후 환호)
            AddTrigTransition(craft, cheer, "Cheer", true, ref addedTrans);
            // ActionEnd — 취소 즉시 복귀
            AddTrigTransition(gather, idle, "ActionEnd", false, ref addedTrans);
            AddTrigTransition(craft, idle, "ActionEnd", false, ref addedTrans);
            // ExitTime 무조건 복귀 — TalkA 선례(0.92)
            AddExitTransition(gather, idle, ref addedTrans);
            AddExitTransition(craft, idle, ref addedTrans);
            AddExitTransition(cheer, idle, ref addedTrans);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // ── 검증 실측 ──
            int vStates = sm.states.Count(s => s.state != null &&
                (s.state.name == "Gather" || s.state.name == "Craft" || s.state.name == "Cheer"));
            Debug.Log($"[129차] 컨트롤러 확장 — 파라미터+{addedParams} 상태+{addedStates} 전이+{addedTrans} " +
                      $"검증: 신규상태={vStates}/3 Idle='{idle.name}'");
        }

        private static AnimatorState EnsureState(AnimatorStateMachine sm, string name, AnimationClip motion, ref int added)
        {
            var existing = sm.states.FirstOrDefault(s => s.state != null && s.state.name == name).state;
            if (existing != null) return existing;
            var st = sm.AddState(name);
            if (motion != null) st.motion = motion;
            else Debug.LogError($"[129차] {name} 모션 부재 — 클립 로드 실패(폴더/파일명 확인)");
            added++;
            return st;
        }

        private static AnimationClip FirstClip(string fileNamePart)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileName(path).Contains(fileNamePart)) continue;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                        return clip;
                }
            }
            return null;
        }

        private static bool HasTransition(AnimatorState src, AnimatorState dst, string trigger, bool needExitTime)
        {
            foreach (var t in src.transitions)
            {
                if (t.destinationState != dst) continue;
                if (t.hasExitTime != needExitTime) continue;
                foreach (var c in t.conditions)
                    if (c.parameter == trigger) return true;
            }
            return false;
        }

        private static void AddTrigTransition(AnimatorState src, AnimatorState dst, string trigger, bool useExitTime, ref int added)
        {
            if (src == null || dst == null || HasTransition(src, dst, trigger, useExitTime)) return;
            var t = src.AddTransition(dst);
            t.hasExitTime = useExitTime;
            t.exitTime = 0.92f;
            t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            added++;
        }

        private static void AddExitTransition(AnimatorState src, AnimatorState dst, ref int added)
        {
            foreach (var existing in src.transitions)
            {
                if (existing.destinationState == dst && existing.hasExitTime && existing.conditions.Length == 0) return;
            }
            var t = src.AddTransition(dst);
            t.hasExitTime = true;
            t.exitTime = 0.92f;
            t.duration = 0.15f;
            added++;
        }
    }
}
