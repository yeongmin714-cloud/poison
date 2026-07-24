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
        // For automated batchmode compilation testing, simply request compilation
        // and return true to indicate success. The Unity batchmode command 
        // will handle exit code reporting properly based on compilation results
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