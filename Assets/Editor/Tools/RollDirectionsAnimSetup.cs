using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [132차 C] Player_AC 구르기 방향 확장 배치 — 129차 PlayerActionsAnimSetup 선례 복제.
    /// 근거: docs/132_ROLL_RUN_REBIND_PLAN.md C섹션 — 파생 구르기 FBX 2종(131차 완료) 기반.
    /// ① 트리거 파라미터 3종 멱등 추가: RollBack/RollSideL/RollSideR.
    /// ② 상태 3종 멱등 추가(존재 시 모션/mirror만 갱신):
    ///    RollBack(Assets/Animations/MeshyUser/Roll_Back.fbx 첫 클립 — 시간반전 후방 재주),
    ///    RollSideL(Roll_Side.fbx 첫 클립, mirror=true — 좌측 반전),
    ///    RollSideR(Roll_Side.fbx 첫 클립, mirror=false).
    /// ③ 전이: Any State → 각 상태(트리거, canTransitionToSelf=false — 기존 Roll 선례와 동일 세팅:
    ///    hasExitTime=false, duration=0.1s) + 각 상태 → Idle(ExitTime 0.92 무조건 — MixamoControllerBuilder
    ///    ExitTo 선례, duration=0.12s — 원샷 재생 후 복귀 계약).
    /// AnyState/ExitTo 헬퍼는 MixamoControllerBuilder(builder private 재사용 불가)의 동일 로직을
    /// 로컬 복제하되 멱등 가드(동일 dst/조건 존재 시 스킵)를 추가.
    /// 클립 로드는 폴더명 검색(129차 FirstClip) 대신 명시 경로 사용 — 파일 부재/클립 0개 시 LogError 실패.
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.RollDirectionsAnimSetup.Run
    /// 완료 마커: "[132차] DONE"
    /// </summary>
    public static class RollDirectionsAnimSetup
    {
        private const string ControllerPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
        private const string RollBackFbx = "Assets/Animations/MeshyUser/Roll_Back.fbx";
        private const string RollSideFbx = "Assets/Animations/MeshyUser/Roll_Side.fbx";

        [MenuItem("Tools/Anim/Setup Player Roll Directions")]
        public static void Run()
        {
            AssetDatabase.Refresh();

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) { Debug.LogError($"[132차] 컨트롤러 부재 — {ControllerPath}"); return; }

            // ── 클립 로드(명시 경로 — LoadAllAssetsAtPath로 첫 실제 클립 추출) ──
            // 파일 부재/클립 0개면 LogError 후 중단(조용히 넘어가지 않음 — 132차 계약).
            var rollBackClip = FirstClip(RollBackFbx, "Roll_Back");
            var rollSideClip = FirstClip(RollSideFbx, "Roll_Side");
            if (rollBackClip == null || rollSideClip == null) return;

            // ── 트리거 파라미터 3종 — 멱등(이미 존재하면 스킵) ──
            string[] triggers = { "RollBack", "RollSideL", "RollSideR" };
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
            if (idle == null) { Debug.LogError("[132차] Idle 상태 부재 — ExitTo 앵커 실패"); return; }

            // ── 상태 3종 — 멱등(이미 존재하면 모션/mirror만 갱신, 추가 스킵) ──
            int addedStates = 0;
            var rollBack = EnsureState(sm, "RollBack", rollBackClip, mirror: false, ref addedStates);
            var rollSideL = EnsureState(sm, "RollSideL", rollSideClip, mirror: true, ref addedStates);
            var rollSideR = EnsureState(sm, "RollSideR", rollSideClip, mirror: false, ref addedStates);

            // ── 전이 — Any State → 신규 상태(트리거, canTransitionToSelf=false — Mixamo Roll 선례)
            //    + 신규 상태 → Idle(ExitTime 0.92 무조건 — 원샷 복귀 계약) ──
            int addedTrans = 0;
            AddAnyStateTransition(sm, rollBack, "RollBack", ref addedTrans);
            AddAnyStateTransition(sm, rollSideL, "RollSideL", ref addedTrans);
            AddAnyStateTransition(sm, rollSideR, "RollSideR", ref addedTrans);
            AddExitTransition(rollBack, idle, ref addedTrans);
            AddExitTransition(rollSideL, idle, ref addedTrans);
            AddExitTransition(rollSideR, idle, ref addedTrans);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // ── 검증 실측: 상태 존재/mirror/모션 + AnyState·Exit 전이 수 ──
            int vParams = triggers.Count(n => ctrl.parameters.Any(p => p.name == n && p.type == AnimatorControllerParameterType.Trigger));
            var back = sm.states.FirstOrDefault(s => s.state != null && s.state.name == "RollBack").state;
            var sideL = sm.states.FirstOrDefault(s => s.state != null && s.state.name == "RollSideL").state;
            var sideR = sm.states.FirstOrDefault(s => s.state != null && s.state.name == "RollSideR").state;
            string sBack = Snapshot(sm, idle, back, "RollBack");
            string sSideL = Snapshot(sm, idle, sideL, "RollSideL");
            string sSideR = Snapshot(sm, idle, sideR, "RollSideR");
            string sIdle = idle ? idle.name : "부재!";
            Debug.Log($"[132차] 컨트롤러 확장 — 파라미터+{addedParams} 상태+{addedStates} 전이+{addedTrans} " +
                      $"검증: 트리거={vParams}/3 {sBack} {sSideL} {sSideR} Idle='{sIdle}'");

            Debug.Log("[132차] DONE");
        }

        /// <summary>상태 1개 실측 스냅샷 — 존재 여부/mirror/모션 + AnyState·ExitTime 전이 수.</summary>
        private static string Snapshot(AnimatorStateMachine sm, AnimatorState idle, AnimatorState st, string trigger)
        {
            if (st == null) return $"{trigger}=부재!";
            int any = sm.anyStateTransitions.Count(t => t.destinationState == st);
            int exit = st.transitions.Count(t => t.destinationState == idle && t.hasExitTime && t.conditions.Length == 0);
            string motion = st.motion != null ? st.motion.name : "부재!";
            return $"{trigger}(mirror={st.mirror}, motion={motion}, any={any}, exit={exit})";
        }

        /// <summary>상태 멱등 확보 — 존재 시 모션/mirror만 갱신(추가 스킵), 부재 시 AddState.</summary>
        private static AnimatorState EnsureState(AnimatorStateMachine sm, string name, AnimationClip motion, bool mirror, ref int added)
        {
            var existing = sm.states.FirstOrDefault(s => s.state != null && s.state.name == name).state;
            if (existing != null)
            {
                existing.motion = motion;
                existing.mirror = mirror;
                return existing;
            }
            var st = sm.AddState(name);
            st.motion = motion;
            st.mirror = mirror;
            if (motion == null) Debug.LogError($"[132차] {name} 모션 부재 — 클립 로드 실패(경로 확인)");
            added++;
            return st;
        }

        /// <summary>명시 경로 FBX에서 첫 실제 클립 추출(129차 FirstClip의 __-프리필터 계승).
        /// 파일 부재 또는 클립 0개면 LogError 후 null 반환(호출부에서 중단).</summary>
        private static AnimationClip FirstClip(string fbxPath, string label)
        {
            if (AssetDatabase.LoadMainAssetAtPath(fbxPath) == null)
            {
                Debug.LogError($"[132차] {label} FBX 파일 부재 — {fbxPath}");
                return null;
            }
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__"))
                    return clip;
            }
            Debug.LogError($"[132차] {label} 클립 0개 — {fbxPath} (Humanoid 임포트/스택 확인 필요)");
            return null;
        }

        /// <summary>Any State → 상태 트리거 전이 — MixamoControllerBuilder.AnyState 로컬 복제
        /// (canTransitionToSelf=false, hasExitTime=false, duration=0.1s — 기존 Roll 선례 동일 세팅) + 멱등 가드.</summary>
        private static void AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState dst, string trigger, ref int added)
        {
            foreach (var existing in sm.anyStateTransitions)
            {
                if (existing.destinationState != dst) continue;
                if (existing.conditions.Any(c => c.parameter == trigger))
                {
                    existing.canTransitionToSelf = false; // 계약 유지(Roll 선례) — 기존 전이 보정
                    return;
                }
            }
            var t = sm.AddAnyStateTransition(dst);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            added++;
        }

        /// <summary>상태 → Idle ExitTime 무조건 전이 — MixamoControllerBuilder.ExitTo 로컬 복제
        /// (hasExitTime=true, exitTime=0.92, duration=0.12 — Roll→Idle 선례 동일 세팅) + 멱등 가드
        /// (동일 dst/ExitTime/무조건 존재 시 스킵 — 129차 AddExitTransition 패턴).</summary>
        private static void AddExitTransition(AnimatorState src, AnimatorState dst, ref int added)
        {
            foreach (var existing in src.transitions)
            {
                if (existing.destinationState == dst && existing.hasExitTime && existing.conditions.Length == 0) return;
            }
            var t = src.AddTransition(dst);
            t.hasExitTime = true;
            t.exitTime = 0.92f;
            t.duration = 0.12f;
            added++;
        }
    }
}
