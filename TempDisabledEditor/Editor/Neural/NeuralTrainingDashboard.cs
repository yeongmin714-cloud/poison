using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;

namespace ProjectName.Editor.Neural
{
    /// <summary>
    /// Editor window for launching and monitoring Neural Animation training.
    /// Bridges Unity ↔ Python training pipeline (train_lightweight.py).
    /// </summary>
    public class NeuralTrainingDashboard : EditorWindow
    {
        [MenuItem("Tools/Neural/Training Dashboard")]
        static void ShowWindow()
        {
            GetWindow<NeuralTrainingDashboard>("Neural Training Dashboard");
        }

        // ──────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────
        // Training config
        string _pythonPath = "python3";
        string _trainingScriptPath = "Assets/Training/TrainingInfra/train_lightweight.py";
        string _avatarType = "biped";
        int _epochs = 50;
        bool _quickMode;
        bool _useCurriculum;
        bool _useStyleEmbedding;
        bool _useTensorboard;
        string _ensembleSeeds = "";
        string _policyType = "locomotion";
        bool _fp16;

        // Process tracking
        Process _trainingProcess;
        bool _isTraining;
        string _lastOutput;
        Vector2 _scrollPos;

        // TensorBoard
        bool _tensorboardRunning;
        Process _tensorboardProcess;

        // Checkpoints
        string _checkpointDir = "Assets/Training/TrainingInfra/checkpoints";
        string[] _checkpointFiles;

        readonly string[] _avatarTypes = { "biped", "quadruped" };
        readonly string[] _policyTypes = { "locomotion", "combat", "react", "interact", "fly", "swim" };

        // ──────────────────────────────────────────────
        //  GUI
        // ──────────────────────────────────────────────

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Neural Training Dashboard", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Python path
            EditorGUILayout.LabelField("Python Configuration", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            _pythonPath = EditorGUILayout.TextField("Python Path", _pythonPath);
            _trainingScriptPath = EditorGUILayout.TextField("Training Script", _trainingScriptPath);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Training config
            EditorGUILayout.LabelField("Training Configuration", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            int avatarIdx = System.Array.IndexOf(_avatarTypes, _avatarType);
            avatarIdx = EditorGUILayout.Popup("Avatar Type", Mathf.Max(0, avatarIdx), _avatarTypes);
            _avatarType = _avatarTypes[Mathf.Clamp(avatarIdx, 0, _avatarTypes.Length - 1)];

            int policyIdx = System.Array.IndexOf(_policyTypes, _policyType);
            policyIdx = EditorGUILayout.Popup("Policy Type", Mathf.Max(0, policyIdx), _policyTypes);
            _policyType = _policyTypes[Mathf.Clamp(policyIdx, 0, _policyTypes.Length - 1)];

            _epochs = EditorGUILayout.IntSlider("Epochs", _epochs, 10, 200);
            _quickMode = EditorGUILayout.Toggle("Quick Mode (10 epochs)", _quickMode);
            _fp16 = EditorGUILayout.Toggle("FP16 Quantization", _fp16);

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Advanced
            EditorGUILayout.LabelField("Advanced Options", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            _useCurriculum = EditorGUILayout.Toggle("Curriculum Learning", _useCurriculum);
            _useStyleEmbedding = EditorGUILayout.Toggle("Style Embedding", _useStyleEmbedding);
            _useTensorboard = EditorGUILayout.Toggle("TensorBoard Logging", _useTensorboard);
            _ensembleSeeds = EditorGUILayout.TextField("Ensemble Seeds (comma)", _ensembleSeeds);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_isTraining;
            if (GUILayout.Button("Start Training", GUILayout.Height(30)))
            {
                StartTraining();
            }
            GUI.enabled = _isTraining;
            if (GUILayout.Button("Stop Training", GUILayout.Height(30)))
            {
                StopTraining();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // TensorBoard
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("TensorBoard", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_tensorboardRunning;
            if (GUILayout.Button("Start TensorBoard"))
            {
                StartTensorBoard();
            }
            GUI.enabled = _tensorboardRunning;
            if (GUILayout.Button("Stop TensorBoard"))
            {
                StopTensorBoard();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Output log
            EditorGUILayout.LabelField("Training Output", EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(_lastOutput))
            {
                EditorGUILayout.TextArea(_lastOutput, GUILayout.Height(150));
            }
            else
            {
                EditorGUILayout.HelpBox("No training output yet. Click 'Start Training' to begin.", MessageType.Info);
            }
            EditorGUILayout.Space();

            // Checkpoints
            RefreshCheckpoints();
            if (_checkpointFiles != null && _checkpointFiles.Length > 0)
            {
                EditorGUILayout.LabelField("Checkpoints", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var cp in _checkpointFiles)
                    EditorGUILayout.LabelField(Path.GetFileName(cp));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();
        }

        // ──────────────────────────────────────────────
        //  Training
        // ──────────────────────────────────────────────

        void StartTraining()
        {
            string scriptPath = Path.Combine(Application.dataPath, "..", _trainingScriptPath);
            if (!File.Exists(scriptPath))
            {
                UnityEngine.Debug.LogError($"[NeuralTrainingDashboard] Script not found: {scriptPath}");
                return;
            }

            string args = $"--avatar_type {_avatarType}";
            if (_quickMode) args += " --quick";
            else args += $" --epochs {_epochs}";

            args += $" --policy_type {_policyType}";
            if (_useCurriculum) args += " --curriculum";
            if (_useStyleEmbedding) args += " --style_embedding";
            if (_useTensorboard) args += " --tensorboard";
            if (!string.IsNullOrEmpty(_ensembleSeeds)) args += $" --ensemble_seeds \"{_ensembleSeeds}\"";
            if (_fp16) args += " --fp16";

            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"{scriptPath} {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath)
            };

            try
            {
                _trainingProcess = new Process { StartInfo = startInfo };
                _trainingProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _lastOutput += e.Data + "\n";
                        UnityEngine.Debug.Log($"[NeuralTrainingDashboard] {e.Data}");
                    }
                };
                _trainingProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _lastOutput += "[ERR] " + e.Data + "\n";
                        UnityEngine.Debug.LogWarning($"[NeuralTrainingDashboard] {e.Data}");
                    }
                };
                _trainingProcess.Exited += (s, e) =>
                {
                    _isTraining = false;
                    _trainingProcess?.Dispose();
                    _trainingProcess = null;
                    UnityEngine.Debug.Log("[NeuralTrainingDashboard] Training completed");
                    RefreshCheckpoints();
                };

                _trainingProcess.Start();
                _trainingProcess.BeginOutputReadLine();
                _trainingProcess.BeginErrorReadLine();
                _isTraining = true;
                _lastOutput = "";
                UnityEngine.Debug.Log($"[NeuralTrainingDashboard] Training started: {args}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[NeuralTrainingDashboard] Failed to start training: {ex.Message}");
            }
        }

        void StopTraining()
        {
            try
            {
                _trainingProcess?.Kill();
                _trainingProcess?.Dispose();
                _trainingProcess = null;
                _isTraining = false;
                UnityEngine.Debug.Log("[NeuralTrainingDashboard] Training stopped by user");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[NeuralTrainingDashboard] Failed to stop training: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        //  TensorBoard
        // ──────────────────────────────────────────────

        void StartTensorBoard()
        {
            string logDir = Path.Combine(Application.dataPath, "..", "Assets/Training/TrainingInfra/tensorboard_logs");
            Directory.CreateDirectory(logDir);

            try
            {
                _tensorboardProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = $"-m tensorboard.main --logdir={logDir} --port=6006",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                _tensorboardProcess.Start();
                _tensorboardRunning = true;
                UnityEngine.Debug.Log($"[NeuralTrainingDashboard] TensorBoard started at http://localhost:6006");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[NeuralTrainingDashboard] TensorBoard not available: {ex.Message}");
            }
        }

        void StopTensorBoard()
        {
            try
            {
                _tensorboardProcess?.Kill();
                _tensorboardProcess?.Dispose();
                _tensorboardProcess = null;
                _tensorboardRunning = false;
                UnityEngine.Debug.Log("[NeuralTrainingDashboard] TensorBoard stopped");
            }
            catch { }
        }

        // ──────────────────────────────────────────────
        //  Checkpoints
        // ──────────────────────────────────────────────

        void RefreshCheckpoints()
        {
            string cpDir = Path.Combine(Application.dataPath, "..", _checkpointDir);
            if (Directory.Exists(cpDir))
            {
                _checkpointFiles = Directory.GetFiles(cpDir, "*.npz");
            }
        }

        void OnDestroy()
        {
            StopTraining();
            StopTensorBoard();
        }
    }
}