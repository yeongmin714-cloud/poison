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

        [Header("Memory Optimization")]
        [SerializeField] bool _enableWorkerAutoUnload = true;
        [SerializeField, Range(10f, 300f)] float _workerIdleTimeoutSec = 60f; // Unload idle workers after this time
        [SerializeField, Range(10f, 300f)] float _cleanupIntervalSec = 60f; // Periodic cleanup interval
        [SerializeField] bool _lodBasedUnload = true; // Unload workers for LOD3 (culled) policies

        [Header("Debug")]
        [SerializeField] bool _logBatchStats = false;
        [SerializeField] bool _logMemoryStats = false;

        // Active batch groups: PolicyType -> list of pending controllers
        Dictionary<NeuralAnimationController.PolicyType, List<BatchRequest>> _pendingBatches = new();
        Dictionary<NeuralAnimationController.PolicyType, float> _batchStartTime = new();
        
        // Worker cache per policy with last used time
        Dictionary<NeuralAnimationController.PolicyType, Worker> _workerCache = new();
        Dictionary<NeuralAnimationController.PolicyType, float> _workerLastUsedTime = new();
        
        // Active policies tracking (from controllers)
        HashSet<NeuralAnimationController.PolicyType> _activePolicies = new();
        Dictionary<NeuralAnimationController.PolicyType, int> _policyUsageCount = new();
        
        // LOD tracking per policy
        Dictionary<NeuralAnimationController.PolicyType, int> _policyMaxLOD = new();

        // Stats
        int _totalBatchesProcessed;
        int _totalItemsProcessed;
        float _totalInferenceTimeMs;
        
        // Cleanup timer
        float _lastCleanupTime;
        int _workersUnloadedCount;
        int _totalMemoryFreedMB;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _lastCleanupTime = Time.realtimeSinceStartup;
        }

        void OnDestroy()
        {
            foreach (var worker in _workerCache.Values)
                worker?.Dispose();
            _workerCache.Clear();
        }

        void Update()
        {
            if (!_enableBatchInference) return;

            float now = Time.realtimeSinceStartup;
            
            // Check for batch timeouts
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

            // Periodic memory cleanup
            if (_enableWorkerAutoUnload && now - _lastCleanupTime >= _cleanupIntervalSec)
            {
                PerformMemoryCleanup(now);
                _lastCleanupTime = now;
            }

            // Update worker last used time for active policies
            foreach (var policy in _activePolicies)
            {
                if (_workerCache.ContainsKey(policy))
                    _workerLastUsedTime[policy] = now;
            }
        }

        /// <summary>
        /// Register a policy as currently active (called by NeuralAnimationController)
        /// </summary>
        public void RegisterActivePolicy(NeuralAnimationController.PolicyType policy, int lodLevel = 0)
        {
            _activePolicies.Add(policy);
            _policyUsageCount[policy] = _policyUsageCount.GetValueOrDefault(policy, 0) + 1;
            _policyMaxLOD[policy] = Mathf.Max(_policyMaxLOD.GetValueOrDefault(policy, 0), lodLevel);
            
            // Update last used time
            _workerLastUsedTime[policy] = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Unregister a policy as inactive (called by NeuralAnimationController when disabled)
        /// </summary>
        public void UnregisterActivePolicy(NeuralAnimationController.PolicyType policy)
        {
            if (_policyUsageCount.TryGetValue(policy, out int count))
            {
                count = Mathf.Max(0, count - 1);
                if (count == 0)
                {
                    _policyUsageCount.Remove(policy);
                    _activePolicies.Remove(policy);
                }
                else
                {
                    _policyUsageCount[policy] = count;
                }
            }
        }

        /// <summary>
        /// Periodic memory cleanup - unload idle workers
        /// </summary>
        void PerformMemoryCleanup(float now)
        {
            var toRemove = new List<NeuralAnimationController.PolicyType>();
            int workersUnloaded = 0;
            long memoryFreedBytes = 0;

            foreach (var kvp in _workerCache)
            {
                var policy = kvp.Key;
                var worker = kvp.Value;

                if (worker == null)
                {
                    toRemove.Add(policy);
                    continue;
                }

                bool shouldUnload = false;
                string reason = "";

                // Check idle timeout
                if (_workerLastUsedTime.TryGetValue(policy, out float lastUsed))
                {
                    float idleTime = now - lastUsed;
                    if (idleTime >= _workerIdleTimeoutSec)
                    {
                        shouldUnload = true;
                        reason = $"idle {idleTime:F1}s > timeout {_workerIdleTimeoutSec}s";
                    }
                }

                // Check LOD-based unload
                if (_lodBasedUnload && !shouldUnload)
                {
                    if (_policyMaxLOD.TryGetValue(policy, out int maxLOD) && maxLOD >= 3)
                    {
                        shouldUnload = true;
                        reason = $"LOD3 culled";
                    }
                }

                // Check if policy is still active
                if (!shouldUnload && !_activePolicies.Contains(policy))
                {
                    // Only unload if not used recently
                    if (_workerLastUsedTime.TryGetValue(policy, out lastUsed))
                    {
                        float idleTime = now - lastUsed;
                        if (idleTime >= _workerIdleTimeoutSec)
                        {
                            shouldUnload = true;
                            reason = $"policy inactive, idle {idleTime:F1}s";
                        }
                    }
                }

                if (shouldUnload)
                {
                    try
                    {
                        // Estimate memory (rough approximation)
                        memoryFreedBytes += EstimateWorkerMemory(worker);
                        worker?.Dispose();
                        workersUnloaded++;
                        if (_logMemoryStats)
                            Debug.Log($"[BatchInferenceManager] Unloaded worker for {policy}: {reason}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BatchInferenceManager] Failed to unload worker for {policy}: {e.Message}");
                    }
                    toRemove.Add(policy);
                }
            }

            foreach (var policy in toRemove)
            {
                _workerCache.Remove(policy);
                _workerLastUsedTime.Remove(policy);
                _policyMaxLOD.Remove(policy);
            }

            _workersUnloadedCount += workersUnloaded;
            _totalMemoryFreedMB += (int)(memoryFreedBytes / (1024 * 1024));

            if (_logMemoryStats && workersUnloaded > 0)
            {
                float memMB = memoryFreedBytes / (1024f * 1024f);
                Debug.Log($"[BatchInferenceManager] Memory cleanup: unloaded {workersUnloaded} workers, freed ~{memMB:F1} MB");
                SendTelegramMemoryNotification(workersUnloaded, memMB);
            }
        }

        long EstimateWorkerMemory(Worker worker)
        {
            // Rough estimation: worker + model weights + tensors
            // Typical ONNX model ~300KB, worker overhead ~50MB
            return 50 * 1024 * 1024; // ~50MB per worker
        }

        void SendTelegramMemoryNotification(int workersUnloaded, float memFreedMB)
        {
            // Send notification via existing telegram system
            // This would need integration with the telegram notification system
            if (TelegramNotifier.Instance != null)
            {
                TelegramNotifier.Instance.Send($"🧠 Worker Pool Cleanup: {workersUnloaded} workers unloaded, ~{memFreedMB:F1} MB freed");
            }
        }

        // ... rest of existing methods ...

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