using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [153차 P2] 농경 채널 피드백 배치 도구 — VFX 프리펩 2종 + Player_AC 굉이질(Till) 상태 배선.
    /// 근거: docs/153_FARMING_QUALITY_ACQ_VFX_PLAN.md §2+§3 P2(+§4 확정결정 1·3번).
    /// 실행(유니티 배치, QA 단계 일괄 — 구현 세션에서 실행 금지):
    ///   batchmode -nographics -quit -executeMethod ProjectName.EditorTools.Farm153EditorTools.RunAll
    /// 129차 PlayerActionsAnimSetup / 133차 GatherVFXPrefabsBuilder의 멱등·검증로그 패턴 준용.
    /// 완료 마커: "[153차 P2] DONE"
    /// </summary>
    public static class Farm153EditorTools
    {
        private const string GatherVfxDir = "Assets/Resources/VFX/Gather";
        private const string PlayerAcPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
        // [156차 P5] 153차 "클립 도착 시 1줄 교체" 승계 — 임시 클립(Male_Bend_Over_Pick_Up) → 사용자 곡갱이질
        //   FBX(클립 2종: 곡갱이질+망치질). 선택 규칙은 ResolvePickaxeClip('곡갱' 포함 우선)으로 승계.
        private const string TillFbxPath = "Assets/플레이어 애니메이션/곡갱이질+망치질.fbx";

        /// <summary>[153차 P2] 배치 진입점 — 프리펩 2종 생성/갱신 후 Player_AC Till 배선 순차 실행.</summary>
        [MenuItem("Tools/Farm/153차 농경 피드백 배치(RunAll)")]
        public static void RunAll()
        {
            Debug.Log("[153차 P2] RunAll 시작 — 농경 VFX 프리펩 2종 + Player_AC Till 상태 배선");
            BuildFarmVfxPrefabs();
            AddTillStateToPlayerAC();
            Debug.Log("[153차 P2] DONE");
        }

        // ══════════════════════ ① 농경 VFX 프리펩 ══════════════════════

        /// <summary>[153차 P2] 농경 파티클 프리펩 2종 생성/갱신(멱등 — 동일 경로 재저장).
        ///  · GatherVFX_SoilBurst: 흙 산포(파티클 20개, 수명 0.6~1.0s, 반구 하단 분사→중력, 갈색, size 0.03~0.06m)
        ///  · GatherVFX_WaterDroplets: 물방울(파티클 25개, 수명 0.8s, 하강, 하늘색 #58A6FF, size 0.02~0.04m)
        /// 저장 시 playOnAwake=false 필수 — 런타임 GatherFeedbackSystem.SpawnVfx가 명시 Play() 호출(짝 계약).</summary>
        public static void BuildFarmVfxPrefabs()
        {
            // 부재 폴더 확보.
            if (!Directory.Exists(GatherVfxDir))
            {
                Directory.CreateDirectory(GatherVfxDir);
                AssetDatabase.Refresh();
                Debug.Log($"[153차 P2] 디렉터리 신규 생성 — {GatherVfxDir}");
            }

            var matSoil = EnsureParticleMaterial("GatherVFX_Mat_SoilBurst");
            var matWater = EnsureParticleMaterial("GatherVFX_Mat_WaterDroplets");

            SaveParticlePrefab("GatherVFX_SoilBurst", matSoil, ConfigureSoilBurst);
            SaveParticlePrefab("GatherVFX_WaterDroplets", matWater, ConfigureWaterDroplets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[153차 P2] BuildFarmVfxPrefabs 완료");
        }

        /// <summary>[153차 P2] 프리펩 저장 공용 — 코드 구성 → SaveAsPrefabAsset(존재 시 갱신) → 씬 오브젝트 즉시 파기.</summary>
        private static void SaveParticlePrefab(string name, Material mat, Action<ParticleSystem> configure)
        {
            string path = $"{GatherVfxDir}/{name}.prefab";
            bool updated = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.05f;
            main.loop = false;
            main.playOnAwake = false;   // [153차 P2] 짝 계약 — 런타임 SpawnVfx가 ps.Play() 명시 호출.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            configure(ps);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            if (mat != null) psr.sharedMaterial = mat;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);   // 씬 오브젝트 파기 — 에셋만 잔존. [153차 QA 수리] CS0104 모호성 해소(Object 명시 한정).
            if (prefab == null)
                Debug.LogError($"[153차 P2] 프리펩 저장 실패 — {path}");
            else
                Debug.Log($"[153차 P2] 프리펩 {(updated ? "갱신" : "생성")} — {path}");
        }

        /// <summary>[153차 P2] 파티클 머티리얼 멱등 확보 — URP 파티클 셰이더 우선, 부재 시 Lit 폴백(SpecialEffects 선례).</summary>
        private static Material EnsureParticleMaterial(string name)
        {
            string path = $"{GatherVfxDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;   // 기존 존재 — 재사용(멱등).

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError($"[153차 P2] 파티클 셰이더 부재 — {name} 머티리얼 생성 불가(프리펩은 기본 머티리얼로 저장됨)");
                return null;
            }

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[153차 P2] 머티리얼 생성 — {path}");
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>[153차 P2] 흙 산포 구성 — 반구 하단 분사 → 중력 낙하, 갈색 톤 랜덤.</summary>
        private static void ConfigureSoilBurst(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);    // 수명 0.6~1.0s
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);      // size 0.03~0.06m
            main.gravityModifier = new ParticleSystem.MinMaxCurve(6f);          // 분사 후 중력 낙하
            main.startColor = new ParticleSystem.MinMaxGradient(MakeGradient(
                C(0x5C4033), C(0x7A5230), C(0x8B6B4A)))
                { mode = ParticleSystemGradientMode.RandomColor };              // 갈색 톤 랜덤

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(0f);               // 버스트 전용
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)20) });    // 파티클 20개(스펙 15~25 중앙값)

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(90f, 0f, 0f);   // 반구 방출축 기본 +Z → 하단(하방) 분사 계약.
        }

        /// <summary>[153차 P2] 물방울 구성 — 하단 분사 → 중력 하강, 하늘색 단색.</summary>
        private static void ConfigureWaterDroplets(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f);          // 수명 0.8s
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);      // size 0.02~0.04m
            main.gravityModifier = new ParticleSystem.MinMaxCurve(4f);          // 하강
            main.startColor = new ParticleSystem.MinMaxGradient(C(0x58A6FF));   // 하늘색 #58A6FF 계약

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = new ParticleSystem.MinMaxCurve(0f);               // 버스트 전용
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)25) });    // 파티클 25개(스펙 20~30 중앙값)

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(90f, 0f, 0f);   // 하단 분사 → 중력 하강.
        }

        // ══════════════════════ ② Player_AC Till 상태 배선 ══════════════════════

        /// <summary>[153차 P2] Player_AC.controller에 굉이질 배선 — ①"Till" Trigger 파라미터 ②"Till" State
        /// (클립=TillFbxPath 내부 AnimationClip — [156차 P5] 곡갱이질 FBX 승계, '곡갱' 포함 우선 규칙, 결정론)
        /// ③AnyState→Till 전이(condition Till, 초기상태 유지) ④Till→Idle 전이(Exit Time 1.0).
        /// 멱등: 기존 "Till" 파라미터/State/전이 존재 시 스킵+로그. Soldier 컨트롤러 계열은 대상 밖(미접촉).</summary>
        public static void AddTillStateToPlayerAC()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAcPath);
            if (ctrl == null)
            {
                Debug.LogError($"[153차 P2] Player_AC 미발견 — {PlayerAcPath}");
                return;
            }

            // ① "Till" Trigger 파라미터.
            if (ctrl.parameters.Any(p => p.name == "Till"))
                Debug.Log("[153차 P2] 'Till' 파라미터 기존 존재 — 스킵(멱등)");
            else
            {
                ctrl.AddParameter("Till", AnimatorControllerParameterType.Trigger);
                Debug.Log("[153차 P2] 'Till' Trigger 파라미터 추가");
            }

            var root = ctrl.layers.Length > 0 ? ctrl.layers[0].stateMachine : null;
            if (root == null)
            {
                Debug.LogError("[153차 P2] Player_AC 루트 상태머신 미발견 — 중단");
                return;
            }

            var allStates = root.states.Select(s => s.state).Where(s => s != null).ToList();
            var tillState = allStates.FirstOrDefault(s => s.name == "Till");

            // ② "Till" State + 굉이질 클립.
            if (tillState != null)
            {
                Debug.Log("[153차 P2] 'Till' State 기존 존재 — 스킵(멱등)");
            }
            else
            {
                AnimationClip clip = ResolveTillClip();
                if (clip == null)
                {
                    Debug.LogError($"[153차 P2] 굉이질 클립 미발견 — {TillFbxPath}([156차 P5] 곡갱이질 FBX 승계 — 임포트 확인 필요)");
                    return;
                }
                tillState = root.AddState("Till", new Vector3(660f, 60f, 0f));
                tillState.motion = clip;
                Debug.Log($"[153차 P2] 'Till' State 추가 — 클립 '{clip.name}'");
            }

            // ③ AnyState → Till (condition: Till).
            var anyTrans = root.anyStateTransitions.FirstOrDefault(t => t.destinationState == tillState);
            if (anyTrans == null)
            {
                var added = root.AddAnyStateTransition(tillState);
                added.duration = 0.1f;
                added.AddCondition(AnimatorConditionMode.If, 1f, "Till");
                Debug.Log("[153차 P2] AnyState→Till 전이 추가(condition Till, 초기상태 유지)");
            }
            else
            {
                Debug.Log("[153차 P2] AnyState→Till 전이 기존 존재 — 스킵(멱등)");
            }

            // ④ Till → Idle (Exit Time 1.0).
            var idleState = allStates.FirstOrDefault(s => s.name == "Idle");
            if (idleState == null)
            {
                Debug.LogError("[153차 P2] Idle State 미발견 — Till→Idle 전이 생략");
                return;
            }
            if (tillState.transitions.Any(t => t.destinationState == idleState && t.hasExitTime))
            {
                Debug.Log("[153차 P2] Till→Idle 전이 기존 존재 — 스킵(멱등)");
            }
            else
            {
                var exitTrans = tillState.AddTransition(idleState);
                exitTrans.hasExitTime = true;
                exitTrans.exitTime = 1f;
                exitTrans.duration = 0.1f;
                Debug.Log("[153차 P2] Till→Idle 전이 추가(Exit Time 1.0)");
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[153차 P2] AddTillStateToPlayerAC 완료 — {PlayerAcPath}");
        }

        /// <summary>[153차 P2→156차 P5 승계] 굉이질 클립 결정론 조회 — [156차 P5] 곡갱이질 선택 규칙 위임
        /// (클립 2종 파일 대응: '곡갱' 포함 우선, 없으면 이름 정렬 첫 항목 — 기존 "정렬 첫 항목" 규칙 승계).</summary>
        private static AnimationClip ResolveTillClip() => ResolvePickaxeClip(TillFbxPath);

        /// <summary>[156차 P5] 곡갱이질 클립 결정론 선택(공용 규칙 — Player156AnimTools 호출 재사용 계약) —
        /// FBX 내장 AnimationClip 전수 로드 후('__' 프리뷰 클립 제외, 이름 오름차순 정렬):
        /// ① 이름에 '곡갱' 포함 첫 항목 우선 ② 없으면 정렬 첫 항목(인덱스 0). 부재 시 null.
        /// 미채택 클립(망치질 등)은 기록만 — 배선 금지(위조 배선 방지).</summary>
        public static AnimationClip ResolvePickaxeClip(string fbxPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__", StringComparison.Ordinal))
                .OrderBy(c => c.name, StringComparer.Ordinal)
                .ToList();
            var pick = clips.FirstOrDefault(c => c.name.Contains("곡갱")) ?? clips.FirstOrDefault();
            if (pick == null)
                return null;
            var rest = clips.Where(c => c != pick).Select(c => c.name).ToList();
            Debug.Log(rest.Count > 0
                ? $"[156차 P5] 클립 후보 {clips.Count}건 — 채택 '{pick.name}'(곡갱이질 규칙), 미채택 [{string.Join(", ", rest)}](망치질 등 — 기록만, 배선 금지)"
                : $"[156차 P5] 클립 후보 1건 — 채택 '{pick.name}'(곡갱이질 규칙)");
            return pick;
        }

        // ══════════════════════ 소형 헬퍼 ══════════════════════

        /// <summary>16진수 → Color 변환(0xRRGGBB) — 133차 GatherVFXPrefabsBuilder 선례 준용.</summary>
        private static Color C(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// <summary>색 톤들을 균등 키로 배치한 그레디언트(RandomColor 픽션용) — 133차 선례 준용.</summary>
        private static Gradient MakeGradient(params Color[] colors)
        {
            var grad = new Gradient();
            var keys = new GradientColorKey[colors.Length];
            var alphaKeys = new GradientAlphaKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float t = colors.Length == 1 ? 0f : (float)i / (colors.Length - 1);
                keys[i] = new GradientColorKey(colors[i], t);
                alphaKeys[i] = new GradientAlphaKey(colors[i].a, t);
            }
            grad.SetKeys(keys, alphaKeys);
            return grad;
        }
    }
}
