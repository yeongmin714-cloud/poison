#if false
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;

/// <summary>
/// Editor utility for recording gameplay via Unity Recorder.
/// Attach this to a GameObject in the scene, or call via -executeMethod from command line.
/// </summary>
public static class RecordGameplay
{
    private static RecorderController _recorderController;
    private static GameObject _runnerObject;

    /// <summary>
    /// Record gameplay for the specified number of seconds.
    /// Automatically enters Play Mode if not already playing.
    /// </summary>
    /// <param name="seconds">Duration in seconds to record.</param>
    public static void RecordForSeconds(float seconds)
    {
        if (Application.isPlaying == false)
        {
            Debug.Log($"[RecordGameplay] Starting Play Mode, will record for {seconds}s...");
            EditorApplication.isPlaying = true;
        }

        // Create or reuse a runner GameObject to handle the coroutine
        if (_runnerObject == null)
        {
            _runnerObject = new GameObject("__RecordGameplay_Runner__");
            Object.DontDestroyOnLoad(_runnerObject);
            var runner = _runnerObject.AddComponent<RecordCoroutineRunner>();
            runner.StartCoroutine(RecordingCoroutine(seconds));
        }
    }

    /// <summary>
    /// Called via -executeMethod from command line. Records 10 seconds.
    /// </summary>
    public static void Record10Seconds()
    {
        RecordForSeconds(10f);
    }

    /// <summary>
    /// Called via -executeMethod from command line. Records 5 seconds.
    /// </summary>
    public static void Record5Seconds()
    {
        RecordForSeconds(5f);
    }

    private static IEnumerator RecordingCoroutine(float seconds)
    {
        // Wait one frame for Play Mode to fully enter
        yield return null;

        // Build output directory: project root / Recordings /
        string projectPath = Application.dataPath.Replace("/Assets", "");
        string outputDir = projectPath + "/Recordings";

        if (!System.IO.Directory.Exists(outputDir))
        {
            System.IO.Directory.CreateDirectory(outputDir);
        }

        // Configure recorder settings
        var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        _recorderController = new RecorderController(controllerSettings);

        // Movie Recorder (MP4)
        var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieSettings.name = "Gameplay Recorder";
        movieSettings.Enabled = true;

        // Select MP4 format
        movieSettings.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;

        // Use GameView as input source
        var gameViewInput = new GameViewInputSettings
        {
            OutputWidth = 1920,
            OutputHeight = 1080
        };
        movieSettings.ImageInputSettings = gameViewInput;

        // Set audio input (from the scene)
        movieSettings.AudioInputSettings.PreserveAudio = true;

        // Output file: Recordings/Gameplay_<timestamp>.mp4
        movieSettings.OutputFile = outputDir + "/Gameplay_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Assign to controller
        controllerSettings.AddRecorderSettings(movieSettings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = 30.0f;
        controllerSettings.FrameRatePlayback = FrameRatePlayback.Variable;

        // Prepare and start recording
        _recorderController.PrepareRecording();
        _recorderController.StartRecording();

        Debug.Log($"[RecordGameplay] Recording started for {seconds}s...");

        // Wait for the specified duration
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Stop recording
        Debug.Log("[RecordGameplay] Stopping recording...");
        _recorderController.StopRecording();

        Debug.Log($"[RecordGameplay] Recording saved to: {outputDir}/");

        // Cleanup
        Cleanup();

        // If we entered Play Mode, exit it after a short delay
        if (EditorApplication.isPlaying)
        {
            yield return new WaitForSeconds(0.5f);
            EditorApplication.isPlaying = false;
        }

        // Destroy the runner object
        if (_runnerObject != null)
        {
            Object.DestroyImmediate(_runnerObject);
            _runnerObject = null;
        }

        Debug.Log("[RecordGameplay] Recording complete. Play Mode stopped.");
    }

    private static void Cleanup()
    {
        if (_recorderController != null)
        {
            _recorderController = null;
        }
    }

    /// <summary>
    /// Internal MonoBehaviour to run coroutines in editor play mode.
    /// </summary>
    private class RecordCoroutineRunner : MonoBehaviour { }
}
#endif