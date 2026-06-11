using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools;
using System.Collections.Generic;
using System.IO;

public static class UnityTestRunner
{
    /// <summary>
    /// CLI entry point: run all EditMode tests and exit with code.
    /// Usage: Unity.exe -quit -batchmode -executeMethod UnityTestRunner.RunAllEditModeTests
    /// </summary>
    public static void RunAllEditModeTests()
    {
        Debug.Log("=== UnityTestRunner: Running all EditMode tests ===");
        
        var results = TestRunner.RunAllMethods(
            new TestRunnerFilter { testMode = TestMode.EditMode }
        );
        
        int passed = results.passedCount;
        int failed = results.failedCount;
        
        Debug.Log($"Passed: {passed}, Failed: {failed}");
        
        if (failed > 0)
        {
            EditorApplication.Exit(1);
        }
        else
        {
            EditorApplication.Exit(0);
        }
    }
}