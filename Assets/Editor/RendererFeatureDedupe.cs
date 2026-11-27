using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.Linq;

/// <summary>
/// [126차] URP 렌더러 피처 정리 — 중복 SSAO 제거.
/// 실측: UniversalRendererData.asset에 SSAO가 동일 설정으로 2건(셋업 이중 실행 잔재).
/// AO 이중 적용 = 음영 과다 + 패스 비용 2배. YAML 수기 편집 대신 SerializedObject 경로(리스크 회피).
/// 배치 실행: Unity.exe -batchmode -executeMethod RendererFeatureDedupe.DedupeSSAO -quit
/// </summary>
public static class RendererFeatureDedupe
{
    private const string RendererPath = "Assets/URP/UniversalRendererData.asset";

    public static void DedupeSSAO()
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (rendererData == null)
        {
            Debug.LogError("[RendererFeatureDedupe] 렌더러 없음: " + RendererPath);
            EditorApplication.Exit(2);
            return;
        }

        var ssaos = rendererData.rendererFeatures
            .Where(f => f != null && f is ScreenSpaceAmbientOcclusion)
            .ToList();

        Debug.Log($"[RendererFeatureDedupe] SSAO 피처 수 = {ssaos.Count}");
        if (ssaos.Count <= 1)
        {
            Debug.Log("[RendererFeatureDedupe] 중복 없음 — 종료");
            EditorApplication.Exit(0);
            return;
        }

        // 첫 1건 유지, 나머지 제거
        var so = new SerializedObject(rendererData);
        var listProp = so.FindProperty("m_RendererFeatures");
        var keep = ssaos[0];
        int removed = 0;
        for (int i = listProp.arraySize - 1; i >= 0; i--)
        {
            var elem = listProp.GetArrayElementAtIndex(i);
            var feat = elem.objectReferenceValue;
            if (feat != null && feat != keep && feat is ScreenSpaceAmbientOcclusion)
            {
                listProp.DeleteArrayElementAtIndex(i);
                // 임베디드 서브에셋 정리(파일 분리 아님 — 안전)
                AssetDatabase.RemoveObjectFromAsset(feat);
                Object.DestroyImmediate(feat, true);
                removed++;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();

        Debug.Log($"[RendererFeatureDedupe] SSAO 중복 {removed}건 제거 완료 — 잔여 {(rendererData.rendererFeatures.Count(f => f is ScreenSpaceAmbientOcclusion))}건");
        EditorApplication.Exit(0);
    }
}
