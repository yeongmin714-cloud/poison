using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// ⚡ 빠른 이동 UI — IMGUI 기반 싱글톤.
    /// MapWindow에서 열리며, 소유한 영지 목록을 표시하고 클릭으로 이동합니다.
    /// ESC 키로 닫을 수 있습니다.
    /// </summary>
    public class FastTravelUI : MonoBehaviour
    {
        private static FastTravelUI _instance;
        public static FastTravelUI Instance => _instance;

        [Header("UI Layout")]
        [SerializeField] private float _windowWidth = 500f;
        [SerializeField] private float _windowHeight = 600f;
        [SerializeField] private float _padding = 15f;
        [SerializeField] private float _itemHeight = 40f;
        [SerializeField] private float _buttonHeight = 35f;

        // 상태
        private static bool _isOpen = false;
        private Vector2 _scrollPosition;
        private List<TerritoryDefinition> _ownedTerritories;
        private int _selectedIndex = -1;
        private bool _showConfirmDialog = false;

        // 캐시된 스타일
        private GUIStyle _titleStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _selectedItemStyle;
        private GUIStyle _costStyle;
        private GUIStyle _confirmStyle;
        private bool _stylesInitialized;

        // ===== 생명주기 =====

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            // ESC 키 감지
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_showConfirmDialog)
                {
                    _showConfirmDialog = false; // 확인 대화상자만 닫기
                }
                else
                {
                    Hide();
                }
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitializeStyles();

            // 창 영역 계산 (화면 중앙)
            float sw = Screen.width;
            float sh = Screen.height;
            float wx = (sw - _windowWidth) * 0.5f;
            float wy = (sh - _windowHeight) * 0.5f;
            Rect windowRect = new Rect(wx, wy, _windowWidth, _windowHeight);

            // 배경
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.12f, 0.18f);
            GUI.Box(windowRect, "");
            GUI.backgroundColor = origBg;

            // 테두리
            Color origColor = GUI.color;
            GUI.color = new Color(0.3f, 0.3f, 0.5f);
            GUI.Box(windowRect, "");
            GUI.color = origColor;

            // 내부 영역
            Rect innerRect = new Rect(
                windowRect.x + _padding,
                windowRect.y + _padding,
                windowRect.width - _padding * 2,
                windowRect.height - _padding * 2
            );

            // 제목
            Rect titleRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 35f);
            GUI.Label(titleRect, "⚡ 빠른 이동", _titleStyle);

            // 골드 정보
            int currentGold = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetItemCount("gold")
                : 0;
            Rect goldRect = new Rect(innerRect.x, titleRect.y + titleRect.height + 2f, innerRect.width, 22f);
            GUI.Label(goldRect, $"💰 보유 골드: {currentGold}G", _costStyle);

            // 목록 영역
            float listY = goldRect.y + goldRect.height + 5f;
            float listHeight = innerRect.height - listY + innerRect.y - _buttonHeight - 15f;
            Rect listRect = new Rect(innerRect.x, listY, innerRect.width, listHeight);

            // 영지 목록 갱신
            RefreshOwnedTerritories();

            // 스크롤뷰
            Rect viewRect = new Rect(0, 0, listRect.width - 20f, (_ownedTerritories.Count + 1) * _itemHeight);
            _scrollPosition = GUI.BeginScrollView(listRect, _scrollPosition, viewRect);

            if (_ownedTerritories.Count == 0)
            {
                Rect emptyRect = new Rect(0, 10f, viewRect.width, 40f);
                GUI.Label(emptyRect, "소유한 영지가 없습니다.\n영지를 정복한 후 이용하세요.", _costStyle);
            }
            else
            {
                for (int i = 0; i < _ownedTerritories.Count; i++)
                {
                    var def = _ownedTerritories[i];
                    int cost = FastTravelSystem.Instance != null
                        ? FastTravelSystem.Instance.GetTravelCost(def.difficulty)
                        : 5;
                    bool canAfford = FastTravelSystem.Instance != null
                        ? FastTravelSystem.Instance.CanAffordTravel(cost)
                        : false;

                    string ringText = GetRingText(def.difficulty);
                    string costText = $"{cost}G";

                    // 아이템 배경
                    Rect itemRect = new Rect(0, i * _itemHeight, viewRect.width, _itemHeight - 2f);

                    if (_selectedIndex == i)
                    {
                        GUI.backgroundColor = new Color(0.2f, 0.4f, 0.6f);
                        GUI.Box(itemRect, "", _selectedItemStyle);
                        GUI.backgroundColor = origBg;
                    }

                    // 영지 이름 + Ring + 비용
                    string label = $"{def.territoryName}  |  {ringText}  |  {costText}";
                    Color textColor = canAfford ? Color.white : Color.gray;

                    GUIStyle tempStyle = new GUIStyle(_itemStyle)
                    {
                        normal = { textColor = textColor }
                    };

                    if (GUI.Button(itemRect, label, tempStyle))
                    {
                        _selectedIndex = i;
                        // 골드가 충분하면 확인 대화상자 표시
                        if (canAfford && FastTravelSystem.Instance != null)
                        {
                            _showConfirmDialog = true;
                        }
                        else
                        {
                            Debug.Log("[FastTravelUI] ⚠️ 골드가 부족하여 이동할 수 없습니다.");
                        }
                    }
                }
            }

            GUI.EndScrollView();

            // 닫기 버튼
            float closeBtnY = windowRect.y + windowRect.height - _buttonHeight - _padding;
            Rect closeBtnRect = new Rect(innerRect.x, closeBtnY, innerRect.width, _buttonHeight);
            if (GUI.Button(closeBtnRect, "[ ESC ] 닫기"))
            {
                Hide();
            }

            // === 확인 대화상자 (오버레이) ===
            if (_showConfirmDialog && _selectedIndex >= 0 && _selectedIndex < _ownedTerritories.Count)
            {
                DrawConfirmDialog(windowRect);
            }
        }

        // ===== 확인 대화상자 =====

        private void DrawConfirmDialog(Rect parentRect)
        {
            var def = _ownedTerritories[_selectedIndex];
            int cost = FastTravelSystem.Instance != null
                ? FastTravelSystem.Instance.GetTravelCost(def.difficulty)
                : 5;

            float dlgWidth = 350f;
            float dlgHeight = 200f;
            float dlgX = parentRect.x + (parentRect.width - dlgWidth) * 0.5f;
            float dlgY = parentRect.y + (parentRect.height - dlgHeight) * 0.5f;
            Rect dlgRect = new Rect(dlgX, dlgY, dlgWidth, dlgHeight);

            // 딤드 배경 (전체 화면)
            Color dimColor = new Color(0f, 0f, 0f, 0.5f);
            GUI.color = dimColor;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.color = Color.white;

            // 대화상자 배경
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            GUI.Box(dlgRect, "");
            GUI.backgroundColor = origBg;

            // 테두리
            Color origBorder = GUI.color;
            GUI.color = new Color(0.4f, 0.35f, 0.2f);
            GUI.Box(dlgRect, "");
            GUI.color = origBorder;

            // 내용
            float margin = 15f;
            Rect dlgInner = new Rect(dlgX + margin, dlgY + margin, dlgWidth - margin * 2, dlgHeight - margin * 2);

            // 제목
            Rect dlgTitleRect = new Rect(dlgInner.x, dlgInner.y, dlgInner.width, 30f);
            GUI.Label(dlgTitleRect, "🚀 빠른 이동 확인", _confirmStyle);

            // 메시지
            string ringText = GetRingText(def.difficulty);
            string msg = $"「{def.territoryName}」(으)로 이동하시겠습니까?\n{ringText} | 비용: {cost}G";
            Rect msgRect = new Rect(dlgInner.x, dlgTitleRect.y + dlgTitleRect.height + 5f, dlgInner.width, 50f);
            GUI.Label(msgRect, msg, _costStyle);

            // 버튼 영역
            float btnY = dlgRect.y + dlgRect.height - margin - _buttonHeight;
            float btnWidth = (dlgInner.width - 10f) * 0.5f;

            // 확인 버튼
            Rect confirmBtnRect = new Rect(dlgInner.x, btnY, btnWidth, _buttonHeight);
            Color origBtnColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0.5f, 0f);
            if (GUI.Button(confirmBtnRect, "✅ 이동"))
            {
                ExecuteFastTravel(def.id);
                _showConfirmDialog = false;
                Hide();
            }
            GUI.backgroundColor = origBtnColor;

            // 취소 버튼
            Rect cancelBtnRect = new Rect(dlgInner.x + btnWidth + 10f, btnY, btnWidth, _buttonHeight);
            GUI.backgroundColor = new Color(0.5f, 0f, 0f);
            if (GUI.Button(cancelBtnRect, "❌ 취소"))
            {
                _showConfirmDialog = false;
            }
            GUI.backgroundColor = origBtnColor;
        }

        // ===== 헬퍼 메서드 =====

        /// <summary>
        /// 빠른 이동을 실제로 실행합니다.
        /// </summary>
        private static void ExecuteFastTravel(TerritoryId targetId)
        {
            if (FastTravelSystem.Instance == null)
            {
                Debug.LogError("[FastTravelUI] FastTravelSystem.Instance가 null입니다!");
                return;
            }
            FastTravelSystem.Instance.ExecuteFastTravel(targetId);
        }

        /// <summary>
        /// 소유 영지 목록을 새로고침합니다.
        /// </summary>
        private void RefreshOwnedTerritories()
        {
            if (FastTravelSystem.Instance == null) return;

            var owned = FastTravelSystem.Instance.GetPlayerOwnedTerritories();
            _ownedTerritories = owned;
        }

        /// <summary>
        /// Ring 난이도에 따른 표시 문자열을 반환합니다.
        /// </summary>
        private static string GetRingText(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => "Ring 1 🟢",
                TerritoryDifficulty.Ring2 => "Ring 2 🟡",
                TerritoryDifficulty.Ring3 => "Ring 3 🟠",
                TerritoryDifficulty.Ring4 => "Ring 4 🔴",
                TerritoryDifficulty.Empire => "Empire 👑",
                _ => "알 수 없음"
            };
        }

        /// <summary>
        /// GUI 스타일을 초기화합니다.
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.70f, 0.20f) } // 금색
            };

            _itemStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                hover = { textColor = Color.yellow },
                active = { textColor = Color.green }
            };

            _selectedItemStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, new Color(0.2f, 0.4f, 0.6f, 0.5f)) }
            };

            _costStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                richText = true,
                wordWrap = true
            };

            _confirmStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.70f, 0.20f) }
            };

            _stylesInitialized = true;
        }

        /// <summary>단색 텍스처 생성 (스타일용)</summary>
        private static Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        // ===== 공개 정적 메서드 =====

        /// <summary>빠른 이동 UI를 엽니다.</summary>
        public static void Show()
        {
            if (_instance == null)
            {
                Debug.LogError("[FastTravelUI] FastTravelUI 인스턴스가 없습니다! Scene에 FastTravelUI를 추가해주세요.");
                return;
            }

            if (_isOpen) return;

            // FastTravelSystem 확인
            if (FastTravelSystem.Instance == null)
            {
                Debug.LogError("[FastTravelUI] FastTravelSystem.Instance가 null입니다! Scene에 FastTravelSystem을 추가해주세요.");
                return;
            }

            _isOpen = true;
            _instance._selectedIndex = -1;
            _instance._showConfirmDialog = false;
            _instance._scrollPosition = Vector2.zero;
            _instance.RefreshOwnedTerritories();

            Debug.Log("[FastTravelUI] ⚡ 빠른 이동 UI 열림");
        }

        /// <summary>빠른 이동 UI를 닫습니다.</summary>
        public static void Hide()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _instance._showConfirmDialog = false;
            Debug.Log("[FastTravelUI] 빠른 이동 UI 닫힘");
        }

        /// <summary>UI가 열려있는지 확인합니다.</summary>
        public static bool IsOpen => _isOpen;
    }
}