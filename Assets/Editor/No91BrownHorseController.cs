using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호 91] 갈색말 컨트롤러 에디터 배치 — 백마_AC 구조 클론(상태 4·전이 임계·Speed 파라미터 승계)
    /// + 모션만 갈색말 FBX 4종(대기/걷기/질주/앞발 들고 서기)으로 교체. 결정론 게이트 —
    /// 클립 부재/상태 누락/전이 임계 불일치 시 배치 실패(예외 → 배치 exit 1).
    /// 수용 배경: 갈색말은 애니 FBX 4종 존재·GLB 부재가 no85 블로크였고, [무번호 90] 백마
    /// baseColorFactor 틴트 파생 GLB로 해소됨. 본 스크립트는 남은 컨트롤러 1종을 채운다.
    /// </summary>
    public static class No91BrownHorseController
    {
        const string SrcCtrlPath = "Assets/Resources/Animation/Controllers/백마_AC.controller";
        const string DstCtrlPath = "Assets/Resources/Animation/Controllers/갈색말_AC.controller";
        const string AnimDir = "Assets/Animations/말/";

        // 상태 → 모션 FBX 매핑(결정론).
        static readonly (string state, string fbx)[] StateClips =
        {
            ("Idle", "갈색말 대기"),
            ("Walk", "갈색말 걷기"),
            ("Gallop", "갈색말 질주"),
            ("Rear", "갈색말 앞발 들고 서기"),
        };

        [MenuItem("Tools/No91/Build Brown Horse Controller")]
        public static void BuildFromMenu() => Build();

        public static void Build()
        {
            var src = AssetDatabase.LoadAssetAtPath<AnimatorController>(SrcCtrlPath);
            if (src == null) throw new System.InvalidOperationException($"원본 컨트롤러 부재 — {SrcCtrlPath}");

            // 1) 갈색말 클립 로드 게이트 — 4종 전부 필수.
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var (state, fbx) in StateClips)
            {
                var all = AssetDatabase.LoadAllAssetsAtPath(AnimDir + fbx + ".fbx")
                    .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
                var clip = all.OrderByDescending(c => c.length).FirstOrDefault();
                if (clip == null)
                    throw new System.InvalidOperationException($"갈색말 클립 부재 — {fbx}.fbx (상태 {state})");
                clips[state] = clip;
                Debug.Log($"[No91] 클립 매핑 {state} ← {clip.name} ({clip.length:0.00}s)");
            }

            // 2) 컨트롤러 생성 + 파라미터(Speed) 승계.
            var dst = AnimatorController.CreateAnimatorControllerAtPath(DstCtrlPath);
            dst.parameters = src.parameters;

            var srcRoot = src.layers[0].stateMachine;
            var root = dst.layers[0].stateMachine;

            // 3) 상태 클론 — 이름/속도/모션(교체).
            var map = new Dictionary<string, AnimatorState>();
            foreach (var child in srcRoot.states)
            {
                var s = child.state;
                if (s == null) continue;
                var ns = root.AddState(s.name);
                ns.speed = s.speed;
                ns.timeParameterActive = s.timeParameterActive;
                ns.writeDefaultValues = s.writeDefaultValues;
                if (clips.TryGetValue(s.name, out var clip)) ns.motion = clip;
                map[s.name] = ns;
            }

            // 4) 전이 클론 — 조건/임계/지속시간 그대로(수치 불변 계약).
            foreach (var child in srcRoot.states)
            {
                var s = child.state;
                if (s == null || !map.TryGetValue(s.name, out var from)) continue;
                foreach (var t in s.transitions)
                {
                    if (t.destinationState == null) continue;
                    if (!map.TryGetValue(t.destinationState.name, out var to)) continue;
                    var nt = from.AddTransition(to);
                    nt.duration = t.duration;
                    nt.offset = t.offset;
                    nt.interruptionSource = t.interruptionSource;
                    nt.orderedInterruption = t.orderedInterruption;
                    nt.canTransitionToSelf = t.canTransitionToSelf;
                    nt.hasExitTime = t.hasExitTime;
                    nt.exitTime = t.exitTime;
                    nt.hasFixedDuration = t.hasFixedDuration;
                    nt.conditions = t.conditions;
                }
            }

            // 5) 기본 상태 = Idle (백마 계약 승계).
            if (map.TryGetValue("Idle", out var idle)) root.defaultState = idle;

            EditorUtility.SetDirty(dst);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6) 저장 후 재검증 게이트.
            var saved = AssetDatabase.LoadAssetAtPath<AnimatorController>(DstCtrlPath);
            if (saved == null) throw new System.InvalidOperationException("저장된 컨트롤러 재로드 실패.");
            var sroot = saved.layers[0].stateMachine;
            foreach (var (state, _) in StateClips)
            {
                var st = sroot.states.Select(x => x.state).FirstOrDefault(x => x != null && x.name == state);
                if (st == null || st.motion == null)
                    throw new System.InvalidOperationException($"저장 검증 실패 — 상태/모션 누락: {state}");
            }
            if (sroot.defaultState == null || sroot.defaultState.name != "Idle")
                throw new System.InvalidOperationException("저장 검증 실패 — 기본 상태가 Idle이 아님.");
            bool idleWalk = HasEdge(sroot, "Idle", "Walk") && HasEdge(sroot, "Walk", "Idle");
            bool walkGallop = HasEdge(sroot, "Walk", "Gallop") && HasEdge(sroot, "Gallop", "Walk");
            if (!idleWalk || !walkGallop)
                throw new System.InvalidOperationException("저장 검증 실패 — 전이 쌍 누락(Idle↔Walk / Walk↔Gallop).");

            Debug.Log("[No91] 갈색말_AC 배치 완료 — 상태 4·모션 4·전이 쌍 2·기본 Idle. PASS");
        }

        static bool HasEdge(AnimatorStateMachine root, string from, string to)
        {
            var s = root.states.Select(x => x.state).FirstOrDefault(x => x != null && x.name == from);
            return s != null && s.transitions.Any(t => t.destinationState != null && t.destinationState.name == to);
        }
    }
}
