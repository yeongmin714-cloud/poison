using UnityEditor;
using UnityEngine;
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
        CompilationPipeline.RequestScriptCompilation();
        return true;
    }
}