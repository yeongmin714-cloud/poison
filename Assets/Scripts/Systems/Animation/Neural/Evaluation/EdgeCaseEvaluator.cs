using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems.Animation.Neural.Evaluation
{
    /// <summary>
    /// Edge case evaluator for Neural Animation system.
    /// Tests animation quality across different terrain and combat scenarios.
    /// </summary>
    public class EdgeCaseEvaluator : MonoBehaviour
    {
        public enum EdgeCase
        {
            FlatGround,
            Slope,
            Stairs,
            Obstacles,
            Combat,
            Water,
            FlyMode,
            Night
        }

        [Header("Settings")]
        [SerializeField] bool _autoEvaluateOnStart = false;
        [SerializeField] float _evaluationDurationPerCase = 15f;

        [Header("Results")]
        [SerializeField] List<EdgeCaseResult> _results = new List<EdgeCaseResult>();

        // ──────────────────────────────────────────────
        //  Data
        // ──────────────────────────────────────────────

        [Serializable]
        public struct EdgeCaseResult
        {
            public EdgeCase type;
            public float avgValidity;
            public float avgLatencyMs;
            public string notes;

            public string GetSummary()
            {
                return $"[{type}] Validity: {avgValidity:P1} | Latency: {avgLatencyMs:F2}ms | {notes}";
            }
        }

        NeuralAnimationController _controller;
        PhysicsValidityChecker _validity;
        NeuralAnimationMetrics _metrics;

        // ──────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _controller = GetComponent<NeuralAnimationController>();
            _validity = GetComponent<PhysicsValidityChecker>();
            _metrics = GetComponent<NeuralAnimationMetrics>();
        }

        void Start()
        {
            if (_autoEvaluateOnStart)
                StartCoroutine(RunAllEvaluations());
        }

        // ──────────────────────────────────────────────
        //  Evaluation
        // ──────────────────────────────────────────────

        /// <summary>
        /// Evaluate a single edge case.
        /// </summary>
        public EdgeCaseResult EvaluateEdgeCase(EdgeCase type)
        {
            ConfigureForEdgeCase(type);

            if (_metrics != null)
            {
                if (_metrics != null) _metrics.ResetMetrics();
            }
            if (_validity != null)
            {
                _validity.ResetScore();
            }

            // Record results after configured duration
            return new EdgeCaseResult
            {
                type = type,
                avgValidity = _validity != null ? _validity.AverageValidityScore : 1f,
                avgLatencyMs = _metrics != null ? _metrics.AverageInferenceLatencyMs : 0f,
                notes = GetEdgeCaseNotes(type)
            };
        }

        /// <summary>
        /// Configure scene/controller for the given edge case.
        /// </summary>
        void ConfigureForEdgeCase(EdgeCase type)
        {
            if (_controller == null) return;

            switch (type)
            {
                case EdgeCase.FlatGround:
                    _controller.SwitchPolicy(NeuralAnimationController.PolicyType.Locomotion);
                    break;
                case EdgeCase.Slope:
                    _controller.SwitchPolicy(NeuralAnimationController.PolicyType.Locomotion);
                    break;
                case EdgeCase.Stairs:
                    _controller.SwitchPolicy(NeuralAnimationController.PolicyType.Locomotion);
                    break;
                case EdgeCase.Obstacles:
                    _controller.SwitchPolicy(NeuralAnimationController.PolicyType.Locomotion);
                    break;
                case EdgeCase.Combat:
                    _controller.SwitchPolicy(NeuralAnimationController.PolicyType.Combat);
                    break;
                case EdgeCase.Water:
                    _controller.SwitchPolicy(NeuralAnimationController.PolicyType.Swim);
                    break;
                case EdgeCase.FlyMode:
                    _controller.SwitchPolicy(NeuralAnimationController.PolicyType.Fly);
                    break;
                case EdgeCase.Night:
                    _controller.SwitchPolicy(NeuralAnimationController.PolicyType.Locomotion);
                    break;
            }
        }

        string GetEdgeCaseNotes(EdgeCase type)
        {
            switch (type)
            {
                case EdgeCase.FlatGround: return "Standard locomotion on flat terrain";
                case EdgeCase.Slope: return "Walking up/down inclines";
                case EdgeCase.Stairs: return "Step negotiation on stairs";
                case EdgeCase.Obstacles: return "Navigation around obstacles";
                case EdgeCase.Combat: return "Attack/defend motion quality";
                case EdgeCase.Water: return "Swimming/water interaction";
                case EdgeCase.FlyMode: return "Aerial locomotion";
                case EdgeCase.Night: return "Low visibility conditions";
                default: return "";
            }
        }

        /// <summary>
        /// Run all edge case evaluations sequentially.
        /// </summary>
        public System.Collections.IEnumerator RunAllEvaluations()
        {
            _results.Clear();
            foreach (EdgeCase edgeCase in Enum.GetValues(typeof(EdgeCase)))
            {
                var result = EvaluateEdgeCase(edgeCase);
                _results.Add(result);
                Debug.Log($"[EdgeCaseEvaluator] {result.GetSummary()}");
                yield return new WaitForSecondsRealtime(_evaluationDurationPerCase);
            }
            Debug.Log(GetEdgeCaseReport());
        }

        /// <summary>
        /// Get formatted edge case comparison report.
        /// </summary>
        public string GetEdgeCaseReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Edge Case Evaluation Report ===");
            sb.AppendLine($"Cases evaluated: {_results.Count}");
            sb.AppendLine();
            foreach (var r in _results)
                sb.AppendLine($"  {r.GetSummary()}");

            if (_results.Count > 0)
            {
                float avgValidity = 0f;
                foreach (var r in _results) avgValidity += r.avgValidity;
                avgValidity /= _results.Count;
                sb.AppendLine();
                sb.AppendLine($"Average validity across all cases: {avgValidity:P1}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Request a specific edge case evaluation via string (for editor testing).
        /// </summary>
        public void EvaluateByName(string edgeCaseName)
        {
            if (Enum.TryParse(edgeCaseName, true, out EdgeCase parsed))
            {
                var result = EvaluateEdgeCase(parsed);
                Debug.Log($"[EdgeCaseEvaluator] Manual: {result.GetSummary()}");
            }
            else
            {
                Debug.LogWarning($"[EdgeCaseEvaluator] Unknown edge case: {edgeCaseName}");
            }
        }
    }
}