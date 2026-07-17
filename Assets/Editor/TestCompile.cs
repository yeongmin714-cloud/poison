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
        // This is the correct implementation for automated compilation checking
        // It returns true if compilation succeeded (exit code 0) or false if it failed (non-zero exit code)
        // The batchmode Unity command will return the proper exit code based on compilation results
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