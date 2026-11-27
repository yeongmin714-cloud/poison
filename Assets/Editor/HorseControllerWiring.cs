using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호 85-P1] 말 컨트롤러 배선 — 백마/적토마 각 4동작(Idle/Walk/Gallop/Rear), Generic 직결
    /// (애니 FBX 본명 = GLB 리그 정확 일치 파싱 실측 11-20 → 리타깃 불필요).
    /// 황마는 애니 FBX 미비로 제외(계획 v2), 갈색말은 GLB 부재로 제외.
    /// §11-5 계약: AssetDatabase/AnimatorController API만 사용, 멱등. Speed float 전이 쌍 계약(§10-4 복귀 전이 필수).
    /// Rear(앞발 들고 서기)는 전이 없이 시스템 직접 재생(연출용).
    /// </summary>
    public static class HorseControllerWiring
    {
        struct HorseSpec
        {
            public string ctrlPath, idleFbx, walkFbx, gallopFbx, rearFbx;
        }

        static readonly HorseSpec[] Specs = new HorseSpec[]
        {
            new HorseSpec
            {
                ctrlPath = "Assets/Resources/Animation/Controllers/백마_AC.controller",
                idleFbx = "Assets/Animations/말/백마 대기.fbx",
                walkFbx = "Assets/Animations/말/백마 걷기.fbx",
                gallopFbx = "Assets/Animations/말/백마 질주.fbx",
                rearFbx = "Assets/Animations/말/백마 앞발들고 서기.fbx",
            },
            new HorseSpec
            {
                ctrlPath = "Assets/Resources/Animation/Controllers/적토마_AC.controller",
                idleFbx = "Assets/Animations/말/적토마 대기.fbx",
                walkFbx = "Assets/Animations/말/적토마 걷기.fbx",
                gallopFbx = "Assets/Animations/말/적토마 질주.fbx",
                rearFbx = "Assets/Animations/말/적토마 앞발 들고 서기.fbx",
            },
        };

        public static void Wire()
        {
            foreach (var spec in Specs) WireOne(spec);
            AssetDatabase.SaveAssets();
            Debug.Log("[HorseWire] ✅ 전체 배선 완료");
            EditorApplication.Exit(0);
        }

        static void WireOne(HorseSpec spec)
        {
            // 1) FBX Generic 보정 — 신규 걷기 3종은 메타 미생성(기본 임포트 값)이라 보정 필요
            foreach (var fbx in new[] { spec.idleFbx, spec.walkFbx, spec.gallopFbx, spec.rearFbx })
            {
                var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (imp == null)
                {
                    Debug.LogError("[HorseWire] 임포터 없음: " + fbx);
                    EditorApplication.Exit(1);
                    return;
                }
                if (imp.animationType != ModelImporterAnimationType.Generic)
                {
                    imp.animationType = ModelImporterAnimationType.Generic;
                    imp.SaveAndReimport();
                    AssetDatabase.Refresh();
                    Debug.Log("[HorseWire] Generic 보정: " + fbx);
                }
            }

            // 2) 클립 로드 — __preview__ 에디터 프리뷰 제외
            AnimationClip Load(string fbx)
            {
                var c = AssetDatabase.LoadAllAssetsAtPath(fbx)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(x => !x.name.StartsWith("__preview__"));
                if (c == null) Debug.LogError("[HorseWire] 클립 없음: " + fbx);
                return c;
            }
            var idle = Load(spec.idleFbx);
            var walk = Load(spec.walkFbx);
            var gallop = Load(spec.gallopFbx);
            var rear = Load(spec.rearFbx);
            if (idle == null || walk == null || gallop == null || rear == null)
            {
                EditorApplication.Exit(1);
                return;
            }

            // 3) 컨트롤러 생성/로드 + Speed 파라미터 + 상태/모션 배선(멱등)
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.ctrlPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(spec.ctrlPath);
                Debug.Log("[HorseWire] 컨트롤러 신설: " + spec.ctrlPath);
            }
            var root = ctrl.layers[0].stateMachine;
            if (ctrl.parameters.All(p => p.name != "Speed"))
                ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var sIdle = Ensure(ctrl, root, "Idle", idle);
            var sWalk = Ensure(ctrl, root, "Walk", walk);
            var sGallop = Ensure(ctrl, root, "Gallop", gallop);
            var sRear = Ensure(ctrl, root, "Rear", rear);

            if (root.defaultState == null || root.defaultState.name != "Idle") root.defaultState = sIdle;

            // 4) 전이 쌍 계약(§10-4) — Idle↔Walk(0.1), Walk↔Gallop(3.0). Rear는 시스템 직접 재생(전이 없음).
            Link(root, sIdle, sWalk, AnimatorConditionMode.Greater, 0.1f);
            Link(root, sWalk, sIdle, AnimatorConditionMode.Less, 0.1f);
            Link(root, sWalk, sGallop, AnimatorConditionMode.Greater, 3f);
            Link(root, sGallop, sWalk, AnimatorConditionMode.Less, 3f);

            EditorUtility.SetDirty(ctrl);
            Debug.Log("[HorseWire] ✅ " + spec.ctrlPath + " 배선 완료");
        }

        static AnimatorState Ensure(AnimatorController ctrl, AnimatorStateMachine root, string name, Motion motion)
        {
            var st = root.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == name);
            if (st == null)
            {
                st = root.AddState(name);
                Debug.Log("[HorseWire] 상태 추가: " + name);
            }
            if (st.motion != motion)
            {
                st.motion = motion;
                Debug.Log("[HorseWire] 모션 배선: " + name + " → " + motion.name);
            }
            return st;
        }

        static void Link(AnimatorStateMachine root, AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
        {
            if (from.transitions.Any(t => t.destinationState == to)) return;   // 멱등
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.conditions = new[]
            {
                new AnimatorCondition { parameter = "Speed", mode = mode, threshold = threshold },
            };
        }
    }
}
