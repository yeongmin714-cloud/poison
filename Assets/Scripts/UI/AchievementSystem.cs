using System.Collections.Generic;
using UnityEngine;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// G3-13: 도전과제 시스템 (업적).
    /// 15개 업적, PlayerPrefs 저장, 달성 팝업 3초 표시.
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        [SerializeField] private float _popupDuration = 3f;
        [SerializeField] private Color _popupBgColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _popupTitleColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _popupTextColor = Color.white;

        // ===== 업적 데이터 =====
        public struct AchievementDef
        {
            public string id;
            public string title;
            public string description;
            public string icon; // 이모지

            public AchievementDef(string id, string title, string desc, string icon = "🏆")
            {
                this.id = id; this.title = title; this.description = desc; this.icon = icon;
            }
        }

        public static readonly AchievementDef[] AllAchievements = new AchievementDef[]
        {
            new AchievementDef("first_kill", "첫 처치", "첫 번째 몬스터 처치", "⚔️"),
            new AchievementDef("first_territory", "첫 영지", "첫 번째 영지 점령", "🏰"),
            new AchievementDef("level_5", "초보 사냥꾼", "레벨 5 달성", "⬆️"),
            new AchievementDef("level_10", "중급 모험가", "레벨 10 달성", "⬆️"),
            new AchievementDef("level_20", "전설의 시작", "레벨 20 달성", "⭐"),
            new AchievementDef("craft_master", "크래프트 마스터", "아이템 50회 제작", "🔨"),
            new AchievementDef("rich_man", "부자", "골드 10000 축적", "💰"),
            new AchievementDef("herb_gather", "약초꾼", "약초 100회 채집", "🌿"),
            new AchievementDef("quest_master", "퀘스트 마스터", "퀘스트 30회 완료", "📜"),
            new AchievementDef("mercenary_king", "용병왕", "용병 5명 고용", "👥"),
            new AchievementDef("poison_master", "독의 달인", "독약 20회 제조", "🧪"),
            new AchievementDef("night_hunter", "밤의 사냥꾼", "밤에 몬스터 50마리 처치", "🌙"),
            new AchievementDef("survivor", "생존자", "사망 없이 10일 생존", "💪"),
            new AchievementDef("explorer", "탐험가", "모든 지역 방문", "🗺️"),
            new AchievementDef("true_ending", "진정한 왕", "게임 클리어", "👑"),
        };

        // ===== 상태 =====
        private HashSet<string> _unlocked = new HashSet<string>();
        private string _currentPopupId;
        private string _currentPopupTitle;
        private string _currentPopupDesc;
        private string _currentPopupIcon;
        private float _popupTimer;

        private UIDesignTheme _theme;
        private GUIStyle _popupBgStyle;
        private GUIStyle _popupTitleStyle;
        private GUIStyle _popupDescStyle;
        private bool _stylesInit;

        private const string PREFS_KEY = "Achievement_";

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _theme = Phase33_Themes.AchievementTheme();
            LoadAll();
        }

        // ===== 저장/로드 =====

        private void LoadAll()
        {
            _unlocked.Clear();
            foreach (var ach in AllAchievements)
            {
                if (PlayerPrefs.GetInt(PREFS_KEY + ach.id, 0) == 1)
                    _unlocked.Add(ach.id);
            }
        }

        private void Save(string id)
        {
            PlayerPrefs.SetInt(PREFS_KEY + id, 1);
            PlayerPrefs.Save();
        }

        // ===== 공개 메서드 =====

        public bool IsUnlocked(string id) => _unlocked.Contains(id);

        public void Unlock(string id)
        {
            if (_unlocked.Contains(id)) return;

            // 찾기
            foreach (var ach in AllAchievements)
            {
                if (ach.id == id)
                {
                    _unlocked.Add(id);
                    Save(id);
                    ShowPopup(ach);
                    Debug.Log($"[Achievement] 🏆 {ach.title} 달성!");
                    return;
                }
            }
        }

        private void ShowPopup(AchievementDef ach)
        {
            _currentPopupId = ach.id;
            _currentPopupTitle = ach.title;
            _currentPopupDesc = ach.description;
            _currentPopupIcon = ach.icon;
            _popupTimer = _popupDuration;
        }

        public int GetUnlockedCount()
        {
            int count = 0;
            foreach (var ach in AllAchievements)
                if (_unlocked.Contains(ach.id)) count++;
            return count;
        }

        public int GetTotalCount() => AllAchievements.Length;

        // ===== UI =====

        private void InitStyles()
        {
            if (_stylesInit) return;
            _popupBgStyle = new GUIStyle { normal = { background = UIStyleManager.MakeTexture(1, 1, _popupBgColor) } };
            _popupTitleStyle = new GUIStyle
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _popupTitleColor }
            };
            _popupDescStyle = new GUIStyle
            {
                fontSize = 14, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _popupTextColor }
            };
            _stylesInit = true;
        }

        private void Update()
        {
            if (_popupTimer > 0f)
                _popupTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (_popupTimer <= 0f) return;
            InitStyles();

            // 우측 상단 팝업
            int w = 300, h = 70;
            int x = Screen.width - w - 15;
            int y = 10;

            GUI.Box(new Rect(x, y, w, h), "", _popupBgStyle);

            // 아이콘
            GUI.Label(new Rect(x + 8, y + 8, 30, 30), _currentPopupIcon, new GUIStyle { fontSize = 24, alignment = TextAnchor.MiddleCenter });

            // 제목
            GUI.Label(new Rect(x + 45, y + 6, w - 55, 28), $"🏆 {_currentPopupTitle}", _popupTitleStyle);

            // 설명
            GUI.Label(new Rect(x + 45, y + 36, w - 55, 28), _currentPopupDesc, _popupDescStyle);
        }
    }
}