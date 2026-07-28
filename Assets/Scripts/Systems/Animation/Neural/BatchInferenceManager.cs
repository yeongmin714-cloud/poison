using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.InferenceEngine;

namespace ProjectName.Systems.Animation.Neural
{
    /// <summary>
    /// Global Batch Inference Manager for Neural Animation.
    /// Groups avatars using the same model and runs batched inference for CPU efficiency.
    /// Optimal batch size on CPU: 4-8 (larger batches don't scale well without GPU).
    /// </summary>
    public class BatchInferenceManager : MonoBehaviour
    {
        public static BatchInferenceManager Instance { get; private set; }

        [Header("Batch Settings")]
        [SerializeField, Range(1, 16)] int _maxBatchSize = 8;
        [SerializeField] bool _enableBatchInference = true;
        [SerializeField, Range(0.001f, 0.1f)] float _batchTimeoutMs = 2f; // Max wait time to fill batch

        [Header("Debug")]
        [SerializeField] bool _logBatchStats = false;

        // Active batch groups: PolicyType -> list of pending controllers
        Dictionary<NeuralAnimationController.PolicyType, List<BatchRequest>> _pendingBatches = new();
        Dictionary<NeuralAnimationController.PolicyType, float> _batchStartTime = new();
        
        // Worker cache per policy
        Dictionary<NeuralAnimationController.PolicyType, Worker> _workerCache = new();
        
        // Stats
        int _totalBatchesProcessed;
        int _totalItemsProcessed;
        float _totalInferenceTimeMs;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            foreach (var worker in _workerCache.Values)
                worker?.Dispose();
            _workerCache.Clear();
        }

        /// <summary>
        /// Request batched inference. Returns true if queued, false if executed immediately or failed.
        /// </summary>
        public bool RequestBatchInference(
            NeuralAnimationController.PolicyType policy,
            Model model,
            BackendType backend,
            NativeArray<float> observation, // Size: observationDim
            float[] actionOutput,           // Size: actionDim
            int observationDim,
            int actionDim,
            NeuralAnimationController controller) // For callback
        {
            if (!_enableBatchInference || model == null)
                return false;

            var request = new BatchRequest
            {
                observation = observation,
                actionOutput = actionOutput,
                observationDim = observationDim,
                actionDim = actionDim,
                controller = controller,
                policy = policy
            };

            if (!_pendingBatches.TryGetValue(policy, out var list))
            {
                list = new List<BatchRequest>(_maxBatchSize);
                _pendingBatches[policy] = list;
            }

            list.Add(request);
            _batchStartTime[policy] = Time.realtimeSinceStartup;

            // Execute immediately if batch is full
            if (list.Count >= _maxBatchSize)
            {
                ExecuteBatch(policy, model, backend);
                return true;
            }

            return true; // Queued
        }

        void Update()
        {
            if (!_enableBatchInference) return;

            // Check for batch timeouts
            float now = Time.realtimeSinceStartup;
            var policiesToCheck = new List<NeuralAnimationController.PolicyType>(_pendingBatches.Keys);
            
            foreach (var policy in policiesToCheck)
            {
                if (!_pendingBatches.TryGetValue(policy, out var list)) continue;
                if (list.Count == 0) continue;

                float elapsed = (now - _batchStartTime[policy]) * 1000f; // ms
                if (elapsed >= _batchTimeoutMs || list.Count >= _maxBatchSize)
                {
                    // Need to get model from one of the controllers
                    if (list[0].controller != null && list[0].controller.TryGetPolicyModel(policy, out Model model))
                    {
                        ExecuteBatch(policy, model, list[0].controller._backendType);
                    }
                }
            }
        }

        void ExecuteBatch(NeuralAnimationController.PolicyType policy, Model model, BackendType backend)
        {
            if (!_pendingBatches.TryGetValue(policy, out var list) || list.Count == 0)
                return;

            int batchSize = list.Count;
            int obsDim = list[0].observationDim;
            int actDim = list[0].actionDim;

            var startTime = Time.realtimeSinceStartup;

            // Get or create worker
            if (!_workerCache.TryGetValue(policy, out Worker worker) || worker == null)
            {
                worker = WorkerFactory.CreateWorker(backend, model);
                _workerCache[policy] = worker;
            }

            try
            {
                // Prepare batched input tensor: [batch, 1, 1, obsDim]
                using (var inputTensor = new TensorFloat(new TensorShape(batchSize, 1, 1, obsDim)))
                {
                    // Fill batch
                    for (int i = 0; i < batchSize; i++)
                    {
                        var req = list[i];
                        for (int j = 0; j < obsDim; j++)
                        {
                            inputTensor[i, 0, 0, j] = req.observation[j];
                        }
                    }

                    // Execute batch inference
                    worker.Execute(inputTensor);
                    using (var outputTensor = worker.PeekOutput() as TensorFloat)
                    {
                        // Distribute results
                        int outputCount = math.min(outputTensor.shape.length, actDim);
                        for (int i = 0; i < batchSize; i++)
                        {
                            var req = list[i];
                            for (int j = 0; j < outputCount; j++)
                            {
                                req.actionOutput[j] = outputTensor[i, 0, 0, j];
                            }
                            // Notify controller
                            req.controller?.OnBatchInferenceComplete(req.policy);
                        }
                    }
                }

                _totalBatchesProcessed++;
                _totalItemsProcessed += batchSize;
                _totalInferenceTimeMs += (Time.realtimeSinceStartup - startTime) * 1000f;

                if (_logBatchStats)
                {
                    float avgTime = _totalInferenceTimeMs / _totalBatchesProcessed;
                    Debug.Log($"[BatchInference] Policy: {policy}, Batch: {batchSize}, Time: {(Time.realtimeSinceStartup - startTime)*1000f:.2f}ms, Avg: {avgTime:.2f}ms");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BatchInferenceManager] Batch inference failed for {policy}: {e.Message}");
                // Fallback to individual inference
                foreach (var req in list)
                {
                    req.controller?.RunSingleInference(req.policy);
                }
            }
            finally
            {
                list.Clear();
            }
        }

        /// <summary>
        /// Force flush all pending batches (e.g., on scene change)
        /// </summary>
        public void FlushAllBatches()
        {
            var policiesToCheck = new List<NeuralAnimationController.PolicyType>(_pendingBatches.Keys);
            
            foreach (var policy in policiesToCheck)
            {
                if (_pendingBatches.TryGetValue(policy, out var list) && list.Count > 0)
                {
                    if (list[0].controller != null && 
                        list[0].controller.TryGetPolicyModel(policy, out Model model))
                    {
                        ExecuteBatch(policy, model, list[0].controller._backendType);
                    }
                }
            }
        }

        /// <summary>
        /// Get worker for policy (create if needed)
        /// </summary>
        public Worker GetOrCreateWorker(NeuralAnimationController.PolicyType policy, Model model, BackendType backend)
        {
            if (_workerCache.TryGetValue(policy, out var worker) && worker != null)
                return worker;

            worker = WorkerFactory.CreateWorker(backend, model);
            _workerCache[policy] = worker;
            return worker;
        }

        /// <summary>
        /// Release unused workers to free memory
        /// </summary>
        public void ReleaseUnusedWorkers(HashSet<NeuralAnimationController.PolicyType> activePolicies)
        {
            var toRemove = new List<NeuralAnimationController.PolicyType>();
            foreach (var kvp in _workerCache)
            {
                if (!activePolicies.Contains(kvp.Key))
                {
                    kvp.Value?.Dispose();
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var policy in toRemove)
                _workerCache.Remove(policy);
        }

        public struct BatchRequest
        {
            public NativeArray<float> observation;
            public float[] actionOutput;
            public int observationDim;
            public int actionDim;
            public NeuralAnimationController controller;
            public NeuralAnimationController.PolicyType policy;
        }

        // Stats
        public int TotalBatchesProcessed => _totalBatchesProcessed;
        public int TotalItemsProcessed => _totalItemsProcessed;
        public float AverageInferenceTimeMs => _totalBatchesProcessed > 0 ? _totalInferenceTimeMs / _totalBatchesProcessed : 0f;
    }
}