using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-11: 처형/살려주기 UI — 영주가 항복했을 때 선택지를 표시합니다.
    /// 
    /// IMGUI 기반 패널로 "영주가 항복했습니다. 처형하시겠습니까?" 메시지를 표시하고,
    /// 처형(Execute) / 살려주기(Spare) 버튼을 제공합니다.
    /// LordSurrenderSystem과 통합되어 영지 점령 및 보상을 처리합니다.
    /// 
    /// 사용법:
    ///   MercyUI.Show(territoryId, lordName);
    ///   MercyUI.Hide();
    /// </summary>
    public class MercyUI : MonoBehaviour
    {
        private static MercyUI _instance;

        [Header("UI 설정")]
        [SerializeField] private float _panelWidth = 400f;
        [SerializeField] private float _panelHeight = 280f;
        [SerializeField] private float _rewardPopupWidth = 350f;
        [SerializeField] private float _rewardPopupHeight = 200f;

        // 내부 상태
        private static TerritoryId _currentTerritoryId;
        private static string _currentLordName;
        private static bool _isVisible = false;
        private static bool _isRewardVisible = false;
        private static string _rewardMessage = "";

        // GUIStyle 캐싱
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleButton;
        private GUIStyle _styleReward;
        private bool _stylesInitialized = false;

        /// <summary>MercyUI 패널이 현재 표시 중인지 여부</summary>
        public static bool IsVisible => _isVisible;

        /// <summary>보상 팝업이 표시 중인지 여부</summary>
        public static bool IsRewardVisible => _isRewardVisible;

        /// <summary>현재 처리 중인 영지 ID</summary>
        public static TerritoryId CurrentTerritoryId => _currentTerritoryId;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// MercyUI 패널 표시 — 영주 항복 메시지와 선택 버튼 출력
        /// </summary>
        /// <param name="territoryId">대상 영지 ID</param>
        /// <param name="lordName">영주 이름</param>
        public static void Show(TerritoryId territoryId, string lordName)
        {
            _currentTerritoryId = territoryId;
            _currentLordName = lordName;
            _isVisible = true;
            _isRewardVisible = false;
            _rewardMessage = "";

            Debug.Log($"[MercyUI] 영주 항복 패널 표시: {lordName} (영지: {territoryId})");
        }

        /// <summary>
        /// MercyUI 패널 숨기기
        /// </summary>
        public static void Hide()
        {
            _isVisible = false;
            _isRewardVisible = false;
            _rewardMessage = "";
            _currentLordName = "";
        }

        /// <summary>
        /// 보상 팝업 표시
        /// </summary>
        private static void ShowRewardPopup(string message)
        {
            _rewardMessage = message;
            _isRewardVisible = true;
            _isVisible = false;
        }

        /// <summary>
        /// 보상 팝업 숨기기
        /// </summary>
        public static void HideReward()
        {
            _isRewardVisible = false;
            _rewardMessage = "";
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (_isVisible)
            {
                DrawMercyPanel();
            }

            if (_isRewardVisible)
            {
                DrawRewardPopup();
            }
        }

        /// <summary>
        /// 처형/살려주기 선택 패널 IMGUI 그리기
        /// </summary>
        private void DrawMercyPanel()
        {
            float x = (Screen.width - _panelWidth) / 2f;
            float y = (Screen.height - _panelHeight) / 2f;

            // 배경 박스
            GUI.Box(new Rect(x, y, _panelWidth, _panelHeight), "");

            // 제목
            GUI.Label(new Rect(x + 20, y + 20, _panelWidth - 40, 30),
                "🏳️ 영주 항복!", _styleTitle);

            // 메시지
            GUI.Label(new Rect(x + 20, y + 60, _panelWidth - 40, 60),
                $"{_currentLordName} 영주가 항복했습니다.\n처형하시겠습니까? 살려주시겠습니까?",
                _styleLabel);

            // 구분선
            GUI.Box(new Rect(x + 20, y + 125, _panelWidth - 40, 2), "");

            // 버튼 영역
            float btnY = y + 145;
            float btnW = 150f;
            float btnH = 50f;
            float gap = 30f;
            float totalBtnWidth = btnW * 2 + gap;
            float startX = x + (_panelWidth - totalBtnWidth) / 2f;

            // 처형 버튼 (빨간색 강조)
            GUI.color = new Color(0.9f, 0.2f, 0.2f);
            if (GUI.Button(new Rect(startX, btnY, btnW, btnH), "⚔️ 처형", _styleButton))
            {
                OnExecute();
            }

            // 살려주기 버튼 (초록색 강조)
            GUI.color = new Color(0.2f, 0.7f, 0.2f);
            if (GUI.Button(new Rect(startX + btnW + gap, btnY, btnW, btnH), "🤝 살려주기", _styleButton))
            {
                OnSpare();
            }

            GUI.color = Color.white;

            // 정보 표시
            float infoY = y + _panelHeight - 50f;
            var lordData = LordSurrenderSystem.GetLordData(_currentTerritoryId);
            string personalityStr = LordSurrenderSystem.GetPersonalityName(lordData.personality);
            GUI.Label(new Rect(x + 20, infoY, _panelWidth - 40, 40),
                $"성격: {personalityStr} | 선호: {lordData.preferredFood ?? "알 수 없음"}",
                _styleLabel);
        }

        /// <summary>
        /// 보상 팝업 IMGUI 그리기
        /// </summary>
        private void DrawRewardPopup()
        {
            float x = (Screen.width - _rewardPopupWidth) / 2f;
            float y = (Screen.height - _rewardPopupHeight) / 2f;

            // 배경
            GUI.Box(new Rect(x, y, _rewardPopupWidth, _rewardPopupHeight), "");

            // 제목
            GUI.Label(new Rect(x + 20, y + 15, _rewardPopupWidth - 40, 30),
                "🎉 영지 점령 완료!", _styleTitle);

            // 보상 메시지
            GUI.Label(new Rect(x + 20, y + 55, _rewardPopupWidth - 40, 80),
                _rewardMessage,
                _styleReward);

            // 확인 버튼
            GUI.color = new Color(0.2f, 0.5f, 0.9f);
            if (GUI.Button(new Rect(x + (_rewardPopupWidth - 120f) / 2f, y + _rewardPopupHeight - 60f, 120f, 40f),
                "확인", _styleButton))
            {
                HideReward();
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// 처형 버튼 콜백 — 영주 처형, 영지 점령, 보상 표시
        /// </summary>
        private void OnExecute()
        {
            Debug.Log($"[MercyUI] ⚔️ 처형 선택: {_currentLordName}");

            // 영주 처형
            LordSurrenderSystem.ExecuteLord(_currentTerritoryId);

            // 영지 정보
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(_currentTerritoryId);
            var state = db.GetState(_currentTerritoryId);
            string territoryName = def.territoryName ?? "알 수 없는 영지";

            // 보상 계산
            int goldReward = GetExecuteGoldReward(def.difficulty);
            int itemCount = Random.Range(1, 4);

            string rewardMsg = $"✔️ {territoryName}을(를) 점령했습니다!\n" +
                               $"💰 골드 +{goldReward}\n" +
                               $"📦 전리품 +{itemCount}개\n" +
                               $"💀 {_currentLordName} 영주 처형됨";

            ShowRewardPopup(rewardMsg);
        }

        /// <summary>
        /// 살려주기 버튼 콜백 — 영주 생존, 영지 점령, 충성도 보너스, 보상 표시
        /// </summary>
        private void OnSpare()
        {
            Debug.Log($"[MercyUI] 🤝 살려주기 선택: {_currentLordName}");

            // 영주 살려주기
            LordSurrenderSystem.SpareLord(_currentTerritoryId);

            // 영지 정보
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(_currentTerritoryId);
            var state = db.GetState(_currentTerritoryId);
            string territoryName = def.territoryName ?? "알 수 없는 영지";

            // 보상 계산
            int goldReward = GetSpareGoldReward(def.difficulty);
            float loyaltyBonus = 30f;

            string rewardMsg = $"✔️ {territoryName}을(를) 점령했습니다!\n" +
                               $"💰 골드 +{goldReward}\n" +
                               $"🤝 충성도 +{loyaltyBonus:F0}\n" +
                               $"🕊️ {_currentLordName} 영주 생존 — 동맹 유지";

            ShowRewardPopup(rewardMsg);
        }

        /// <summary>
        /// 처형 시 골드 보상 계산 (난이도 기반)
        /// </summary>
        private static int GetExecuteGoldReward(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 100;
                case TerritoryDifficulty.Ring2: return 250;
                case TerritoryDifficulty.Ring3: return 500;
                case TerritoryDifficulty.Ring4: return 1000;
                case TerritoryDifficulty.Empire: return 2000;
                default: return 100;
            }
        }

        /// <summary>
        /// 살려주기 시 골드 보상 계산 (난이도 기반, 처형보다 적음)
        /// </summary>
        private static int GetSpareGoldReward(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 50;
                case TerritoryDifficulty.Ring2: return 150;
                case TerritoryDifficulty.Ring3: return 300;
                case TerritoryDifficulty.Ring4: return 600;
                case TerritoryDifficulty.Empire: return 1200;
                default: return 50;
            }
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _styleButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _styleReward = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.8f, 1f, 0.8f) }
            };

            _stylesInitialized = true;
        }
    }
}