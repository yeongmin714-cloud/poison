using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Neural.Evaluation;

namespace ProjectName.Systems.Animation.Neural.Evaluation
{
    /// <summary>
    /// A/B Test Framework for comparing Procedural vs Neural vs Hybrid animation performance.
    /// Singleton — attach to a single GameObject in the scene.
    /// </summary>
    public class ABTestFramework : MonoBehaviour
    {
        public enum TestMode { Procedural, Neural, Hybrid }

        [Header("Test Settings")]
        [SerializeField] TestMode _currentMode = TestMode.Hybrid;
        [SerializeField] float _defaultTestDuration = 30f;
        [SerializeField] bool _autoRunOnStart = false;

        [Header("References")]
        [SerializeField] NeuralAnimationMetrics _metrics;
        [SerializeField] PhysicsValidityChecker _validityChecker;

        // ──────────────────────────────────────────────
        //  Test Results
        // ──────────────────────────────────────────────

        [Serializable]
        public struct TestResult
        {
            public TestMode mode;
            public float duration;
            public float avgFPS;
            public float avgLatencyMs;
            public float avgValidity;
            public int policySwitchCount;
            public string timestamp;

            public string GetSummary()
            {
                return $"[{mode}] {duration:F1}s | FPS: {avgFPS:F1} | Lat: {avgLatencyMs:F2}ms | Validity: {avgValidity:P1} | Switches: {policySwitchCount}";
            }
        }

        List<TestResult> _results = new List<TestResult>();
        bool _isRunning;
        float _testTimer;
        float _fpsSum;
        int _fpsCount;
        int _switchStart;

        /// <summary>All completed test results.</summary>
        public IReadOnlyList<TestResult> Results => _results;

        /// <summary>Current test mode.</summary>
        public TestMode CurrentMode => _currentMode;

        /// <summary>Whether a test is currently running.</summary>
        public bool IsRunning => _isRunning;

        // ──────────────────────────────────────────────
        //  Singleton
        // ──────────────────────────────────────────────

        public static ABTestFramework Instance { get; private set; }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            if (_metrics == null)
                _metrics = GetComponent<NeuralAnimationMetrics>();
            if (_validityChecker == null)
                _validityChecker = GetComponent<PhysicsValidityChecker>();
        }

        void Start()
        {
            if (_autoRunOnStart)
                RunFullComparison(_defaultTestDuration);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_isRunning) return;
            _testTimer += Time.unscaledDeltaTime;

            if (_metrics != null)
            {
                _fpsSum += _metrics.CurrentFPS;
                _fpsCount++;
            }
        }

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Switch all active controllers to the given test mode.
        /// </summary>
        public void SwitchTestMode(TestMode mode)
        {
            _currentMode = mode;
            var controllers = FindObjectsOfType<HybridAnimationController>();
            foreach (var ctrl in controllers)
            {
                switch (mode)
                {
                    case TestMode.Procedural:
                        ctrl.SetBaseWeights(1f, 0f);
                        ctrl.ClearAllPolicyOverrides();
                        break;
                    case TestMode.Neural:
                        ctrl.SetBaseWeights(0f, 1f);
                        foreach (var p in System.Enum.GetValues(typeof(NeuralAnimationController.PolicyType)))
                            ctrl.SetPolicyOverride((NeuralAnimationController.PolicyType)p, true);
                        break;
                    case TestMode.Hybrid:
                        // Restore to phase-based config
                        if (ProgressiveRolloutManager.Instance != null)
                            ProgressiveRolloutManager.Instance.ConfigureHybridController(ctrl);
                        break;
                }
            }
            Debug.Log($"[ABTestFramework] Switched to {mode}");
        }

        /// <summary>
        /// Run a single test for the specified duration.
        /// </summary>
        public TestResult RunTest(TestMode mode, float duration)
        {
            SwitchTestMode(mode);
            _isRunning = true;
            _testTimer = 0f;
            _fpsSum = 0f;
            _fpsCount = 0;

            if (_metrics != null) _metrics.ResetMetrics();
            if (_validityChecker != null) _validityChecker.ResetScore();

            // Wait for duration (this runs over multiple frames via Update)
            var startTime = Time.time;
            var waitUntil = startTime + duration;
            var result = new TestResult
            {
                mode = mode,
                duration = duration,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            // We can't actually block here — use coroutine
            StartCoroutine(TestCoroutine(mode, duration));
            return result;
        }

        System.Collections.IEnumerator TestCoroutine(TestMode mode, float duration)
        {
            yield return new WaitForSecondsRealtime(duration);

            _isRunning = false;

            var result = new TestResult
            {
                mode = mode,
                duration = duration,
                avgFPS = _fpsCount > 0 ? _fpsSum / _fpsCount : 0f,
                avgLatencyMs = _metrics != null ? _metrics.AverageInferenceLatencyMs : 0f,
                avgValidity = _validityChecker != null ? _validityChecker.AverageValidityScore : 1f,
                policySwitchCount = _metrics != null ? _metrics.TotalPolicySwitches : 0,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            _results.Add(result);
            Debug.Log($"[ABTestFramework] Test complete: {result.GetSummary()}");
        }

        /// <summary>
        /// Run all 3 modes sequentially for comparison.
        /// </summary>
        public void RunFullComparison(float durationPerMode)
        {
            StartCoroutine(FullComparisonCoroutine(durationPerMode));
        }

        System.Collections.IEnumerator FullComparisonCoroutine(float durationPerMode)
        {
            _results.Clear();
            Debug.Log("[ABTestFramework] Starting full A/B comparison...");

            // Phase 1: Procedural
            RunTest(TestMode.Procedural, durationPerMode);
            yield return new WaitForSecondsRealtime(durationPerMode + 0.5f);

            // Phase 2: Neural
            RunTest(TestMode.Neural, durationPerMode);
            yield return new WaitForSecondsRealtime(durationPerMode + 0.5f);

            // Phase 3: Hybrid
            RunTest(TestMode.Hybrid, durationPerMode);
            yield return new WaitForSecondsRealtime(durationPerMode + 0.5f);

            Debug.Log(GetComparisonReport());
        }

        /// <summary>
        /// Get formatted comparison report of all completed tests.
        /// </summary>
        public string GetComparisonReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== A/B Test Comparison Report ===");
            sb.AppendLine($"Total tests: {_results.Count}");
            sb.AppendLine();

            foreach (var r in _results)
            {
                sb.AppendLine($"  {r.GetSummary()}");
            }

            if (_results.Count >= 2)
            {
                sb.AppendLine();
                sb.AppendLine("--- Rankings ---");
                // Best FPS
                _results.Sort((a, b) => b.avgFPS.CompareTo(a.avgFPS));
                sb.AppendLine($"Best FPS: {_results[0].mode} ({_results[0].avgFPS:F1})");
                // Best latency
                _results.Sort((a, b) => a.avgLatencyMs.CompareTo(b.avgLatencyMs));
                sb.AppendLine($"Best Latency: {_results[0].mode} ({_results[0].avgLatencyMs:F2}ms)");
                // Best validity
                _results.Sort((a, b) => b.avgValidity.CompareTo(a.avgValidity));
                sb.AppendLine($"Best Validity: {_results[0].mode} ({_results[0].avgValidity:P1})");
            }

            return sb.ToString();
        }
    }
}