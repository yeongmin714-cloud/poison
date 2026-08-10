using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildTools
{
    [MenuItem("Tools/Build Windows Standalone")]
    public static void BuildWindows()
    {
        string[] scenes = {
            "Assets/Scenes/MainScene.unity",
            "Assets/Scenes/IndoorScene.unity",
            "Assets/Scenes/TopDownScene.unity",
            "Assets/Scenes/WorldMap.unity"
        };
        string buildPath = "Builds/Windows/Game.exe";

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {buildPath} ({summary.totalSize / 1048576} MB)");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"Build failed: {summary.result}");
            EditorApplication.Exit(1);
        }
    }
}