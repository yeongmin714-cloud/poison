using System.Collections.Generic;
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

        // --- OnGUI GC-safe 캐시 (매 프레임 1회 갱신) ---
        private MercenaryData[] _cachedMercData;
        private readonly HashSet<string> _cachedHiredIds = new HashSet<string>();
        private float _cachedContentHeight;

        // 재사용 GUIContent (string GC 억제)
        private static readonly GUIContent _titleContent = new GUIContent("🍺 용병 고용소");
        private static readonly GUIContent _emptyContent = GUIContent.none;
        private readonly GUIContent _goldContent = new GUIContent();
        private readonly GUIContent _hiredContent = new GUIContent();
        private readonly GUIContent _backStoryContent = new GUIContent();
        private readonly GUIContent _affinityContent = new GUIContent();

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

        /// <summary>
        /// OnGUI 진입 전 1회 호출: 모든 데이터 캐싱으로 GC 할당 최소화.
        /// </summary>
        private void RefreshCachedData()
        {
            // 용병 데이터 배열 (매니저 내부에서 할당 — 피할 수 없음, 1회만)
            _cachedMercData = MercenaryManager.Instance != null
                ? MercenaryManager.Instance.GetAllMercenaryData()
                : System.Array.Empty<MercenaryData>();

            // 고용된 용병 ID 집합 → O(1) 조회
            _cachedHiredIds.Clear();
            if (MercenaryManager.Instance != null)
            {
                var hired = MercenaryManager.Instance.GetHiredMercenaries();
                for (int i = 0; i < hired.Length; i++)
                    _cachedHiredIds.Add(hired[i].data.id);
            }

            _cachedContentHeight = _cachedMercData.Length * 222.5f + 20f;
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            EnsureStyles();
            RefreshCachedData();

            float panelW = 930f;
            float panelH = 780f;
            float x = (Screen.width - panelW) / 2f;
            float y = (Screen.height - panelH) / 2f;

            // 배경
            Color bgColor = _theme != null ? _theme.BgColor : new Color(0.1f, 0.1f, 0.15f, 0.6f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), _emptyContent);
            var oldColor = GUI.color;
            GUI.color = bgColor;
            GUI.Box(new Rect(x, y, panelW, panelH), _emptyContent);
            GUI.color = oldColor;

            // 타이틀 (캐시된 GUIContent)
            GUI.Label(new Rect(x + 10, y + 10, panelW - 20, 45), _titleContent, _titleStyle);

            // 상태 메시지
            if (!string.IsNullOrEmpty(_statusMessage) && _statusTimer > 0)
            {
                GUI.Label(new Rect(x + 10, y + 40, panelW - 20, 36), _statusMessage, _msgStyle);
            }

            // [FIX] 골드 & 고용 인원 — ScrollView 밖에서 절대 좌표로 그림
            DrawGoldDisplay(x + 10, y + 55);
            DrawHiredCount(x + panelW - 160, y + 55);

            float contentTop = y + 80;
            float contentH = panelH - 100;

            _scrollPos = GUI.BeginScrollView(
                new Rect(x + 10, contentTop, panelW - 20, contentH),
                _scrollPos,
                new Rect(0, 0, panelW - 40, _cachedContentHeight)
            );

            float cy = 0;

            for (int i = 0; i < _cachedMercData.Length; i++)
            {
                var merc = _cachedMercData[i];
                bool isHired = _cachedHiredIds.Contains(merc.id);
                bool isSelected = _selectedMercenaryId == merc.id;

                DrawMercenaryEntry(merc, isHired, isSelected, panelW - 40, ref cy);
            }

            GUI.EndScrollView();
        }

        private void DrawGoldDisplay(float x, float y)
        {
            int gold = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetItemCount("gold")
                : 0;
            _goldContent.text = $"💰 {gold}G";
            GUI.Label(new Rect(x, y, 338, 33), _goldContent, _statStyle);
        }

        private void DrawHiredCount(float x, float y)
        {
            int hired = MercenaryManager.Instance != null ? MercenaryManager.Instance.HiredCount : 0;
            int max = MercenaryManager.Instance != null ? MercenaryManager.Instance.MaxMercenaries : 10;
            _hiredContent.text = $"👥 {hired}/{max}";
            GUI.Label(new Rect(x, y, 338, 33), _hiredContent, _statStyle);
        }

        private void DrawMercenaryEntry(MercenaryData merc, bool isHired, bool isSelected, float entryW, ref float cy)
        {
            float entryH = 127.5f;
            float xOff = 5f;

            // 배경 박스
            GUI.Box(new Rect(0, cy, entryW, entryH), _emptyContent);

            // 등급 표시 (GradeStars는 switch const string 반환 — GC-safe)
            GUI.Label(new Rect(xOff, cy + 5, 135, 33), merc.GradeStars, _nameStyle);

            // 이름
            GUI.Label(new Rect(xOff + 65, cy + 5, 338, 33), merc.mercenaryName, _titleStyle);

            // 직업
            string jobIcon = merc.jobType == "Bard" ? "🎵" : "⚔️";
            GUI.Label(new Rect(xOff + 220, cy + 5, 180, 33), $"{jobIcon} {merc.jobType}", _statStyle);

            // 능력치
            GUI.Label(new Rect(xOff, cy + 30, 675, 30), $"❤️{merc.maxHP} ⚔️{merc.attack} 🛡️{merc.defense} 💨{merc.moveSpeed}", _statStyle);

            // 특수 능력
            GUI.Label(new Rect(xOff, cy + 50, 675, 30), $"✨ {merc.specialAbility}", _statStyle);

            // 고용 비용
            GUI.Label(new Rect(xOff + 310, cy + 5, 225, 33), $"💰 {merc.hireCost}G", _nameStyle);

            // 버튼
            float btnX = entryW - 235;
            if (isHired)
            {
                // 해고 버튼 (빨간색 계열)
                GUI.color = new Color(1f, 0.4f, 0.3f);
                if (GUI.Button(new Rect(btnX, cy + 25, 225, 42), "🔴 해고", _buttonStyle))
                {
                    OnFireMercenary(merc.id);
                }
                GUI.color = Color.white;

                // 고용됨 표시
                GUI.Label(new Rect(btnX, cy + 58, 225, 30), "✅ 고용됨", _statStyle);
            }
            else
            {
                // 고용 버튼
                if (GUI.Button(new Rect(btnX, cy + 25, 225, 42), "📋 고용", _buttonStyle))
                {
                    OnHireMercenary(merc.id);
                }
            }

            // 상세 보기 버튼
            if (GUI.Button(new Rect(btnX - 55, cy + 25, 112, 42), "📖", _buttonStyle))
            {
                _selectedMercenaryId = _selectedMercenaryId == merc.id ? "" : merc.id;
            }

            // 선택된 용병의 상세 정보 표시
            if (isSelected)
            {
                float detailY = cy + entryH + 2;
                float detailH = 90f;
                GUI.Box(new Rect(0, detailY, entryW, detailH), _emptyContent);
                _backStoryContent.text = $"📜 {merc.backStory}";
                GUI.Label(new Rect(xOff + 5, detailY + 5, entryW - 10, 75), _backStoryContent, _storyStyle);

                // 호감도 표시 (고용된 경우)
                if (isHired)
                {
                    float aff = MercenaryManager.Instance.GetAffinity(merc.id);
                    _affinityContent.text = $"❤️ 호감도: {(int)aff}% (보너스: +{aff / 100f * 0.2f * 100:F0}%)";
                    GUI.Label(new Rect(xOff + 5, detailY + detailH - 22, 450, 30), _affinityContent, _msgStyle);
                }

                cy += entryH + detailH + 5;
            }
            else
            {
                cy += entryH + 5;
            }
        }

        // ===== 이벤트 핸들러 =====

        private void OnHireMercenary(string mercenaryId)
        {
            if (MercenaryManager.Instance == null) return;

            if (!MercenaryManager.Instance.TryGetMercenaryData(mercenaryId, out var data))
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
                Debug.Log($"[MercenaryHireUI] 🎭 용병 고용: {data.mercenaryName}");
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

            string name = mercenaryId;
            if (MercenaryManager.Instance.TryGetMercenaryData(mercenaryId, out var data))
                name = data.mercenaryName;

            if (MercenaryManager.Instance.FireMercenary(mercenaryId))
            {
                _statusMessage = $"🔴 {name} 해고됨.";
                _statusTimer = 3f;
            }
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 72, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _statStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                normal = { textColor = new Color(0.8f, 0.8f, 0.9f) }
            };

            _storyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44, fontStyle = FontStyle.Italic,
                wordWrap = true,
                normal = { textColor = new Color(0.7f, 0.9f, 0.7f) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                normal = { textColor = Color.white },
                hover = { textColor = Color.yellow }
            };

            _msgStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };

            _stylesInitialized = true;
        }
    }
}
