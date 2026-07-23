using System;
using System.Collections.Generic;
using System.Diagnostics;
using UDebug = UnityEngine.Debug;
using UnityEngine;

namespace ProjectName.Systems.Animation.Neural.Evaluation
{
    /// <summary>
    /// Runtime metrics collector for Neural Animation performance evaluation.
    /// Attach to any GameObject with NeuralAnimationController to track inference metrics.
    /// </summary>
    public class NeuralAnimationMetrics : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] bool _enableLogging = false;
        [SerializeField, Range(1f, 60f)] float _logIntervalSeconds = 10f;

        [Header("Live Readings (Read Only)")]
        [SerializeField] float _currentInferenceLatencyMs;
        [SerializeField] float _averageInferenceLatencyMs;
        [SerializeField] float _currentFPS;
        [SerializeField] int _totalPolicySwitches;
        [SerializeField] int _totalLODChanges;

        // ──────────────────────────────────────────────
        //  Public Properties
        // ──────────────────────────────────────────────

        /// <summary>Current frames per second.</summary>
        public float CurrentFPS => _currentFPS;

        /// <summary>Average inference latency in milliseconds.</summary>
        public float AverageInferenceLatencyMs => _averageInferenceLatencyMs;

        /// <summary>Total policy switches counted.</summary>
        public int TotalPolicySwitches => _totalPolicySwitches;

        /// <summary>Current inference latency in milliseconds.</summary>
        public float CurrentInferenceLatencyMs => _currentInferenceLatencyMs;

        // ──────────────────────────────────────────────
        //  Metrics State
        // ──────────────────────────────────────────────

        Stopwatch _inferenceStopwatch = new Stopwatch();
        float _logTimer;
        int _frameCount;
        float _fpsTimer;
        float _latencySum;
        int _latencyCount;
        NeuralAnimationController _controller;
        int _lastLODLevel;
        int _lastPolicy;

        public Dictionary<NeuralAnimationController.PolicyType, int> PolicyInferenceCounts { get; } =
            new Dictionary<NeuralAnimationController.PolicyType, int>();

        // ──────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _controller = GetComponent<NeuralAnimationController>();
            if (_controller == null)
                UDebug.LogWarning("[NeuralAnimationMetrics] No NeuralAnimationController found");
        }

        void Start()
        {
            _lastLODLevel = _controller != null ? _controller.CurrentLODLevel : 0;
        }

        void Update()
        {
            // FPS counter
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 1f)
            {
                _currentFPS = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0f;
            }

            // Track LOD changes
            if (_controller != null)
            {
                int currentLOD = _controller.CurrentLODLevel;
                if (currentLOD != _lastLODLevel)
                {
                    _totalLODChanges++;
                    _lastLODLevel = currentLOD;
                }

                int currentPolicy = (int)_controller.ActivePolicy;
                if (currentPolicy != _lastPolicy && _lastPolicy != 0)
                {
                    _totalPolicySwitches++;
                }
                _lastPolicy = currentPolicy;
            }

            // Periodic logging
            if (_enableLogging)
            {
                _logTimer += Time.unscaledDeltaTime;
                if (_logTimer >= _logIntervalSeconds)
                {
                    _logTimer = 0f;
                    UDebug.Log(GetMetricsReport());
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Call this around each inference call to measure latency.
        /// </summary>
        public void BeginInferenceTiming()
        {
            _inferenceStopwatch.Reset();
            _inferenceStopwatch.Start();
        }

        /// <summary>
        /// Call after inference completes.
        /// </summary>
        public void EndInferenceTiming(NeuralAnimationController.PolicyType policy)
        {
            _inferenceStopwatch.Stop();
            float ms = (float)_inferenceStopwatch.Elapsed.TotalMilliseconds;
            _currentInferenceLatencyMs = ms;
            _latencySum += ms;
            _latencyCount++;
            _averageInferenceLatencyMs = _latencySum / Mathf.Max(_latencyCount, 1);

            if (!PolicyInferenceCounts.ContainsKey(policy))
                PolicyInferenceCounts[policy] = 0;
            PolicyInferenceCounts[policy]++;
        }

        /// <summary>
        /// Get a formatted metrics report.
        /// </summary>
        public string GetMetricsReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Neural Animation Metrics Report ===");
            sb.AppendLine($"FPS: {_currentFPS:F1}");
            sb.AppendLine($"Inference Latency: {_averageInferenceLatencyMs:F3}ms avg (current: {_currentInferenceLatencyMs:F3}ms)");
            sb.AppendLine($"Policy Switches: {_totalPolicySwitches}");
            sb.AppendLine($"LOD Changes: {_totalLODChanges}");
            sb.AppendLine("--- Policy Inference Counts ---");
            foreach (var kvp in PolicyInferenceCounts)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            if (_controller != null)
            {
                sb.AppendLine($"Current Policy: {_controller.ActivePolicy}");
                sb.AppendLine($"Current LOD: {_controller.CurrentLODLevel}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Reset all collected metrics.
        /// </summary>
        public void ResetMetrics()
        {
            _latencySum = 0f;
            _latencyCount = 0;
            _averageInferenceLatencyMs = 0f;
            _currentInferenceLatencyMs = 0f;
            _totalPolicySwitches = 0;
            _totalLODChanges = 0;
            PolicyInferenceCounts.Clear();
        }

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (_enableLogging) return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.8f,
                $"FPS: {_currentFPS:F0} | Lat: {_averageInferenceLatencyMs:F2}ms");
#endif
        }
    }
}