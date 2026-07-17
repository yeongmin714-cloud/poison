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
        // Request compilation and return true to indicate success for batchmode
        CompilationPipeline.RequestScriptCompilation();
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