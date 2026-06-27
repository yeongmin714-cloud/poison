using ProjectName.Core;
using ProjectName.Core.Data;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// NPC 대화 + 퀘스트 IMGUI UI.
    /// UIWindow를 상속받아 Show/Hide 기능을 제공합니다.
    /// 
    /// 기능:
    /// - NPC 이름 + 나이 타입 아이콘 표시
    /// - 대화 텍스트 영역 (현재 대사)
    /// - 퀘스트 목록 (수락 가능/진행 중 / 완료 상태)
    /// - "퀘스트 수락" / "보상 받기" / "닫기" 버튼
    /// </summary>
    public class NPCDialogueWindow : UIWindow
    {
        public static NPCDialogueWindow Instance { get; private set; }

        [Header("Dialogue Settings")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private KeyCode _nextKey = KeyCode.E;
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        // 현재 대화 중인 NPC 데이터
        private NPCInstance _currentNPC;
        private int _currentDialogueIndex = 0;
        private bool _isInDialogue = false;
        private bool _showingQuestList = false;

        // 대화 라인 (NPCInstance.dialogues + 퀘스트 제안)
        private List<string> _dialogueLines = new List<string>();

        // 라인 인덱스 → QuestId 매핑 (OnGUI에서 Substring 방지)
        private Dictionary<int, string> _lineQuestIds = new Dictionary<int, string>();

        // NPC 헤더 캐시 (OnGUI 문자열 할당 방지)
        private string _cachedHeaderText = string.Empty;

        private Vector2 _scrollPosition = Vector2.zero;

        // IMGUI 스타일 캐싱
        private GUIStyle _titleStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _dialogueStyle;
        private GUIStyle _questStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _windowBgStyle;
        private bool _stylesInitialized = false;

        protected override void Awake()
        {
            base.Awake();

            var npcTheme = Phase33_Themes.NPCDialogueTheme();
            ApplyTheme(npcTheme);

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 시작 시 닫힌 상태
            if (_windowRoot != null)
                _windowRoot.SetActive(false);
            _isOpen = false;
        }

        private void Update()
        {
            if (!_isOpen) return;

            if (_isInDialogue && Input.GetKeyDown(_nextKey))
            {
                AdvanceDialogue();
            }

            if (Input.GetKeyDown(_closeKey))
            {
                CloseDialogue();
            }
        }

        // ===== IMGUI =====

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitializeStyles();

            // 배경 딤드
            Rect fullRect = new Rect(0, 0, Screen.width, Screen.height);
            GUI.Box(fullRect, "", _dimStyle);

            // 대화 창 (중앙 하단)
            float windowWidth = Mathf.Min(500, Screen.width * 0.8f);
            float windowHeight = _showingQuestList ? Mathf.Min(400, Screen.height * 0.5f) : Mathf.Min(250, Screen.height * 0.35f);
            float x = (Screen.width - windowWidth) / 2;
            float y = Screen.height - windowHeight - 60;

            Rect windowRect = new Rect(x, y, windowWidth, windowHeight);
            GUI.Box(windowRect, "", _windowBgStyle);

            // NPC 이름 + 나이 아이콘
            DrawNPCHeader(new Rect(x + 10, y + 10, windowWidth - 20, 30));

            // 대화 내용
            if (_isInDialogue || _showingQuestList)
            {
                DrawDialogueArea(new Rect(x + 10, y + 45, windowWidth - 20, windowHeight - 90));

                // 버튼 영역
                DrawButtonArea(new Rect(x + 10, y + windowHeight - 40, windowWidth - 20, 30));
            }
        }

        private void DrawNPCHeader(Rect rect)
        {
            GUI.Label(rect, _cachedHeaderText, _nameStyle);
        }

        private void DrawDialogueArea(Rect rect)
        {
            _scrollPosition = GUI.BeginScrollView(new Rect(rect.x, rect.y, rect.width, rect.height - 5),
                _scrollPosition,
                new Rect(0, 0, rect.width - 20, _dialogueLines.Count * 40));

            for (int i = 0; i < _dialogueLines.Count; i++)
            {
                if (_showingQuestList && i > 0)
                {
                    // 퀘스트 목록 렌더링
                    string questId = GetQuestIdForLine(i);
                    if (!string.IsNullOrEmpty(questId))
                    {
                        DrawQuestEntry(new Rect(5, i * 40, rect.width - 30, 35), questId, i);
                        continue;
                    }
                }

                GUI.Label(new Rect(5, i * 40, rect.width - 30, 35), _dialogueLines[i], _dialogueStyle);
            }

            GUI.EndScrollView();
        }

        private void DrawQuestEntry(Rect rect, string questId, int index)
        {
            QuestData quest = QuestManager.GetQuest(questId);
            QuestState state = QuestManager.GetQuestState(questId);

            // 유효하지 않은 퀘스트 ID 처리
            if (string.IsNullOrEmpty(quest.questName))
            {
                GUI.Label(new Rect(rect.x, rect.y, rect.width - 80, 20), $"[알 수 없는 퀘스트: {questId}]", _questStyle);
                return;
            }

            string stateIcon = state switch
            {
                QuestState.Available => "📋",
                QuestState.Active => "⏳",
                QuestState.Completed => "✅",
                QuestState.Locked => "🔒",
                _ => "❓"
            };

            string info = $"{stateIcon} {quest.questName} (Lv.{quest.requiredLevel})";

            GUI.Label(new Rect(rect.x, rect.y, rect.width - 80, 20), info, _questStyle);

            if (state == QuestState.Available)
            {
                if (GUI.Button(new Rect(rect.x + rect.width - 75, rect.y, 70, 20), "수락", _buttonStyle))
                {
                    AcceptQuest(questId);
                }
            }
            else if (state == QuestState.Active)
            {
                // 진행 상태 표시
                string progress = GetQuestProgress(quest);
                GUI.Label(new Rect(rect.x + rect.width - 75, rect.y, 70, 20), progress, _statusStyle);
            }
            else if (state == QuestState.Completed)
            {
                if (GUI.Button(new Rect(rect.x + rect.width - 75, rect.y, 70, 20), "보상", _buttonStyle))
                {
                    ClaimReward(questId);
                }
            }
        }

        private void DrawButtonArea(Rect rect)
        {
            float btnWidth = 100;
            float spacing = 10;
            float totalWidth = btnWidth * 2 + spacing;
            float startX = rect.x + (rect.width - totalWidth) / 2;

            if (_isInDialogue)
            {
                if (GUI.Button(new Rect(startX, rect.y, btnWidth, 25), "다음 ▶", _buttonStyle))
                {
                    AdvanceDialogue();
                }
            }
            else if (_showingQuestList)
            {
                if (_currentNPC.HasQuests)
                {
                    if (GUI.Button(new Rect(startX, rect.y, btnWidth, 25), "퀘스트 목록", _buttonStyle))
                    {
                        // 이미 퀘스트 목록 표시 중
                    }
                }
            }

            if (GUI.Button(new Rect(startX + btnWidth + spacing, rect.y, btnWidth, 25), "닫기 ✕", _buttonStyle))
            {
                CloseDialogue();
            }
        }

        // ===== 퍼블릭 메서드 =====

        /// <summary>NPC 대화 UI 열기</summary>
        public void ShowDialogue(NPCInstance npc)
        {
            _currentNPC = npc;
            _currentDialogueIndex = 0;
            _isInDialogue = true;
            _showingQuestList = false;

            // 헤더 텍스트 캐시 (OnGUI GC 방지)
            string ageIcon = npc.AgeType switch
            {
                NPCData.NPCAgeType.Child => "🧒",
                NPCData.NPCAgeType.Elderly => "👴",
                _ => "🧑"
            };
            string questBadge = npc.HasQuests ? " ❓" : "";
            _cachedHeaderText = $"{ageIcon} {npc.NpcName}{questBadge}";

            // 대화 라인 구성
            _dialogueLines.Clear();
            _lineQuestIds.Clear();
            _dialogueLines.Add($"\"{npc.Greeting}\"");

            if (npc.HasQuests)
            {
                _dialogueLines.Add($"\"{npc.QuestOfferLine}\"");
                _dialogueLines.Add("---");
                _dialogueLines.Add("(NPC가 퀘스트를 줄 준비가 되었다.)");
                _dialogueLines.Add("[E] 퀘스트 목록 보기");
            }
            else
            {
                _dialogueLines.Add("(NPC는 할 말이 없는 것 같다.)");
            }

            _scrollPosition = Vector2.zero;

            Show();
        }

        /// <summary>대화 닫기</summary>
        public void CloseDialogue()
        {
            _isInDialogue = false;
            _showingQuestList = false;
            _currentNPC = default;
            _dialogueLines.Clear();
            _lineQuestIds.Clear();
            Hide();
        }

        // ===== 내부 메서드 =====

        private void AdvanceDialogue()
        {
            if (_currentDialogueIndex < _dialogueLines.Count - 1)
            {
                _currentDialogueIndex++;

                // 퀘스트 목록 지점 도달
                if (_currentDialogueIndex >= _dialogueLines.Count - 1 &&
                    _dialogueLines.Count > 0 &&
                    _dialogueLines[_dialogueLines.Count - 1].Contains("퀘스트 목록"))
                {
                    ShowQuestList();
                }
            }
            else
            {
                ShowQuestList();
            }
        }

        private void ShowQuestList()
        {
            _isInDialogue = false;
            _showingQuestList = true;

            // 퀘스트 목록 대화로 전환
            _dialogueLines.Clear();
            _lineQuestIds.Clear();
            _dialogueLines.Add($"--- {_currentNPC.NpcName}의 퀘스트 ---");

            if (_currentNPC.QuestIds != null)
            {
                for (int i = 0; i < _currentNPC.QuestIds.Count; i++)
                {
                    string questId = _currentNPC.QuestIds[i];
                    int lineIndex = _dialogueLines.Count;
                    _dialogueLines.Add(questId);
                    _lineQuestIds[lineIndex] = questId;
                }
            }

            if (_dialogueLines.Count == 1)
            {
                _dialogueLines.Add("(사용 가능한 퀘스트가 없습니다.)");
            }

            _scrollPosition = Vector2.zero;
        }

        private string GetQuestIdForLine(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= _dialogueLines.Count)
                return null;

            if (_lineQuestIds.TryGetValue(lineIndex, out string questId))
                return questId;

            return null;
        }

        private void AcceptQuest(string questId)
        {
            if (QuestManager.AcceptQuest(questId))
            {
                Debug.Log($"[NPCDialogue] ✅ 퀘스트 수락됨: {questId}");
            }
        }

        private void ClaimReward(string questId)
        {
            if (QuestManager.TryCompleteQuest(questId))
            {
                Debug.Log($"[NPCDialogue] 🎁 보상 수령: {questId}");
                // 대화 새로고침
                ShowQuestList();
            }
        }

        private string GetQuestProgress(QuestData quest)
        {
            if (quest.objectives == null || quest.objectives.Count == 0)
                return "";

            int completed = 0;
            int total = quest.objectives.Count;
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                if (quest.objectives[i].IsMet) completed++;
            }

            return $"{completed}/{total}";
        }

        // ===== 스타일 초기화 =====

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _dimStyle = new GUIStyle(GUI.skin.box);
            _dimStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.5f));

            _windowBgStyle = new GUIStyle(GUI.skin.box);
            _windowBgStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.9f));
            _windowBgStyle.border = new RectOffset(4, 4, 4, 4);

            _nameStyle = new GUIStyle(GUI.skin.label);
            _nameStyle.fontSize = 72;
            _nameStyle.fontStyle = FontStyle.Bold;
            _nameStyle.normal.textColor = Color.white;
            _nameStyle.alignment = TextAnchor.MiddleLeft;

            _dialogueStyle = new GUIStyle(GUI.skin.label);
            _dialogueStyle.fontSize = 56;
            _dialogueStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            _dialogueStyle.wordWrap = true;
            _dialogueStyle.richText = true;

            _questStyle = new GUIStyle(GUI.skin.label);
            _questStyle.fontSize = 52;
            _questStyle.normal.textColor = new Color(0.8f, 0.9f, 1.0f);
            _questStyle.wordWrap = true;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 48;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.textColor = Color.yellow;

            _statusStyle = new GUIStyle(GUI.skin.label);
            _statusStyle.fontSize = 44;
            _statusStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            _statusStyle.alignment = TextAnchor.MiddleCenter;

            _stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}