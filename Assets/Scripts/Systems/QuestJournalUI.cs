using System;
using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    /// <summary>
    /// G3-09: 퀘스트 저널 UI — IMGUI 기반, J 키로 토글.
    /// 진행 중 / 완료 탭, 진행도 추적, 완료 효과 제공.
    /// QuestManager에 의존하지 않고 자체적으로 퀘스트 목록을 관리합니다.
    /// </summary>
    public class QuestJournalUI : MonoBehaviour
    {
        #region === 내부 데이터 ===

        /// <summary>저널 내 퀘스트 항목 (런타임 추적용)</summary>
        [Serializable]
        public class JournalQuestEntry
        {
            public string questId;
            public string questName;
            public string description;
            public QuestObjective[] objectives;
            public QuestState state;
            public string completionTime; // 완료 시점 저장 (HH:mm:ss)

            public JournalQuestEntry() { }

            public JournalQuestEntry(string id, string name, string desc, QuestObjective[] objs)
            {
                questId = id;
                questName = name;
                description = desc;
                objectives = objs ?? Array.Empty<QuestObjective>();
                state = QuestState.Active;
                completionTime = "";
            }
        }

        /// <summary>완료 이펙트 정보</summary>
        private class CompletionEffect
        {
            public string questName;
            public float startTime;
            public float duration;

            public CompletionEffect(string name, float duration = 2f)
            {
                questName = name;
                startTime = Time.time;
                this.duration = duration;
            }

            public bool IsExpired => Time.time - startTime >= duration;
            public float Progress => Mathf.Clamp01((Time.time - startTime) / duration);
        }

        #endregion

        #region === 스타일 상수 ===

        private static readonly Color ColorBg = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        private static readonly Color ColorBorder = new Color(0.85f, 0.70f, 0.15f, 1f); // 금색
        private static readonly Color ColorTabActive = new Color(0.85f, 0.70f, 0.15f, 0.25f);
        private static readonly Color ColorTabInactive = new Color(0.12f, 0.12f, 0.15f, 0.8f);
        private static readonly Color ColorTextLight = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color ColorTextDim = new Color(0.6f, 0.6f, 0.65f, 1f);
        private static readonly Color ColorProgress = new Color(0.3f, 0.8f, 1f, 1f); // 진행 바
        private static readonly Color ColorGold = new Color(1f, 0.85f, 0.3f, 1f);

        private const float WINDOW_WIDTH = 500f;
        private const float WINDOW_HEIGHT = 400f;
        private const float TAB_HEIGHT = 32f;
        private const float TAB_WIDTH = 120f;
        private const float ENTRY_HEIGHT = 72f;
        private const float HEADER_Y = 10f;
        private const float CONTENT_PADDING = 10f;

        #endregion

        #region === 필드 ===

        [Header("---- 퀘스트 저널 UI ----")]
        [SerializeField] private bool _isOpen;
        [SerializeField] private int _activeTab; // 0 = Active, 1 = Completed

        [Header("퀘스트 데이터")]
        [SerializeField] private List<JournalQuestEntry> _quests = new List<JournalQuestEntry>();

        private Vector2 _scrollPos;
        private bool _stylesInitialized;

        // 스타일
        private GUIStyle _styleTitle;
        private GUIStyle _styleTab;
        private GUIStyle _styleTabActive;
        private GUIStyle _styleLabel;
        private GUIStyle _styleDimLabel;
        private GUIStyle _styleProgressLabel;
        private GUIStyle _styleProgressLabelMet; // ★ 추가: 달성 시 초록색
        private GUIStyle _styleEffect;
        private GUIStyle _styleObjective;
        private GUIStyle _styleEntryBox;

        // 완료 이펙트
        private CompletionEffect _currentEffect;
        private List<CompletionEffect> _effectQueue = new List<CompletionEffect>();

        #endregion

        #region === 유니티 생명주기 ===

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            // ★ Texture2D 메모리 정리 — MakeTexture 누수 방지
            if (_bgTex != null) Destroy(_bgTex);
            if (_borderTex != null) Destroy(_borderTex);
        }

        private void Update()
        {
            // J 키 토글
            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
            {
                _isOpen = !_isOpen;
                _scrollPos = Vector2.zero;
                Debug.Log($"[QuestJournalUI] 저널 {( _isOpen ? "열림" : "닫힘" )}");
            }

            // 완료 이펙트 큐 처리
            if (_effectQueue.Count > 0 && (_currentEffect == null || _currentEffect.IsExpired))
            {
                _currentEffect = _effectQueue[0];
                _effectQueue.RemoveAt(0);
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            EnsureStyles();

            float x = (Screen.width - WINDOW_WIDTH) / 2f;
            float y = (Screen.height - WINDOW_HEIGHT) / 2f;

            DrawJournalWindow(x, y);

            // 완료 이펙트 (창과 별개로 중앙에 표시)
            if (_currentEffect != null && !_currentEffect.IsExpired)
            {
                DrawCompletionEffect();
            }
        }

        #endregion

        #region === 퍼블릭 API ===

        /// <summary>퀘스트 추가 (Active 상태로 등록)</summary>
        public void AddQuest(string id, string name, string desc, QuestObjective[] objectives)
        {
            // 중복 체크
            for (int i = 0; i < _quests.Count; i++)
            {
                if (_quests[i].questId == id)
                {
                    Debug.LogWarning($"[QuestJournalUI] 이미 존재하는 퀘스트: {id}");
                    return;
                }
            }

            _quests.Add(new JournalQuestEntry(id, name, desc, objectives));
            Debug.Log($"[QuestJournalUI] 📋 퀘스트 추가: {name} ({id})");
        }

        /// <summary>특정 퀘스트의 목표 진행도 갱신</summary>
        public void UpdateProgress(string questId, int objIndex, int count)
        {
            JournalQuestEntry entry = FindQuest(questId);
            if (entry == null) return;
            if (entry.state != QuestState.Active) return;
            if (objIndex < 0 || objIndex >= entry.objectives.Length) return;

            QuestObjective obj = entry.objectives[objIndex];
            obj.currentCount = Mathf.Min(obj.requiredCount, obj.currentCount + count);
            entry.objectives[objIndex] = obj;

            Debug.Log($"[QuestJournalUI] 진행 갱신: {entry.questName} — 목표[{objIndex}] {obj.currentCount}/{obj.requiredCount}");
        }

        /// <summary>퀘스트 완료 처리 (모든 목표 달성 시)</summary>
        public bool CompleteQuest(string questId)
        {
            JournalQuestEntry entry = FindQuest(questId);
            if (entry == null)
            {
                Debug.LogWarning($"[QuestJournalUI] 완료 실패 — 퀘스트 없음: {questId}");
                return false;
            }
            if (entry.state != QuestState.Active)
            {
                Debug.LogWarning($"[QuestJournalUI] 완료 실패 — {questId} 상태: {entry.state}");
                return false;
            }

            // 모든 목표 달성 확인
            for (int i = 0; i < entry.objectives.Length; i++)
            {
                if (!entry.objectives[i].IsMet)
                {
                    Debug.LogWarning($"[QuestJournalUI] 완료 실패 — 목표 미달성: {entry.questName}[{i}]");
                    return false;
                }
            }

            entry.state = QuestState.Completed;
            entry.completionTime = DateTime.Now.ToString("HH:mm:ss");

            // 완료 이펙트 큐에 추가
            _effectQueue.Add(new CompletionEffect(entry.questName));
            Debug.Log($"[QuestJournalUI] ✅ 퀘스트 완료: {entry.questName}");

            return true;
        }

        /// <summary>저널 열기/닫기 (외부에서 호출 가능)</summary>
        public void Toggle()
        {
            _isOpen = !_isOpen;
            _scrollPos = Vector2.zero;
        }

        /// <summary>저널 열기</summary>
        public void Open()
        {
            _isOpen = true;
            _scrollPos = Vector2.zero;
        }

        /// <summary>저널 닫기</summary>
        public void Close()
        {
            _isOpen = false;
        }

        /// <summary>저널 열림 여부</summary>
        public bool IsOpen => _isOpen;

        /// <summary>등록된 퀘스트 목록 (읽기 전용)</summary>
        public IReadOnlyList<JournalQuestEntry> Quests => _quests;

        /// <summary>진행 중인 퀘스트 개수</summary>
        public int ActiveQuestCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _quests.Count; i++)
                    if (_quests[i].state == QuestState.Active) count++;
                return count;
            }
        }

        /// <summary>완료된 퀘스트 개수</summary>
        public int CompletedQuestCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _quests.Count; i++)
                    if (_quests[i].state == QuestState.Completed) count++;
                return count;
            }
        }

        #endregion

        #region === 내부 헬퍼 ===

        private JournalQuestEntry FindQuest(string questId)
        {
            for (int i = 0; i < _quests.Count; i++)
            {
                if (_quests[i].questId == questId)
                    return _quests[i];
            }
            return null;
        }

        #endregion

        #region === IMGUI 드로잉 ===

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = ColorGold }
            };

            _styleTab = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextDim, background = MakeTexture(1, 1, ColorTabInactive) },
                hover = { textColor = ColorTextLight, background = MakeTexture(1, 1, ColorTabActive) },
                border = new RectOffset(2, 2, 2, 2)
            };

            _styleTabActive = new GUIStyle(_styleTab)
            {
                normal = { textColor = ColorGold, background = MakeTexture(1, 1, ColorTabActive) }
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = false,
                richText = true,
                normal = { textColor = ColorTextLight }
            };

            _styleDimLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = ColorTextDim }
            };

            _styleProgressLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ColorProgress }
            };

            _styleProgressLabelMet = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.green }
            };

            _styleObjective = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.85f, 1f, 1f) }
            };

            _styleEffect = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorGold }
            };

            _styleEntryBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, new Color(0.1f, 0.1f, 0.14f, 0.85f)) },
                border = new RectOffset(1, 1, 1, 1)
            };

            _stylesInitialized = true;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        private void DrawJournalWindow(float x, float y)
        {
            // 배경 박스
            GUI.Box(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), "");
            if (_bgTex == null) _bgTex = MakeTexture(1, 1, ColorBg);
            GUI.DrawTexture(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), _bgTex);

            // 금색 테두리
            DrawBorder(x, y, WINDOW_WIDTH, WINDOW_HEIGHT, ColorBorder, 2f);

            // 타이틀
            GUI.Label(new Rect(x + 15, y + 8, 200, 24), "📜 퀘스트 저널", _styleTitle);

            // 통계
            string statText = $"🔄 진행 중: {ActiveQuestCount}개  |  ✅ 완료: {CompletedQuestCount}개";
            GUI.Label(new Rect(x + WINDOW_WIDTH - 220, y + 10, 200, 20), statText, _styleDimLabel);

            // 탭 버튼
            float tabY = y + HEADER_Y + 30;
            DrawTabButton(x + 20, tabY, 0, "🔄 진행 중");
            DrawTabButton(x + 20 + TAB_WIDTH + 6, tabY, 1, "✅ 완료");

            // 퀘스트 리스트 영역
            float listX = x + CONTENT_PADDING;
            float listY = tabY + TAB_HEIGHT + 8;
            float listW = WINDOW_WIDTH - CONTENT_PADDING * 2;
            float listH = WINDOW_HEIGHT - (listY - y) - CONTENT_PADDING;

            // 현재 탭에 맞는 퀘스트 리스트
            List<JournalQuestEntry> filteredQuests = new List<JournalQuestEntry>();
            for (int i = 0; i < _quests.Count; i++)
            {
                if (_activeTab == 0 && _quests[i].state == QuestState.Active)
                    filteredQuests.Add(_quests[i]);
                else if (_activeTab == 1 && _quests[i].state == QuestState.Completed)
                    filteredQuests.Add(_quests[i]);
            }

            float contentH = Mathf.Max(listH, filteredQuests.Count * ENTRY_HEIGHT + 10);

            _scrollPos = GUI.BeginScrollView(
                new Rect(listX, listY, listW, listH),
                _scrollPos,
                new Rect(0, 0, listW - 20, contentH)
            );

            if (filteredQuests.Count == 0)
            {
                string emptyMsg = _activeTab == 0
                    ? "진행 중인 퀘스트가 없습니다.\nNPC를 찾아 퀘스트를 수락하세요."
                    : "완료된 퀘스트가 없습니다.\n퀘스트를 완료하면 여기에 표시됩니다.";
                GUI.Label(new Rect(10, 10, listW - 40, 40), emptyMsg, _styleDimLabel);
            }
            else
            {
                for (int i = 0; i < filteredQuests.Count; i++)
                {
                    DrawQuestEntry(filteredQuests[i], i, listW - 20);
                }
            }

            GUI.EndScrollView();

            // 닫기 안내
            GUI.Label(
                new Rect(x + WINDOW_WIDTH - 170, y + WINDOW_HEIGHT - 22, 160, 18),
                "J 키로 닫기",
                _styleDimLabel
            );
        }

        private Texture2D _borderTex;
        private Texture2D _bgTex;
        private void DrawBorder(float x, float y, float w, float h, Color color, float thickness)
        {
            if (_borderTex == null) _borderTex = MakeTexture(1, 1, color);
            GUI.DrawTexture(new Rect(x, y, w, thickness), _borderTex);
            GUI.DrawTexture(new Rect(x, y + h - thickness, w, thickness), _borderTex);
            GUI.DrawTexture(new Rect(x, y, thickness, h), _borderTex);
            GUI.DrawTexture(new Rect(x + w - thickness, y, thickness, h), _borderTex);
        }

        private void DrawTabButton(float x, float y, int tabIndex, string label)
        {
            GUIStyle style = _activeTab == tabIndex ? _styleTabActive : _styleTab;
            if (GUI.Button(new Rect(x, y, TAB_WIDTH, TAB_HEIGHT), label, style))
            {
                _activeTab = tabIndex;
                _scrollPos = Vector2.zero;
            }
        }

        private void DrawQuestEntry(JournalQuestEntry entry, int index, float width)
        {
            float yPos = index * ENTRY_HEIGHT;

            // 엔트리 배경
            GUI.Box(new Rect(0, yPos, width, ENTRY_HEIGHT - 2), "", _styleEntryBox);

            // 퀘스트 이름
            GUI.Label(new Rect(8, yPos + 3, width - 16, 20), entry.questName, _styleLabel);

            // 설명
            if (!string.IsNullOrEmpty(entry.description))
            {
                GUI.Label(new Rect(8, yPos + 22, width - 16, 16), entry.description, _styleDimLabel);
            }

            if (entry.state == QuestState.Active)
            {
                // 진행 중 — 목표 진행도 표시
                float objY = yPos + 40;
                for (int i = 0; i < entry.objectives.Length; i++)
                {
                    QuestObjective obj = entry.objectives[i];
                    string progressText = $"{obj.currentCount}/{obj.requiredCount}";
                    string objDesc = !string.IsNullOrEmpty(obj.description) ? obj.description : obj.type.ToString();

                    GUI.Label(new Rect(8, objY, width - 60, 16),
                        $"▸ {objDesc}", _styleObjective);

                    GUIStyle progStyle = obj.IsMet ? _styleProgressLabelMet : _styleProgressLabel;
                    GUI.Label(new Rect(width - 55, objY, 48, 16), progressText, progStyle);

                    objY += 15;
                }
            }
            else if (entry.state == QuestState.Completed)
            {
                // 완료 — 완료 시각 표시
                string completeInfo = !string.IsNullOrEmpty(entry.completionTime)
                    ? $"✅ 완료 ({entry.completionTime})"
                    : "✅ 완료";

                GUI.Label(new Rect(8, yPos + 40, width - 16, 20),
                    $"<color=#44FF44>{completeInfo}</color>",
                    _styleLabel);
            }
        }

        private void DrawCompletionEffect()
        {
            float progress = _currentEffect.Progress;
            float alpha = 1f;

            // 0~20% 페이드 인, 80~100% 페이드 아웃
            if (progress < 0.2f)
                alpha = progress / 0.2f;
            else if (progress > 0.8f)
                alpha = (1f - progress) / 0.2f;

            // 위로 떠오르는 효과
            float riseOffset = -progress * 40f;

            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f + riseOffset;

            // ★ 기존 _styleEffect 재사용 — 매 프레임 new GUIStyle 방지
            Color effectColor = ColorGold;
            effectColor.a = alpha;
            _styleEffect.normal.textColor = effectColor;
            _styleEffect.fontSize = Mathf.RoundToInt(24 + (1f - progress) * 6);

            string text = $"✨ <color=#FFD700>✅ 퀘스트 완료!</color>\n<color=#FFAA00>{_currentEffect.questName}</color>";

            float textWidth = 400f;
            float textHeight = 80f;
            GUI.Label(new Rect(cx - textWidth / 2f, cy - textHeight / 2f, textWidth, textHeight),
                text,
                _styleEffect);

            // 만료 시 제거
            if (_currentEffect.IsExpired)
            {
                _currentEffect = null;
            }
        }

        #endregion
    }
}