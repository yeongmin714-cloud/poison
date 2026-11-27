using UnityEditor;
using UnityEngine;

/// <summary>
/// [141차 P3] GLB/FBX 임베드 텍스처 품질 상시 후처리기 — **무조건 고품질 계약의 상시 적용**(건물/프롭/캐릭터 GLB 전부).
/// 배경: 사용자 보고 — "glb로 넣어둔 건물들도 몇 개 깨지고 있었던 것 같다" + "특정 에셋만 깨짐" →
/// 임베드 텍스처도 NPC 텍스처와 동일한 압축(BC1 q50) 기본값을 타고 있음.
/// 본 후처리기는 Models/UserProvided 하위 모델(GLB/FBX)에서 나오는 임베드 텍스처에
/// maxSize 4096(원본 유지) + compressionQuality 100을 상시 적용한다(무조건 고품질 계약 — 141차).
/// NPC 단독 PNG(base_color/normal)는 Npc141TextureQuality 배치가 담당(중복 무해 — 동일 값).
/// </summary>
public sealed class Npc141GlbTextureQualityPostprocessor : AssetPostprocessor
{
    private void OnPostprocessTexture(Texture2D texture)
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/Resources/Models/UserProvided/")) return;
        if (path.Contains("/Npc2026/Textures/")) return;   // 단독 PNG — 배치 도구 소관(타입 변경 포함)

        var importer = (TextureImporter)assetImporter;
        bool changed = false;
        if (importer.maxTextureSize < 4096) { importer.maxTextureSize = 4096; changed = true; }   // 원본 해상도 유지
        if (importer.compressionQuality != 100) { importer.compressionQuality = 100; changed = true; }
        if (importer.textureType == TextureImporterType.Default && importer.sRGBTexture == false)
        {
            // 색 텍스처인데 sRGB 꺼진 비정형 — 건물 알베도는 sRGB가 정합.
            importer.sRGBTexture = true;
            changed = true;
        }
        if (changed)
            Debug.Log($"[141차 P3] 임베드 텍스처 고품질 적용 — {path} maxSize=4096 q=100");
    }
}
