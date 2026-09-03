using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// 믹사모 클립 FBX 81개를 Humanoid 리그로 일괄 전환한다.
    /// Humanoid 아바타(플레이어/병사)에서 믹사모 클립이 재생되려면
    /// 클립 FBX도 Humanoid로 임포트되어야 한다 (Generic이면 리타겟 불가 → T-pose 정지).
    /// Unity는 mixamorig 본 네이밍을 자동 매핑하므로 별도 수동 매핑 불필요.
    /// Tools > Anim > Apply Humanoid To Mixamo Clips
    /// </summary>
    public static class MixamoClipRigSetup
    {
        private const string MixamoDir = "Assets/Animations/Mixamo";

        [MenuItem("Tools/Anim/Apply Humanoid To Mixamo Clips")]
        public static void Apply()
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { MixamoDir });
            int changed = 0, total = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLower().EndsWith(".fbx")) continue;
                total++;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human; // Unity 6.4: Humanoid → Human 개명
                    importer.SaveAndReimport();
                    changed++;
                    Debug.Log($"[MixamoClipRig] Humanoid 전환: {System.IO.Path.GetFileName(path)}");
                }
            }
            Debug.Log($"[MixamoClipRig] 완료: 총 {total}개 중 {changed}개 Humanoid 전환 (나머지는 이미 Humanoid)");
        }
    }
}
