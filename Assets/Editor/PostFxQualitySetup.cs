using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Editor
{
    /// <summary>
    /// [174차 P0] 전역 고품질화 튜닝 — 활성 URP 파이프라인의 기존 SSAO 값 조정.
    ///
    /// 실측(174차): 활성 파이프라인 = GraphicsSettings 기본(Assets/URP/URPAsset.asset, 품질 레벨 4).
    /// 렌더러(Assets/URP/UniversalRendererData.asset)에 SSAO 피처가 이미 존재(m_Active=1)하나
    /// Intensity 3.0 / Radius 0.035(기본값 — 3.5cm 접촉AO만, 저폴리 소품 실루엣에 좁음).
    /// 본 스크립트는 기존 피처의 값을 저폴리 고품질화용으로 튜닝한다(멱등 — 재실행 안전).
    ///
    /// 카메라 post ON·ACES 톤매핑은 런타임(GlobalPostFxBootstrap) 담당.
    /// 상수는 계약 테스트(PostFxQualityTests) 소스 기반 검증 대상 — Play 판정 후 트림.
    /// </summary>
    public static class PostFxQualitySetup
    {
        // ===== SSAO 튜닝 상수 — 저폴리 크레비스 그림자(Play 판정 후 트림 대상) =====
        public const float SsaoIntensity = 1.4f;   // 기본 3.0 → 넓은 반경에선 과음영
        public const float SsaoRadius = 0.4f;      // 기본 0.035m → 소품 스케일 실루엣 커버
        public const string SsaoFeatureName = "SSAO";

        [MenuItem("Tools/PostFx/Tune SSAO (Low-poly Quality)")]
        public static void ApplyFromMenu()
        {
            Apply();
            AssetDatabase.SaveAssets();
        }

        /// <summary>-batchmode -executeMethod 진입점 (-quit 조합).</summary>
        public static void SetupFromBatch()
        {
            Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("[PostFxSetup] batch tuning 완료");
        }

        private static void Apply()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogError("[PostFxSetup] 활성 URP 파이프라인 없음 — 중단");
                return;
            }
            if (pipeline.rendererDataList.Length == 0 || pipeline.rendererDataList[0] == null)
            {
                Debug.LogError("[PostFxSetup] 렌더러 데이터 없음 — 중단");
                return;
            }
            ScriptableRendererData rendererData = pipeline.rendererDataList[0];
            Debug.Log($"[PostFxSetup] 활성 렌더러: {AssetDatabase.GetAssetPath(rendererData)}");

            // ── 기존 SSAO 피처 값 튜닝 (멱등) ──
            ScreenSpaceAmbientOcclusion ssao = null;
            var features = rendererData.rendererFeatures;
            if (features != null)
            {
                for (int i = 0; i < features.Count; i++)
                {
                    if (features[i] is ScreenSpaceAmbientOcclusion found)
                    {
                        ssao = found;
                        break;
                    }
                }
            }
            if (ssao == null)
            {
                Debug.LogError("[PostFxSetup] 활성 렌더러에 SSAO 피처 없음 — 수동 확인 필요");
                return;
            }

            var so = new SerializedObject(ssao);
            var settings = so.FindProperty("m_Settings");
            var intensity = settings.FindPropertyRelative("Intensity");
            var radius = settings.FindPropertyRelative("Radius");
            bool changed = false;
            if (intensity.floatValue != SsaoIntensity)
            {
                intensity.floatValue = SsaoIntensity;
                changed = true;
            }
            if (radius.floatValue != SsaoRadius)
            {
                radius.floatValue = SsaoRadius;
                changed = true;
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ssao);
                EditorUtility.SetDirty(rendererData);
                Debug.Log($"[PostFxSetup] ✅ SSAO 튜닝 — Intensity={SsaoIntensity} Radius={SsaoRadius}");
            }
            else
            {
                Debug.Log($"[PostFxSetup] SSAO 이미 튜닝값 — 스킵 (I={SsaoIntensity} R={SsaoRadius})");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
