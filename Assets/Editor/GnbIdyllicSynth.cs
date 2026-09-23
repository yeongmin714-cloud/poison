// GNB P-A: synth new nature GLB into IdyllicPrefabs pool prefabs.
// Run: Unity -quit -batchmode -projectPath <win> -executeMethod GnbIdyllicSynth.SynthAll -logFile <win>
using UnityEngine;
using UnityEditor;

public class GnbIdyllicSynth
{
    static readonly string SRC = "Assets/새로운 glb/nature";
    static readonly string DST = "Assets/Resources/IdyllicPrefabs/";

    static string BaseName(string path)
    {
        string b = path.Replace('\\', '/');
        int i = b.LastIndexOf("/");
        return i >= 0 ? b.Substring(i + 1) : b;
    }

    public static void SynthAll()
    {
        Debug.Log("[GNB-Synth] start");
        string[] guids = AssetDatabase.FindAssets("", new string[] { SRC });
        int ok = 0, skip = 0, stray = 0;
        int treeFir = 0, treeBlossom = 0, treeBroad = 0, bush = 0, grass = 0, flowerCnt = 0;
        int rockBig = 0, rockMed = 0, rockSmall = 0, shore = 0, water = 0;
        long counter = 1000;

        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p == null || p.Length == 0 || !p.EndsWith(".glb")) continue;
            string lower = BaseName(p).ToLower();
            string folder = null;
            string name = null;
            if (lower.Contains("pine") || lower.Contains("fir")) { folder = "Trees"; name = "Fir_" + (++treeFir); }
            else if (lower.Contains("fruit") || lower.Contains("blossom")) { folder = "Trees"; name = "BlossomTree_" + (++treeBlossom); }
            else if (lower.Contains("-tree") || lower.Contains("birch") || lower.Contains("broadleaf"))
            { folder = "Trees"; name = "BroadleafTree_" + (++treeBroad) + "_Green"; }
            else if (lower.Contains("shrub") || lower.Contains("bush") || lower.Contains("berry") || lower.Contains("hydrangea")
                || lower.Contains("boxwood") || lower.Contains("stump") || lower.Contains("bamboo"))
            { folder = "Bushes"; name = "Bush_" + (++bush); }
            else if (lower.Contains("rock") || lower.Contains("boulder") || lower.Contains("mound") || lower.Contains("sandstone")
                || lower.Contains("limestone") || lower.Contains("quartz") || lower.Contains("outcrop") || lower.Contains("granite")
                || lower.Contains("slate") || lower.Contains("stone"))
            {
                folder = "Rocks";
                long r = counter++;
                if (r % 7 < 2) { name = "Rock_Big_" + (++rockBig); }
                else if (r % 7 < 4) { name = "Rock_Medium_" + (++rockMed); }
                else { name = "Rock_Small_" + (++rockSmall); }
            }
            else if (lower.Contains("herb") || lower.Contains("mushroom") || lower.Contains("flower") || lower.Contains("daisy")
                || lower.Contains("tulip") || lower.Contains("sunflower") || lower.Contains("clover") || lower.Contains("bluebell"))
            { folder = "Flowers"; name = "Flower_Yellow_" + (++flowerCnt); }
            else if (lower.Contains("reed") || lower.Contains("cattail") || lower.Contains("marsh") || lower.Contains("pond")
                || lower.Contains("sedge"))
            { folder = "Shore"; name = "Reeds_" + (++shore); }
            else if (lower.Contains("lily")) { folder = "Water"; name = "Waterlily_" + (++water); }
            else if (lower.Contains("grass") || lower.Contains("tuft") || lower.Contains("fern") || lower.Contains("meadow")
                || lower.Contains("clump") || lower.Contains("patch") || lower.Contains("ground") || lower.Contains("succulent"))
            { folder = "Grass"; name = "Grass_" + (++grass); }
            else { stray++; skip++; continue; }

            string dstPath = DST + folder + "/" + name + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (asset == null) { skip++; continue; }
            var inst = Object.Instantiate(asset);
            inst.name = name;
            var saved = PrefabUtility.SaveAsPrefabAsset(inst, dstPath);
            Object.DestroyImmediate(inst);
            if (saved != null) ok++; else { skip++; Debug.LogWarning("[GNB-Synth] save fail " + dstPath); }
        }
        AssetDatabase.Refresh();
        Debug.Log("[GNB-Synth] done ok=" + ok + " skip=" + skip + " stray=" + stray
            + " Fir=" + treeFir + " Blossom=" + treeBlossom + " Broad=" + treeBroad
            + " Bush=" + bush + " Grass=" + grass + " Flower=" + flowerCnt
            + " RockBig=" + rockBig + " RockMed=" + rockMed + " RockSmall=" + rockSmall
            + " Shore=" + shore + " Water=" + water);
        EditorApplication.Exit(0);
    }
}