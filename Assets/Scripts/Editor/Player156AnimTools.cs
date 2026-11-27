using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [156차 P5] 곡갱이질 FBX 수용·연동 배치 도구 — ①FBX 임포트 계약(animationType=Human) + ②클립 식별·로그 +
    /// ③Player_AC "Till" 모션 교체(153차 임시 클립 승계) + ④"Mine" 상태 신설 배선(동굴 채굴 애니).
    /// 근거: docs/plans/2026-11-06_cave-floor-dirt-safety-scale4-pickaxe-fbx-plan.md Phase P5.
    /// 실행(유니티 배치 — 구현 세션에서 실행 금지, 부모 세션이 -executeMethod로 실행):
    ///   batchmode -nographics -quit -executeMethod ProjectName.EditorTools.Player156AnimTools.RunAll
    /// Farm153EditorTools(153차 P2)의 멱등·검증로그 패턴 1:1 준용. 클립 선택 규칙은
    /// Farm153EditorTools.ResolvePickaxeClip 공용 헬퍼 위임(복사 금지 — 호출 재사용).
    /// 완료 마커: "[156차 P5] DONE"
    /// </summary>
    public static class Player156AnimTools
    {
        private const string PickaxeFbxPath = "Assets/플레이어 애니메이션/곡갱이질+망치질.fbx";
        private const string PlayerAcPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";

        /// <summary>[156차 P5] 배치 진입점 — ①임포트 계약 → ②클립 로그 → ③Till 모션 교체 → ④Mine 배선 순차 실행.</summary>
        [MenuItem("Tools/Player/156차 곡갱이질 FBX 수용·연동(RunAll)")]
        public static void RunAll()
        {
            Debug.Log("[156차 P5] RunAll 시작 — 곡갱이질 FBX 임포트 계약 + Player_AC Till 교체/Mine 배선");
            ImportPickaxeFbx();
            LogPickaxeClips();
            ReplaceTillMotion();
            WireMineState();
            Debug.Log("[156차 P5] DONE");
        }

        // ══════════════════════ ① FBX 임포트 계약 ══════════════════════

        /// <summary>[156차 P5] 곡갱이 FBX 임포트 계약 — animationType=Human 강제 후 SaveAndReimport.
        /// Unity 6.4 enum명은 'Human'('Humanoid' 아님 — CS0117 주의). 애니 클립만 사용, 메시/skin은 미사용
        /// (플레이어 외형 기존 유지 — 원본 유지 계약, 삭제 금지). 이미 Human이면 재임포트 스킵(멱등).</summary>
        public static void ImportPickaxeFbx()
        {
            var importer = AssetImporter.GetAtPath(PickaxeFbxPath) as ModelImporter;
            if (importer == null)
            {
                // .meta 미생성(신규 투입 직후) 케이스 — 명시 임포트 1회 후 재취득.
                AssetDatabase.ImportAsset(PickaxeFbxPath);
                importer = AssetImporter.GetAtPath(PickaxeFbxPath) as ModelImporter;
            }
            if (importer == null)
            {
                Debug.LogError($"[156차 P5] 곡갱이 FBX 미발견/임포트 실패 — {PickaxeFbxPath}");
                return;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();
                Debug.Log($"[156차 P5] 임포트 계약 적용 — animationType=Human, 재임포트 완료({PickaxeFbxPath})");
            }
            else
            {
                Debug.Log("[156차 P5] 임포트 계약 기존 충족(animationType=Human) — 스킵(멱등)");
            }
        }

        // ══════════════════════ ② 클립 식별·로그 ══════════════════════

        /// <summary>[156차 P5] FBX 내장 클립 전수 로그 — 이름/길이 기록(mixamo.com 이름 함정 회피 목적).
        /// '__' 프리뷰 클립 제외, 이름 오름차순 정렬(결정론). 곡갱이질/망치질 선별 규칙은
        /// Farm153EditorTools.ResolvePickaxeClip(곡갱이질) / 나머지는 망치질로 기록만(배선 금지).</summary>
        public static void LogPickaxeClips()
        {
            var clips = LoadPickaxeClipsSorted();
            for (int i = 0; i < clips.Count; i++)
                Debug.Log($"[156차 P5] clip[{i}] name={clips[i].name} len={clips[i].length:F2}s");
            if (clips.Count == 0)
                Debug.LogError($"[156차 P5] 클립 미발견 — {PickaxeFbxPath}(임포트 계약 ① 확인 필요)");
        }

        /// <summary>[156차 P5] 곡갱이 FBX 내장 클립 결정론 로드 — '__' 프리뷰 제외 + 이름 오름차순 정렬.</summary>
        private static List<AnimationClip> LoadPickaxeClipsSorted()
        {
            return AssetDatabase.LoadAllAssetsAtPath(PickaxeFbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__", StringComparison.Ordinal))
                .OrderBy(c => c.name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>[156차 P5] 곡갱이질 클립 결정론 조회 — Farm153EditorTools 공용 규칙 위임
        /// (이름에 '곡갱' 포함 우선, 없으면 이름 정렬 첫 항목 — ② 규칙 단일 출처 계약).</summary>
        public static AnimationClip ResolvePickaxeClip() =>
            Farm153EditorTools.ResolvePickaxeClip(PickaxeFbxPath);

        // ══════════════════════ ③ Player_AC Till 모션 교체 ══════════════════════

        /// <summary>[156차 P5] Player_AC "Till" State 모션 교체 — 153차 임시 클립(Male_Bend_Over_Pick_Up)을
        /// 곡갱이질 클립으로 승계 교체(153차 "클립 도착 시 1줄 교체" 계약의 실행 단계). State 신설은
        /// Farm153EditorTools.AddTillStateToPlayerAC 소유 — 부재 시 여기서 만들지 않는다(소유 채널 분리).
        /// 멱등: 모션이 이미 곡갱이질이면 스킵.</summary>
        public static void ReplaceTillMotion()
        {
            var ctrl = LoadPlayerAc();
            if (ctrl == null) return;

            var tillState = FindState(ctrl, "Till");
            if (tillState == null)
            {
                Debug.LogError("[156차 P5] 'Till' State 미발견 — Farm153EditorTools.AddTillStateToPlayerAC 선행 실행 필요");
                return;
            }

            var clip = ResolvePickaxeClip();
            if (clip == null)
            {
                Debug.LogError($"[156차 P5] 곡갱이질 클립 미발견 — Till 모션 교체 생략({PickaxeFbxPath})");
                return;
            }

            if (tillState.motion == clip)   // Unity Object 비교 — 동일 에셋이면 스킵(멱등)
            {
                Debug.Log($"[156차 P5] Till 모션 기존 곡갱이질('{clip.name}') — 스킵(멱등)");
                return;
            }

            string prevName = tillState.motion != null ? tillState.motion.name : "(null)";
            tillState.motion = clip;
            EditorUtility.SetDirty(tillState);   // 서브에셋 더티 — 컨트롤러 SetDirty와 쌍(영속성 보강).
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[156차 P5] Till 모션 교체 — '{prevName}'(Male_Bend_Over_Pick_Up 임시) → '{clip.name}'(곡갱이질, 153차 승계)");
        }

        // ══════════════════════ ④ Player_AC Mine 상태 신설 배선 ══════════════════════

        /// <summary>[156차 P5] Player_AC에 채굴(Mine) 배선 — ①"Mine" Trigger 파라미터 ②"Mine" State
        /// (모션=곡갱이질 클립) ③AnyState→Mine 전이(condition Mine, canTransitionToSelf=false)
        /// ④Mine→Idle 전이(Exit Time 1.0). Farm153EditorTools.AddTillStateToPlayerAC의 Till 배선 동일 구조.
        /// 멱등: 기존 "Mine" 파라미터/State/전이 존재 시 스킵+로그(State 모션 불일치 시 곡갱이질로 정렬).</summary>
        public static void WireMineState()
        {
            var ctrl = LoadPlayerAc();
            if (ctrl == null) return;

            var clip = ResolvePickaxeClip();
            if (clip == null)
            {
                Debug.LogError($"[156차 P5] 곡갱이질 클립 미발견 — Mine 배선 생략({PickaxeFbxPath})");
                return;
            }

            // ① "Mine" Trigger 파라미터.
            if (ctrl.parameters.Any(p => p.name == "Mine"))
                Debug.Log("[156차 P5] 'Mine' 파라미터 기존 존재 — 스킵(멱등)");
            else
            {
                ctrl.AddParameter("Mine", AnimatorControllerParameterType.Trigger);
                Debug.Log("[156차 P5] 'Mine' Trigger 파라미터 추가");
            }

            var root = ctrl.layers.Length > 0 ? ctrl.layers[0].stateMachine : null;
            if (root == null)
            {
                Debug.LogError("[156차 P5] Player_AC 루트 상태머신 미발견 — 중단");
                return;
            }

            var allStates = root.states.Select(s => s.state).Where(s => s != null).ToList();
            var mineState = allStates.FirstOrDefault(s => s.name == "Mine");

            // ② "Mine" State + 곡갱이질 클립.
            if (mineState != null)
            {
                if (mineState.motion != clip)
                {
                    string prev = mineState.motion != null ? mineState.motion.name : "(null)";
                    mineState.motion = clip;
                    EditorUtility.SetDirty(mineState);   // 서브에셋 더티 — 영속성 보강.
                    Debug.Log($"[156차 P5] 'Mine' State 모션 정렬 — '{prev}' → '{clip.name}'");
                }
                else
                {
                    Debug.Log("[156차 P5] 'Mine' State 기존 존재(모션=곡갱이질) — 스킵(멱등)");
                }
            }
            else
            {
                mineState = root.AddState("Mine", new Vector3(780f, 60f, 0f));   // Till(660,60) 우측 인접 배치
                mineState.motion = clip;
                Debug.Log($"[156차 P5] 'Mine' State 추가 — 클립 '{clip.name}'");
            }

            // ③ AnyState → Mine (condition: Mine, canTransitionToSelf=false).
            var anyTrans = root.anyStateTransitions.FirstOrDefault(t => t.destinationState == mineState);
            if (anyTrans == null)
            {
                var added = root.AddAnyStateTransition(mineState);
                added.duration = 0.1f;
                added.canTransitionToSelf = false;   // [156차 P5] AnyState 자기전이 금지 계약.
                added.AddCondition(AnimatorConditionMode.If, 1f, "Mine");
                Debug.Log("[156차 P5] AnyState→Mine 전이 추가(condition Mine, canTransitionToSelf=false)");
            }
            else if (anyTrans.canTransitionToSelf)
            {
                anyTrans.canTransitionToSelf = false;   // [156차 P5] 멱등 보강 — 기존 전이 계약 정렬.
                Debug.Log("[156차 P5] AnyState→Mine 기존 전이 — canTransitionToSelf=false 정렬");
            }
            else
            {
                Debug.Log("[156차 P5] AnyState→Mine 전이 기존 존재 — 스킵(멱등)");
            }

            // ④ Mine → Idle (Exit Time 1.0).
            var idleState = allStates.FirstOrDefault(s => s.name == "Idle");
            if (idleState == null)
            {
                Debug.LogError("[156차 P5] Idle State 미발견 — Mine→Idle 전이 생략");
                return;
            }
            if (mineState.transitions.Any(t => t.destinationState == idleState && t.hasExitTime))
            {
                Debug.Log("[156차 P5] Mine→Idle 전이 기존 존재 — 스킵(멱등)");
            }
            else
            {
                var exitTrans = mineState.AddTransition(idleState);
                exitTrans.hasExitTime = true;
                exitTrans.exitTime = 1f;
                exitTrans.duration = 0.1f;
                Debug.Log("[156차 P5] Mine→Idle 전이 추가(Exit Time 1.0)");
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[156차 P5] WireMineState 완료 — {PlayerAcPath}");
        }

        // ══════════════════════ 소형 헬퍼 ══════════════════════

        /// <summary>[156차 P5] Player_AC 로드 — 부재 시 에러 로그(호출부 반환 처리).</summary>
        private static AnimatorController LoadPlayerAc()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAcPath);
            if (ctrl == null)
                Debug.LogError($"[156차 P5] Player_AC 미발견 — {PlayerAcPath}");
            return ctrl;
        }

        /// <summary>[156차 P5] 루트 레이어 상태 이름 조회 — Farm153/132차 FindState 선례 준용.</summary>
        private static AnimatorState FindState(AnimatorController ctrl, string stateName)
        {
            var root = ctrl.layers.Length > 0 ? ctrl.layers[0].stateMachine : null;
            return root == null
                ? null
                : root.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == stateName);
        }
    }
}
