using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class AnimationRegressionTestRunner
{
    static TestRunnerApi runner;
    static bool done;
    static bool failed;

    [MenuItem("Tests/Run Animation Regression Tests")]
    public static void Run()
    {
        done = false;
        failed = false;
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(new Callback());
        var filter = new Filter
        {
            testMode = TestMode.EditMode,
            testNames = new[]
            {
                "SynchronizedFlapAxis_MirroredWingsMoveInSameVerticalDirection",
                "BuildMap_QuadrupedWithRejectedLegPair_StillMapsBothWings",
                "Manticore_FourDistinctMirroredLimbs_MapAndTailIsExcluded"
            }
        };
        runner.Execute(new ExecutionSettings(filter));
        double deadline = EditorApplication.timeSinceStartup + 240.0;
        EditorApplication.update += WaitForCompletion;
        void WaitForCompletion()
        {
            if (!done && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= WaitForCompletion;
            if (!done) File.WriteAllText("TestOutput/anim-regression-summary.txt", "TIMEOUT\n");
            Object.DestroyImmediate(runner);
            runner = null;
            EditorApplication.Exit(failed || !done ? 1 : 0);
        }
    }

    sealed class Callback : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { Debug.Log("[AnimRegression] RUN START"); }
        public void TestStarted(ITestAdaptor test) { Debug.Log("[AnimRegression] TEST " + test.FullName); }
        public void TestFinished(ITestResultAdaptor result)
        {
            string line = result.Test.FullName + " " + result.TestStatus + " " + result.Message;
            Debug.Log("[AnimRegression] " + line);
            File.AppendAllText("TestOutput/anim-regression.txt", line + "\n");
            if (result.TestStatus == TestStatus.Failed) failed = true;
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            failed |= result.FailCount > 0;
            File.WriteAllText("TestOutput/anim-regression-summary.txt",
                result.ResultState + " " + result.PassCount + " passed " + result.FailCount + " failed\n");
            Debug.Log("[AnimRegression] RUN FINISHED " + result.ResultState + " failures=" + result.FailCount);
            done = true;
        }
    }
}
