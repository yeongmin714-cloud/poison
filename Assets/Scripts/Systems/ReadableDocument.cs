using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37-01: 읽을 수 있는 문서/편지/일기 데이터 ScriptableObject.
    /// ID/제목/내용/발견위치/중요도/분류를 저장합니다.
    /// 발견 시 Phase 42 도감/퀘스트 연동을 위한 훅을 포함합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewReadableDocument", menuName = "Environmental/ReadableDocument")]
    public class ReadableDocument : ScriptableObject
    {
        /// <summary>문서 고유 식별자 (예: "letter_blacksmith_01")</summary>
        [Header("문서 정보")]
        [SerializeField] private string _documentId;
        public string DocumentId => _documentId;

        /// <summary>문서 제목 (UI 표시용)</summary>
        [SerializeField] private string _title;
        public string Title => _title;

        /// <summary>문서 본문 텍스트</summary>
        [SerializeField, TextArea(5, 30)] private string _content;
        public string Content => _content;

        /// <summary>발견 위치 설명 (예: "대장간 화로 옆")</summary>
        [SerializeField] private string _locationDescription;
        public string LocationDescription => _locationDescription;

        /// <summary>문서 중요도 (일반/중요/퀘스트 필수)</summary>
        [SerializeField] private DocumentImportance _importance = DocumentImportance.Normal;
        public DocumentImportance Importance => _importance;

        /// <summary>문서 분류</summary>
        [SerializeField] private DocumentCategory _category = DocumentCategory.Letter;
        public DocumentCategory Category => _category;

        /// <summary>연결된 퀘스트 ID (없으면 빈 문자열)</summary>
        [SerializeField] private string _linkedQuestId;
        public string LinkedQuestId => _linkedQuestId;

        // ================================================================
        // 열거형
        // ================================================================

        public enum DocumentImportance
        {
            Normal,     // 일반 문서
            Important,  // 중요 문서 (황금빛 파티클)
            QuestRequired // 퀘스트 필수 문서
        }

        public enum DocumentCategory
        {
            Letter,      // 편지
            Diary,       // 일기
            OfficialDoc, // 공문
            Scroll,      // 스크롤
            Wanted       // 현상수배
        }

        // ================================================================
        // 검증
        // ================================================================

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_documentId))
            {
                Debug.LogWarning($"[ReadableDocument] Document ID is empty for '{name}'");
            }
        }

        /// <summary>
        /// 문서 ID로 문서 데이터를 반환합니다.
        /// AmbientDialogueManager가 관리하는 레지스트리에서 조회합니다.
        /// </summary>
        public static ReadableDocument FindById(string documentId)
        {
            if (string.IsNullOrEmpty(documentId)) return null;
            // Resources.Load 또는 Addressables를 통한 로드 (추후 확장)
            return Resources.Load<ReadableDocument>($"Documents/{documentId}");
        }
    }
}