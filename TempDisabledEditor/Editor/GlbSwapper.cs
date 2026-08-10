using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

public static class GlbSwapper
{
    [MenuItem("Tools/Swap GLB Models (for cronjob)")]
    public static void SwapAndSave()
    {
        ModelSwapper.SwapAndSave();
    }
}