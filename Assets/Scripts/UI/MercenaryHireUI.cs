using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// P25-02~03: 용병 고용/관리 IMGUI UI.
    /// 선술집에서 용병 목록을 보고 고용/해고할 수 있습니다.
    /// </summary>
    public class MercenaryHireUI : MonoBehaviour
    {
        public static MercenaryHireUI Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.H;
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        private bool _isOpen = false;
        private Vector2 _scrollPos;
        private string _selectedMercenaryId = "";
        private string _statusMessage = "";
        private float _statusTimer = 0f;

        // 스타일 캐싱
        private GUIStyle _titleStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _statStyle;
        private GUIStyle _storyStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _msgStyle;
        private bool _stylesInitialized = false;

        private UIDesignTheme _theme;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _theme = Phase33_Themes.MercenaryTheme();
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _isOpen = !_isOpen;
                if (!_isOpen) _selectedMercenaryId = "";
            }

            if (_isOpen && Input.GetKeyDown(_closeKey))
            {
                _isOpen = false;
                _selectedMercenaryId = "";
            }

            if (_statusTimer > 0)
                _statusTimer -= Time.deltaTime;
        }

        /// <summary>UI 열기 (외부 호출용)</summary>
        public void Open()
        {
            _isOpen = true;
        }

        /// <summary>UI 닫기</summary>
        public void Close()
        {
            _isOpen = false;
            _selectedMercenaryId = "";
        }

        /// <summary>UI 열림 상태</summary>
        public bool IsOpen => _isOpen;

        private void OnGUI()
        {
            if (!_isOpen) return;

            EnsureStyles();

            float panelW = 620f;
            float panelH = 520f;
            float x = (Screen.width - panelW) / 2f;
            float y = (Screen.height - panelH) / 2f;

            // 배경
            Color bgColor = _theme != null ? _theme.BgColor : new Color(0.1f, 0.1f, 0.15f, 0.6f);
            Color borderColor = _theme != null ? _theme.BorderColor : new Color(0.6f, 0.4f, 0.2f, 0.85f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            var oldColor = GUI.color;
            GUI.color = bgColor;
            GUI.Box(new Rect(x, y, panelW, panelH), "");
            GUI.color = oldColor;

            // 타이틀
            GUI.Label(new Rect(x + 10, y + 10, panelW - 20, 30), "🍺 용병 고용소", _titleStyle);

            if (!string.IsNullOrEmpty(_statusMessage) && _statusTimer > 0)
            {
                GUI.Label(new Rect(x + 10, y + 40, panelW - 20, 24), _statusMessage, _msgStyle);
            }

            float contentTop = y + 70;
            float contentH = panelH - 90;

            _scrollPos = GUI.BeginScrollView(
                new Rect(x + 10, contentTop, panelW - 20, contentH),
                _scrollPos,
                new Rect(0, 0, panelW - 40, GetContentHeight())
            );

            float cy = 0;
            DrawGoldDisplay(x + panelW - 160, y + 12);
            DrawHiredCount(x + panelW - 160, y + 36);

            // 용병 목록
            var allMercs = GetMercenaryDataList();
            for (int i = 0; i < allMercs.Count; i++)
            {
                var merc = allMercs[i];
                bool isHired = IsMercenaryHired(merc.id);
                bool isSelected = _selectedMercenaryId == merc.id;

                DrawMercenaryEntry(merc, isHired, isSelected, i, panelW - 40, ref cy);
            }

            GUI.EndScrollView();
        }

        private float GetContentHeight()
        {
            var allMercs = GetMercenaryDataList();
            return allMercs.Count * 90f + 20f;
        }

        private void DrawGoldDisplay(float x, float y)
        {
            int gold = 0;
            if (PlayerInventory.Instance != null)
                gold = PlayerInventory.Instance.GetItemCount("gold");
            GUI.Label(new Rect(x, y, 150, 22), $"💰 {gold}G", _statStyle);
        }

        private void DrawHiredCount(float x, float y)
        {
            int hired = MercenaryManager.Instance != null ? MercenaryManager.Instance.HiredCount : 0;
            int max = MercenaryManager.Instance != null ? MercenaryManager.Instance.MaxMercenaries : 10;
            GUI.Label(new Rect(x, y, 150, 22), $"👥 {hired}/{max}", _statStyle);
        }

        private void DrawMercenaryEntry(MercenaryData merc, bool isHired, bool isSelected, int index, float entryW, ref float cy)
        {
            float entryH = 85f;
            float xOff = 5f;

            // 배경 박스
            Color bgColor = isSelected ? new Color(0.2f, 0.3f, 0.4f, 0.8f) : new Color(0.1f, 0.1f, 0.15f, 0.6f);
            GUI.Box(new Rect(0, cy, entryW, entryH), "");

            // 등급 표시
            GUI.Label(new Rect(xOff, cy + 5, 60, 22), merc.GradeStars, _nameStyle);

            // 이름
            GUI.Label(new Rect(xOff + 65, cy + 5, 150, 22), merc.mercenaryName, _titleStyle);

            // 직업
            string jobIcon = merc.jobType == "Bard" ? "🎵" : "⚔️";
            GUI.Label(new Rect(xOff + 220, cy + 5, 80, 22), $"{jobIcon} {merc.jobType}", _statStyle);

            // 능력치
            string stats = $"❤️{merc.maxHP} ⚔️{merc.attack} 🛡️{merc.defense} 💨{merc.moveSpeed}";
            GUI.Label(new Rect(xOff, cy + 30, 300, 20), stats, _statStyle);

            // 특수 능력
            GUI.Label(new Rect(xOff, cy + 50, 300, 20), $"✨ {merc.specialAbility}", _statStyle);

            // 고용 비용
            GUI.Label(new Rect(xOff + 310, cy + 5, 100, 22), $"💰 {merc.hireCost}G", _nameStyle);

            // 버튼
            float btnX = entryW - 110;
            if (isHired)
            {
                // 해고 버튼 (빨간색 계열)
                GUI.color = new Color(1f, 0.4f, 0.3f);
                if (GUI.Button(new Rect(btnX, cy + 25, 100, 28), "🔴 해고", _buttonStyle))
                {
                    OnFireMercenary(merc.id);
                }
                GUI.color = Color.white;

                // 고용됨 표시
                GUI.Label(new Rect(btnX, cy + 58, 100, 20), "✅ 고용됨", _statStyle);
            }
            else
            {
                // 고용 버튼
                if (GUI.Button(new Rect(btnX, cy + 25, 100, 28), "📋 고용", _buttonStyle))
                {
                    OnHireMercenary(merc.id);
                }
            }

            // 상세 보기 버튼
            if (GUI.Button(new Rect(btnX - 55, cy + 25, 50, 28), "📖", _buttonStyle))
            {
                _selectedMercenaryId = _selectedMercenaryId == merc.id ? "" : merc.id;
            }

            // 선택된 용병의 상세 정보 표시
            if (isSelected)
            {
                float detailY = cy + entryH + 2;
                float detailH = 60f;
                GUI.Box(new Rect(0, detailY, entryW, detailH), "");
                GUI.Label(new Rect(xOff + 5, detailY + 5, entryW - 10, 50), $"📜 {merc.backStory}", _storyStyle);

                // 호감도 표시 (고용된 경우)
                if (isHired)
                {
                    float aff = MercenaryManager.Instance.GetAffinity(merc.id);
                    GUI.Label(new Rect(xOff + 5, detailY + detailH - 22, 200, 20), $"❤️ 호감도: {(int)aff}% (보너스: +{aff / 100f * 0.2f * 100:F0}%)", _msgStyle);
                }

                cy += entryH + detailH + 5;
            }
            else
            {
                cy += entryH + 5;
            }
        }

        private List<MercenaryData> GetMercenaryDataList()
        {
            var list = new List<MercenaryData>();
            if (MercenaryManager.Instance != null)
            {
                list.AddRange(MercenaryManager.Instance.GetAllMercenaryData());
            }
            return list;
        }

        private bool IsMercenaryHired(string mercenaryId)
        {
            if (MercenaryManager.Instance == null) return false;
            var hired = MercenaryManager.Instance.GetHiredMercenaries();
            foreach (var h in hired)
            {
                if (h.data.id == mercenaryId) return true;
            }
            return false;
        }

        private void OnHireMercenary(string mercenaryId)
        {
            if (MercenaryManager.Instance == null) return;

            var data = MercenaryManager.Instance.GetMercenaryData(mercenaryId);
            if (data.id == null)
            {
                _statusMessage = "⚠️ 알 수 없는 용병입니다.";
                _statusTimer = 3f;
                return;
            }

            // 골드 확인
            int gold = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount("gold") : 0;
            if (gold < data.hireCost)
            {
                _statusMessage = $"⚠️ 골드 부족! 필요: {data.hireCost}G, 보유: {gold}G";
                _statusTimer = 3f;
                return;
            }

            // 고용
            if (MercenaryManager.Instance.HireMercenary(mercenaryId))
            {
                PlayerInventory.Instance.RemoveItem("gold", data.hireCost);
                _statusMessage = $"✅ {data.mercenaryName} 고용 완료! ({data.hireCost}G 지불)";
                _statusTimer = 3f;

                // NPC 대화 이벤트 호출
                if (NPCDialogueWindow.Instance != null)
                {
                    // 용병 고용 시 NPC 대화 표시를 위한 간단한 문자열 전달
                    string dialogue = $"{data.mercenaryName}: \\\"{data.backStory}\\\"";
                    // 직접 NPCInstance를 만들 순 없으므로 ShowDialogue 대신 간이 메시지 처리
                    Debug.Log($"[MercenaryHireUI] 🎭 용병 대화: {dialogue}");

                    // 용병 개성 대화를 NPCDialogueWindow에 표시
                    TriggerMercenaryDialogue(data);
                }
            }
            else
            {
                _statusMessage = "⚠️ 더 이상 고용할 수 없습니다. (최대 인원 초과)";
                _statusTimer = 3f;
            }
        }

        private void OnFireMercenary(string mercenaryId)
        {
            if (MercenaryManager.Instance == null) return;

            var data = MercenaryManager.Instance.GetMercenaryData(mercenaryId);
            if (MercenaryManager.Instance.FireMercenary(mercenaryId))
            {
                _statusMessage = $"🔴 {data.mercenaryName} 해고됨.";
                _statusTimer = 3f;
            }
        }

        /// <summary>
        /// 용병 고용 시 NPC 대화 이벤트 (NPCDialogueWindow 연동).
        /// </summary>
        private void TriggerMercenaryDialogue(MercenaryData data)
        {
            if (NPCDialogueWindow.Instance == null)
            {
                Debug.LogWarning("[MercenaryHireUI] NPCDialogueWindow.Instance 없음");
                return;
            }

            // NPCDialogueWindow의 ShowDialogue를 직접 호출할 수 없으므로
            // 대화 내용을 콘솔과 상태 메시지로 전달
            string[] lines = new string[]
            {
                $"\\\"{data.mercenaryName}이라 합니다.\\\"",
                $"\\\"{data.backStory}\\\"",
                $"\\\"${data.specialAbility}\\\" - 등급: {data.GradeStars}"
            };

            foreach (var line in lines)
            {
                Debug.Log($"[MercenaryDialogue] 🎭 {line}");
            }
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _statStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.8f, 0.9f) }
            };

            _storyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Italic,
                wordWrap = true,
                normal = { textColor = new Color(0.7f, 0.9f, 0.7f) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                normal = { textColor = Color.white },
                hover = { textColor = Color.yellow }
            };

            _msgStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };

            _stylesInitialized = true;
        }
    }
}