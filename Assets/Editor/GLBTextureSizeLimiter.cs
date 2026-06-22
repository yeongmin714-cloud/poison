using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// GLB 파일 임포트 시 텍스처 크기를 자동 제한하여 메모리 오류 방지
/// Unity Editor에서 GLB/GLTF 파일을 임포트할 때 추출되는 텍스처의
/// 최대 크기를 1024로 제한합니다 (개발 중).
///
/// 빌드 시에는 원본 해상도 사용 (BuildPipeline 호환)
/// </summary>
public class GLBTextureSizeLimiter : AssetPostprocessor
{
    // 개발 중 최대 텍스처 크기 (메모리 절약)
    private const int DEV_MAX_TEXTURE_SIZE = 1024;

    // 이 폴더 아래의 GLB만 처리
    private static readonly string[] TargetFolders = {
        "Assets/Models/UserProvided_Archive"
    };

    void OnPreprocessTexture()
    {
        // GLB 파일에서 추출된 텍스처인지 확인
        if (!IsUserProvidedGLBTexture(assetPath))
            return;

        var textureImporter = (TextureImporter)assetImporter;

        // 이미 작게 설정된 텍스처는 건너뜀
        if (textureImporter.maxTextureSize <= DEV_MAX_TEXTURE_SIZE)
            return;

        // 텍스처 크기 제한
        textureImporter.maxTextureSize = DEV_MAX_TEXTURE_SIZE;

        // 추가 최적화
        textureImporter.mipmapEnabled = false;            // 밉맵 비활성화 (VRAM 절약)
        textureImporter.textureCompression = TextureImporterCompression.Compressed; // 압축 활성화
        textureImporter.crunchedCompression = true;       // 크런치 압축 (디스크+VRAM 모두 절약)
        textureImporter.compressionQuality = 50;          // 중간 품질 (50/100)

        // 플랫폼별 오버라이드
        var androidSettings = textureImporter.GetPlatformTextureSettings("Android");
        androidSettings.overridden = true;
        androidSettings.maxTextureSize = 512;
        androidSettings.format = TextureImporterFormat.ASTC_6x6;
        textureImporter.SetPlatformTextureSettings(androidSettings);

        var iOSSettings = textureImporter.GetPlatformTextureSettings("iPhone");
        iOSSettings.overridden = true;
        iOSSettings.maxTextureSize = 512;
        iOSSettings.format = TextureImporterFormat.ASTC_6x6;
        textureImporter.SetPlatformTextureSettings(iOSSettings);

        var standaloneSettings = textureImporter.GetPlatformTextureSettings("Standalone");
        standaloneSettings.overridden = true;
        standaloneSettings.maxTextureSize = DEV_MAX_TEXTURE_SIZE;
        standaloneSettings.format = TextureImporterFormat.DXT5;
        textureImporter.SetPlatformTextureSettings(standaloneSettings);

        Debug.Log($"[GLBTextureSizeLimiter] 텍스처 크기 {DEV_MAX_TEXTURE_SIZE}로 제한: {Path.GetFileName(assetPath)}");
    }

    void OnPreprocessModel()
    {
        if (!IsUserProvidedGLB(assetPath))
            return;

        var modelImporter = (ModelImporter)assetImporter;

        // 리깅 설정 유지 (Animation Type 확인)
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

        // Material 검색은 프로젝트 전체에서
        modelImporter.materialSearch = ModelImporterMaterialSearch.Everywhere;

        Debug.Log($"[GLBTextureSizeLimiter] GLB 임포트 처리: {Path.GetFileName(assetPath)}");
    }

    void OnPostprocessModel(GameObject model)
    {
        if (!IsUserProvidedGLB(assetPath))
            return;

        // GLB가 너무 큰 메시를 포함하면 경고
        var meshFilters = model.GetComponentsInChildren<MeshFilter>();
        int totalVertices = 0;
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh != null)
                totalVertices += mf.sharedMesh.vertexCount;
        }

        if (totalVertices > 100000)
        {
            Debug.LogWarning($"[GLBTextureSizeLimiter] 대용량 메시 발견: {Path.GetFileName(assetPath)} — {totalVertices}개 정점");
        }
    }

    #region Helpers

    private static bool IsUserProvidedGLB(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".glb" && ext != ".gltf") return false;

        foreach (var folder in TargetFolders)
        {
            if (path.StartsWith(folder))
                return true;
        }
        return false;
    }

    private static bool IsUserProvidedGLBTexture(string path)
    {
        // GLB에서 추출된 텍스처는 보통 "Assets/Resources/Models/UserProvided/..." 또는
        // Library 내부 경로로 들어옴.
        // Library에서 오는 텍스처도 UserProvided GLB에서 추출된 것이므로 처리
        if (path.Contains("Library") && path.Contains("UserProvided"))
            return true;

        return IsUserProvidedGLB(path);
    }

    #endregion

    #region Editor Menu

    [MenuItem("Tools/GLB/Reimport All with Texture Limits")]
    public static void ReimportAllGLBWithLimits()
    {
        if (!EditorUtility.DisplayDialog(
            "GLB 재임포트",
            $"UserProvided 폴더의 모든 GLB를 재임포트합니다.\n텍스처 최대 크기: {DEV_MAX_TEXTURE_SIZE}\n\n계속하시겠습니까?",
            "예", "아니오"))
            return;

        foreach (var folder in TargetFolders)
        {
            if (!Directory.Exists(folder)) continue;

            string[] glbFiles = Directory.GetFiles(folder, "*.glb", SearchOption.AllDirectories);
            int count = 0;
            foreach (var glbPath in glbFiles)
            {
                // processed 폴더는 제외
                if (glbPath.Replace("\\", "/").Contains("/processed/"))
                    continue;

                string assetPath = glbPath.Replace("\\", "/");
                if (!assetPath.StartsWith("Assets/"))
                    continue;

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                count++;

                // 진행 상황 표시
                if (count % 20 == 0)
                {
                    EditorUtility.DisplayProgressBar("GLB 재임포트 중...",
                        $"{count}/{glbFiles.Length} — {Path.GetFileName(glbPath)}",
                        (float)count / glbFiles.Length);
                }
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"[GLBTextureSizeLimiter] {count}개 GLB 재임포트 완료. 텍스처 크기 제한: {DEV_MAX_TEXTURE_SIZE}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/GLB/Reimport with High Quality (Build)")]
    public static void ReimportForBuild()
    {
        if (!EditorUtility.DisplayDialog(
            "빌드용 고품질 재임포트",
            "텍스처를 원본 해상도(최대 4096)로 재임포트합니다.\n메모리 사용량이 크게 증가합니다.\n\n계속하시겠습니까?",
            "예", "아니오"))
            return;

        foreach (var folder in TargetFolders)
        {
            if (!Directory.Exists(folder)) continue;

            string[] glbFiles = Directory.GetFiles(folder, "*.glb", SearchOption.AllDirectories);
            int count = 0;
            foreach (var glbPath in glbFiles)
            {
                if (glbPath.Replace("\\", "/").Contains("/processed/"))
                    continue;

                string assetPath = glbPath.Replace("\\", "/");
                if (!assetPath.StartsWith("Assets/"))
                    continue;

                // 텍스처 임포트 설정 초기화 (원본 해상도)
                var textures = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var asset in textures)
                {
                    if (asset is Texture2D)
                    {
                        string texPath = AssetDatabase.GetAssetPath(asset);
                        var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                        if (importer != null)
                        {
                            importer.maxTextureSize = 4096;
                            importer.SaveAndReimport();
                        }
                    }
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                count++;
            }

            Debug.Log($"[GLBTextureSizeLimiter] {count}개 GLB 빌드용 재임포트 완료.");
            AssetDatabase.Refresh();
        }
    }

    #endregion
}