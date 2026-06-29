using System;
using System.Collections.Generic;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 42: 백과사전/도감 UI 윈도우 (IMGUI 기반).
    /// L 키로 열기 (UIManager에 등록).
    /// 좌측: 8개 카테고리 탭, 중앙: 항목 리스트, 우측: 상세 정보 패널.
    /// </summary>
    public class EncyclopediaWindow : MonoBehaviour
    {
        // ===== 상수 =====
        private const int TAB_COUNT = 8;
        private static readonly string[] TAB_ICONS =
            { "🌿", "🥩", "🍲", "🧪", "👑", "🏰", "📜", "🏆" };
        private static readonly string[] TAB_NAMES =
            { "약초", "몬스터", "요리", "약물", "영주", "영지", "문서", "업적" };
        private static readonly EncyclopediaCategory[] TAB_CATEGORIES =
        {
            EncyclopediaCategory.Herb,
            EncyclopediaCategory.Monster,
            EncyclopediaCategory.Cooking,
            EncyclopediaCategory.Potion,
            EncyclopediaCategory.Lord,
            EncyclopediaCategory.Territory,
            EncyclopediaCategory.Document,
            EncyclopediaCategory.Achievement
        };

        // ===== 윈도우 설정 =====
        [Header("윈도우 설정")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.L;
        [SerializeField] private int _windowWidth = 900;
        [SerializeField] private int _windowHeight = 620;
        [SerializeField] private GUISkin _customSkin;

        // ===== 상태 =====
        private bool _isOpen;
        private int _selectedTabIndex;
        private Vector2 _entryListScroll;
        private Vector2 _detailScroll;
        private string _searchText = "";
        private string _prevSearchText = "";
        private EncyclopediaEntry _selectedEntry;
        private bool _showAllEntries = true; // true=모두 표시, false=발견만

        // ===== 캐시 =====
        private GUIStyle _tabButtonStyle;
        private GUIStyle _tabButtonActiveStyle;
        private GUIStyle _entryStyle;
        private GUIStyle _entryDiscoveredStyle;
        private GUIStyle _entrySelectedStyle;
        private GUIStyle _detailLabelStyle;
        private GUIStyle _detailValueStyle;
        private GUIStyle _progressBarBackStyle;
        private GUIStyle _progressBarFillStyle;
        private GUIStyle _searchFieldStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _rarityStyle;
        private GUIStyle _toggleButtonStyle;
        private bool _stylesInitialized;

        // ===== 윈도우 Rect (화면 중앙) =====
        private Rect _windowRect;

        // ===== 공개 프로퍼티 =====
        public bool IsOpen => _isOpen;
        public KeyCode ToggleKey => _toggleKey;

        // ===== 생명주기 =====

        private void Start()
        {
            // 윈도우 위치 초기화 (화면 중앙)
            CenterWindow();
        }

        private void CenterWindow()
        {
            _windowRect = new Rect(
                (Screen.width - _windowWidth) * 0.5f,
                (Screen.height - _windowHeight) * 0.5f,
                _windowWidth,
                _windowHeight
            );
        }

        private void Update()
        {
            // L 키 입력 처리 (UIManager에도 등록 가능)
            if (Input.GetKeyDown(_toggleKey))
            {
                Toggle();
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitializeStyles();

            // 배경 딤드
            DrawDimBackground();

            // 메인 윈도우
            _windowRect = GUI.Window(
                GetWindowHash(),
                _windowRect,
                DrawWindowContent,
                "📖 백과사전"
            );
        }

        // ===== 윈도우 토글 =====

        public void Show()
        {
            _isOpen = true;
            _selectedTabIndex = 0;
            _selectedEntry = null;
            _searchText = "";
            _entryListScroll = Vector2.zero;
            _detailScroll = Vector2.zero;
            CenterWindow();
        }

        public void Hide()
        {
            _isOpen = false;
        }

        public void Toggle()
        {
            if (_isOpen) Hide();
            else Show();
        }

        // ===== 스타일 초기화 =====

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            // 탭 버튼 (비활성)
            _tabButtonStyle = new GUIStyle(GUI.skin.button);
            _tabButtonStyle.fontSize = 14;
            _tabButtonStyle.normal.textColor = Color.gray;
            _tabButtonStyle.hover.textColor = Color.white;
            _tabButtonStyle.alignment = TextAnchor.MiddleCenter;
            _tabButtonStyle.fixedHeight = 36;
            _tabButtonStyle.margin = new RectOffset(2, 2, 0, 0);
            _tabButtonStyle.padding = new RectOffset(4, 4, 2, 2);
            _tabButtonStyle.border = new RectOffset(2, 2, 2, 2);

            // 탭 버튼 (활성)
            _tabButtonActiveStyle = new GUIStyle(_tabButtonStyle);
            _tabButtonActiveStyle.normal.textColor = Color.white;
            Color activeBg = new Color(0.25f, 0.45f, 0.65f, 1f);
            _tabButtonActiveStyle.normal.background = MakeTexture(1, 1, activeBg);

            // 항목 리스트 스타일
            _entryStyle = new GUIStyle(GUI.skin.label);
            _entryStyle.fontSize = 13;
            _entryStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            _entryStyle.hover.textColor = Color.white;
            _entryStyle.padding = new RectOffset(8, 4, 4, 4);
            _entryStyle.margin = new RectOffset(0, 0, 1, 1);
            _entryStyle.fixedHeight = 28;
            _entryStyle.alignment = TextAnchor.MiddleLeft;
            _entryStyle.wordWrap = false;

            // 발견 항목 스타일
            _entryDiscoveredStyle = new GUIStyle(_entryStyle);
            _entryDiscoveredStyle.normal.textColor = new Color(0.7f, 1f, 0.7f);

            // 선택된 항목 스타일
            _entrySelectedStyle = new GUIStyle(_entryStyle);
            _entrySelectedStyle.normal.textColor = Color.white;
            Color selBg = new Color(0.3f, 0.5f, 0.7f, 0.6f);
            _entrySelectedStyle.normal.background = MakeTexture(1, 1, selBg);

            // 상세 정보 라벨
            _detailLabelStyle = new GUIStyle(GUI.skin.label);
            _detailLabelStyle.fontSize = 12;
            _detailLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            _detailLabelStyle.alignment = TextAnchor.UpperLeft;
            _detailLabelStyle.wordWrap = true;
            _detailLabelStyle.padding = new RectOffset(2, 2, 2, 2);

            _detailValueStyle = new GUIStyle(GUI.skin.label);
            _detailValueStyle.fontSize = 13;
            _detailValueStyle.normal.textColor = Color.white;
            _detailValueStyle.alignment = TextAnchor.UpperLeft;
            _detailValueStyle.wordWrap = true;
            _detailValueStyle.padding = new RectOffset(2, 2, 2, 2);

            // 프로그레스 바
            _progressBarBackStyle = new GUIStyle(GUI.skin.label);
            _progressBarBackStyle.normal.background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.2f));
            _progressBarBackStyle.border = new RectOffset(1, 1, 1, 1);

            _progressBarFillStyle = new GUIStyle(GUI.skin.label);
            _progressBarFillStyle.normal.background = MakeTexture(1, 1, new Color(0.2f, 0.7f, 0.2f));
            _progressBarFillStyle.border = new RectOffset(1, 1, 1, 1);

            // 검색 필드
            _searchFieldStyle = new GUIStyle(GUI.skin.textField);
            _searchFieldStyle.fontSize = 13;
            _searchFieldStyle.padding = new RectOffset(6, 6, 4, 4);
            _searchFieldStyle.normal.textColor = Color.white;
            _searchFieldStyle.focused.textColor = Color.white;

            // 헤더
            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 16;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = Color.white;
            _headerStyle.alignment = TextAnchor.MiddleLeft;
            _headerStyle.padding = new RectOffset(8, 4, 4, 4);

            // 등급 스타일
            _rarityStyle = new GUIStyle(GUI.skin.label);
            _rarityStyle.fontSize = 12;
            _rarityStyle.alignment = TextAnchor.MiddleLeft;

            // 토글 버튼
            _toggleButtonStyle = new GUIStyle(GUI.skin.button);
            _toggleButtonStyle.fontSize = 11;
            _toggleButtonStyle.padding = new RectOffset(4, 4, 2, 2);
            _toggleButtonStyle.fixedHeight = 22;

            _stylesInitialized = true;
        }

        // ===== 드로잉 =====

        private void DrawDimBackground()
        {
            Color dimColor = new Color(0f, 0f, 0f, 0.6f);
            var tex = MakeTexture(1, 1, dimColor);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), tex, ScaleMode.StretchToFill);
        }

        private void DrawWindowContent(int windowId)
        {
            // 상단 영역: 검색바 + 필터 토글
            DrawTopBar();

            // 좌측: 카테고리 탭
            DrawCategoryTabs();

            // 중앙: 항목 리스트
            DrawEntryList();

            // 우측: 상세 정보 패널
            DrawDetailPanel();

            // 하단: 수집률 프로그레스 바 (전체 + 카테고리별)
            DrawProgressBar();

            // 드래그 가능
            GUI.DragWindow(new Rect(0, 0, _windowWidth, 30));
        }

        private void DrawTopBar()
        {
            const float topBarHeight = 32f;
            const float padding = 8f;
            float y = 30f; // 타이틀 바 아래

            // 검색 필드
            float searchWidth = 220f;
            float toggleWidth = 120f;
            float closeWidth = 60f;

            GUI.Label(new Rect(padding, y + 2, 80f, 22f), "🔍 검색:", _detailLabelStyle);

            string newSearch = GUI.TextField(
                new Rect(80f, y, searchWidth, 22f),
                _searchText,
                _searchFieldStyle
            );

            // 검색어 변경 시 선택 초기화
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                _selectedEntry = null;
                _entryListScroll = Vector2.zero;
            }

            // 필터 토글
            Rect toggleRect = new Rect(80f + searchWidth + 10f, y, toggleWidth, 22f);
            if (GUI.Button(toggleRect, _showAllEntries ? "✅ 전체 보기" : "📌 발견만", _toggleButtonStyle))
            {
                _showAllEntries = !_showAllEntries;
            }

            // 닫기 버튼
            Rect closeRect = new Rect(_windowWidth - closeWidth - padding, y, closeWidth, 22f);
            if (GUI.Button(closeRect, "✕ 닫기"))
            {
                Hide();
            }
        }

        private void DrawCategoryTabs()
        {
            const float tabStartX = 8f;
            const float tabStartY = 68f;
            const float tabWidth = 100f;
            const float tabHeight = 38f;
            const float tabSpacing = 4f;

            for (int i = 0; i < TAB_COUNT; i++)
            {
                float y = tabStartY + i * (tabHeight + tabSpacing);
                Rect tabRect = new Rect(tabStartX, y, tabWidth, tabHeight);

                // 발견/전체 수 표시
                int discovered = 0;
                int total = 0;
                if (EncyclopediaManager.Instance != null)
                {
                    discovered = EncyclopediaManager.Instance.GetCategoryDiscoveredCount(TAB_CATEGORIES[i]);
                    total = EncyclopediaManager.Instance.GetCategoryTotalCount(TAB_CATEGORIES[i]);
                }

                string label = $"{TAB_ICONS[i]} {TAB_NAMES[i]}\n{discovered}/{total}";

                GUIStyle style = (i == _selectedTabIndex) ? _tabButtonActiveStyle : _tabButtonStyle;

                if (GUI.Button(tabRect, label, style))
                {
                    if (_selectedTabIndex != i)
                    {
                        _selectedTabIndex = i;
                        _selectedEntry = null;
                        _entryListScroll = Vector2.zero;
                        _detailScroll = Vector2.zero;
                    }
                }
            }
        }

        private void DrawEntryList()
        {
            const float listStartX = 116f;
            const float listStartY = 68f;
            const float listWidth = 340f;
            const float listHeight = 440f;
            const float entryHeight = 28f;

            var entries = GetFilteredEntries();
            float contentHeight = entries.Count * entryHeight;
            Rect viewRect = new Rect(0, 0, listWidth - 20f, contentHeight);
            Rect scrollRect = new Rect(listStartX, listStartY, listWidth, listHeight);

            GUI.Box(scrollRect, "", _progressBarBackStyle);

            _entryListScroll = GUI.BeginScrollView(scrollRect, _entryListScroll, viewRect, false, true);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float y = i * entryHeight;

                Rect entryRect = new Rect(4, y, listWidth - 28f, entryHeight);

                // 선택 상태
                bool isSelected = (_selectedEntry == entry);

                // 스타일 결정
                GUIStyle style;
                if (isSelected)
                    style = _entrySelectedStyle;
                else if (entry.IsDiscovered)
                    style = _entryDiscoveredStyle;
                else
                    style = _entryStyle;

                // 항목 표시
                string icon = entry.IsDiscovered ? "✅" : "❌";
                string name = entry.IsDiscovered ? entry.entryName : "???";
                string label = $"{icon} {name}";

                if (GUI.Button(entryRect, label, style))
                {
                    _selectedEntry = entry;
                    _detailScroll = Vector2.zero;
                }

                // 마지막 항목 구분선
                if (i < entries.Count - 1)
                {
                    GUI.Label(new Rect(8, y + entryHeight - 1, listWidth - 36f, 1f),
                        "", _progressBarBackStyle);
                }
            }

            GUI.EndScrollView();
        }

        private void DrawDetailPanel()
        {
            const float detailStartX = 468f;
            const float detailStartY = 68f;
            const float detailWidth = 420f;
            const float detailHeight = 440f;

            // 배경
            GUI.Box(new Rect(detailStartX, detailStartY, detailWidth, detailHeight),
                "", _progressBarBackStyle);

            if (_selectedEntry == null)
            {
                // 선택된 항목 없음 안내
                GUI.Label(
                    new Rect(detailStartX + 20f, detailStartY + 180f, detailWidth - 40f, 40f),
                    "좌측 목록에서 항목을 선택하세요.",
                    _detailLabelStyle
                );
                return;
            }

            var entry = _selectedEntry;
            float x = detailStartX + 12f;
            float y = detailStartY + 12f;
            float labelW = 100f;
            float valueW = detailWidth - 130f;
            float lineH = 28f;

            _detailScroll = GUI.BeginScrollView(
                new Rect(detailStartX, detailStartY, detailWidth, detailHeight),
                _detailScroll,
                new Rect(0, 0, detailWidth - 20f, 420f),
                false, true
            );

            float cy = 8f; // content y (scroll view 내부)

            // 항목명
            if (entry.IsDiscovered)
            {
                GUI.Label(new Rect(8f, cy, labelW, lineH), "📛 이름:", _detailLabelStyle);
                GUI.Label(new Rect(8f + labelW, cy, valueW, lineH), entry.entryName, _detailValueStyle);
                cy += lineH + 4f;
            }
            else
            {
                GUI.Label(new Rect(8f, cy, labelW, lineH), "📛 이름:", _detailLabelStyle);
                GUI.Label(new Rect(8f + labelW, cy, valueW, lineH), "??? (미발견)", _detailValueStyle);
                cy += lineH + 4f;
            }

            // 카테고리
            string catName = GetCategoryName(entry.category);
            GUI.Label(new Rect(8f, cy, labelW, lineH), "📂 분류:", _detailLabelStyle);
            GUI.Label(new Rect(8f + labelW, cy, valueW, lineH), catName, _detailValueStyle);
            cy += lineH + 4f;

            // 등급
            if (entry.IsDiscovered || (EncyclopediaManager.Instance != null &&
                EncyclopediaManager.Instance.OverallCompletionRate >= 0.10f))
            {
                GUI.Label(new Rect(8f, cy, labelW, lineH), "⭐ 등급:", _detailLabelStyle);
                _rarityStyle.normal.textColor = entry.GetRarityColor();
                GUI.Label(new Rect(8f + labelW, cy, valueW, lineH),
                    entry.GetRarityName(), _rarityStyle);
                cy += lineH + 4f;
            }

            // 발견 상태
            GUI.Label(new Rect(8f, cy, labelW, lineH), "🔍 상태:", _detailLabelStyle);
            string statusStr = entry.IsDiscovered
                ? $"✅ 발견됨 ({entry.DiscoveryDate})"
                : "❌ 미발견";
            GUI.Label(new Rect(8f + labelW, cy, valueW, lineH), statusStr, _detailValueStyle);
            cy += lineH + 4f;

            // 설명
            string descText = "설명";
            if (entry.IsDiscovered)
            {
                descText = entry.description;
            }
            else if (EncyclopediaManager.Instance != null &&
                     EncyclopediaManager.Instance.OverallCompletionRate >= 0.10f)
            {
                // 10% 보상: 기본 정보 표시
                descText = entry.description;
            }
            else
            {
                descText = "아직 발견되지 않은 정보입니다.\n도감 수집률 10% 달성 시 기본 정보가 공개됩니다.";
            }

            GUI.Label(new Rect(8f, cy, labelW, lineH), "📝 설명:", _detailLabelStyle);
            cy += lineH;

            float descHeight = GetTextHeight(descText, valueW, 13);
            GUI.Label(new Rect(8f, cy, valueW, descHeight), descText, _detailLabelStyle);
            cy += descHeight + 8f;

            // 발견 위치 (발견 시에만)
            if (entry.IsDiscovered && !string.IsNullOrEmpty(entry.location))
            {
                GUI.Label(new Rect(8f, cy, labelW, lineH), "📍 위치:", _detailLabelStyle);
                GUI.Label(new Rect(8f + labelW, cy, valueW, lineH), entry.location, _detailValueStyle);
                cy += lineH + 4f;
            }

            GUI.EndScrollView();
        }

        private void DrawProgressBar()
        {
            const float barX = 116f;
            const float barY = 515f;
            const float barWidth = 770f;
            const float barHeight = 22f;

            if (EncyclopediaManager.Instance == null) return;

            // 전체 수집률
            float overallRate = EncyclopediaManager.Instance.OverallCompletionRate;
            int totalDiscovered = EncyclopediaManager.Instance.TotalDiscoveredCount;
            int totalEntries = EncyclopediaManager.Instance.TotalEntryCount;

            // 배경
            GUI.Box(new Rect(barX, barY, barWidth, barHeight), "", _progressBarBackStyle);

            // 채움
            float fillWidth = barWidth * overallRate;
            if (fillWidth > 2f)
            {
                GUI.Box(new Rect(barX, barY, fillWidth, barHeight), "", _progressBarFillStyle);
            }

            // 텍스트
            string progressText = $"📊 전체 수집률: {totalDiscovered}/{totalEntries} ({overallRate * 100f:F1}%)";
            GUI.Label(new Rect(barX + 8f, barY + 1f, barWidth - 16f, barHeight - 2f),
                progressText, _detailValueStyle);

            // 현재 탭의 수집률
            float catRate = EncyclopediaManager.Instance.GetCategoryCompletionRate(TAB_CATEGORIES[_selectedTabIndex]);
            int catDisc = EncyclopediaManager.Instance.GetCategoryDiscoveredCount(TAB_CATEGORIES[_selectedTabIndex]);
            int catTotal = EncyclopediaManager.Instance.GetCategoryTotalCount(TAB_CATEGORIES[_selectedTabIndex]);

            string catText = $"🏷️ [{TAB_ICONS[_selectedTabIndex]} {TAB_NAMES[_selectedTabIndex]}] {catDisc}/{catTotal} ({catRate * 100f:F1}%)";
            GUI.Label(new Rect(barX + 8f, barY + barHeight + 4f, barWidth - 16f, 20f),
                catText, _detailLabelStyle);

            // 보상 임계값 표시
            DrawRewardThresholds(barX, barY + barHeight + 28f, barWidth);
        }

        private void DrawRewardThresholds(float x, float y, float width)
        {
            if (EncyclopediaManager.Instance == null) return;

            float rate = EncyclopediaManager.Instance.OverallCompletionRate;
            GUI.Label(new Rect(x, y, width, 18f), "🎁 수집률 보상:", _headerStyle);

            string[] rewardTexts =
            {
                "10% - 기본 정보 잠금 해제",
                "25% - 제작 성공률 +5%",
                "50% - 특수 레시피 잠금 해제",
                "75% - 제작 성공률 +10%",
                "100% - 전설 아이템/업적 해금"
            };
            float[] thresholds = { 0.10f, 0.25f, 0.50f, 0.75f, 1.00f };

            float rewardY = y + 22f;
            for (int i = 0; i < rewardTexts.Length; i++)
            {
                bool achieved = rate >= thresholds[i] - 0.001f;
                string icon = achieved ? "✅" : "⬜";
                Color color = achieved ? new Color(0.7f, 1f, 0.7f) : new Color(0.6f, 0.6f, 0.6f);
                GUIStyle style = new GUIStyle(_detailLabelStyle);
                style.normal.textColor = color;
                GUI.Label(new Rect(x + 10f, rewardY, width - 20f, 18f),
                    $"{icon} {rewardTexts[i]}", style);
                rewardY += 18f;
            }
        }

        // ===== 헬퍼 =====

        /// <summary>윈도우 ID 해시 (고유값)</summary>
        private int GetWindowHash()
        {
            return "EncyclopediaWindow".GetHashCode();
        }

        private List<EncyclopediaEntry> GetFilteredEntries()
        {
            if (EncyclopediaManager.Instance == null)
                return new List<EncyclopediaEntry>();

            var allEntries = EncyclopediaManager.Instance.GetCategoryEntries(TAB_CATEGORIES[_selectedTabIndex]);
            var result = new List<EncyclopediaEntry>();

            for (int i = 0; i < allEntries.Count; i++)
            {
                var entry = allEntries[i];

                // 필터: 발견만
                if (!_showAllEntries && !entry.IsDiscovered)
                    continue;

                // 검색어 필터 (발견된 항목만 검색 가능)
                if (!string.IsNullOrEmpty(_searchText) && entry.IsDiscovered)
                {
                    string search = _searchText.ToLower();
                    bool match = entry.entryName.ToLower().Contains(search) ||
                                 entry.description.ToLower().Contains(search) ||
                                 entry.entryId.ToLower().Contains(search);
                    if (!match)
                        continue;
                }

                result.Add(entry);
            }

            // 정렬: 발견된 항목 먼저, 그 다음 미발견
            result.Sort((a, b) =>
            {
                if (a.IsDiscovered && !b.IsDiscovered) return -1;
                if (!a.IsDiscovered && b.IsDiscovered) return 1;
                return string.Compare(a.entryName, b.entryName, StringComparison.Ordinal);
            });

            return result;
        }

        private string GetCategoryName(EncyclopediaCategory cat)
        {
            for (int i = 0; i < TAB_CATEGORIES.Length; i++)
            {
                if (TAB_CATEGORIES[i] == cat)
                    return $"{TAB_ICONS[i]} {TAB_NAMES[i]}";
            }
            return cat.ToString();
        }

        /// <summary>텍스트 높이 계산</summary>
        private float GetTextHeight(string text, float width, int fontSize)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = fontSize, wordWrap = true };
            return style.CalcHeight(new GUIContent(text), width);
        }

        /// <summary>단색 텍스처 생성 (IMGUI 스타일 용)</summary>
        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }
    }
}