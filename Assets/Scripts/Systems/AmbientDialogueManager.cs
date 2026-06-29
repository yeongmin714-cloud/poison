using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37: 환경 스토리텔링 통합 매니저 싱글톤.
    /// 문서 발견, 묘비 읽기, 저주 물건 상호작용 등의 이력을 추적하고
    /// 다른 시스템(Phase 42 도감/퀘스트)과의 연동을 관리합니다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class AmbientDialogueManager : MonoBehaviour
    {
        public static AmbientDialogueManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private bool _verbose = true;

        [Header("문서 발견 기록")]
        [SerializeField] private List<string> _discoveredDocumentIds = new List<string>();

        [Header("묘비 읽기 기록")]
        [SerializeField] private List<string> _readGravestoneNames = new List<string>();

        [Header("저주 물건 기록")]
        [SerializeField] private List<string> _interactedCursedObjects = new List<string>();

        [Header("전체 발견 수")]
        [SerializeField] private int _totalDiscoveries;

        // ===== 이벤트 =====
        /// <summary>문서 발견 시 발생 (documentId 전달)</summary>
        public event System.Action<string> OnDocumentDiscovered;
        /// <summary>묘비 읽기 시 발생 (묘비 이름 전달)</summary>
        public event System.Action<string> OnGravestoneRead;
        /// <summary>저주 물건 상호작용 시 발생</summary>
        public event System.Action<string> OnCursedObjectInteracted;
        /// <summary>어떤 환경 스토리 요소든 처음 발견 시 발생</summary>
        public event System.Action<string> OnAnyDiscovery;

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_verbose)
                Debug.Log("[AmbientDialogueManager] 환경 스토리텔링 시스템 초기화 완료.");
        }

        // ================================================================
        // 문서 발견
        // ================================================================

        /// <summary>
        /// 문서 발견 등록. 이미 발견된 문서는 무시합니다.
        /// </summary>
        public void RegisterDiscovery(string documentId)
        {
            if (string.IsNullOrEmpty(documentId)) return;
            if (_discoveredDocumentIds.Contains(documentId))
            {
                if (_verbose)
                    Debug.Log($"[AmbientDialogueManager] 이미 발견된 문서: {documentId}");
                return;
            }

            _discoveredDocumentIds.Add(documentId);
            _totalDiscoveries++;

            OnDocumentDiscovered?.Invoke(documentId);
            OnAnyDiscovery?.Invoke($"document_{documentId}");

            if (_verbose)
                Debug.Log($"[AmbientDialogueManager] 문서 발견 등록: {documentId} (총 {_totalDiscoveries}개)");
        }

        /// <summary>
        /// 특정 문서가 이미 발견되었는지 확인.
        /// </summary>
        public bool IsDocumentDiscovered(string documentId)
        {
            return _discoveredDocumentIds.Contains(documentId);
        }

        /// <summary>
        /// 발견된 모든 문서 ID 목록을 반환.
        /// </summary>
        public IReadOnlyList<string> GetDiscoveredDocuments()
        {
            return _discoveredDocumentIds.AsReadOnly();
        }

        // ================================================================
        // 묘비 읽기
        // ================================================================

        /// <summary>
        /// 묘비 읽기 등록.
        /// </summary>
        public void RegisterGravestoneRead(string personName)
        {
            if (string.IsNullOrEmpty(personName)) return;

            if (!_readGravestoneNames.Contains(personName))
            {
                _readGravestoneNames.Add(personName);
                _totalDiscoveries++;
            }

            OnGravestoneRead?.Invoke(personName);
            OnAnyDiscovery?.Invoke($"gravestone_{personName}");

            if (_verbose)
                Debug.Log($"[AmbientDialogueManager] 묘비 읽음: {personName}");
        }

        // ================================================================
        // 저주 물건 상호작용
        // ================================================================

        /// <summary>
        /// 저주받은 물건 상호작용 등록.
        /// </summary>
        public void RegisterCursedObjectInteraction(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return;

            if (!_interactedCursedObjects.Contains(objectName))
            {
                _interactedCursedObjects.Add(objectName);
                _totalDiscoveries++;
            }

            OnCursedObjectInteracted?.Invoke(objectName);
            OnAnyDiscovery?.Invoke($"cursed_{objectName}");

            if (_verbose)
                Debug.Log($"[AmbientDialogueManager] 저주 물건 상호작용: {objectName}");
        }

        // ================================================================
        // 통계
        // ================================================================

        /// <summary>
        /// 전체 환경 스토리 발견 횟수.
        /// </summary>
        public int TotalDiscoveries => _totalDiscoveries;

        /// <summary>
        /// 발견된 문서 수.
        /// </summary>
        public int DiscoveredDocumentCount => _discoveredDocumentIds.Count;

        /// <summary>
        /// 읽은 묘비 수.
        /// </summary>
        public int ReadGravestoneCount => _readGravestoneNames.Count;

        /// <summary>
        /// 상호작용한 저주 물건 수.
        /// </summary>
        public int InteractedCursedObjectCount => _interactedCursedObjects.Count;

        // ================================================================
        // 저장/로드 연동 (Phase 42 대비)
        // ================================================================

        /// <summary>
        /// 세이브 데이터용 발견 상태를 직렬화합니다.
        /// </summary>
        public AmbientDiscoverySaveData GetSaveData()
        {
            return new AmbientDiscoverySaveData
            {
                discoveredDocumentIds = new List<string>(_discoveredDocumentIds),
                readGravestoneNames = new List<string>(_readGravestoneNames),
                interactedCursedObjects = new List<string>(_interactedCursedObjects)
            };
        }

        /// <summary>
        /// 세이브 데이터에서 발견 상태를 복원합니다.
        /// </summary>
        public void LoadSaveData(AmbientDiscoverySaveData saveData)
        {
            if (saveData == null) return;

            _discoveredDocumentIds = saveData.discoveredDocumentIds ?? new List<string>();
            _readGravestoneNames = saveData.readGravestoneNames ?? new List<string>();
            _interactedCursedObjects = saveData.interactedCursedObjects ?? new List<string>();
            _totalDiscoveries = _discoveredDocumentIds.Count + _readGravestoneNames.Count + _interactedCursedObjects.Count;

            if (_verbose)
                Debug.Log($"[AmbientDialogueManager] 세이브 데이터 복원: 문서 {_discoveredDocumentIds.Count}개, 묘비 {_readGravestoneNames.Count}개, 저주 {_interactedCursedObjects.Count}개");
        }
    }

    /// <summary>
    /// AmbientDialogueManager 세이브/로드용 직렬화 데이터.
    /// </summary>
    [System.Serializable]
    public class AmbientDiscoverySaveData
    {
        public List<string> discoveredDocumentIds;
        public List<string> readGravestoneNames;
        public List<string> interactedCursedObjects;
    }
}