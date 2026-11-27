using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [130차 후속] 파생 구르기 클립(Roll_Back/Roll_Side) Humanoid 임포트 설정 — Npc2026ImportSetup 선례 준용.
    /// 소스: Assets/Animations/MeshyUser/Roll_Dodge.fbx(Blender 파생 — 시간반전=뒷구르기 / 바인드 Rz90=옆구르기).
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.DerivedRollImportSetup.ConfigureDerivedRolls
    /// 멱등: 이미 목표 설정이면 SaveAndReimport 스킵.
    /// </summary>
    public static class DerivedRollImportSetup
    {
        private static readonly string[] DerivedPaths =
        {
            "Assets/Animations/MeshyUser/Roll_Back.fbx",
            "Assets/Animations/MeshyUser/Roll_Side.fbx",
        };

        [MenuItem("Tools/Anim/Configure Derived Roll Import")]
        public static void ConfigureDerivedRolls()
        {
            foreach (var path in DerivedPaths)
            {
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null)
                {
                    Debug.LogError($"[DerivedRollImportSetup] importer null — {path}");
                    continue;
                }
                bool changed = false;
                if (mi.animationType != ModelImporterAnimationType.Human)
                {
                    mi.animationType = ModelImporterAnimationType.Human;   // 3 — Roll_Dodge 소스와 동일 계약
                    changed = true;
                }
                if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    changed = true;
                }
                var clips = mi.clipAnimations;
                // 구르기 = 원샷 계약(loopTime=0 — Roll_Dodge 소스와 동일)
                if (clips != null && clips.Length > 0 && clips.Any(c => c.loopTime))
                {
                    foreach (var c in clips) c.loopTime = false;
                    mi.clipAnimations = clips;
                    changed = true;
                }
                if (changed)
                    mi.SaveAndReimport();

                var re = AssetImporter.GetAtPath(path) as ModelImporter;
                var reClips = re != null ? re.clipAnimations : null;
                Debug.Log($"[DerivedRollImportSetup] {path} animType={(re != null ? (int)re.animationType : -1)} " +
                          $"avatar={(re != null ? (int)re.avatarSetup : -1)} loop={(reClips != null && reClips.Length > 0 ? reClips[0].loopTime : false)}");
            }
        }
    }
}
