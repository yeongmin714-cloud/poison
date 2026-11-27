using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호 84] 창 찌르기 애니 교체 — 사용자 제공 "플레이어 창 휘두르기.fbx"(단일 테이크 Take 001,
    /// 바이너리 파싱 실측 11-20)를 Player_AC의 AttackThrust 상태 모션으로 배선.
    /// - 임포트: 휴머노이드 강제(같은 폴더 선례 곡갱이질+망치질=animationType 3 계약) + 클립 명명 안정화(SpearSwing)
    /// - 배선: §11-5 계약 — fileID/YAML 직접 편집 금지, AssetDatabase/AnimatorController API만 사용, 멱등
    /// - 드라이버 프래그먼트 갱신(HumanoidClipDriver.SpearOpenerClipNameFragment)은 별도 소스 수리(배치 후)
    /// </summary>
    public static class SpearThrustClipWiring
    {
        const string FbxPath = "Assets/플레이어 애니메이션/플레이어 창 휘두르기.fbx";
        const string CtrlPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
        const string StateName = "AttackThrust";
        // [무번호 84] 클립명 계약 — Blender 기본 테이크명 "Take 001"(실측 3.77s). 이 Unity 버전은
        //   clipAnimations 자동 생성이 없어 리네임 경로가 무효(11-20 실측) → 테이크명 원직 사용.
        //   드라이버 프래그먼트(SpearOpenerClipNameFragment)와 동일 문자열 계약.
        const string ClipName = "Take 001";

        public static void Wire()
        {
            // 1) 임포트 — 휴머노이드 강제 + 클립 명명 안정화
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[SpearWire] FBX 임포터 없음 — 경로 확인: " + FbxPath);
                EditorApplication.Exit(1);
                return;
            }
            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)   // [Unity 6] 휴머노이드 멤버명 = Human(HeatAvatarMappingFix 선례)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
                Debug.Log("[SpearWire] animationType → Human(Humanoid)");
            }
            if (dirty)
            {
                importer.SaveAndReimport();   // 1단 — 신규 FBX: 이 재임포트에서 테이크 클립이 자동 생성됨
                AssetDatabase.Refresh();
            }

            // [84] 리네임 경로 폐기 — 이 Unity 버전은 clipAnimations 자동 생성이 없어(11-20 실측) 테이크명
            //   "Take 001" 원직 사용으로 계약 확정. 리네임 무효 시도 블록 삭제.

            // 2) 클립 로드 — __preview__ 에디터 프리뷰 클립 제외 후 Take 001 확정 매치
            var allClips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .ToArray();
            foreach (var c in allClips)
                Debug.Log("[SpearWire] 로드 클립: " + c.name + " len=" + c.length.ToString("F2") + "s");
            var clip = allClips.FirstOrDefault(c => c.name == ClipName);
            if (clip == null)
            {
                Debug.LogError("[SpearWire] " + ClipName + " 클립 미발견 — 위 로드 목록으로 진단");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log(string.Format("[SpearWire] 클립 로드: {0} len={1:F2}s legacy={2}", clip.name, clip.length, clip.legacy));

            // 3) 컨트롤러 상태 모션 교체(멱등) — 상태 재귀 탐색
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (ctrl == null)
            {
                Debug.LogError("[SpearWire] 컨트롤러 없음: " + CtrlPath);
                EditorApplication.Exit(1);
                return;
            }
            AnimatorState target = null;
            foreach (var layer in ctrl.layers)
            {
                if (layer.stateMachine == null) continue;
                target = FindStateRecursive(layer.stateMachine, StateName);
                if (target != null) break;
            }
            if (target == null)
            {
                Debug.LogError("[SpearWire] 상태 없음: " + StateName);
                EditorApplication.Exit(1);
                return;
            }

            string oldName = target.motion != null ? target.motion.name : "null";
            if (target.motion == clip)
            {
                Debug.Log("[SpearWire] 이미 배선됨 — 멱등 스킵: " + StateName + " motion=" + oldName);
            }
            else
            {
                target.motion = clip;
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log("[SpearWire] ✅ 배선 완료: " + StateName + " " + oldName + " → " + ClipName +
                          " (" + clip.length.ToString("F2") + "s)");
            }
            EditorApplication.Exit(0);
        }

        static AnimatorState FindStateRecursive(AnimatorStateMachine sm, string stateName)
        {
            if (sm == null) return null;
            foreach (var st in sm.states)
                if (st.state != null && st.state.name == stateName) return st.state;
            foreach (var child in sm.stateMachines)
                if (child.stateMachine != null)
                {
                    var found = FindStateRecursive(child.stateMachine, stateName);
                    if (found != null) return found;
                }
            return null;
        }
    }
}
