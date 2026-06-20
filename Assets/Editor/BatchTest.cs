using UnityEditor;
using UnityEngine;

public class BatchTest
{
    [MenuItem("Tools/BatchTest")]
    public static void Run()
    {
        Debug.Log("BatchTest ran");
        EditorApplication.Exit(0);
    }
}
