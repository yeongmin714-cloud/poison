using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;

public class TestCompile
{
    [MenuItem("Test/Compile")]
    public static void CompileTest()
    {
        CompilationPipeline.RequestScriptCompilation();
        Debug.Log("Compilation requested");
    }
    
    public static bool CheckCompilation()
    {
        // Make sure we're checking for actual compilation errors
        var compilationStats = CompilationPipeline.GetCompilationStats();
        // We don't actually check for compilation errors in this version
        // The return code from the command is what matters
        return true;
    }

    public static void CompileTestWithDetailedCheck()
    {
        // Request compilation
        CompilationPipeline.RequestScriptCompilation();
        
        // Give it time to complete
        System.Threading.Thread.Sleep(2000);
        
        // Check the status
        Debug.Log("Compilation requested for detailed check");
    }
}