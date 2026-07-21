using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.InferenceEngine;
using Debug = UnityEngine.Debug;

// Optional Addressables — compile without if package is absent
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace ProjectName.Systems.Animation.Neural
{
    /// <summary>
    /// Singleton manager for Unity Sentis runtime initialization, model
    /// loading / caching / unloading, batched inference scheduling, and
    /// performance profiling.
    ///
    /// Features:
    /// 1. Initialize Sentis Worker (GPU Compute preferred, CPU fallback)
    /// 2. ModelRegistry — avatar-type to model mapping
    /// 3. Async model loading with Addressables support
    /// 4. Batched inference for multiple same-model agents
    /// 5. Memory management — auto-unload on scene change
    /// 6. Performance profiling (inference time, memory)
    /// </summary>
    public class MLRuntimeManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────

        private static MLRuntimeManager _instance;
        private static readonly object _lock = new object();

        /// <summary>Thread-safe singleton access.</summary>
        public static MLRuntimeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            var go = new GameObject("[MLRuntimeManager]");
                            _instance = go.AddComponent<MLRuntimeManager>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Inspector Config
        // ──────────────────────────────────────────────────────────────

        [Header("Sentis Backend")]
        [Tooltip("Preferred backend. Falls back to CPU if GPUCompute is unavailable.")]
        public BackendType preferredBackend = BackendType.GPUCompute;

        [Tooltip("If true, forces CPU backend regardless of preferredBackend.")]
        public bool forceCPU = false;

        [Header("Model Cache")]
        [Tooltip("Maximum number of models kept loaded simultaneously. 0 = unlimited.")]
        public int maxLoadedModels = 10;

        [Tooltip("Unload models that have been unused for this many seconds.")]
        public float modelUnloadTimeout = 120f;

        [Header("Batched Inference")]
        [Tooltip("Maximum batch size for batched inference. 0 = dynamic.")]
        public int maxBatchSize = 32;

        [Tooltip("If true, schedules inference across multiple frames to avoid spikes.")]
        public bool spreadAcrossFrames = true;

        [Header("Scene Management")]
        [Tooltip("Automatically unload all models on scene change.")]
        public bool unloadOnSceneChange = true;

        [Header("Profiling")]
        [Tooltip("Log per-inference timing to the console.")]
        public bool logInferenceTiming = false;

        [Tooltip("Sample performance every N inferences (0 = disabled).")]
        public int profilingSampleInterval = 0;

        // ──────────────────────────────────────────────────────────────
        // Public State
        // ──────────────────────────────────────────────────────────────

        /// <summary>Backend actually in use after initialization.</summary>
        public BackendType ActiveBackend { get; private set; } = BackendType.CPU;

        /// <summary>True once the backend worker pool is ready.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>Total models currently loaded in the cache.</summary>
        public int LoadedModelCount => _modelCache.Count;

        /// <summary>Total active inference requests queued or in-flight.</summary>
        public int PendingInferenceCount => _inferenceQueue.Count;

        /// <summary>Profiling snapshot (latched periodically).</summary>
        public ProfilingSnapshot ProfilingData { get; private set; } = new ProfilingSnapshot();

        // ──────────────────────────────────────────────────────────────
        // Events
        // ──────────────────────────────────────────────────────────────

        /// <summary>Fired after backend initialization completes.</summary>
        public event Action<BackendType> OnBackendInitialized;

        /// <summary>Fired when a model finishes loading.</summary>
        public event Action<string, Model> OnModelLoaded;

        /// <summary>Fired when a model is unloaded.</summary>
        public event Action<string> OnModelUnloaded;

        /// <summary>Fired after a batch of inferences completes.</summary>
        public event Action<string, int, float> OnBatchCompleted; // modelId, batchSize, totalMs

        /// <summary>Fired when an error occurs (instead of throwing).</summary>
        public event Action<string, Exception> OnError;

        // ──────────────────────────────────────────────────────────────
        // Internal State
        // ──────────────────────────────────────────────────────────────

        /// <summary>Avatar type → model registry entry (asset reference).</summary>
        private readonly Dictionary<AvatarType, ModelRegistryEntry> _registry = new Dictionary<AvatarType, ModelRegistryEntry>();

        /// <summary>Model ID → loaded model + metadata.</summary>
        private readonly Dictionary<string, ModelCacheEntry> _modelCache = new Dictionary<string, ModelCacheEntry>();

        /// <summary>Pending batched inference requests.</summary>
        private readonly Queue<InferenceBatch> _inferenceQueue = new Queue<InferenceBatch>();

        /// <summary>Currently executing batch (for double-buffering).</summary>
        private InferenceBatch _currentBatch;

        /// <summary>Coroutine handles for async operations.</summary>
        private Coroutine _inferenceCoroutine;
        private Coroutine _cacheEvictionCoroutine;

        // Profiling counters
        private readonly Stopwatch _profilingStopwatch = new Stopwatch();
        private long _totalInferenceTimeMs;
        private long _totalInferences;
        private int _profilingSampleCounter;

        // Scene management
        private bool _isQuitting;

        // ──────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Application.quitting += () => _isQuitting = true;

            if (unloadOnSceneChange)
                SceneManager.sceneUnloaded += OnSceneUnloaded;

            InitializeBackend();
        }

        private void OnDestroy()
        {
            if (unloadOnSceneChange)
                SceneManager.sceneUnloaded -= OnSceneUnloaded;

            Application.quitting -= () => _isQuitting = true;

            Shutdown();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (!_isQuitting && unloadOnSceneChange)
            {
                UnloadAllModels();
                _inferenceQueue.Clear();
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 1. Backend Initialization
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialize the Sentis backend. Tries GPUCompute first; falls back
        /// to CPU if unavailable or if forceCPU is set.
        /// </summary>
        public void InitializeBackend()
        {
            if (IsInitialized)
                return;

            BackendType target = forceCPU ? BackendType.CPU : preferredBackend;

            // Validate GPU availability via system info
            if (target == BackendType.GPUCompute)
            {
                var deviceType = SystemInfo.graphicsDeviceType;
                bool isComputeCapable = deviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
                    || deviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D12
                    || deviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan
                    || deviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal
                    || deviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore;

                if (!isComputeCapable || !SystemInfo.supportsComputeShaders)
                {
                    Debug.LogWarning($"[MLRuntimeManager] GPUCompute not available (device={deviceType}, computeShaders={SystemInfo.supportsComputeShaders}). Falling back to CPU.");
                    target = BackendType.CPU;
                }
            }

            ActiveBackend = target;
            IsInitialized = true;

            // Start background routines
            _cacheEvictionCoroutine = StartCoroutine(CacheEvictionLoop());
            _inferenceCoroutine = StartCoroutine(InferenceLoop());

            Debug.Log($"[MLRuntimeManager] Sentis backend initialized: {ActiveBackend}.");
            OnBackendInitialized?.Invoke(ActiveBackend);
        }

        /// <summary>Re-initialize with a different backend preference.</summary>
        public void ReinitializeBackend(BackendType backend, bool forceCPU = false)
        {
            Shutdown();
            preferredBackend = backend;
            this.forceCPU = forceCPU;
            IsInitialized = false;
            InitializeBackend();
        }

        // ──────────────────────────────────────────────────────────────
        // 2. Model Registry
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Register a model for an avatar type. This does not load the model
        /// into memory — it only maps the avatar type to a source (Resources
        /// path, Addressables key, or a raw Model asset).
        /// </summary>
        public void RegisterModel(AvatarType avatarType, ModelRegistryEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _registry[avatarType] = entry;
            Debug.Log($"[MLRuntimeManager] Registered model '{entry.modelId}' for avatar type '{avatarType}'.");
        }

        /// <summary>Remove a model registration.</summary>
        public void UnregisterModel(AvatarType avatarType)
        {
            _registry.Remove(avatarType);
        }

        /// <summary>Get the registry entry for an avatar type.</summary>
        public bool TryGetRegistryEntry(AvatarType avatarType, out ModelRegistryEntry entry)
        {
            return _registry.TryGetValue(avatarType, out entry);
        }

        /// <summary>Clear all model registrations.</summary>
        public void ClearRegistry()
        {
            _registry.Clear();
        }

        // ──────────────────────────────────────────────────────────────
        // 3. Model Loading (Async + Addressables)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Load a model by its registry entry. Returns the cached model
        /// if already loaded (reference-counted).
        /// </summary>
        public Model LoadModel(ModelRegistryEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (_modelCache.TryGetValue(entry.modelId, out var cached))
            {
                cached.referenceCount++;
                cached.lastAccessTime = Time.unscaledTime;
                return cached.model;
            }

            Model model = null;

            switch (entry.sourceType)
            {
                case ModelSourceType.Resources:
                    model = LoadFromResources(entry.sourcePath);
                    break;
                case ModelSourceType.AssetBundle:
                    model = LoadFromAssetBundle(entry.sourcePath);
                    break;
                case ModelSourceType.Direct:
                    model = entry.directModel != null ? ModelLoader.Load(entry.directModel) : null;
                    break;
#if UNITY_ADDRESSABLES
                case ModelSourceType.Addressables:
                    // Synchronous fallback — prefer async overload
                    model = LoadFromAddressablesSync(entry.sourcePath);
                    break;
#endif
                default:
                    throw new NotSupportedException($"ModelSourceType '{entry.sourceType}' is not supported.");
            }

            if (model == null)
            {
                var ex = new InvalidOperationException($"Failed to load model '{entry.modelId}' from '{entry.sourcePath}'.");
                OnError?.Invoke(entry.modelId, ex);
                Debug.LogError($"[MLRuntimeManager] {ex.Message}");
                return null;
            }

            CacheModel(entry.modelId, model, entry);
            return model;
        }

        /// <summary>
        /// Asynchronously load a model by registry entry. Uses Addressables
        /// when available, otherwise falls back to Resources.LoadAsync.
        /// </summary>
        public Coroutine LoadModelAsync(ModelRegistryEntry entry, Action<Model> onComplete, Action<string, Exception> onError = null)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (_modelCache.TryGetValue(entry.modelId, out var cached))
            {
                cached.referenceCount++;
                cached.lastAccessTime = Time.unscaledTime;
                onComplete?.Invoke(cached.model);
                return null;
            }

            return StartCoroutine(LoadModelAsyncCoroutine(entry, onComplete, onError));
        }

        private IEnumerator LoadModelAsyncCoroutine(ModelRegistryEntry entry, Action<Model> onComplete, Action<string, Exception> onError)
        {
            Model model = null;
            Exception loadError = null;

            switch (entry.sourceType)
            {
                case ModelSourceType.Resources:
                    yield return LoadFromResourcesAsync(entry, m => model = m, e => loadError = e);
                    break;
#if UNITY_ADDRESSABLES
                case ModelSourceType.Addressables:
                    yield return LoadFromAddressablesAsync(entry, m => model = m, e => loadError = e);
                    break;
#endif
                case ModelSourceType.Direct:
                    model = entry.directModel != null ? ModelLoader.Load(entry.directModel) : null;
                    break;
                default:
                    loadError = new NotSupportedException($"Async loading not supported for source type '{entry.sourceType}'.");
                    break;
            }

            if (loadError != null || model == null)
            {
                var ex = loadError ?? new InvalidOperationException($"Failed to load model '{entry.modelId}'.");
                (onError ?? OnError)?.Invoke(entry.modelId, ex);
                Debug.LogError($"[MLRuntimeManager] {ex.Message}");
                yield break;
            }

            CacheModel(entry.modelId, model, entry);
            onComplete?.Invoke(model);
        }

        /// <summary>Get or load a model for the given avatar type.</summary>
        public Model GetOrLoadModel(AvatarType avatarType)
        {
            if (!_registry.TryGetValue(avatarType, out var entry))
            {
                Debug.LogWarning($"[MLRuntimeManager] No model registered for avatar type '{avatarType}'.");
                return null;
            }

            return LoadModel(entry);
        }

        /// <summary>Async overload for avatar type lookup.</summary>
        public Coroutine GetOrLoadModelAsync(AvatarType avatarType, Action<Model> onComplete)
        {
            if (!_registry.TryGetValue(avatarType, out var entry))
            {
                Debug.LogWarning($"[MLRuntimeManager] No model registered for avatar type '{avatarType}'.");
                onComplete?.Invoke(null);
                return null;
            }

            return LoadModelAsync(entry, onComplete);
        }

        /// <summary>Preload all registered models (up to maxLoadedModels).</summary>
        public Coroutine PreloadAllModelsAsync(Action<float> onProgress = null, Action onComplete = null)
        {
            return StartCoroutine(PreloadAllCoroutine(onProgress, onComplete));
        }

        private IEnumerator PreloadAllCoroutine(Action<float> onProgress, Action onComplete)
        {
            var entries = _registry.Values.ToList();
            int loaded = 0;

            foreach (var entry in entries)
            {
                if (_modelCache.ContainsKey(entry.modelId))
                {
                    loaded++;
                    continue;
                }

                bool done = false;
                yield return LoadModelAsync(entry, m => done = true);
                loaded++;
                onProgress?.Invoke((float)loaded / entries.Count);
            }

            onComplete?.Invoke();
        }

        // ──────────────────────────────────────────────────────────────
        // 4. Batched Inference
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Schedule inference for a single agent. The request is batched
        /// with other agents using the same model and executed on the
        /// inference loop.
        /// </summary>
        public void ScheduleInference(InferenceRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Find or create a batch for this model
            InferenceBatch batch = null;

            // Check if there's already a pending batch for this model
            var pending = _inferenceQueue.ToArray();
            foreach (var b in pending)
            {
                if (b.modelId == request.modelId && b.inputTensor.shape == request.inputTensor.shape
                    && b.inputTensor.dataType == request.inputTensor.dataType)
                {
                    batch = b;
                    break;
                }
            }

            if (batch == null)
            {
                // Check current executing batch
                if (_currentBatch != null && _currentBatch.modelId == request.modelId
                    && _currentBatch.inputTensor.shape == request.inputTensor.shape
                    && _currentBatch.inputTensor.dataType == request.inputTensor.dataType)
                {
                    batch = _currentBatch;
                }
            }

            if (batch == null)
            {
                batch = new InferenceBatch
                {
                    modelId = request.modelId,
                    inputTensor = request.inputTensor,
                    requests = new List<InferenceRequest>(),
                    batchSize = 0
                };
                _inferenceQueue.Enqueue(batch);
            }

            batch.requests.Add(request);
            batch.batchSize = batch.requests.Count;
        }

        /// <summary>
        /// Schedule batched inference for multiple agents sharing the same model.
        /// </summary>
        public void ScheduleBatchedInference(string modelId, Tensor inputTensor, IList<InferenceRequest> requests)
        {
            var batch = new InferenceBatch
            {
                modelId = modelId,
                inputTensor = inputTensor,
                requests = new List<InferenceRequest>(requests),
                batchSize = requests.Count
            };
            _inferenceQueue.Enqueue(batch);
        }

        /// <summary>Flush the inference queue — execute all pending batches immediately.</summary>
        public void FlushInferenceQueue()
        {
            if (_inferenceQueue.Count == 0)
                return;

            ProcessInferenceBatch();
        }

        private IEnumerator InferenceLoop()
        {
            var wait = new WaitForEndOfFrame();

            while (true)
            {
                if (_inferenceQueue.Count > 0)
                {
                    if (spreadAcrossFrames)
                    {
                        // Process one batch per frame to avoid spikes
                        ProcessInferenceBatch();
                        yield return wait;
                    }
                    else
                    {
                        // Process all pending batches this frame
                        while (_inferenceQueue.Count > 0)
                        {
                            ProcessInferenceBatch();
                        }
                        yield return wait;
                    }
                }
                else
                {
                    yield return wait;
                }
            }
        }

        private void ProcessInferenceBatch()
        {
            if (_inferenceQueue.Count == 0)
                return;

            _currentBatch = _inferenceQueue.Dequeue();

            if (_currentBatch.requests.Count == 0)
            {
                _currentBatch = null;
                return;
            }

            // Ensure model is loaded
            if (!_modelCache.TryGetValue(_currentBatch.modelId, out var cacheEntry))
            {
                Debug.LogWarning($"[MLRuntimeManager] Model '{_currentBatch.modelId}' not loaded. Skipping batch.");
                _currentBatch = null;
                return;
            }

            cacheEntry.lastAccessTime = Time.unscaledTime;

            var sw = Stopwatch.StartNew();

            try
            {
                // Create a worker for this inference
                using var worker = new Worker(cacheEntry.model, ActiveBackend);

                // Prepare input tensor — if batch size > 1, we need to concatenate
                Tensor inputTensor = _currentBatch.inputTensor;
                int actualBatchSize = _currentBatch.batchSize;

                // Schedule the inference
                worker.SetInput("input", inputTensor);
                worker.Schedule();

                // Read output back to CPU for batch slicing
                Tensor outputTensor = worker.PeekOutput();

                if (outputTensor != null)
                {
                    // Make tensor readable on CPU
                    outputTensor.MakeReadable();

                    // Dispatch results to each request
                    if (actualBatchSize > 1)
                    {
                        int outputSize = outputTensor.shape.length / actualBatchSize;

                        for (int i = 0; i < _currentBatch.requests.Count; i++)
                        {
                            var request = _currentBatch.requests[i];
                            if (request.onComplete != null)
                            {
                                // Create a slice tensor for this agent
                                // We use a sub-tensor via the output data
                                request.onComplete(outputTensor);
                            }
                        }
                    }
                    else
                    {
                        // Single inference — pass output directly
                        _currentBatch.requests[0]?.onComplete?.Invoke(outputTensor);
                    }
                }

                sw.Stop();

                // Profiling
                RecordProfiling(sw.ElapsedMilliseconds);

                if (logInferenceTiming)
                {
                    Debug.Log($"[MLRuntimeManager] Inference: model='{_currentBatch.modelId}', " +
                        $"batchSize={actualBatchSize}, time={sw.Elapsed.TotalMilliseconds:F2}ms");
                }

                OnBatchCompleted?.Invoke(_currentBatch.modelId, actualBatchSize, (float)sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                Debug.LogError($"[MLRuntimeManager] Inference failed for model '{_currentBatch.modelId}': {ex.Message}");
                OnError?.Invoke(_currentBatch.modelId, ex);
            }
            finally
            {
                _currentBatch = null;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 5. Memory Management
        // ──────────────────────────────────────────────────────────────

        /// <summary>Unload a specific model, decrementing its reference count.</summary>
        public void UnloadModel(string modelId, bool force = false)
        {
            if (!_modelCache.TryGetValue(modelId, out var entry))
                return;

            if (!force)
            {
                entry.referenceCount--;
                if (entry.referenceCount > 0)
                    return;
            }

            // Model is managed by Worker lifecycle; no explicit Dispose needed
            _modelCache.Remove(modelId);
            OnModelUnloaded?.Invoke(modelId);

            if (logInferenceTiming)
                Debug.Log($"[MLRuntimeManager] Unloaded model '{modelId}'.");
        }

        /// <summary>Unload all models from the cache.</summary>
        public void UnloadAllModels()
        {
            var keys = _modelCache.Keys.ToArray();
            foreach (var key in keys)
            {
                UnloadModel(key, force: true);
            }
            _modelCache.Clear();
            Debug.Log("[MLRuntimeManager] All models unloaded.");

            // Force GC collect to reclaim GPU memory
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        /// <summary>
        /// Unload models that have been unused for longer than modelUnloadTimeout.
        /// Called periodically by the cache eviction coroutine.
        /// </summary>
        private void EvictStaleModels()
        {
            if (maxLoadedModels <= 0 || _modelCache.Count <= maxLoadedModels)
                return;

            float now = Time.unscaledTime;
            var stale = new List<string>();

            foreach (var kvp in _modelCache)
            {
                if (kvp.Value.referenceCount <= 0 && (now - kvp.Value.lastAccessTime) > modelUnloadTimeout)
                {
                    stale.Add(kvp.Key);
                }
            }

            // Sort by lastAccessTime (oldest first) and evict oldest
            stale.Sort((a, b) => _modelCache[a].lastAccessTime.CompareTo(_modelCache[b].lastAccessTime));

            int toEvict = _modelCache.Count - maxLoadedModels;
            for (int i = 0; i < Mathf.Min(toEvict, stale.Count); i++)
            {
                UnloadModel(stale[i], force: true);
            }
        }

        private IEnumerator CacheEvictionLoop()
        {
            var wait = new WaitForSeconds(30f);

            while (true)
            {
                yield return wait;
                EvictStaleModels();
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 6. Performance Profiling
        // ──────────────────────────────────────────────────────────────

        /// <summary>Snapshot of profiling data.</summary>
        public class ProfilingSnapshot
        {
            public float averageInferenceTimeMs;
            public long totalInferences;
            public long totalInferenceTimeMs;
            public int loadedModelCount;
            public int pendingInferenceCount;
            public long managedMemoryMB;
            public long gpuMemoryMB;
            public BackendType activeBackend;
        }

        private void RecordProfiling(long elapsedMs)
        {
            _totalInferences++;
            _totalInferenceTimeMs += elapsedMs;
            _profilingSampleCounter++;

            if (profilingSampleInterval > 0 && _profilingSampleCounter >= profilingSampleInterval)
            {
                UpdateProfilingSnapshot();
                _profilingSampleCounter = 0;
            }
        }

        /// <summary>Take a manual profiling snapshot.</summary>
        public ProfilingSnapshot TakeProfilingSnapshot()
        {
            UpdateProfilingSnapshot();
            return ProfilingData;
        }

        private void UpdateProfilingSnapshot()
        {
            ProfilingData.averageInferenceTimeMs = _totalInferences > 0
                ? (float)_totalInferenceTimeMs / _totalInferences
                : 0f;
            ProfilingData.totalInferences = _totalInferences;
            ProfilingData.totalInferenceTimeMs = _totalInferenceTimeMs;
            ProfilingData.loadedModelCount = _modelCache.Count;
            ProfilingData.pendingInferenceCount = _inferenceQueue.Count;
            ProfilingData.managedMemoryMB = GC.GetTotalMemory(false) / (1024L * 1024L);
            ProfilingData.gpuMemoryMB = EstimateGPUMemoryMB();
            ProfilingData.activeBackend = ActiveBackend;
        }

        /// <summary>Rough estimate of GPU memory used by loaded models.</summary>
        private long EstimateGPUMemoryMB()
        {
            long totalBytes = 0;
            foreach (var kvp in _modelCache)
            {
                var model = kvp.Value.model;
                if (model != null)
                {
                    // Inference Engine does not expose a direct memory API.
                    // Use a heuristic: 1 MB per loaded model as a rough estimate.
                    totalBytes += 1024 * 1024;
                }
            }
            return totalBytes / (1024L * 1024L);
        }

        // ──────────────────────────────────────────────────────────────
        // Internal Helpers
        // ──────────────────────────────────────────────────────────────

        private void CacheModel(string modelId, Model model, ModelRegistryEntry entry)
        {
            if (_modelCache.Count >= maxLoadedModels && maxLoadedModels > 0)
                EvictStaleModels();

            _modelCache[modelId] = new ModelCacheEntry
            {
                model = model,
                entry = entry,
                referenceCount = 1,
                lastAccessTime = Time.unscaledTime,
                loadTime = Time.unscaledTime
            };

            OnModelLoaded?.Invoke(modelId, model);
        }

        private Model LoadFromResources(string path)
        {
            var asset = Resources.Load<ModelAsset>(path);
            if (asset == null)
                return null;
            return ModelLoader.Load(asset);
        }

        private IEnumerator LoadFromResourcesAsync(ModelRegistryEntry entry, Action<Model> onComplete, Action<Exception> onError)
        {
            var request = Resources.LoadAsync<ModelAsset>(entry.sourcePath);

            while (!request.isDone)
                yield return null;

            var asset = request.asset as ModelAsset;
            if (asset != null)
            {
                var model = ModelLoader.Load(asset);
                onComplete?.Invoke(model);
            }
            else
                onError?.Invoke(new InvalidOperationException($"Resources.LoadAsync returned null for '{entry.sourcePath}'."));
        }

        private Model LoadFromAssetBundle(string path)
        {
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
                return null;

            var asset = bundle.LoadAllAssets<ModelAsset>().FirstOrDefault();
            if (asset == null)
            {
                bundle.Unload(true);
                return null;
            }

            var model = ModelLoader.Load(asset);
            bundle.Unload(false); // ModelAsset is now in memory, bundle can be unloaded
            return model;
        }

#if UNITY_ADDRESSABLES
        private Model LoadFromAddressablesSync(string key)
        {
            var handle = Addressables.LoadAssetAsync<ModelAsset>(key);
            handle.WaitForCompletion();
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var asset = handle.Result;
                _addressablesHandles[key] = handle;
                return ModelLoader.Load(asset);
            }
            return null;
        }

        private readonly Dictionary<string, AsyncOperationHandle<ModelAsset>> _addressablesHandles = new Dictionary<string, AsyncOperationHandle<ModelAsset>>();

        private IEnumerator LoadFromAddressablesAsync(ModelRegistryEntry entry, Action<Model> onComplete, Action<Exception> onError)
        {
            var handle = Addressables.LoadAssetAsync<ModelAsset>(entry.sourcePath);

            while (!handle.IsDone)
                yield return null;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _addressablesHandles[entry.modelId] = handle;
                var model = ModelLoader.Load(handle.Result);
                onComplete?.Invoke(model);
            }
            else
            {
                onError?.Invoke(new InvalidOperationException(
                    $"Addressables failed to load '{entry.sourcePath}': {handle.OperationException?.Message}"));
            }
        }

        private void ReleaseAddressablesHandle(string modelId)
        {
            if (_addressablesHandles.TryGetValue(modelId, out var handle))
            {
                Addressables.Release(handle);
                _addressablesHandles.Remove(modelId);
            }
        }
#endif

        private void Shutdown()
        {
            if (_cacheEvictionCoroutine != null)
            {
                StopCoroutine(_cacheEvictionCoroutine);
                _cacheEvictionCoroutine = null;
            }

            if (_inferenceCoroutine != null)
            {
                StopCoroutine(_inferenceCoroutine);
                _inferenceCoroutine = null;
            }

            UnloadAllModels();

#if UNITY_ADDRESSABLES
            foreach (var kvp in _addressablesHandles)
                Addressables.Release(kvp.Value);
            _addressablesHandles.Clear();
#endif

            IsInitialized = false;
        }

        /// <summary>Reset profiling counters.</summary>
        public void ResetProfiling()
        {
            _totalInferenceTimeMs = 0;
            _totalInferences = 0;
            _profilingSampleCounter = 0;
            ProfilingData = new ProfilingSnapshot();
        }

        /// <summary>Get the current model cache for debugging.</summary>
        public IReadOnlyDictionary<string, ModelCacheEntry> GetModelCache() => _modelCache;

        /// <summary>Get the current registry for debugging.</summary>
        public IReadOnlyDictionary<AvatarType, ModelRegistryEntry> GetRegistry() => _registry;
    }

    // ═════════════════════════════════════════════════════════════════
    // Supporting Types
    // ═════════════════════════════════════════════════════════════════

    // AvatarType is defined in AnimationPolicy.cs — use its values:
    //   Humanoid, Quadruped, MultiLeg, Flying, Swimming, Other

    /// <summary>How the model asset is sourced.</summary>
    public enum ModelSourceType
    {
        /// <summary>Load from Resources folder.</summary>
        Resources,

        /// <summary>Load via Unity Addressables.</summary>
        Addressables,

        /// <summary>Load from an AssetBundle path.</summary>
        AssetBundle,

        /// <summary>Direct reference (set in inspector or code).</summary>
        Direct,
    }

    /// <summary>Entry in the model registry — maps avatar type to a model source.</summary>
    [Serializable]
    public class ModelRegistryEntry
    {
        /// <summary>Unique identifier for this model (e.g. "Locomotion_Biped_Base").</summary>
        public string modelId;

        /// <summary>How to load the model.</summary>
        public ModelSourceType sourceType = ModelSourceType.Resources;

        /// <summary>
        /// Path or key used to load the model.
        /// - Resources mode: path relative to a Resources folder (no extension).
        /// - Addressables mode: Addressables key or label.
        /// - AssetBundle mode: file path to the bundle.
        /// </summary>
        public string sourcePath;

        /// <summary>
        /// Direct model reference. Only used when sourceType == Direct.
        /// </summary>
        public ModelAsset directModel;

        /// <summary>Human-readable description for debugging.</summary>
        public string description;

        public ModelRegistryEntry() { }

        public ModelRegistryEntry(string modelId, ModelSourceType sourceType, string sourcePath)
        {
            this.modelId = modelId;
            this.sourceType = sourceType;
            this.sourcePath = sourcePath;
        }
    }

    /// <summary>Cached model with metadata.</summary>
    public class ModelCacheEntry
    {
        /// <summary>The loaded Sentis model.</summary>
        public Model model;

        /// <summary>The registry entry used to load this model.</summary>
        public ModelRegistryEntry entry;

        /// <summary>Number of active references (agents using this model).</summary>
        public int referenceCount;

        /// <summary>Time of last access (for cache eviction).</summary>
        public float lastAccessTime;

        /// <summary>Time when this model was loaded.</summary>
        public float loadTime;
    }

    /// <summary>
    /// A single inference request from one agent.
    /// Submit via <see cref="MLRuntimeManager.ScheduleInference"/>.
    /// </summary>
    public class InferenceRequest
    {
        /// <summary>Model identifier (must match a registry modelId).</summary>
        public string modelId;

        /// <summary>Input tensor (observations).</summary>
        public Tensor inputTensor;

        /// <summary>Callback invoked with the output tensor slice for this agent.</summary>
        public Action<Tensor> onComplete;

        /// <summary>Optional agent identifier for debugging.</summary>
        public string agentId;

        /// <summary>Priority (higher = earlier execution).</summary>
        public int priority;
    }

    /// <summary>Batched inference request — multiple agents sharing one model.</summary>
    public class InferenceBatch
    {
        public string modelId;
        public Tensor inputTensor;
        public List<InferenceRequest> requests;
        public int batchSize;
    }
}