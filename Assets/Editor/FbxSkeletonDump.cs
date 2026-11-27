using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>[126차 후속] FBX 스켈레탈 실측 덤프 — 플레이어/병사 FBX 본 계층·월드 실측치·리그 설정 일괄 출력.
/// 읽기전용 유틸(에셋/씬 무변경) — 배치모드 -executeMethod FbxSkeletonDump.Run 로 실행.</summary>
public static class FbxSkeletonDump
{
    private static readonly string[] Targets =
    {
        "Assets/Resources/Models/UserProvided/fbx/Player_Rigged.fbx",
        "Assets/Resources/Models/UserProvided/fbx/Player_Rigged_Heat.fbx",
        "Assets/Resources/Models/UserProvided/fbx/soldier_lv1-20_rigged.fbx",
    };

    public static void Run()
    {
        var sb = new StringBuilder();
        var outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestOutput");
        Directory.CreateDirectory(outDir);

        foreach (var path in Targets)
        {
            sb.AppendLine($"===== {path} =====");
            if (!File.Exists(path)) { sb.AppendLine("FILE NOT FOUND"); continue; }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                sb.AppendLine($"rig: animationType={importer.animationType} globalScale={importer.globalScale} useFileUnits={importer.useFileUnits}");
                sb.AppendLine($"rig: skinWeights={importer.skinWeights} optimizeGameObjects={importer.optimizeGameObjects} blendShape={importer.importBlendShapes}");
                var hd = importer.humanDescription;
                sb.AppendLine($"human: bones={hd.human.Length} armStretch={hd.armStretch:F2} legStretch={hd.legStretch:F2} feetSpacing={hd.feetSpacing:F4}");
                foreach (var h in hd.human)
                    sb.AppendLine($"  humanBone: {h.humanName} <- {h.boneName}");
                var clips = importer.clipAnimations;
                sb.AppendLine($"clips={clips.Length}: {string.Join(", ", System.Array.ConvertAll(clips, c => c.name))}");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("LOAD FAIL"); continue; }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var allT = inst.GetComponentsInChildren<Transform>(true);
            sb.AppendLine($"transforms={allT.Length}");
            foreach (var t in allT)
            {
                var p = t.parent ? t.parent.name : "(root)";
                var lp = t.localPosition; var ls = t.localScale; var wp = t.position;
                sb.AppendLine($"[{t.name}] parent={p} localPos=({lp.x:F4},{lp.y:F4},{lp.z:F4}) localScale=({ls.x:F3},{ls.y:F3},{ls.z:F3}) worldPos=({wp.x:F4},{wp.y:F4},{wp.z:F4}) children={t.childCount}");
            }
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var b = smr.bounds;
                var rb = smr.rootBone ? smr.rootBone.name : "(none)";
                sb.AppendLine($"SMR[{smr.name}] center=({b.center.x:F3},{b.center.y:F3},{b.center.z:F3}) size=({b.size.x:F3},{b.size.y:F3},{b.size.z:F3}) bones={smr.bones.Length} rootBone={rb}");
            }
            var meshFilters = inst.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.GetComponent<Renderer>().bounds;
                sb.AppendLine($"MF[{mf.name}] mesh={mf.sharedMesh.name} center=({mb.center.x:F3},{mb.center.y:F3},{mb.center.z:F3}) size=({mb.size.x:F3},{mb.size.y:F3},{mb.size.z:F3})");
            }
            var animator = inst.GetComponentInChildren<Animator>();
            if (animator != null && animator.avatar != null)
                sb.AppendLine($"Animator: avatar={animator.avatar.name} isHuman={animator.avatar.isHuman}");
            Object.DestroyImmediate(inst);
        }

        var outPath = Path.Combine(outDir, "player_skeleton_dump.txt");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"[FbxSkeletonDump] done -> {outPath} ({sb.Length} chars)");
    }
}
