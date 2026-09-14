using UnityEngine;
using UnityEditor;

public static class DiagSlashShaderGraphImport
{
    public static void Run()
    {
        const string path = "Assets/slash5-HungNguyen/slash shader/slash5.shadergraph";
        Debug.Log("[Diag] 강제 재임포트 시작: " + path);
        try
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.DontDownloadFromCacheServer);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Diag] ImportAsset 예외: " + e);
        }

        var sg = AssetDatabase.LoadAssetAtPath<Shader>(path);
        var sgGraph = AssetDatabase.LoadAssetAtPath<Object>(path);
        Debug.Log($"[Diag] 결과: Shader={sg != null}, Object={sgGraph != null}, 이름={(sgGraph != null ? sgGraph.name : "null")}, GUID={AssetDatabase.AssetPathToGUID(path)}");
        Debug.Log("[Diag] 완료");
    }
}
