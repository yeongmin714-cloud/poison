using UnityEditor;
using UnityEngine;
using System.IO;

public class TestCompile
{
    [MenuItem("Test/Compile")]
    public static void CompileTest()
    {
        // Perform actual compilation check
        var result = UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        
        // Log compilation result
        if (result != UnityEditor.Compilation.ScriptCompilationResult.Success)
        {
            Debug.LogError("Compilation failed");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("Test compile successful");
        }
    }
    
    // Method to check compilation without exiting
    public static bool CheckCompilation()
    {
        var result = UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        if (result != UnityEditor.Compilation.ScriptCompilationResult.Success)
        {
            return false;
        }
        return true;
    }
}